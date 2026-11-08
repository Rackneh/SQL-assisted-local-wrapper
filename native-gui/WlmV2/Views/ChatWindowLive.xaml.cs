using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MSNPSharp;
using WLMRevivalV2Desktop.Protocol;
using WLMRevivalV2Desktop.Services;
using WLMRevivalV2Desktop.Views.Controls;
using Microsoft.Win32;
using DoryNative.XpSim;

namespace WLMRevivalV2Desktop.Views
{
    // WLM Live (2011 / Wave 4) skin of the conversation window. Same protocol
    // logic as ChatWindow, different chrome.
    public partial class ChatWindowLive : Window
    {
        private readonly MessengerService _messenger;
        private readonly Contact _contact;

        private DateTime _lastTypingSent = DateTime.MinValue;
        private DispatcherTimer _typingClearTimer;

        private FontFamily _composeFont;
        private double _composeSize = 12;
        private Brush _composeBrush;

        private static HashSet<string> _emoticonNames;
        private static string _emoticonDir;

        private readonly Dictionary<string, Border> _ghostContainers = new Dictionary<string, Border>();

        // ===== IBA live activity (reasoning / tools / parallel tasks / MCQ waits) =====
        private class IbaToolState
        {
            public string Step = "";
            public ChatSegment Seg = new ChatSegment();
            public FrameworkElement Element;
        }

        private sealed class ToolCardParts
        {
            public System.Windows.Controls.Primitives.ToggleButton Toggle;
            public StackPanel Body;
        }

        private class IbaTaskView
        {
            public string TaskId = "";
            public string TaskTitle = "";
            public Border Column;
            public StackPanel Body;
            public TextBlock Header;
            public TextBlock ObjectiveLine;
            public Expander OpenThink;
            public TextBlock OpenThinkBody;
            public string ThinkText = "";
            public int ThinkTokens;
            public readonly Dictionary<string, IbaToolState> Tools = new Dictionary<string, IbaToolState>();
            public DateTime LastActivity = DateTime.MinValue;
            public bool HasPendingTool;
            public bool Waiting;
            public bool Completed;
            public bool TitleRevealed;
        }

        private readonly Dictionary<string, IbaTaskView> _ibaViews = new Dictionary<string, IbaTaskView>();
        private StackPanel _ibaHost;               // horizontal panel holding parallel task columns
        private static readonly TimeSpan IbaParallelWindow = TimeSpan.FromSeconds(30);

        // Track server ids already rendered so history sync doesn't duplicate
        // messages that were already shown locally (sent or received live).
        private readonly HashSet<long> _renderedServerIds = new HashSet<long>();

        // Local-only messages rendered before their server id is known.
        private readonly List<HistoryMessage> _renderedLocalMessages = new List<HistoryMessage>();

        // Signature cache for every rendered message so live + sync duplicates
        // (same content, type and sender side within a few minutes) are suppressed.
        private readonly Dictionary<string, DateTime> _renderedSignatures =
            new Dictionary<string, DateTime>();

        // Background sync timer so messages sent/received on another client
        // (e.g. the Android app) appear while this window is open.
        private DispatcherTimer _historySyncTimer;
        private bool _syncInProgress;

        // IBA continuation: when the chat reopens while a task was mid-flight,
        // resume paused work through the normal chat stream and poll history
        // for tasks still executing in the backend.
        private DispatcherTimer _ibaPollTimer;
        private bool _ibaAutoResumeTried;
        private bool _ibaPollBusy;
        private string _ibaPollSignature;

        public ChatWindowLive(MessengerService messenger, Contact contact)
        {
            InitializeComponent();
            _messenger = messenger;
            _contact = contact;
            EnsureEmoticonIndex();

            Title = "Conversation - " + contact.Name;
            ContactNameText.Text = contact.Name;
            ContactStatusText.Text = " " + AvatarHelper.StatusText(contact.Status);
            var pm = contact.PersonalMessage != null ? contact.PersonalMessage.Message : null;
            ContactPmText.Text = pm ?? "";

            ContactStatusFrame.Source = AvatarHelper.WlmImage(AvatarHelper.StatusFrameLarge(contact.Status));
            var cdp = MessengerService.TryGetDisplayImage(contact)
                      ?? ContactStyleManager.GetContactDisplayImage(contact.Mail);
            if (cdp != null) ContactTile.Source = cdp;

            if (_messenger.CurrentUser != null)
            {
                OwnStatusFrame.Source = AvatarHelper.WlmImage(AvatarHelper.StatusFrameLarge(_messenger.CurrentUser.Status));
                var odp = _messenger.GetCurrentUserDisplayImage();
                if (odp != null) OwnTile.Source = odp;
            }

            _messenger.MessageReceived += OnMessageReceived;
            _messenger.ContactStatusChanged += OnContactStatusChanged;
            _messenger.TypingReceived += OnTypingReceived;
            _messenger.NudgeReceived += OnNudgeReceived;
            _messenger.WinkReceived += OnWinkReceived;
            _messenger.ContactDisplayImageChanged += OnDisplayImageChanged;
            _messenger.IbaEventReceived += OnIbaEventReceived;

            _messenger.RequestDisplayPicture(_contact);

            // Fetch contact style (banner, colors, display picture) from server
            LoadContactStyle();
            ContactStyleManager.StyleChanged += OnContactStyleChanged;

            MessageBox.TextChanged += MessageBox_TextChanged;

            _typingClearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _typingClearTimer.Tick += (s, e) => { _typingClearTimer.Stop(); LastReceivedText.Text = ""; };

            _historySyncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _historySyncTimer.Tick += async (s, e) => await SyncHistoryAsync();
            _historySyncTimer.Start();

            LoadHistory();
        }

