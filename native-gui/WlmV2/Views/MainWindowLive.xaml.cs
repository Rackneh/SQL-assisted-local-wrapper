using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MSNPSharp;
using WLMRevivalV2Desktop.Protocol;
using WLMRevivalV2Desktop.Services;
using WLMRevivalV2Desktop.Views.Controls;
using DoryNative.XpSim;
using MessageBox = DoryNative.XpSim.SimMessageBox;

namespace WLMRevivalV2Desktop.Views
{
    // WLM Live (2011 / Wave 4) skin of the contact list. Same protocol logic
    // as MainWindow, different chrome. Selected via Options > Layout.
    public partial class MainWindowLive : Window
    {
        private readonly MessengerService _messenger;
        private readonly Action _onSignOut;
        private readonly Dictionary<string, Window> _openChats = new Dictionary<string, Window>();
        private const string PmPrompt = "Share a quick message";

        public MainWindowLive(MessengerService messenger, Action onSignOut = null)
        {
            InitializeComponent();
            _messenger = messenger;
            _onSignOut = onSignOut;

            _messenger.ContactListSynchronized += (s, e) =>
            {
                RefreshContacts();
                UpdateCurrentUserVisual();
                // Fetch styles for all contacts
                foreach (var c in _messenger.ForwardList)
                    ContactStyleManager.FetchStyleAsync(c.Mail);
            };
            _messenger.ContactStatusChanged += (s, e) => { RefreshContacts(); UpdateCurrentUserVisual(); };
            _messenger.ContactAddedToList += (s, e) => RefreshContacts();
            _messenger.ContactRemovedFromList += (s, e) => RefreshContacts();
            _messenger.GroupsChanged += (s, e) => RefreshContacts();
            _messenger.ContactDisplayImageChanged += (s, e) =>
            {
                RefreshContacts();
                UpdateCurrentUserVisual();
            };
            ContactStyleManager.StyleChanged += (s, e) =>
            {
                Dispatcher.Invoke(RefreshContacts);
            };
            _messenger.ContactSignedIn += OnContactSignedIn;
            _messenger.ContactRequestReceived += OnContactRequestReceived;
            _messenger.MessageReceived += (s, e) =>
            {
                if (ChatContent.IsGhostMessage(e.Text))
                    return;
                if (!_openChats.ContainsKey(e.From.Mail))
                    OpenChat(e.From, e.Text);
            };
            _messenger.SignedOff += (s, e) => ConnectionStatus.Text = "Disconnected";

            if (_messenger.CurrentUser != null)
            {
                UserNameText.Text = _messenger.CurrentUser.Name;
                ConnectionStatus.Text = "127.0.0.1:1863";
                UpdateCurrentUserVisual();
            }

            RefreshContacts();
            ApplyLayoutSettings();
            ApplyContactListScene(AppSettings.ContactListScene);

            BotContactHelper.Refresh();
        }

        // ===== WLM asset helpers =====
        private static BitmapImage WlmImage(string fileName)
        {
            return new BitmapImage(new Uri(
                "pack://application:,,,/Assets/wlm/" + fileName, UriKind.Absolute));
        }

        private static string StatusFrameSmall(PresenceStatus status)
        {
            switch (status)
            {
                case PresenceStatus.Online: return "status_frame_online_small.png";
                case PresenceStatus.Busy: return "status_frame_busy_small.png";
                case PresenceStatus.Away:
                case PresenceStatus.Idle: return "status_frame_away_small.png";
                default: return "status_frame_offline_small.png";
            }
        }

        private static string StatusDot(PresenceStatus status)
        {
            switch (status)
            {
                case PresenceStatus.Online: return "status_online_dot.png";
                case PresenceStatus.Busy: return "status_busy_dot.png";
                case PresenceStatus.Away:
                case PresenceStatus.Idle: return "status_away_dot.png";
                default: return "status_offline_dot.png";
            }
        }

        private static bool IsOffline(PresenceStatus s)
        {
            return s == PresenceStatus.Offline || s == PresenceStatus.Hidden || s == PresenceStatus.Unknown;
        }