        private async Task SyncHistoryAsync()
        {
            if (_syncInProgress || _contact == null) return;
            _syncInProgress = true;
            try
            {
                var fetched = await HistoryService.SyncAsync(_contact.Mail);
                if (fetched.Count > 0)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        ClientLog.Write($"ChatWindowLive.SyncHistoryAsync rendering {fetched.Count} fetched messages");
                        foreach (var msg in fetched)
                            RenderHistoryMessage(msg);
                        MessagesScroller.ScrollToEnd();
                    }));
                    MarkRead();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChatWindowLive.SyncHistoryAsync: " + ex.Message);
            }
            finally
            {
                _syncInProgress = false;
            }
        }

        private async void LoadHistory()
        {
            if (_contact == null) return;

            // Prefer the IBA session history — it carries reasoning, tool
            // calls and step groups, which is the canonical chat record.
            try
            {
                var iba = await _messenger.GetSessionHistoryAsync(_contact.Mail);
                if (iba != null && iba.Messages.Count > 0)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        RenderIbaHistory(iba);
                        MessagesScroller.ScrollToEnd();
                        MaybeContinueIbaTask(iba);
                    });
                    return;
                }
            }
            catch { /* fall through to legacy history */ }

            LoadLegacyHistory();
        }

        private void LoadLegacyHistory()
        {
            if (_contact == null) return;

            var history = HistoryService.LoadHistory(_contact.Mail);
            if (history.Count > 0)
            {
                ClientLog.Write($"ChatWindowLive.LoadHistory rendering {history.Count} cached messages");
                foreach (var msg in history)
                    RenderHistoryMessage(msg);
                MessagesScroller.ScrollToEnd();
            }
            else
            {
                // Web GUI parity: empty session shows a persona intro message.
                var bio = _contact.PersonalMessage?.Message;
                var intro = $"Hello! I am {_contact.Name} for session {_contact.Mail}." +
                            (string.IsNullOrEmpty(bio) ? "" : $" {bio}") +
                            " Commands execute safely in the Linux Sandbox VM (Z:). Host actions will ask your permission!";
                AddMessage(_contact.Name, intro, false);
            }

            MarkRead();

            // Sync with the server in the background and render anything new.
            _ = Task.Run(async () =>
            {
                var fetched = await HistoryService.SyncAsync(_contact.Mail);
                if (fetched.Count > 0)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        ClientLog.Write($"ChatWindowLive.LoadHistory sync rendering {fetched.Count} fetched messages");
                        foreach (var msg in fetched)
                            RenderHistoryMessage(msg);
                        MessagesScroller.ScrollToEnd();
                    }));
                }
                MarkRead();
            });
        }

        private void MarkRead()
        {
            if (_contact == null) return;
            var history = HistoryService.LoadHistory(_contact.Mail);
            var maxId = history.Where(m => m.Id > 0 && !m.Mine).Max(m => (long?)m.Id);
            if (maxId.HasValue)
                _ = HistoryService.MarkReadAsync(_contact.Mail, maxId.Value);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            MarkRead();
        }

        private void RenderHistoryMessage(HistoryMessage msg)
        {
            if (msg == null) return;

            // Ghost/streaming placeholders are updated in-place by id, so always
            // re-render them even if the server id has already been seen.
            if (string.Equals(msg.MessageType, "ghost", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() => HandleGhostMessage(msg.Content));
                TrackRenderedMessage(msg);
                return;
            }

            var signature = RenderSignature(msg);
            if (_renderedSignatures.TryGetValue(signature, out var renderedAt))
            {
                if (Math.Abs((msg.CreatedAt - renderedAt).TotalMinutes) < 5)
                {
                    ClientLog.Write($"ChatWindowLive.RenderHistoryMessage SKIP signature id={msg.Id} sig={signature} age={(msg.CreatedAt - renderedAt).TotalSeconds:F1}s");
                    // Still record the server id so a later sync doesn't try again, and update the
                    // timestamp so a flurry of identical messages stays suppressed.
                    if (msg.Id > 0)
                        _renderedServerIds.Add(msg.Id);
                    _renderedSignatures[signature] = msg.CreatedAt;
                    return;
                }
            }

            if (msg.Id > 0)
            {
                if (_renderedServerIds.Contains(msg.Id))
                {
                    ClientLog.Write($"ChatWindowLive.RenderHistoryMessage SKIP serverId={msg.Id}");
                    return;
                }
                _renderedServerIds.Add(msg.Id);
            }
            else
            {
                // Local-only message: remember it so we can skip the server copy
                // when it later arrives with a real id.
                _renderedLocalMessages.Add(msg);
            }

            // If this server message matches a previously rendered local-only
            // message (same sender side, content and within 5 minutes), don't
            // render it again; just remember the server id.
            if (msg.Id > 0)
            {
                var dup = _renderedLocalMessages.FirstOrDefault(m =>
                    m.Mine == msg.Mine
                    && string.Equals(m.Content, msg.Content, StringComparison.Ordinal)
                    && m.MessageType == msg.MessageType
                    && Math.Abs((m.CreatedAt - msg.CreatedAt).TotalMinutes) < 5);
                if (dup != null)
                {
                    ClientLog.Write($"ChatWindowLive.RenderHistoryMessage SKIP localDup serverId={msg.Id} content={EscapeForLog(msg.Content)}");
                    _renderedServerIds.Add(msg.Id);
                    return;
                }
            }

            TrackRenderedMessage(msg);
            ClientLog.Write($"ChatWindowLive.RenderHistoryMessage RENDER id={msg.Id} mine={msg.Mine} type={msg.MessageType} content={EscapeForLog(msg.Content)}");

            var name = msg.Mine
                ? (_messenger.CurrentUser != null ? _messenger.CurrentUser.Name : "Me")
                : (msg.SenderName ?? _contact.Name);

            switch (msg.MessageType?.ToLowerInvariant())
            {
                case "nudge":
                    AddSystemLine(msg.Mine ? "You have just sent a nudge." : "You have just received a nudge!");
                    if (!msg.Mine) ShakeWindow();
                    break;
                case "wink":
                    // History render: show the line only, never replay the wink overlay.
                    DisplayWink(name, msg.Content, alreadyRead: true);
                    break;
                case "chat":
                case "gif":
                case "file":
                case "image":
                default:
                    AddMessage(name, msg.Content, msg.Mine);
                    break;
            }
        }

        private static string RenderSignature(HistoryMessage msg)
        {
            return $"{msg.Mine}|{msg.MessageType ?? "chat"}|{msg.Content ?? string.Empty}";
        }

        private void TrackRenderedMessage(HistoryMessage msg)
        {
            if (msg == null) return;
            if (msg.Id > 0)
                _renderedServerIds.Add(msg.Id);
            else
                _renderedLocalMessages.Add(msg);
            _renderedSignatures[RenderSignature(msg)] = msg.CreatedAt;
        }

        private static void EnsureEmoticonIndex()
        {
            if (_emoticonNames != null) return;
            _emoticonNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                _emoticonDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Assets", "wlm", "emoticons");
                if (Directory.Exists(_emoticonDir))
                    foreach (var f in Directory.GetFiles(_emoticonDir, "*.gif"))
                        _emoticonNames.Add(Path.GetFileNameWithoutExtension(f));
            }
            catch { /* best effort */ }
        }

        public void ShowInitialMessage(string text)
        {
            ClientLog.Write($"ChatWindowLive.ShowInitialMessage contact={_contact?.Mail} text={EscapeForLog(text)}");
            if (string.IsNullOrEmpty(text)) return;
            RenderHistoryMessage(new HistoryMessage
            {
                Id = 0,
                SenderEmail = _contact.Mail,
                SenderName = _contact.Name,
                RecipientEmail = _messenger.CurrentUser?.Mail,
                MessageType = HistoryService.ClassifyContent(text),
                Content = text,
                CreatedAt = DateTime.UtcNow,
                Read = false,
                Mine = false,
            });
            SetLastReceived();
        }

        // ===== asset helpers (shared via AvatarHelper) =====

        private void OnDisplayImageChanged(object sender, ContactPresenceEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.Contact != null && _contact != null && e.Contact.Mail == _contact.Mail)
                {
                    var cdp = MessengerService.TryGetDisplayImage(_contact)
                              ?? ContactStyleManager.GetContactDisplayImage(_contact.Mail);
                    if (cdp != null) ContactTile.Source = cdp;
                }
                if (_messenger.CurrentUser != null && e.Contact != null && e.Contact.Mail == _messenger.CurrentUser.Mail)
                {
                    var odp = _messenger.GetCurrentUserDisplayImage();
                    if (odp != null) OwnTile.Source = odp;
                }
            });
        }

        private void OnContactStatusChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_contact == null) return;
                ContactStatusFrame.Source = AvatarHelper.WlmImage(AvatarHelper.StatusFrameLarge(_contact.Status));
                ContactStatusText.Text = " " + AvatarHelper.StatusText(_contact.Status);
            });
        }

        // ===== Contact banner / style =====
        private void LoadContactStyle()
        {
            if (_contact == null) return;

            // Try cache first
            var style = ContactStyleManager.GetStyle(_contact.Mail);
            if (style != null)
                ApplyContactStyle(style);

            // Fetch from server (async)
            ContactStyleManager.FetchStyleAsync(_contact.Mail);
        }

        private void ApplyContactStyle(ContactStyle style)
        {
            if (style == null) return;

            Dispatcher.Invoke(() =>
            {
                // Refresh the contact tile in case the HTTP display picture only
                // just finished downloading (P2P picture still absent for bots).
                var cdp = MessengerService.TryGetDisplayImage(_contact)
                          ?? ContactStyleManager.GetContactDisplayImage(_contact.Mail);
                if (cdp != null) ContactTile.Source = cdp;

                // Apply colour scheme first so the header band shows the contact's
                // colour; the banner (if any) then layers on top and fades into it.
                if (!string.IsNullOrEmpty(style.ColorScheme))
                {
                    ApplyColorSchemeTint(style.ColorScheme);
                }

                // Apply banner (non-blocking: use cache, else download in background
                // and re-apply on the StyleChanged callback).
                if (style.HasBanner)
                {
                    var banner = ContactStyleManager.GetCachedBannerImage(style.BannerUrl);
                    if (banner != null)
                    {
                        ContactBannerBrush.ImageSource = banner;
                        ContactBanner.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ContactStyleManager.EnsureBannerImageAsync(style.BannerUrl, _contact.Mail, style);
                    }
                }
                else
                {
                    ContactBanner.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void ApplyColorSchemeTint(string schemeName)
        {
            // Adopt the CONTACT's colour scheme for this chat window only, by
            // overriding the themed resources at window scope. The header banner
            // binds to {DynamicResource WlmBannerImage}; a window-level resource of
            // the same key shadows the application-level (local user's) scene, so
            // the contact's colour banner shows here instead of ours.
            ColorSchemeManager.ApplyTo(this.Resources, schemeName);
        }

        private void OnContactStyleChanged(object sender, ContactStyleEventArgs e)
        {
            if (_contact != null && string.Equals(e.Email, _contact.Mail, StringComparison.OrdinalIgnoreCase))
            {
                ApplyContactStyle(e.Style);
            }
        }

        private static readonly Regex WinkToken = new Regex(
            @"\[wink:(?<name>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private void OnMessageReceived(object sender, IncomingMessageEventArgs e)
        {
            ClientLog.Write($"ChatWindowLive.OnMessageReceived from={e.From?.Mail} contact={_contact?.Mail} text={EscapeForLog(e.Text)}");
            if (e.From.Mail != _contact.Mail)
            {
                ClientLog.Write($"ChatWindowLive.OnMessageReceived IGNORED: {e.From?.Mail} != {_contact?.Mail}");
                return;
            }

            var winkMatch = WinkToken.Match(e.Text ?? string.Empty);
            if (winkMatch.Success)
            {
                ClientLog.Write($"ChatWindowLive parsing wink marker: {winkMatch.Groups["name"].Value}");
                Dispatcher.Invoke(() => DisplayWink(e.From.Name, winkMatch.Groups["name"].Value + ".mct", false));
                TrackRenderedMessage(new HistoryMessage
                {
                    Id = 0,
                    SenderEmail = e.From.Mail,
                    SenderName = e.From.Name,
                    RecipientEmail = _messenger.CurrentUser?.Mail,
                    MessageType = "wink",
                    Content = winkMatch.Groups["name"].Value + ".mct",
                    CreatedAt = DateTime.UtcNow,
                    Read = false,
                    Mine = false,
                });
                return;
            }

            if (ChatContent.IsGhostMessage(e.Text))
            {
                Dispatcher.Invoke(() =>
                {
                    HandleGhostMessage(e.Text);
                    MessagesScroller.ScrollToEnd();
                });
                return;
            }
            RenderHistoryMessage(new HistoryMessage
            {
                Id = 0,
                SenderEmail = e.From.Mail,
                SenderName = e.From.Name,
                RecipientEmail = _messenger.CurrentUser?.Mail,
                MessageType = HistoryService.ClassifyContent(e.Text),
                Content = e.Text ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                Read = false,
                Mine = false,
            });
            SetLastReceived();
            if (!IsActive)
            {
                if (ChatContent.IsMcqMessage(e.Text))
                    SoundManager.PlayMultipleChoiceQuestion();
                else if (!ChatContent.IsIntermediateMessage(e.Text))
                    SoundManager.PlayMessage();
            }
        }

        private void OnTypingReceived(object sender, ContactPresenceEventArgs e)
        {
            if (e.Contact == null || e.Contact.Mail != _contact.Mail) return;
            Dispatcher.Invoke(() =>
            {
                LastReceivedText.Text = _contact.Name + " is writing a message...";
                _typingClearTimer.Stop();
                _typingClearTimer.Start();
            });
        }

        private void OnNudgeReceived(object sender, ContactPresenceEventArgs e)
        {
            if (e.Contact == null || e.Contact.Mail != _contact.Mail) return;
            Dispatcher.Invoke(() =>
            {
                AddSystemLine("You have just received a nudge!");
                SoundManager.PlayNudge();
                ShakeWindow();
                TrackRenderedMessage(new HistoryMessage
                {
                    Id = 0,
                    SenderEmail = e.Contact.Mail,
                    SenderName = e.Contact.Name,
                    RecipientEmail = _messenger.CurrentUser?.Mail,
                    MessageType = "nudge",
                    Content = "",
                    CreatedAt = DateTime.UtcNow,
                    Read = false,
                    Mine = false,
                });
            });
        }

        private void OnWinkReceived(object sender, WinkReceivedEventArgs e)
        {
            ClientLog.Write($"ChatWindowLive.OnWinkReceived from={e.From?.Mail} location={e.Location} contact={_contact?.Mail}");
            if (e.From == null || e.From.Mail != _contact.Mail) return;
            Dispatcher.Invoke(() => DisplayWink(_contact.Name, e.Location, false));
            TrackRenderedMessage(new HistoryMessage
            {
                Id = 0,
                SenderEmail = e.From.Mail,
                SenderName = e.From.Name,
                RecipientEmail = _messenger.CurrentUser?.Mail,
                MessageType = "wink",
                Content = e.Location,
                CreatedAt = DateTime.UtcNow,
                Read = false,
                Mine = false,
            });
        }

        // Render a received/sent wink: a one-line system line (like a nudge)
        // plus a fullscreen overlay that plays the wink image/video.
        // If alreadyRead is true (history replay), only the system line is shown.
        private void DisplayWink(string from, string location, bool alreadyRead)
        {
            location = MessengerService.NormalizeWinkLocation(location);
            ClientLog.Write($"DisplayWinkLive from={from} location={location} alreadyRead={alreadyRead}");
            AddSystemLine(from + " sent a wink.");
            if (alreadyRead) return;
            if (string.IsNullOrEmpty(location)) return;
            try
            {
                string winksPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Assets", "wlm", "winks");
                string baseName = Path.GetFileNameWithoutExtension(location);

                string videoPath = FindWinkVideo(winksPath, baseName);
                if (videoPath != null)
                {
                    ClientLog.Write($"DisplayWinkLive playing video: {videoPath}");
                    PlayWinkOverlayVideo(videoPath);
                }
                else
                {
                    string thumbPath = FindWinkThumbnail(winksPath, baseName);
                    ClientLog.Write($"DisplayWinkLive video not found, thumbnail={thumbPath ?? "(none)"}");
                    if (thumbPath != null)
                        PlayWinkOverlayImage(thumbPath);
                }
            }
            catch (Exception ex) { ClientLog.Write("DisplayWinkLive failed", ex); }
        }

        private void PlayWinkOverlayImage(string imagePath)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    WinkOverlayImage.Source = new BitmapImage(new Uri(imagePath));
                    WinkOverlay.Visibility = Visibility.Visible;
                    _winkOverlayTimer?.Stop();
                    _winkOverlayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
                    _winkOverlayTimer.Tick += (s, e) => HideWinkOverlay();
                    _winkOverlayTimer.Start();
                }
                catch { HideWinkOverlay(); }
            });
        }

        private MediaElement _winkOverlayMedia;
        private void PlayWinkOverlayVideo(string videoPath)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    _winkOverlayMedia?.Stop();
                    _winkOverlayMedia?.Close();
                    _winkOverlayMedia = new MediaElement
                    {
                        Source = new Uri(videoPath),
                        LoadedBehavior = MediaState.Play,
                        UnloadedBehavior = MediaState.Manual,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    _winkOverlayMedia.MediaEnded += (s, e) => HideWinkOverlay();
                    _winkOverlayMedia.MediaFailed += (s, e) => HideWinkOverlay();
                    // Replace the overlay's child image with the media element for playback.
                    WinkOverlay.Child = _winkOverlayMedia;
                    WinkOverlay.Visibility = Visibility.Visible;
                }
                catch { HideWinkOverlay(); }
            });
        }

        private DispatcherTimer _winkOverlayTimer;

        private void HideWinkOverlay()
        {
            Dispatcher.Invoke(() =>
            {
                _winkOverlayTimer?.Stop();
                _winkOverlayTimer = null;
                try { _winkOverlayMedia?.Stop(); } catch { }
                try { _winkOverlayMedia?.Close(); } catch { }
                _winkOverlayMedia = null;
                WinkOverlay.Visibility = Visibility.Collapsed;
                WinkOverlay.Child = WinkOverlayImage;
                WinkOverlayImage.Source = null;
            });
        }

        private void WinkOverlay_Click(object sender, MouseButtonEventArgs e)
        {
            HideWinkOverlay();
        }

        private static string FindWinkThumbnail(string winksPath, string baseName)
        {
            foreach (var ext in new[] { ".png", ".jpg" })
            {
                string path = Path.Combine(winksPath, baseName + ext);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private static string FindWinkVideo(string winksPath, string baseName)
        {
            string videosPath = Path.Combine(winksPath, "videos");
            if (!Directory.Exists(videosPath)) return null;
            return Directory.GetFiles(videosPath)
                .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f)
                    .IndexOf(baseName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string EscapeForLog(string text)
        {
            if (string.IsNullOrEmpty(text)) return "(empty)";
            var s = text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
            if (s.Length > 120) s = s.Substring(0, 117) + "...";
            return s;
        }

        private void MessageBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(MessageBox.Text)) return;
            if ((DateTime.Now - _lastTypingSent).TotalSeconds < 4) return;
            _lastTypingSent = DateTime.Now;
            _messenger.SendTyping(_contact);
        }

        private void ShakeWindow()
        {
            double left = Left;
            var anim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            var t = TimeSpan.Zero;
            double[] offsets = { 0, 12, -12, 10, -10, 7, -7, 4, -4, 0 };
            foreach (var o in offsets)
            {
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(left + o, KeyTime.FromTimeSpan(t)));
                t += TimeSpan.FromMilliseconds(35);
            }
            anim.Completed += (s, e) => { BeginAnimation(LeftProperty, null); Left = left; };
            BeginAnimation(LeftProperty, anim);
        }

        // ===== "Name says (h:mm tt):" - grey sender line, like Wave 4 =====
        private void AddMessage(string from, string text, bool isMine)
        {
            MessagesPanel.Children.Add(new TextBlock
            {
                Text = from + " says (" + DateTime.Now.ToString("HH:mm") + "):",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x99, 0xA8)),
                Margin = new Thickness(0, 4, 0, 0)
            });

            if (ChatContent.HasRich(text))
            {
                AddRichBody(text, isMine);
                MessagesScroller.ScrollToEnd();
                return;
            }

            var body = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = isMine ? _composeSize : 12,
                FontFamily = isMine && _composeFont != null ? _composeFont : this.FontFamily,
                Foreground = isMine && _composeBrush != null
                    ? _composeBrush
                    : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                Margin = new Thickness(0, 0, 0, 2)
            };
            AppendWithEmoticons(body, text);
            MessagesPanel.Children.Add(body);

            MessagesScroller.ScrollToEnd();
        }

        private void AppendWithEmoticons(TextBlock target, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('[', i);
                if (open < 0)
                {
                    target.Inlines.Add(new Run(text.Substring(i)));
                    break;
                }

                int close = text.IndexOf(']', open + 1);
                if (close < 0)
                {
                    target.Inlines.Add(new Run(text.Substring(i)));
                    break;
                }

                string token = text.Substring(open + 1, close - open - 1);
                if (open > i) target.Inlines.Add(new Run(text.Substring(i, open - i)));

                if (_emoticonNames != null && _emoticonNames.Contains(token))
                {
                    try
                    {
                        var img = new Image
                        {
                            Width = 19,
                            Height = 19,
                            Stretch = Stretch.Uniform,
                            Source = new BitmapImage(new Uri(Path.Combine(_emoticonDir, token + ".gif"))),
                            Margin = new Thickness(1, 0, 1, -3)
                        };
                        target.Inlines.Add(new InlineUIContainer(img));
                    }
                    catch { target.Inlines.Add(new Run("[" + token + "]")); }
                }
                else
                {
                    target.Inlines.Add(new Run("[" + token + "]"));
                }

                i = close + 1;
            }
        }

        // ===== rich content (GIFs / files) =====
        private static SolidColorBrush B(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));

        private void AddRichBody(string text, bool isMine)
        {
            foreach (var seg in ChatContent.Parse(text))
            {
                switch (seg.Kind)
                {
                    case ChatSegmentKind.Text:
                        if (!string.IsNullOrWhiteSpace(seg.Text))
                        {
                            var tb = new TextBlock
                            {
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = isMine ? _composeSize : 12,
                                FontFamily = isMine && _composeFont != null ? _composeFont : this.FontFamily,
                                Foreground = isMine && _composeBrush != null ? _composeBrush : B(0x1A, 0x1A, 0x1A),
                                Margin = new Thickness(0, 0, 0, 2)
                            };
                            AppendWithEmoticons(tb, seg.Text);
                            MessagesPanel.Children.Add(tb);
                        }
                        break;
                    case ChatSegmentKind.Gif:
                        MessagesPanel.Children.Add(BuildGifBubble(seg.Url, isMine));
                        break;
                    case ChatSegmentKind.File:
                        MessagesPanel.Children.Add(BuildFileElement(seg, isMine));
                        break;
                    case ChatSegmentKind.Think:
                        var think = BuildThinkBubble(seg.Text);
                        if (think != null) MessagesPanel.Children.Add(think);
                        break;
                    case ChatSegmentKind.Tool:
                        MessagesPanel.Children.Add(BuildToolCard(seg));
                        break;
                    case ChatSegmentKind.Mcq:
                        MessagesPanel.Children.Add(BuildMcqCard(seg));
                        break;
                    case ChatSegmentKind.Ghost:
                        // Ghost messages are handled by HandleGhostMessage.
                        break;
                }
            }
        }

        private void HandleGhostMessage(string text)
        {
            var parsed = ChatContent.Parse(text);
            if (parsed.Count == 0) return;
            var ghostSeg = parsed.FirstOrDefault(s => s.Kind == ChatSegmentKind.Ghost);
            if (ghostSeg == null) return;

            if (!_ghostContainers.TryGetValue(ghostSeg.GhostId ?? string.Empty, out var container))
            {
                container = new Border
                {
                    Background = B(0xF8, 0xF8, 0xF8),
                    BorderBrush = B(0xD4, 0xD4, 0xD4),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6),
                    Margin = new Thickness(16, 2, 0, 4),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    MaxWidth = 460
                };
                container.Child = new StackPanel();
                _ghostContainers[ghostSeg.GhostId ?? string.Empty] = container;
                MessagesPanel.Children.Add(container);
            }

            var panel = (StackPanel)container.Child;
            panel.Children.Clear();
            AddSegmentsToPanel(panel, ghostSeg.Text, false);
            MessagesScroller.ScrollToEnd();
        }

        private void AddSegmentsToPanel(Panel panel, string text, bool isMine)
        {
            foreach (var seg in ChatContent.Parse(text))
            {
                switch (seg.Kind)
                {
                    case ChatSegmentKind.Text:
                        if (!string.IsNullOrWhiteSpace(seg.Text))
                        {
                            var tb = new TextBlock
                            {
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = 11,
                                Foreground = B(0x66, 0x66, 0x66),
                                Margin = new Thickness(0, 0, 0, 2)
                            };
                            AppendWithEmoticons(tb, seg.Text);
                            panel.Children.Add(tb);
                        }
                        break;
                    case ChatSegmentKind.Think:
                        var think = BuildThinkBubble(seg.Text);
                        if (think != null) panel.Children.Add(think);
                        break;
                    case ChatSegmentKind.Tool:
                        panel.Children.Add(BuildToolCard(seg));
                        break;
                    case ChatSegmentKind.Mcq:
                        panel.Children.Add(BuildMcqCard(seg));
                        break;
                }
            }
        }

        private FrameworkElement BuildGifBubble(string url, bool isMine)
        {
            var host = new Grid
            {
                HorizontalAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = new Thickness(0, 2, 0, 4),
                MaxWidth = 240
            };

            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                BorderBrush = B(0xD4, 0xE1, 0xEE),
                BorderThickness = new Thickness(1),
                Background = B(0xF0, 0xF4, 0xF8)
            };
            var img = new AnimatedGifImage { Stretch = Stretch.Uniform, MaxWidth = 240, MaxHeight = 240 };
            border.Child = img;
            img.BeginLoad(url);
            host.Children.Add(border);

            host.Children.Add(MakeChatStar(url));
            return host;
        }

        private FrameworkElement BuildFileElement(ChatSegment seg, bool isMine)
        {
            bool isImage = ChatContent.IsImage(seg.FileType) || ChatContent.IsImage(seg.FileName) || ChatContent.IsImage(seg.Url);
            if (isImage && !string.IsNullOrEmpty(seg.Url))
            {
                var host = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    ClipToBounds = true,
                    BorderBrush = B(0xD4, 0xE1, 0xEE),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    Margin = new Thickness(0, 2, 0, 4),
                    MaxWidth = 240,
                    Cursor = Cursors.Hand,
                    ToolTip = (seg.FileName ?? "image") + " - click to open"
                };

                bool isGif = (seg.FileType ?? "").IndexOf("gif", StringComparison.OrdinalIgnoreCase) >= 0
                             || (seg.Url ?? "").ToLowerInvariant().Contains(".gif");
                if (isGif)
                {
                    var g = new AnimatedGifImage { Stretch = Stretch.Uniform, MaxWidth = 240, MaxHeight = 240 };
                    g.BeginLoad(seg.Url);
                    host.Child = g;
                }
                else
                {
                    var im = new Image { Stretch = Stretch.Uniform, MaxWidth = 240, MaxHeight = 240 };
                    try
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.UriSource = new Uri(seg.Url, UriKind.Absolute);
                        bi.EndInit();
                        im.Source = bi;
                    }
                    catch { /* leave empty if it can't load */ }
                    host.Child = im;
                }
                host.MouseLeftButtonUp += (s, e) => OpenUrl(seg.Url);
                return host;
            }

            var card = new Border
            {
                Background = B(0xF4, 0xF8, 0xFC),
                BorderBrush = B(0xBF, 0xD3, 0xE6),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 6, 10, 6),
                Margin = new Thickness(0, 2, 0, 4),
                HorizontalAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Cursor = Cursors.Hand,
                MaxWidth = 300,
                ToolTip = "Click to open"
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };

            string ext = (System.IO.Path.GetExtension(seg.FileName ?? string.Empty) ?? string.Empty).TrimStart('.').ToUpperInvariant();
            var glyph = new Border
            {
                Width = 28, Height = 32, Background = Brushes.White,
                BorderBrush = B(0x9C, 0xB4, 0xCC), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 8, 0)
            };
            glyph.Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(ext) ? "FILE" : ext,
                FontSize = 8, FontWeight = FontWeights.Bold, Foreground = B(0x5A, 0x6B, 0x7B),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(glyph);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(seg.FileName) ? "file" : seg.FileName,
                FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = B(0x1A, 0x1A, 0x1A),
                TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 230
            });
            info.Children.Add(new TextBlock
            {
                Text = (seg.FileSize > 0 ? FileTransferService.HumanSize(seg.FileSize) + "  -  " : "") + "Click to open",
                FontSize = 10, Foreground = B(0x8A, 0x99, 0xA8)
            });
            sp.Children.Add(info);

            card.Child = sp;
            card.MouseLeftButtonUp += (s, e) => OpenUrl(seg.Url);
            return card;
        }

        private FrameworkElement BuildThinkBubble(string text)
        {
            if (!AppSettings.ShowReasoningSummaries)
                return null;

            var expander = new System.Windows.Controls.Expander
            {
                Header = "Thinking...",
                Foreground = B(0x88, 0x88, 0x88),
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                Margin = new Thickness(16, 2, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                IsExpanded = true
            };
            var body = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = B(0x66, 0x66, 0x66),
                Margin = new Thickness(4, 2, 0, 0)
            };
            expander.Content = body;
            return expander;
        }

        private FrameworkElement BuildToolCard(ChatSegment seg)
        {
            bool failed = string.Equals(seg.ToolStatus, "failed", StringComparison.OrdinalIgnoreCase);
            bool running = string.Equals(seg.ToolStatus, "running", StringComparison.OrdinalIgnoreCase);
            byte r = failed ? (byte)0xE0 : running ? (byte)0xF0 : (byte)0xF4;
            byte g = failed ? (byte)0x88 : running ? (byte)0xB0 : (byte)0xF8;
            byte b = failed ? (byte)0x88 : running ? (byte)0x70 : (byte)0xFC;

            bool expanded = AppSettings.AutoUncollapseToolUsage;

            var card = new Border
            {
                Background = B(r, g, b),
                BorderBrush = failed ? B(0xE0, 0x88, 0x88) : B(0xBF, 0xD3, 0xE6),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 6, 10, 6),
                Margin = new Thickness(16, 2, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 420
            };

            var sp = new StackPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            string statusGlyph = failed ? "\u2717" : running ? "\u25CB" : "\u2713";
            header.Children.Add(new TextBlock
            {
                Text = statusGlyph,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = failed ? B(0xC0, 0x30, 0x30) : running ? B(0x80, 0x80, 0x40) : B(0x30, 0x90, 0x30),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            header.Children.Add(new TextBlock
            {
                Text = seg.ToolTitle ?? seg.ToolName ?? "Tool",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = B(0x1A, 0x1A, 0x1A),
                VerticalAlignment = VerticalAlignment.Center
            });

            var toggle = new System.Windows.Controls.Primitives.ToggleButton
            {
                Content = expanded ? "\u25BC" : "\u25B6",
                FontSize = 10,
                Foreground = B(0x66, 0x66, 0x66),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 0, 4, 0),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = expanded,
                Focusable = false,
                ToolTip = expanded ? "Collapse" : "Expand"
            };
            header.Children.Add(toggle);

            if (!string.IsNullOrEmpty(seg.ToolSummary))
            {
                var summary = new TextBlock
                {
                    Text = "  " + seg.ToolSummary,
                    FontSize = 11,
                    Foreground = B(0x66, 0x66, 0x66),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 230,
                    VerticalAlignment = VerticalAlignment.Center
                };
                header.Children.Add(summary);
            }
            sp.Children.Add(header);

            var bodyPanel = new StackPanel();
            if (!string.IsNullOrEmpty(seg.ToolError))
            {
                bodyPanel.Children.Add(new TextBlock
                {
                    Text = seg.ToolError,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = B(0xC0, 0x30, 0x30),
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            if (!string.IsNullOrEmpty(seg.Text))
            {
                bodyPanel.Children.Add(new TextBlock
                {
                    Text = seg.Text,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = B(0x33, 0x33, 0x33),
                    FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            bodyPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            toggle.Checked += (s, e) =>
            {
                bodyPanel.Visibility = Visibility.Visible;
                toggle.Content = "\u25BC";
                toggle.ToolTip = "Collapse";
            };
            toggle.Unchecked += (s, e) =>
            {
                bodyPanel.Visibility = Visibility.Collapsed;
                toggle.Content = "\u25B6";
                toggle.ToolTip = "Expand";
            };

            sp.Children.Add(bodyPanel);
            card.Child = sp;
            card.Tag = new ToolCardParts { Toggle = toggle, Body = bodyPanel };
            return card;
        }

        private static void SetToolCardExpanded(FrameworkElement el, bool expanded)
        {
            if (el?.Tag is ToolCardParts p && p.Toggle != null)
                p.Toggle.IsChecked = expanded; // fires Checked/Unchecked -> updates body + glyph
        }

        private FrameworkElement BuildMcqCard(ChatSegment seg)
        {
            var card = new Border
            {
                Background = B(0xF0, 0xF8, 0xFF),
                BorderBrush = B(0x6E, 0x96, 0xBC),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(16, 2, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 420
            };

            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = seg.McqQuestion ?? "Choose an option:",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = B(0x1A, 0x1A, 0x1A),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var optionsPanel = new StackPanel();
            if (seg.McqOptions != null)
            {
                for (int i = 0; i < seg.McqOptions.Length; i++)
                {
                    string option = seg.McqOptions[i];
                    string letter = ((char)('A' + i)).ToString();
                    var btn = new Button
                    {
                        Content = $"{letter}. {option}",
                        Tag = option,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 2, 0, 2),
                        Padding = new Thickness(8, 4, 8, 4),
                        Background = Brushes.White,
                        BorderBrush = B(0xBF, 0xD3, 0xE6),
                        Cursor = Cursors.Hand
                    };
                    btn.Click += OnMcqOptionClick;
                    optionsPanel.Children.Add(btn);
                }
            }
            sp.Children.Add(optionsPanel);
            card.Child = sp;
            return card;
        }

        private void OnMcqOptionClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string option)
            {
                MessageBox.Text = option;
                SendMessage();
            }
        }

        private System.Windows.Controls.Primitives.ToggleButton MakeChatStar(string url)
        {
            var star = new System.Windows.Controls.Primitives.ToggleButton
            {
                Width = 22, Height = 22,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 4, 0),
                Cursor = Cursors.Hand,
                Focusable = false,
                IsChecked = GifFavorites.IsFavorite(url),
                ToolTip = "Favourite this GIF"
            };
            star.Template = BuildStarTemplate();
            star.Click += (s, e) =>
            {
                bool now = GifFavorites.Toggle(new GifResult { Gif = url, Preview = url });
                star.IsChecked = now;
                e.Handled = true;
            };
            return star;
        }

        private static ControlTemplate BuildStarTemplate()
        {
            var t = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0xB0, 0x00, 0x00, 0x00)));
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.TextProperty, "★"); // star glyph
            text.SetValue(TextBlock.FontSizeProperty, 13.0);
            text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            text.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xCF, 0xD8, 0xE2)));
            text.Name = "Glyph";
            border.AppendChild(text);
            t.VisualTree = border;
            var on = new Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
            on.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xFF, 0xC4, 0x3A)), "Glyph"));
            t.Triggers.Add(on);
            return t;
        }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("OpenUrl: " + ex.Message);
            }
        }

        // ===== GIF + file send =====
        private void Gif_Click(object sender, RoutedEventArgs e)
        {
            var picker = new GifPickerWindow(this);
            if (picker.ShowDialogSim() == true && picker.SelectedGif != null)
                SendGif(picker.SelectedGif);
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Share a file",
                Filter = "All files (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) == true)
                foreach (var f in dlg.FileNames)
                    SendFile(f);
        }

        private void SendGif(GifResult gif)
        {
            if (gif == null || string.IsNullOrEmpty(gif.Gif)) return;
            string token = ChatContent.BuildGif(gif.Gif);
            _messenger.SendMessage(_contact, token);
            var me = _messenger.CurrentUser != null ? _messenger.CurrentUser.Name : "Me";
            AddMessage(me, token, true);
            HistoryService.AppendLocalMessage(_contact.Mail, token, true);
            SoundManager.PlayOutgoing();
        }

        private void SendFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
            string name = System.IO.Path.GetFileName(path);
            AddSystemLine("Uploading " + name + "...");

            System.Threading.Tasks.Task.Run(() =>
            {
                var result = FileTransferService.Upload(path);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!result.Success)
                    {
                        AddSystemLine("Couldn't send " + name +
                            (string.IsNullOrEmpty(result.Error) ? "." : ": " + result.Error));
                        return;
                    }
                    string token = ChatContent.BuildFile(result.Url, result.Name, result.Size, result.Type);
                    _messenger.SendMessage(_contact, token);
                    var me = _messenger.CurrentUser != null ? _messenger.CurrentUser.Name : "Me";
                    AddMessage(me, token, true);
                    HistoryService.AppendLocalMessage(_contact.Mail, token, true);
                    SoundManager.PlayOutgoing();
                }));
            });
        }

        private void SetLastReceived()
        {
            LastReceivedText.Text = "Last message received at " +
                DateTime.Now.ToString("HH:mm") + " on " + DateTime.Now.ToString("dd/MM/yyyy") + ".";
        }

        private void AddSystemLine(string text)
        {
            MessagesPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x99, 0xA8)),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 6, 0, 6)
            });
            MessagesScroller.ScrollToEnd();
        }

        // ===== Window chrome =====
        private Rect _restoreBounds;
        private bool _isMaximized;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // When hosted inside the XP sim, window ops route to the XpWindow.
            if (DoryNative.WlmSim.WlmSimHost.IsHosted(this))
            {
                if (e.ClickCount == 2) { DoryNative.WlmSim.WlmSimHost.ToggleMaximizeWindow(this); return; }
                if (e.ButtonState == MouseButtonState.Pressed)
                    DoryNative.WlmSim.WlmSimHost.DragWindow(this);
                return;
            }
            if (e.ClickCount == 2) { ToggleMaximize(); return; }
            if (e.ButtonState == MouseButtonState.Pressed && !_isMaximized)
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => DoryNative.WlmSim.WlmSimHost.MinimizeWindow(this);

        private void Maximize_Click(object sender, RoutedEventArgs e)
            => DoryNative.WlmSim.WlmSimHost.ToggleMaximizeWindow(this);

        private void ToggleMaximize()
        {
            if (_isMaximized)
            {
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
                _isMaximized = false;
            }
            else
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);
                var wa = SystemParameters.WorkArea;
                Left = wa.Left; Top = wa.Top;
                Width = wa.Width; Height = wa.Height;
                _isMaximized = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ===== send =====
        private void MessageBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private void SendMessage()
        {
            var text = MessageBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            ClientLog.Write($"ChatWindowLive.SendMessage contact={_contact?.Mail} text={EscapeForLog(text)}");
            _messenger.SendMessage(_contact, text);
            HistoryService.AppendLocalMessage(_contact.Mail, text, true);
            RenderHistoryMessage(new HistoryMessage
            {
                Id = 0,
                SenderEmail = _messenger.CurrentUser?.Mail,
                SenderName = _messenger.CurrentUser != null ? _messenger.CurrentUser.Name : "Me",
                RecipientEmail = _contact.Mail,
                MessageType = HistoryService.ClassifyContent(text),
                Content = text,
                CreatedAt = DateTime.UtcNow,
                Read = true,
                Mine = true,
            });
            MessageBox.Text = "";
            SoundManager.PlayOutgoing();
        }

        private void NudgeButton_Click(object sender, RoutedEventArgs e)
        {
            _messenger.SendNudge(_contact);
            AddSystemLine("You have just sent a nudge.");
            HistoryService.AppendLocalEvent(_contact.Mail, "nudge", null, true);
            TrackRenderedMessage(new HistoryMessage
            {
                Id = 0,
                SenderEmail = _messenger.CurrentUser?.Mail,
                SenderName = _messenger.CurrentUser?.Name,
                RecipientEmail = _contact.Mail,
                MessageType = "nudge",
                Content = "",
                CreatedAt = DateTime.UtcNow,
                Read = true,
                Mine = true,
            });
            SoundManager.PlayNudge();
            ShakeWindow();
        }

        // ===== toolbar handlers =====
        private void Emoticon_Click(object sender, RoutedEventArgs e)
        {
            var picker = new EmoticonPicker(this);
            if (picker.ShowDialogSim() == true)
            {
                var emoticon = picker.SelectedEmoticon;
                if (!string.IsNullOrEmpty(emoticon))
                {
                    MessageBox.AppendText(emoticon);
                    MessageBox.Focus();
                }
            }
        }

        private void Wink_Click(object sender, RoutedEventArgs e)
        {
            var picker = new WinksPicker(this);
            if (picker.ShowDialogSim() == true)
            {
                var winkName = picker.SelectedWink;
                if (!string.IsNullOrEmpty(winkName))
                {
                    _messenger.SendWink(_contact, winkName);
                    DisplayWink("You", winkName + ".mct", false);
                    HistoryService.AppendLocalEvent(_contact.Mail, "wink", winkName + ".mct", true);
                }
            }
        }

        private void Voice_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSettings.AllowVoiceClips)
            {
                DoryNative.XpSim.SimMessageBox.Show(
                    "Voice clips are turned off in Options.",
                    "Windows Live Messenger");
                return;
            }

            if (_immediateCts != null)
            {
                _immediateCts.Cancel();
                return;
            }

            if (AppSettings.SttImmediateMode)
            {
                StartImmediateRecording();
                return;
            }

            var dialog = new System.Windows.Window
            {
                Title = "Voice message",
                Width = 260,
                Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Content = new System.Windows.Controls.Grid
                {
                    Children =
                    {
                        new System.Windows.Controls.TextBlock
                        {
                            Name = "RecordingLabel",
                            Text = "Hold on, listening…",
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            VerticalAlignment = System.Windows.VerticalAlignment.Center,
                            FontSize = 14
                        }
                    }
                }
            };

            var label = (System.Windows.Controls.TextBlock)((System.Windows.Controls.Grid)dialog.Content).Children[0];

            var recordTask = SttService.RecordAndTranscribeAsync(
                TimeSpan.FromSeconds(10),
                status => Dispatcher.Invoke(() => label.Text = status));

            _ = recordTask.ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    dialog.Close();
                    if (t.IsFaulted)
                    {
                        DoryNative.XpSim.SimMessageBox.Show(
                            t.Exception?.GetBaseException()?.Message ?? "Transcription failed.",
                            "Voice message");
                    }
                    else if (!string.IsNullOrWhiteSpace(t.Result))
                    {
                        var existing = MessageBox.Text ?? string.Empty;
                        MessageBox.Text = existing + (string.IsNullOrEmpty(existing) ? "" : " ") + t.Result;
                        MessageBox.Focus();
                        MessageBox.CaretIndex = MessageBox.Text.Length;
                    }
                });
            });

            dialog.ShowDialogSim();
        }

        private void VideoCall_Click(object sender, RoutedEventArgs e)
        {
            DoryNative.XpSim.SimMessageBox.Show("Voice and video calls are not available in this build.", "Windows Live Messenger");
        }

        private void VoiceOptions_Click(object sender, RoutedEventArgs e)
        {
            var menu = new System.Windows.Controls.ContextMenu();
            var immediateItem = new System.Windows.Controls.MenuItem
            {
                Header = "Immediate mode",
                IsCheckable = true,
                IsChecked = AppSettings.SttImmediateMode,
                ToolTip = "Continuously transcribe speech in chunks while the mic is active."
            };
            immediateItem.Click += (s, args) =>
            {
                AppSettings.SttImmediateMode = immediateItem.IsChecked;
            };
            menu.Items.Add(immediateItem);
            menu.PlacementTarget = (System.Windows.UIElement)sender;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private System.Threading.CancellationTokenSource _immediateCts;
        private System.Windows.Media.Animation.Storyboard _voiceGlowStoryboard;

        private void SetVoiceButtonGlow(bool glow)
        {
            if (VoiceButton == null) return;
            if (glow)
            {
                var effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Cyan,
                    ShadowDepth = 0,
                    BlurRadius = 18,
                    Opacity = 0.9
                };
                VoiceButton.Effect = effect;

                _voiceGlowStoryboard?.Stop();
                _voiceGlowStoryboard = new System.Windows.Media.Animation.Storyboard();
                var anim = new System.Windows.Media.Animation.DoubleAnimation(0.5, 1.0, TimeSpan.FromSeconds(0.7))
                {
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(anim, effect);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(anim, new System.Windows.PropertyPath("Opacity"));
                _voiceGlowStoryboard.Children.Add(anim);
                _voiceGlowStoryboard.Begin();
            }
            else
            {
                _voiceGlowStoryboard?.Stop();
                _voiceGlowStoryboard = null;
                VoiceButton.Effect = null;
            }
        }

        private void StartImmediateRecording()
        {
            if (_immediateCts != null) return;

            _immediateCts = new System.Threading.CancellationTokenSource();
            var cts = _immediateCts;
            SetVoiceButtonGlow(true);

            var recordTask = SttService.RecordAndTranscribeChunksAsync(
                TimeSpan.FromSeconds(2),
                result => Dispatcher.Invoke(() =>
                {
                    if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
                    {
                        var existing = MessageBox.Text ?? string.Empty;
                        MessageBox.Text = existing + (string.IsNullOrEmpty(existing) ? "" : " ") + result.Text;
                        MessageBox.Focus();
                        MessageBox.CaretIndex = MessageBox.Text.Length;
                    }
                }),
                status => { },
                cts.Token);

            _ = recordTask.ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    _immediateCts = null;
                    SetVoiceButtonGlow(false);
                    if (t.IsFaulted)
                    {
                        DoryNative.XpSim.SimMessageBox.Show(
                            t.Exception?.GetBaseException()?.Message ?? "Transcription failed.",
                            "Voice message");
                    }
                });
            });
        }

        private void Font_Click(object sender, RoutedEventArgs e)
        {
            // Font picker requires WinForms; cycle through a small preset list instead.
            var fonts = new[] { "Segoe UI", "Tahoma", "Arial", "Courier New", "Consolas", "Comic Sans MS" };
            var sizes = new[] { 10.0, 11.0, 12.0, 14.0, 16.0 };
            var idx = Array.IndexOf(fonts, _composeFont?.Source ?? "Segoe UI");
            _composeFont = new FontFamily(fonts[(idx + 1) % fonts.Length]);
            _composeSize = sizes[(int)Math.Round((_composeSize - 10) / 2) % sizes.Length];
            MessageBox.FontFamily = _composeFont;
            MessageBox.FontSize = _composeSize;
        }

        // ===== IBA live rendering =====
        // SSE events (thought / iba_tool_start / iba_tool_result / iba_wait /
        // iba_step_* / iba_task_init) render into per-task columns. Two or more
        // recently-active tasks share a horizontal host (side-by-side); when
        // activity drops back to one task its column moves out to full width.

        private void OnIbaEventReceived(object sender, IbaEventArgs e)
        {
            if (e.From == null || _contact == null || e.From.Mail != _contact.Mail) return;
            var name = e.EventName;
            var data = e.Data;
            Dispatcher.BeginInvoke(new Action(() => RenderIbaEvent(name, data)));
        }

        private static string JStr(JsonElement d, string name)
        {
            if (d.ValueKind == JsonValueKind.Object && d.TryGetProperty(name, out var p)
                && p.ValueKind == JsonValueKind.String)
                return p.GetString();
            return null;
        }

        private static int JInt(JsonElement d, string name)
        {
            if (d.ValueKind == JsonValueKind.Object && d.TryGetProperty(name, out var p)
                && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i))
                return i;
            return 0;
        }

        private static string PrettyJson(JsonElement el)
        {
            try { return JsonSerializer.Serialize(el, new JsonSerializerOptions { WriteIndented = true }); }
            catch { return el.ToString(); }
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("\n", " ").Replace("\r", " ");
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        private void RenderIbaEvent(string name, JsonElement d)
        {
            if (d.ValueKind != JsonValueKind.Object) return;
            // task_submitted uses "task_id"; step/tool events use "task"
            string taskId = JStr(d, "task") ?? JStr(d, "task_id");
            string taskTitle = JStr(d, "task_title");

            switch (name)
            {
                case "answer":
                case "error":
                {
                    // Answers render as normal bubbles; collapse every open
                    // reasoning block across all task columns (web GUI parity).
                    foreach (var tv in _ibaViews.Values) CloseThink(tv);
                    // Mark the finished task(s) complete immediately — without
                    // this a done column stays "active" for the 30s idle window
                    // and drags subsequent single-task work into the parallel
                    // host (non-parallel work rendering side-by-side).
                    var doneId = JStr(d, "task") ?? JStr(d, "task_id");
                    IbaTaskView layoutAnchor = null;
                    if (!string.IsNullOrEmpty(doneId) && _ibaViews.TryGetValue(doneId, out var dv))
                    {
                        dv.Completed = true;
                        dv.HasPendingTool = false;
                        dv.LastActivity = DateTime.MinValue;
                        layoutAnchor = dv;
                    }
                    else if (string.IsNullOrEmpty(doneId))
                    {
                        foreach (var tv in _ibaViews.Values)
                            if (!tv.Waiting && !tv.Completed)
                            {
                                tv.Completed = true;
                                tv.HasPendingTool = false;
                                tv.LastActivity = DateTime.MinValue;
                                layoutAnchor ??= tv;
                            }
                    }
                    if (layoutAnchor?.Column != null)
                        SyncIbaLayout(layoutAnchor);
                    return;
                }

                case "done":
                    return;

                case "iba_task_init":
                {
                    var v = EnsureIbaView(taskId, taskTitle);
                    var obj = JStr(d, "objective");
                    if (!string.IsNullOrEmpty(obj) && v.ObjectiveLine.Text.Length == 0)
                        v.ObjectiveLine.Text = "▸ " + Trim(obj, 180);
                    SyncIbaLayout(v);
                    return;
                }

                case "iba_step_start":
                {
                    var v = EnsureIbaView(taskId, taskTitle);
                    CloseThink(v);
                    int n = JInt(d, "step_number");
                    string st = JStr(d, "step_title") ?? "";
                    AddIbaLine(v, $"— Step {n}: {st} —");
                    SyncIbaLayout(v);
                    return;
                }

                case "thought":
                {
                    var v = EnsureIbaView(taskId, taskTitle);
                    // First chunk of a new reasoning block: collapse completed
                    // tool cards so the transcript reads reasoning -> tools ->
                    // reasoning in tidy collapsed sections (web GUI parity).
                    if (v.OpenThink == null)
                        foreach (var t in v.Tools.Values)
                            if (t.Seg.ToolStatus != "running")
                                SetToolCardExpanded(t.Element, false);
                    AppendThink(v, JStr(d, "content") ?? "");
                    SyncIbaLayout(v);
                    return;
                }

                case "iba_tool_start":
                {
                    var v = EnsureIbaView(taskId, taskTitle);
                    CloseThink(v);
                    string step = JStr(d, "step") ?? ("s" + v.Tools.Count);
                    string tool = JStr(d, "tool") ?? "tool";
                    string argsJson = d.TryGetProperty("args", out var a) && a.ValueKind != JsonValueKind.Undefined
                        ? PrettyJson(a) : "{}";
                    string title = JStr(d, "step_title") ?? tool;
                    var seg = new ChatSegment
                    {
                        Kind = ChatSegmentKind.Tool,
                        ToolName = tool,
                        ToolStatus = "running",
                        ToolTitle = title,
                        ToolSummary = Trim(argsJson, 80),
                        Text = argsJson
                    };
                    var el = BuildToolCard(seg);
                    v.Body.Children.Add(el);
                    v.Tools[step] = new IbaToolState { Step = step, Seg = seg, Element = el };
                    v.HasPendingTool = true;
                    SyncIbaLayout(v);
                    return;
                }

                case "iba_tool_result":
                {
                    var v = EnsureIbaView(taskId, taskTitle);
                    string step = JStr(d, "step") ?? "";
                    string status = JStr(d, "status") ?? "completed";
                    bool failed = string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);
                    if (v.Tools.TryGetValue(step, out var ts))
                    {
                        ts.Seg.ToolStatus = failed ? "failed" : "completed";
                        string resJson = d.TryGetProperty("result", out var r) ? PrettyJson(r) : "";
                        if (resJson.Length > 3000) resJson = resJson.Substring(0, 3000) + "\n… (truncated)";
                        ts.Seg.Text = failed ? ts.Seg.Text : resJson;
                        if (failed)
                            ts.Seg.ToolError = Trim(resJson, 300);
                        ts.Seg.ToolSummary = failed ? "" : Trim(resJson, 80);
                        var idx = v.Body.Children.IndexOf(ts.Element);
                        var el = BuildToolCard(ts.Seg);
                        if (idx >= 0)
                        {
                            v.Body.Children.RemoveAt(idx);
                            v.Body.Children.Insert(idx, el);
                        }
                        else v.Body.Children.Add(el);
                        ts.Element = el;
                    }
                    else
                    {
                        // Orphan result (missed start): standalone line
                        string tool = JStr(d, "tool") ?? "tool";
                        AddIbaLine(v, $"✓ {tool} — {(failed ? "failed" : "completed")}");
                    }
                    // Still active if any other tool call in this task is running.
                    v.HasPendingTool = v.Tools.Values.Any(t => t.Seg.ToolStatus == "running");
                    SyncIbaLayout(v);
                    return;
                }

                case "iba_step_completed":
                {
                    var v = EnsureIbaView(taskId, taskTitle);
                    string status = JStr(d, "status") ?? "";
                    if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                        AddIbaLine(v, "✗ Step failed", true);
                    SyncIbaLayout(v);
                    return;
                }

                case "iba_wait":
                {
                    var v = EnsureIbaView(taskId, taskTitle);
                    CloseThink(v);
                    v.Waiting = true;
                    string q = JStr(d, "text") ?? "The agent has a question:";
                    string[] opts = null;
                    if (d.TryGetProperty("options", out var op) && op.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var o in op.EnumerateArray())
                            if (o.ValueKind == JsonValueKind.String) list.Add(o.GetString());
                        if (list.Count > 0) opts = list.ToArray();
                    }
                    var seg = new ChatSegment
                    {
                        Kind = ChatSegmentKind.Mcq,
                        McqId = taskId,
                        McqQuestion = q,
                        McqOptions = opts ?? new[] { "Continue", "Stop" }
                    };
                    v.Body.Children.Add(BuildMcqCard(seg));
                    SyncIbaLayout(v);
                    return;
                }
            }
        }

        /// <summary>
        /// Render persisted IBA session history: user objectives as outgoing
        /// bubbles, per-task step groups as collapsed task columns containing
        /// reasoning expanders and completed tool cards, and the final answer
        /// as an incoming bubble.
        /// </summary>
        private void RenderIbaHistory(DoryNative.Models.SessionHistoryResponse hist)
        {
            var me = _messenger.CurrentUser != null ? _messenger.CurrentUser.Name : "Me";
            foreach (var msg in hist.Messages)
            {
                if (string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(msg.Content))
                        AddMessage(me, msg.Content, true);
                    continue;
                }

                // Assistant message: replay step groups as task columns.
                var groups = msg.StepGroups;
                if (groups == null || groups.Count == 0)
                {
                    if (msg.Steps != null && msg.Steps.Count > 0)
                    {
                        groups = msg.Steps
                            .GroupBy(s => string.IsNullOrEmpty(s.TaskId) ? msg.TaskId : s.TaskId)
                            .Select(g => new DoryNative.Models.IbaStepGroup
                            {
                                TaskId = g.Key,
                                Title = msg.TaskTitle,
                                Steps = g.ToList()
                            }).ToList();
                    }
                }

                if (groups != null)
                {
                    // Web GUI parity: multiple step groups render side-by-side
                    // in a parallel-cols block; a single group renders flat.
                    var groupViews = new List<IbaTaskView>();
                    foreach (var g in groups)
                        groupViews.Add(RenderIbaHistoryGroup(g, msg.TaskTitle));
                    if (groupViews.Count > 1)
                    {
                        EnsureIbaHost();
                        foreach (var gv in groupViews)
                        {
                            MoveIbaIntoHost(gv);
                            gv.TitleRevealed = true;
                            gv.Header.Visibility = Visibility.Visible;
                        }
                        ResizeIbaColumns();
                        // Frozen history block — release so later live phases
                        // and messages get their own hosts below it.
                        _ibaHost = null;
                    }
                }

                if (!string.IsNullOrEmpty(msg.Content))
                    AddMessage(_contact.Name, msg.Content, false);
                else if (!string.IsNullOrEmpty(msg.Reasoning) && (groups == null || groups.Count == 0))
                    AddMessage(_contact.Name, msg.Reasoning, false);

                if (string.Equals(msg.Status, "failed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(msg.Status, "waiting", StringComparison.OrdinalIgnoreCase))
                {
                    AddIbaLineSystem($"— task {msg.Status}{(string.IsNullOrEmpty(msg.Reason) ? "" : ": " + msg.Reason)} —");
                }
            }
        }

        private IbaTaskView RenderIbaHistoryGroup(DoryNative.Models.IbaStepGroup g, string fallbackTitle)
        {
            var v = EnsureIbaView(g.TaskId, string.IsNullOrEmpty(g.Title) ? fallbackTitle : g.Title);
            v.Completed = true;
            v.LastActivity = DateTime.MinValue;
            v.Waiting = false;
            v.HasPendingTool = false;

            foreach (var step in g.Steps)
            {
                if (!string.IsNullOrEmpty(step.Reasoning))
                {
                    AppendThink(v, step.Reasoning);
                    CloseThink(v);
                }

                if (string.IsNullOrEmpty(step.ActionName)) continue;

                AddIbaLine(v, $"— Step {step.StepNumber}: {step.StepTitle} —");

                string argsJson = "{}";
                if (step.ActionArgs != null && step.ActionArgs.Count > 0)
                {
                    try { argsJson = JsonSerializer.Serialize(step.ActionArgs); }
                    catch { }
                }
                bool failed = string.Equals(step.ActionState, "failed", StringComparison.OrdinalIgnoreCase);
                var seg = new ChatSegment
                {
                    Kind = ChatSegmentKind.Tool,
                    ToolName = step.ActionName,
                    ToolStatus = failed ? "failed" : "completed",
                    ToolTitle = step.StepTitle,
                    ToolSummary = Trim(argsJson, 80),
                    Text = !string.IsNullOrEmpty(step.ActionResultBody)
                        ? (step.ActionResultBody.Length > 3000 ? step.ActionResultBody.Substring(0, 3000) + "\n… (truncated)" : step.ActionResultBody)
                        : argsJson
                };
                if (failed) seg.ToolError = Trim(step.ActionResultBody, 300);
                var card = BuildToolCard(seg);
                // History renders collapsed tool cards regardless of the
                // auto-uncollapse setting — the live stream keeps them open.
                SetToolCardExpanded(card, false);
                v.Body.Children.Add(card);
            }
            CloseThink(v);
            SyncIbaLayout(v);
            return v;
        }

        /// <summary>
        /// After history loads, pick up where the session left off: tasks still
        /// executing in the backend are polled until they finish; paused/stale
        /// tasks are resumed through the chat stream so work continues visibly.
        /// </summary>
        private void MaybeContinueIbaTask(DoryNative.Models.SessionHistoryResponse hist)
        {
            var last = hist?.Messages?.LastOrDefault(m =>
                string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(m.TaskId));
            if (last == null)
            {
                StopIbaPoll();
                return;
            }

            var status = (last.Status ?? "").ToLowerInvariant();
            switch (status)
            {
                case "running":
                    // A live worker holds the task in 'running' — poll for new
                    // steps instead of spawning a duplicate runner. ('ready'
                    // falls through to the resume path: claim() flips to
                    // 'running', so 'ready' always means no worker is on it.)
                    StartIbaPoll();
                    return;
                case "waiting":
                case "completed":
                case "cancelled":
                case "failed":
                    StopIbaPoll();
                    return;
                default:
                    // paused / stale / unknown — resume unless the user paused it.
                    if ((last.Reason ?? "").StartsWith("User", StringComparison.OrdinalIgnoreCase))
                    {
                        StopIbaPoll();
                        return;
                    }
                    StopIbaPoll();
                    if (!_ibaAutoResumeTried && _contact != null)
                    {
                        _ibaAutoResumeTried = true;
                        AddIbaLineSystem($"— resuming task {last.TaskId} —");
                        _messenger.SendMessage(_contact, "/task resume " + last.TaskId);
                    }
                    return;
            }
        }

        private void StartIbaPoll()
        {
            if (_ibaPollTimer == null)
            {
                _ibaPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                _ibaPollTimer.Tick += async (_, _) => await PollIbaHistoryAsync();
            }
            _ibaPollSignature = null;
            _ibaPollTimer.Start();
        }

        private void StopIbaPoll()
        {
            _ibaPollTimer?.Stop();
        }

        private async System.Threading.Tasks.Task PollIbaHistoryAsync()
        {
            if (_contact == null || _ibaPollBusy) return;
            _ibaPollBusy = true;
            try
            {
                var hist = await _messenger.GetSessionHistoryAsync(_contact.Mail);
                if (hist?.Messages == null || hist.Messages.Count == 0) return;

                var last = hist.Messages.LastOrDefault(m =>
                    string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(m.TaskId));
                int totalSteps = hist.Messages.Sum(m =>
                    (m.Steps?.Count ?? 0) + (m.StepGroups?.Sum(g => g.Steps?.Count ?? 0) ?? 0));
                var signature = $"{last?.Status}|{last?.TaskId}|{totalSteps}|{hist.Messages.Count}";

                var status = (last?.Status ?? "").ToLowerInvariant();
                bool terminal = status is "waiting" or "completed" or "cancelled" or "failed"
                    || ((last?.Reason ?? "").StartsWith("User", StringComparison.OrdinalIgnoreCase) && status != "running" && status != "ready");
                if (terminal)
                {
                    StopIbaPoll();
                    if (signature != _ibaPollSignature)
                        await Dispatcher.InvokeAsync(() => RefreshIbaHistory(hist));
                    return;
                }

                if (signature == _ibaPollSignature) return;
                _ibaPollSignature = signature;
                await Dispatcher.InvokeAsync(() => RefreshIbaHistory(hist));
            }
            catch { /* transient backend errors — keep polling */ }
            finally { _ibaPollBusy = false; }
        }

        /// <summary>
        /// Re-render the full IBA history snapshot. Skipped while a live run is
        /// actively streaming so in-flight views are never wiped mid-step.
        /// </summary>
        private void RefreshIbaHistory(DoryNative.Models.SessionHistoryResponse hist)
        {
            // Skip only while a live run is ACTIVELY streaming — a dead stream
            // leaves OpenThink/HasPendingTool stuck forever, which would
            // otherwise freeze history refreshes permanently. Treat views
            // with no events for 90s as dead and let the snapshot through.
            var liveCutoff = DateTime.Now - TimeSpan.FromSeconds(90);
            if (_ibaViews.Values.Any(v =>
                    (v.OpenThink != null || v.HasPendingTool) && v.LastActivity > liveCutoff))
                return;
            MessagesPanel.Children.Clear();
            _ibaViews.Clear();
            _ibaHost = null;
            RenderIbaHistory(hist);
            MessagesScroller.ScrollToEnd();
            MaybeContinueIbaTask(hist);
        }

        private void AddIbaLineSystem(string text)
        {
            MessagesPanel.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Foreground = B(0x88, 0x88, 0x88),
                Margin = new Thickness(16, 3, 0, 3),
                HorizontalAlignment = HorizontalAlignment.Left
            });
        }

        private void AddIbaLine(IbaTaskView v, string text, bool isError = false)
        {
            v.Body.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Foreground = isError ? B(0xC0, 0x30, 0x30) : B(0x88, 0x88, 0x88),
                Margin = new Thickness(0, 3, 0, 3)
            });
        }

        private void AppendThink(IbaTaskView v, string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            if (v.OpenThink == null)
            {
                var exp = new Expander
                {
                    IsExpanded = true,
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Foreground = B(0x88, 0x88, 0x88),
                    Header = "Reasoning...",
                    Margin = new Thickness(0, 2, 0, 4),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                var tb = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = B(0x66, 0x66, 0x66),
                    Margin = new Thickness(4, 2, 0, 0),
                    MaxWidth = 430
                };
                exp.Content = tb;
                v.Body.Children.Add(exp);
                v.OpenThink = exp;
                v.OpenThinkBody = tb;
                v.ThinkText = "";
                v.ThinkTokens = 0;
            }
            v.ThinkText += token;
            v.OpenThinkBody.Text = v.ThinkText;
            // cheap token estimate: count whitespace-separated words in the tail
            v.ThinkTokens += token.Split(new[] { ' ', '\n', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            v.OpenThink.Header = $"Reasoning ({v.ThinkTokens} tokens)";
        }

        private void CloseThink(IbaTaskView v)
        {
            if (v.OpenThink != null)
            {
                v.OpenThink.IsExpanded = false;
                v.OpenThink = null;
                v.OpenThinkBody = null;
            }
        }

        private IbaTaskView EnsureIbaView(string taskId, string title)
        {
            if (string.IsNullOrEmpty(taskId)) taskId = "main";
            if (!_ibaViews.TryGetValue(taskId, out var v))
            {
                v = new IbaTaskView { TaskId = taskId };
                _ibaViews[taskId] = v;
            }
            v.LastActivity = DateTime.Now;
            v.Waiting = false; // any new event means the task is no longer blocked on a question
            EnsureIbaColumn(v);
            if (!string.IsNullOrEmpty(title) && v.TaskTitle != title)
            {
                v.TaskTitle = title;
                v.Header.Text = title;
            }
            return v;
        }

        private void EnsureIbaColumn(IbaTaskView v)
        {
            if (v.Column != null) return;
            var body = new StackPanel();
            var header = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = B(0x44, 0x44, 0x44),
                Margin = new Thickness(0, 0, 0, 2),
                TextTrimming = TextTrimming.CharacterEllipsis,
                // Web GUI hides the column title until a second column proves
                // this is a parallel run; revealed permanently after that.
                Visibility = v.TitleRevealed ? Visibility.Visible : Visibility.Collapsed
            };
            if (!string.IsNullOrEmpty(v.TaskTitle)) header.Text = v.TaskTitle;
            var obj = new TextBlock
            {
                FontSize = 10,
                Foreground = B(0x99, 0x99, 0x99),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4),
                MaxHeight = 34
            };
            var inner = new StackPanel();
            inner.Children.Add(header);
            inner.Children.Add(obj);
            inner.Children.Add(body);
            v.Column = new Border
            {
                BorderBrush = B(0xBF, 0xD3, 0xE6),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF)),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(16, 2, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = inner
            };
            v.Body = body;
            v.Header = header;
            v.ObjectiveLine = obj;
            MessagesPanel.Children.Add(v.Column);
        }

        private bool IbaViewActive(IbaTaskView v)
        {
            if (v.Completed || v.Waiting) return false;
            if (v.HasPendingTool) return true; // long tool calls produce no events
            return DateTime.Now - v.LastActivity < IbaParallelWindow;
        }

        private void SyncIbaLayout(IbaTaskView current)
        {
            int active = _ibaViews.Values.Count(IbaViewActive);
            if (active >= 2)
            {
                EnsureIbaHost();
                foreach (var v in _ibaViews.Values)
                    if (IbaViewActive(v)) MoveIbaIntoHost(v);
                // A second column proves a parallel run — reveal all titles
                // (web GUI: titles stay hidden for single-task columns).
                if (_ibaHost.Children.Count >= 2)
                    foreach (var v in _ibaViews.Values)
                        if (v.Column != null && v.Column.Parent == _ibaHost)
                        {
                            v.TitleRevealed = true;
                            v.Header.Visibility = Visibility.Visible;
                        }
                ResizeIbaColumns();
            }
            else
            {
                var host = _ibaHost;
                // Collapse back to full width: the surviving task's column moves
                // below the frozen parallel block and keeps growing there.
                if (host != null && current.Column.Parent == host)
                {
                    // The column's logical parent is the host panel, not
                    // MessagesPanel — remove it from its actual parent first
                    // or Children.Add throws "already the logical child".
                    if (current.Column.Parent is Panel colParent)
                        colParent.Children.Remove(current.Column);
                    MessagesPanel.Children.Add(current.Column);
                    current.Column.HorizontalAlignment = HorizontalAlignment.Stretch;
                    current.Column.ClearValue(WidthProperty);
                    current.Column.ClearValue(MinWidthProperty);
                    current.Column.ClearValue(MaxWidthProperty);
                }
                // Clean handoff (web GUI parity): the parallel block is now
                // frozen — release our reference so the NEXT parallel phase
                // builds a fresh host appended below instead of mixing new
                // columns into this finished block. An empty host is removed;
                // one still holding frozen columns stays rendered.
                if (host != null)
                {
                    if (host.Children.Count == 0)
                        MessagesPanel.Children.Remove(host);
                    _ibaHost = null;
                }
            }
            MessagesScroller.ScrollToEnd();
        }

        private void EnsureIbaHost()
        {
            if (_ibaHost != null) return;
            _ibaHost = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top
            };
            _ibaHost.SizeChanged += (s, e) => ResizeIbaColumns();
            MessagesPanel.Children.Add(_ibaHost);
        }

        private void MoveIbaIntoHost(IbaTaskView v)
        {
            if (v.Column.Parent == _ibaHost) return;
            if (v.Column.Parent is Panel p) p.Children.Remove(v.Column);
            v.Column.Margin = new Thickness(0, 0, 10, 4);
            v.Column.HorizontalAlignment = HorizontalAlignment.Left;
            v.Column.MinWidth = 280;
            v.Column.MaxWidth = 480;
            _ibaHost.Children.Add(v.Column);
        }

        private void ResizeIbaColumns()
        {
            if (_ibaHost == null || _ibaHost.Children.Count == 0) return;
            double avail = MessagesPanel.ActualWidth - 40;
            if (avail < 300) avail = 560;
            double w = Math.Max(280, avail / _ibaHost.Children.Count - 12);
            foreach (UIElement c in _ibaHost.Children)
                if (c is FrameworkElement fe) fe.Width = w;
        }

        protected override void OnClosed(EventArgs e)
        {
            _immediateCts?.Cancel();
            _messenger.MessageReceived -= OnMessageReceived;
            _messenger.ContactStatusChanged -= OnContactStatusChanged;
            _messenger.TypingReceived -= OnTypingReceived;
            _messenger.NudgeReceived -= OnNudgeReceived;
            _messenger.WinkReceived -= OnWinkReceived;
            _messenger.ContactDisplayImageChanged -= OnDisplayImageChanged;
            _messenger.IbaEventReceived -= OnIbaEventReceived;
            ContactStyleManager.StyleChanged -= OnContactStyleChanged;
            _typingClearTimer?.Stop();
            _historySyncTimer?.Stop();
            _ibaPollTimer?.Stop();
            base.OnClosed(e);
        }
    }
}