        // ===== Current user header =====
        private void UpdateCurrentUserVisual()
        {
            var currentUser = _messenger.CurrentUser;
            if (currentUser == null) return;

            UserNameText.Text = currentUser.Name;
            HeaderStatusFrame.Source = AvatarHelper.WlmImage(AvatarHelper.StatusFrameSmall(currentUser.Status));

            var dp = _messenger.GetCurrentUserDisplayImage();
            HeaderTile.Source = dp ?? AvatarHelper.WlmImage("usertile_default.png");

            var pm = _messenger.GetPersonalMessage();
            PersonalMessageBox.Text = string.IsNullOrEmpty(pm) ? PmPrompt : pm;

            this.Icon = AvatarHelper.StatusApplicationIcon(currentUser.Status);
        }

        // ===== Avatar composite (status frame + display picture) =====
        private FrameworkElement BuildAvatar(Contact contact, double frameSize)
        {
            var grid = new Grid { Width = frameSize, Height = frameSize };

            var frame = new Image
            {
                Source = AvatarHelper.WlmImage(AvatarHelper.StatusFrameSmall(contact.Status)),
                Stretch = Stretch.Fill
            };
            Panel.SetZIndex(frame, 0);

            double tileSize = Math.Round(frameSize * 0.62);
            var tile = new Border
            {
                Width = tileSize,
                Height = tileSize,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Clip = new RectangleGeometry(new Rect(0, 0, tileSize, tileSize), 2, 2)
            };
            var dp = MessengerService.TryGetDisplayImage(contact);
            tile.Child = new Image
            {
                Source = dp ?? AvatarHelper.WlmImage("usertile_default.png"),
                Stretch = Stretch.UniformToFill
            };
            Panel.SetZIndex(tile, 1);

            grid.Children.Add(frame);
            grid.Children.Add(tile);
            return grid;
        }

        // Wave-4 rows: favorites = DP tile + name (size from layout settings);
        // everyone else = avatar or status dot + name (+ grey PM inline).
        private ListBoxItem BuildContactItem(Contact contact, bool offline, bool tileRow = false)
        {
            var pm = contact.PersonalMessage != null ? contact.PersonalMessage.Message : null;
            string displayName = GetDisplayName(contact);

            // Content row (avatar + text)
            var contentRow = new StackPanel { Orientation = Orientation.Horizontal };
            if (tileRow)
            {
                double favSize = AppSettings.FavoritesViewStyle switch
                {
                    "Large" => 54,
                    "Medium" => 40,
                    _ => 30
                };
                var avatar = AvatarHelper.BuildAvatar(contact, favSize);
                avatar.Margin = new Thickness(0, 1, 7, 1);
                contentRow.Children.Add(avatar);

                var nameBlock = new TextBlock
                {
                    Text = displayName,
                    FontSize = 12,
                    Foreground = offline
                        ? new SolidColorBrush(Color.FromRgb(0x9A, 0xA7, 0xB3))
                        : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                contentRow.Children.Add(nameBlock);
            }
            else
            {
                string otherStyle = AppSettings.OtherContactsViewStyle;
                if (otherStyle == "Large" || otherStyle == "Medium")
                {
                    double size = otherStyle == "Large" ? 40 : 30;
                    var avatar = AvatarHelper.BuildAvatar(contact, size);
                    avatar.Margin = new Thickness(0, 1, 7, 1);
                    contentRow.Children.Add(avatar);
                }
                else
                {
                    contentRow.Children.Add(new Image
                    {
                        Source = AvatarHelper.WlmImage(AvatarHelper.StatusDot(offline ? PresenceStatus.Offline : contact.Status)),
                        Width = 10,
                        Height = 10,
                        Margin = new Thickness(6, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                var line = new TextBlock
                {
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                line.Inlines.Add(new System.Windows.Documents.Run(displayName)
                {
                    Foreground = offline
                        ? new SolidColorBrush(Color.FromRgb(0x9A, 0xA7, 0xB3))
                        : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
                });
                if (AppSettings.ShowStatusInLabels && !offline)
                {
                    line.Inlines.Add(new System.Windows.Documents.Run(" - " + AvatarHelper.StatusLabel(contact.Status))
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA7, 0xB3))
                    });
                }
                if (!offline && !string.IsNullOrEmpty(pm))
                {
                    line.Inlines.Add(new System.Windows.Documents.Run("  " + pm)
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA7, 0xB3))
                    });
                }
                contentRow.Children.Add(line);
            }

            // Build contact item with optional banner background
            var item = new ListBoxItem
            {
                Tag = contact,
                Padding = new Thickness(6, 2, 6, 2),
                ContextMenu = BuildContactContextMenu(contact)
            };

            // Port-5000 semantics: single click on a session opens its chat.
            item.AddHandler(System.Windows.UIElement.MouseLeftButtonUpEvent,
                new System.Windows.Input.MouseButtonEventHandler((s, e) => OpenChat(contact)), true);

            // Layer the contact's banner + colour scheme behind the row (banner
            // fades to transparent on the right). Centralised in ContactRowDecorator
            // so both Live skins render identically.
            item.Content = ContactRowDecorator.Decorate(contentRow, contact.Mail);

            return item;
        }

        private static string GetDisplayName(Contact contact)
        {
            return AppSettings.LabelContactsBy switch
            {
                "Nickname" => contact.NickName ?? contact.Name,
                "E-mail address" => contact.Mail ?? contact.Name,
                _ => contact.Name
            };
        }

        private static string StatusLabel(PresenceStatus status)
        {
            switch (status)
            {
                case PresenceStatus.Online: return "Available";
                case PresenceStatus.Busy: return "Busy";
                case PresenceStatus.Away:
                case PresenceStatus.Idle: return "Away";
                case PresenceStatus.Hidden: return "Appear offline";
                default: return "Offline";
            }
        }

        // ===== Per-contact right-click menu (port-5000 session semantics) =====
        private ContextMenu BuildContactContextMenu(Contact contact)
        {
            var menu = new ContextMenu();
            var isDefault = string.Equals(contact.Mail, "default", StringComparison.OrdinalIgnoreCase);
            var isActive = string.Equals(contact.Mail, _messenger.ActiveSessionId, StringComparison.OrdinalIgnoreCase);

            var chat = new MenuItem { Header = "Open Chat" };
            chat.Click += (s, e) => OpenChat(contact);
            menu.Items.Add(chat);

            menu.Items.Add(new Separator());

            var activate = new MenuItem
            {
                Header = isActive ? "✓ Active Session" : "Set as Active"
            };
            if (!isActive)
                activate.Click += async (s, e) =>
                {
                    await _messenger.SetActiveSessionAsync(contact.Mail);
                    RefreshContacts();
                };
            else
                activate.IsEnabled = false;
            menu.Items.Add(activate);

            if (!isDefault)
            {
                menu.Items.Add(new Separator());
                // Two-step inline confirm, like the port-5000 session menu.
                // StaysOpenOnClick keeps the menu open for the confirming click;
                // armed state resets whenever the menu closes so a stale arm
                // can't delete on a later open.
                var armed = false;
                var del = new MenuItem { Header = "Delete Session", StaysOpenOnClick = true };
                del.Click += async (s, e) =>
                {
                    if (!armed)
                    {
                        armed = true;
                        del.Header = "⚠ Click again to confirm delete";
                        return;
                    }
                    try
                    {
                        await _messenger.RemoveContactAsync(contact);
                        menu.IsOpen = false;
                        RefreshContacts();
                    }
                    catch (Exception ex) { DoryNative.XpSim.SimMessageBox.Show("Delete failed: " + ex.Message, "Windows Live Messenger"); }
                };
                menu.Opened += (s, e) => { armed = false; del.Header = "Delete Session"; };
                menu.Items.Add(del);
            }

            return menu;
        }

        private void RefreshContacts()
        {
            FavoritesList.Items.Clear();
            AvailableList.Items.Clear();
            OfflineList.Items.Clear();
            GroupsPanel.Children.Clear();

            var all = new List<Contact>(_messenger.ForwardList);
            var groups = new List<ContactGroup>(_messenger.Groups);

            bool showOffline = AppSettings.ShowOfflineContacts;
            bool sortByStatus = AppSettings.SortContactsBy == "Status";

            int fav = 0, avail = 0, off = 0;

            // Build sorted contact list
            var sortedContacts = sortByStatus
                ? all.OrderBy(c => AvatarHelper.IsOffline(c.Status) ? 1 : 0)
                     .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                     .ToList()
                : all.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var contact in sortedContacts)
            {
                bool offline = AvatarHelper.IsOffline(contact.Status);

                if (_messenger.IsFavorite(contact))
                {
                    FavoritesList.Items.Add(BuildContactItem(contact, offline, tileRow: true));
                    fav++;
                }

                if (IsInAnyGroup(contact, groups)) continue;

                if (offline)
                {
                    if (showOffline)
                    {
                        OfflineList.Items.Add(BuildContactItem(contact, true));
                        off++;
                    }
                }
                else
                {
                    AvailableList.Items.Add(BuildContactItem(contact, false));
                    avail++;
                }
            }

            int groupCount = 0;
            foreach (var group in groups)
            {
                var membersQuery = all.Where(c => ContactInGroup(c, group));
                if (sortByStatus)
                    membersQuery = membersQuery.OrderBy(c => AvatarHelper.IsOffline(c.Status) ? 1 : 0)
                                               .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);
                else
                    membersQuery = membersQuery.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

                var members = membersQuery.ToList();

                if (members.Count == 0) continue;
                groupCount++;

                int online = members.Count(c => !AvatarHelper.IsOffline(c.Status));

                var memberBox = new ListBox
                {
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                ScrollViewer.SetHorizontalScrollBarVisibility(memberBox, ScrollBarVisibility.Disabled);
                memberBox.MouseDoubleClick += ContactList_MouseDoubleClick;

                foreach (var contact in members)
                {
                    bool offline = AvatarHelper.IsOffline(contact.Status);
                    if (!showOffline && offline) continue;
                    memberBox.Items.Add(BuildContactItem(contact, offline));
                }

                var expander = new Expander
                {
                    Style = (Style)FindResource("GroupExpanderLive"),
                    IsExpanded = true,
                    Header = new TextBlock
                    {
                        Text = group.Name + "  (" + online + "/" + members.Count + ")",
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55))
                    },
                    Content = memberBox
                };
                GroupsPanel.Children.Add(expander);
            }

            FavoritesCount.Text = fav > 0 ? "(" + fav + ")" : "";
            GroupsCount.Text = groupCount > 0 ? "(" + groupCount + ")" : "";
            AvailableCount.Text = "(" + avail + ")";
            OfflineCount.Text = showOffline ? "(" + off + ")" : "";
            OfflineList.Visibility = showOffline ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool ContactInGroup(Contact contact, ContactGroup group)
        {
            if (contact.ContactGroups == null) return false;
            foreach (var g in contact.ContactGroups)
                if (g != null && g.Guid == group.Guid) return true;
            return false;
        }

        private static bool IsInAnyGroup(Contact contact, List<ContactGroup> groups)
        {
            foreach (var g in groups)
                if (ContactInGroup(contact, g)) return true;
            return false;
        }

        // ===== Sign-in toast / contact request =====
        private void OnContactSignedIn(object sender, ContactPresenceEventArgs e)
        {
            RefreshContacts();
            SoundManager.PlayOnline();
            if (!AppSettings.ShowNotifications || e.Contact == null) return;
            var img = MessengerService.TryGetDisplayImage(e.Contact) ?? AvatarHelper.WlmImage("usertile_default.png");
            try { new ContactNotification(e.Contact.Name, e.Contact.Mail, img).ShowSim(); }
            catch { /* notification is best-effort */ }
        }

        private void OnContactRequestReceived(object sender, ContactPresenceEventArgs e)
        {
            if (e.Contact == null) return;
            var r = DoryNative.XpSim.SimMessageBox.Show(
                $"{e.Contact.Name} ({e.Contact.Mail}) has added you.\n\nAdd them to your contact list?",
                "Contact request", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                try { _messenger.AddContact(e.Contact.Mail); RefreshContacts(); }
                catch (Exception ex)
                {
                    ClientLog.Write("AddContact (accept request) for " + e.Contact?.Mail, ex);
                    // The server-side add often succeeds even when MSNPSharp throws
                    // while parsing a later sync response, so refresh rather than
                    // leaving the list stale.
                    RefreshContacts();
                    DoryNative.XpSim.SimMessageBox.Show("Add failed: " + ex.Message + "\n\nDetails were written to the log.",
                        "Windows Live Messenger");
                }
            }
        }

        // ===== Chat =====
        private void ContactList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListBox list && list.SelectedItem is ListBoxItem item && item.Tag is Contact contact)
                OpenChat(contact);
        }

        private void OpenChat(Contact contact, string initialMessage = null)
        {
            if (_openChats.ContainsKey(contact.Mail))
            {
                _openChats[contact.Mail].Focus();
                return;
            }

            // Chat windows live inside the XP sim as XpWindow chrome — the V2
            // window is parked off-screen and only its content is shown.
            var chat = DoryNative.WlmSim.WlmSimHost.OpenChatWindow(_messenger, contact);
            if (chat == null) return;
            chat.Closed += (s, e) => _openChats.Remove(contact.Mail);
            _openChats[contact.Mail] = chat;

            if (!string.IsNullOrEmpty(initialMessage) && chat is ChatWindowLive cwl)
                cwl.ShowInitialMessage(initialMessage);
        }

        // ===== Status dropdown (also hosts Sign out) =====
        private int _lastStatusIndex = 0;

        private void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = StatusCombo.SelectedItem as ComboBoxItem;
            var label = item?.Content?.ToString();

            if (label == "Sign out")
            {
                StatusCombo.SelectedIndex = _lastStatusIndex;
                SignOut();
                return;
            }

            if (_messenger?.Owner == null) return;

            PresenceStatus newStatus;
            switch (label)
            {
                case "(Available)": newStatus = PresenceStatus.Online; break;
                case "(Busy)": newStatus = PresenceStatus.Busy; break;
                case "(Away)": newStatus = PresenceStatus.Away; break;
                case "(Appear offline)": newStatus = PresenceStatus.Hidden; break;
                default: return;
            }

            _lastStatusIndex = StatusCombo.SelectedIndex;
            _messenger.SetStatus(newStatus);
            UpdateCurrentUserVisual();
        }

        // ===== Personal message =====
        private void PersonalMessageBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                CommitPersonalMessage();
                System.Windows.Input.Keyboard.ClearFocus();
            }
        }

        private void PersonalMessageBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitPersonalMessage();
        }

        private void CommitPersonalMessage()
        {
            var text = PersonalMessageBox.Text;
            if (text == PmPrompt) return;
            _messenger.SetPersonalMessage(text);
        }

        // ===== Search =====
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;

            var q = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(q)) { ShowAll(); return; }

            FilterList(AvailableList, q);
            FilterList(OfflineList, q);
            FilterList(FavoritesList, q);
            foreach (var child in GroupsPanel.Children)
                if (child is Expander ex && ex.Content is ListBox lb)
                    FilterList(lb, q);
        }

        private void ShowAll()
        {
            foreach (var l in new[] { AvailableList, OfflineList, FavoritesList })
                foreach (ListBoxItem it in l.Items)
                    it.Visibility = Visibility.Visible;
            foreach (var child in GroupsPanel.Children)
                if (child is Expander ex && ex.Content is ListBox lb)
                    foreach (ListBoxItem it in lb.Items)
                        it.Visibility = Visibility.Visible;
        }

        private void FilterList(ListBox list, string q)
        {
            foreach (ListBoxItem it in list.Items)
            {
                var c = it.Tag as Contact;
                bool match = c != null &&
                    ((c.Name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     (c.Mail ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
                it.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ===== Window chrome =====
        private Rect _restoreBounds;
        private bool _isMaximized;

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // When hosted inside the XP sim, window ops route to the XpWindow.
            if (DoryNative.WlmSim.WlmSimHost.IsHosted(this))
            {
                if (e.ClickCount == 2) { DoryNative.WlmSim.WlmSimHost.ToggleMaximizeWindow(this); return; }
                if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                    DoryNative.WlmSim.WlmSimHost.DragWindow(this);
                return;
            }
            if (e.ClickCount == 2) { ToggleMaximize(); return; }
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed && !_isMaximized)
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

        // ===== Sign out (reached via the status dropdown) =====
        private void SignOut()
        {
            if (_onSignOut != null) _onSignOut();
            else { _messenger.Logout(); Close(); }
        }

        // ===== Toolbar handlers =====
        private void AddContact_Click(object sender, RoutedEventArgs e)
        {
            // Port-5000 semantics: "contacts" are IBA sessions.
            var dialog = new NewSessionDialog(this);
            if (dialog.ShowDialogSim() != true) return;
            _ = Task.Run(async () =>
            {
                var id = await _messenger.CreateSessionAsync(dialog.SessionName, dialog.Description);
                Dispatcher.Invoke(() =>
                {
                    ConnectionStatus.Text = string.IsNullOrEmpty(id) ? "Could not create session" : $"Session '{id}' created";
                    if (!string.IsNullOrEmpty(id)) OpenChatByMail(id);
                });
            });
        }

        private void OpenChatByMail(string sessionId)
        {
            var contact = _messenger.ForwardList.FirstOrDefault(c =>
                string.Equals(c.Mail, sessionId, StringComparison.OrdinalIgnoreCase));
            if (contact != null) OpenChat(contact);
        }

        private void Mail_Click(object sender, RoutedEventArgs e)
        {
            DoryNative.XpSim.SimMessageBox.Show("This revival server does not provide a web inbox.",
                "Windows Live Messenger");
        }

        private void Scene_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SceneDialog { Owner = this };
            if (dialog.ShowDialogSim() != true) return;

            // Sync banner and color scheme to server so contacts can see them
            dialog.SyncToServer();

            // The first scene (0001.png) is the default banner â€“ selecting it
            // clears any custom scene overlay so only the colour scheme shows.
            string fname = System.IO.Path.GetFileName(dialog.SelectedScene ?? "");
            bool clearScene = string.IsNullOrEmpty(dialog.SelectedScene)
                           || fname.StartsWith("0001", StringComparison.OrdinalIgnoreCase);

            if (clearScene)
            {
                AppSettings.ContactListScene = string.Empty;
                ApplyContactListScene(string.Empty);
                // Switch to the default no-scene window so the banner anchors to
                // the bottom of the contact list instead of the search bar.
                ((DoryNative.App)Application.Current).ReopenMainWindow();
                return;
            }
            else
            {
                AppSettings.ContactListScene = dialog.SelectedScene;
                ApplyContactListScene(dialog.SelectedScene);
            }
        }

        private void ApplyContactListScene(string sceneRel)
        {
            // BannerBackground carries the colour-scheme banner across the
            // header + search area and is always visible.
            BannerBackground.Visibility = Visibility.Visible;

            if (string.IsNullOrEmpty(sceneRel))
            {
                // Default: colour scheme only, no custom scene overlay.
                HeaderSceneImage.Visibility = Visibility.Collapsed;
                return;
            }

            BitmapImage src = null;

            // 1) Try the local output directory
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "wlm", sceneRel);
            if (File.Exists(localPath))
            {
                try { src = new BitmapImage(new Uri(localPath)); }
                catch { }
            }

            // 2) Try the shared repo directory
            if (src == null)
            {
                string sharedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "shared", "assets", "scenes", Path.GetFileName(sceneRel));
                if (File.Exists(sharedPath))
                {
                    try { src = new BitmapImage(new Uri(sharedPath)); }
                    catch { }
                }
            }

            // 3) Fall back to pack URI
            if (src == null)
            {
                try
                {
                    src = new BitmapImage(new Uri(
                        "pack://application:,,,/Assets/wlm/" + sceneRel, UriKind.Absolute));
                }
                catch { }
            }

            if (src != null)
            {
                HeaderSceneImage.Source = src;
                HeaderSceneImage.Visibility = Visibility.Visible;
            }
            else
            {
                HeaderSceneImage.Visibility = Visibility.Collapsed;
            }
        }

        private void Layout_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OptionsDialog(_messenger) { Owner = this };
            dialog.ShowDialogSim();

            // Style switched back to WLM 2009: re-open the contact list in that skin.
            if (AppSettings.UiStyle == "WLM2009")
            {
                ((DoryNative.App)Application.Current).ReopenMainWindow();
                return;
            }

            ApplyLayoutSettings();
            UpdateCurrentUserVisual();
            RefreshContacts();
        }

        private void CreateGroup_Click(object sender, RoutedEventArgs e)
        {
            var name = PromptDialog.Show(this, "Create a group", "Group name:");
            if (!string.IsNullOrEmpty(name))
            {
                try { _messenger.CreateGroup(name); }
                catch (Exception ex) { DoryNative.XpSim.SimMessageBox.Show("Could not create group: " + ex.Message, "Windows Live Messenger"); }
            }
        }

        private void ApplyLayoutSettings()
        {
            FavoritesList.Visibility = AppSettings.ShowFavorites ? Visibility.Visible : Visibility.Collapsed;
            GroupsExpander.Visibility = AppSettings.ShowGroups ? Visibility.Visible : Visibility.Collapsed;
        }

        // ===== Display picture =====
        private void HeaderTile_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dialog = new DisplayPictureDialog { Owner = this };
            if (dialog.ShowDialogSim() == true && !string.IsNullOrEmpty(dialog.SelectedPicturePath))
            {
                try
                {
                    HeaderTile.Source = new BitmapImage(new Uri(dialog.SelectedPicturePath));
                    _messenger.SetDisplayPicture(dialog.SelectedPicturePath);
                }
                catch (Exception ex)
                {
                    DoryNative.XpSim.SimMessageBox.Show($"Failed to update display picture on server: {ex.Message}",
                        "Windows Live Messenger", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}




