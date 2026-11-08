// Backend-backed MessengerService.
// Preserves the public API surface the V2 views were written against, but the
// "contacts" are IBA sessions served by the Python FastAPI backend and messages
// stream through /api/chat/stream (SSE) instead of MSNPSharp.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text.Json;
using DoryNative.Models;
using DoryNative.Services;
using MSNPSharp;

namespace WLMRevivalV2Desktop.Protocol
{
    public class MessengerService
    {
        private readonly BackendApi _backend;
        private readonly Dispatcher _uiDispatcher;
        private readonly Dictionary<string, Contact> _contacts =
            new Dictionary<string, Contact>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ContactGroup> _groups = new List<ContactGroup>();
        private readonly HashSet<string> _favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DisplayImage> _displayImages =
            new Dictionary<string, DisplayImage>(StringComparer.OrdinalIgnoreCase);

        private Contact _currentUser;
        private string _personalMessage = string.Empty;
        private string _displayName;
        private Timer? _pollTimer;
        private bool _started;
        private int _streamCounter;

        public MessengerService(BackendApi backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _currentUser = new Contact
            {
                Mail = "me@localhost",
                Name = Environment.UserName,
                Status = PresenceStatus.Online
            };
        }

        // ---- events (same surface as the MSNPSharp-based original) ----
        public event EventHandler? SignedIn;
        public event EventHandler<MessengerErrorEventArgs>? SignInFailed;
        public event EventHandler<SignedOffEventArgs>? SignedOff;
        public event EventHandler? ContactListSynchronized;
        public event EventHandler<ContactPresenceEventArgs>? ContactStatusChanged;
        public event EventHandler<IncomingMessageEventArgs>? MessageReceived;
        public event EventHandler<ContactPresenceEventArgs>? ContactSignedIn;
        public event EventHandler<ContactPresenceEventArgs>? ContactAddedToList;
        public event EventHandler<ContactPresenceEventArgs>? ContactRemovedFromList;
        public event EventHandler<ContactPresenceEventArgs>? ContactRequestReceived;
        public event EventHandler<ContactPresenceEventArgs>? TypingReceived;
        public event EventHandler<ContactPresenceEventArgs>? NudgeReceived;
        public event EventHandler<WinkReceivedEventArgs>? WinkReceived;
        public event EventHandler? GroupsChanged;
        public event EventHandler<ContactPresenceEventArgs>? ContactDisplayImageChanged;

        /// <summary>
        /// Raw IBA SSE events (thought, iba_tool_start, iba_tool_result, iba_wait,
        /// iba_step_start, iba_task_init, answer, done, error...) forwarded to the
        /// chat window for rich rendering.
        /// </summary>
        public event EventHandler<IbaEventArgs>? IbaEventReceived;

        // ---- public API ----
        public object? Underlying => null;

        public Contact? CurrentUser => _currentUser;
        public Contact? Owner => CurrentUser;

        public IEnumerable<Contact> ForwardList
        {
            get
            {
                lock (_contacts)
                {
                    return _contacts.Values
                        .Where(c => !_blocked.Contains(c.Mail))
                        .OrderByDescending(c => _favorites.Contains(c.Mail))
                        .ThenBy(c => c.Name ?? c.Mail)
                        .ToList();
                }
            }
        }

        public IEnumerable<ContactGroup> Groups
        {
            get { lock (_contacts) return _groups.ToList(); }
        }

        public Contact? GetContact(string email)
        {
            lock (_contacts) return _contacts.TryGetValue(email ?? "", out var c) ? c : null;
        }

        public bool IsFavorite(Contact contact)
        {
            return contact != null && _favorites.Contains(contact.Mail);
        }

        // ---- lifecycle ----
        public void Login(string email, string password)
        {
            // No real auth — the backend is localhost. Just start polling.
            Start();
            Marshal(() => SignedIn?.Invoke(this, EventArgs.Empty));
        }

        public void Logout()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
            _started = false;
            Marshal(() => SignedOff?.Invoke(this, new SignedOffEventArgs { Reason = "Signed out" }));
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
            _ = RefreshContactsAsync();
            _pollTimer = new Timer(_ => _ = RefreshContactsAsync(), null,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        private async Task RefreshContactsAsync()
        {
            try
            {
                var sessions = await _backend.GetSessionsAsync();
                ActiveSessionId = sessions.ActiveSession ?? "default";
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var changed = false;

                lock (_contacts)
                {
                    foreach (var s in sessions.Sessions)
                    {
                        var mail = s.SessionId;
                        if (string.IsNullOrEmpty(mail)) continue;
                        seen.Add(mail);

                        var name = s.Persona?.Name;
                        if (string.IsNullOrEmpty(name)) name = mail;
                        var bio = s.Persona?.Bio;

                        if (!_contacts.TryGetValue(mail, out var c))
                        {
                            c = new Contact
                            {
                                Mail = mail,
                                Name = name,
                                NickName = name,
                                Status = PresenceStatus.Online,
                                PersonalMessage = new PersonalMessage { Message = bio ?? string.Empty }
                            };
                            _contacts[mail] = c;
                            changed = true;
                        }
                        else if (c.Name != name || (c.PersonalMessage?.Message ?? "") != (bio ?? ""))
                        {
                            c.Name = name;
                            c.NickName = name;
                            c.PersonalMessage = new PersonalMessage { Message = bio ?? string.Empty };
                            changed = true;
                        }

                        // Avatar -> display image cache
                        var avatar = s.Persona?.Avatar;
                        if (!string.IsNullOrEmpty(avatar) && !_displayImages.ContainsKey(mail))
                        {
                            try
                            {
                                var uri = avatar.StartsWith("/static/", StringComparison.OrdinalIgnoreCase)
                                    ? new Uri("http://127.0.0.1:5000" + avatar, UriKind.Absolute)
                                    : new Uri(avatar, UriKind.RelativeOrAbsolute);
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.UriSource = uri;
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.EndInit();
                                bmp.Freeze(); // created on a sync thread — must be free-threaded for UI use
                                _displayImages[mail] = new DisplayImage { ImageData = null };
                                c.DisplayImage = _displayImages[mail];
                                c.Tag = bmp;
                            }
                            catch { }
                        }
                    }

                    // Remove contacts whose sessions disappeared
                    var removed = _contacts.Keys.Where(k => !seen.Contains(k)).ToList();
                    foreach (var k in removed)
                    {
                        var c = _contacts[k];
                        _contacts.Remove(k);
                        _displayImages.Remove(k);
                        changed = true;
                        Marshal(() => ContactRemovedFromList?.Invoke(this, new ContactPresenceEventArgs(c)));
                    }
                }

                if (changed)
                    Marshal(() => ContactListSynchronized?.Invoke(this, EventArgs.Empty));
            }
            catch { /* backend may be down; keep polling */ }
        }

        // ---- contact management (sessions, port-5000 semantics) ----
        /// <summary>Currently active IBA session id (from /api/iba/sessions).</summary>
        public string ActiveSessionId { get; private set; } = "default";

        /// <summary>Create a new IBA session; returns the new session id.</summary>
        public async Task<string?> CreateSessionAsync(string? name, string? description = null)
        {
            try
            {
                var resp = await _backend.CreateSessionAsync(name, description);
                await RefreshContactsAsync();
                return resp;
            }
            catch { return null; }
        }

        /// <summary>Mark a session as the active IBA session.</summary>
        public async Task SetActiveSessionAsync(string sessionId)
        {
            try
            {
                await _backend.SelectSessionAsync(sessionId);
                ActiveSessionId = sessionId;
                await RefreshContactsAsync();
            }
            catch { }
        }

        public void AddContact(string email)
        {
            // Sessions are created server-side; treat "add contact" as creating
            // a session named after the entered text.
            _ = Task.Run(async () => await CreateSessionAsync(email));
        }

        public void RemoveContact(Contact contact)
        {
            if (contact == null) return;
            _ = Task.Run(async () =>
            {
                try { await _backend.DeleteSessionAsync(contact.Mail); }
                catch { }
                await RefreshContactsAsync();
            });
        }

        /// <summary>Delete the session behind a contact and refresh the list.</summary>
        public async Task RemoveContactAsync(Contact contact)
        {
            if (contact == null) return;
            await _backend.DeleteSessionAsync(contact.Mail);
            await RefreshContactsAsync();
        }

        public void SetBlocked(Contact contact, bool blocked)
        {
            if (contact == null) return;
            lock (_contacts)
            {
                if (blocked) _blocked.Add(contact.Mail); else _blocked.Remove(contact.Mail);
                contact.Blocked = blocked;
            }
            Marshal(() => ContactStatusChanged?.Invoke(this, new ContactPresenceEventArgs(contact)));
        }

        public void CreateGroup(string name)
        {
            lock (_contacts)
            {
                if (_groups.All(g => !string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
                    _groups.Add(new ContactGroup { Name = name });
            }
            Marshal(() => GroupsChanged?.Invoke(this, EventArgs.Empty));
        }

        public void RemoveGroup(ContactGroup group)
        {
            lock (_contacts) _groups.RemoveAll(g => g.Guid == group.Guid);
            Marshal(() => GroupsChanged?.Invoke(this, EventArgs.Empty));
        }

        public void AddContactToGroup(Contact contact, string groupName)
        {
            lock (_contacts)
            {
                var g = _groups.FirstOrDefault(x => string.Equals(x.Name, groupName, StringComparison.OrdinalIgnoreCase));
                if (g == null) { g = new ContactGroup { Name = groupName }; _groups.Add(g); }
                if (!g.Contacts.Contains(contact)) g.Contacts.Add(contact);
                if (contact.ContactGroups.All(x => x.Guid != g.Guid)) contact.ContactGroups.Add(g);
            }
            Marshal(() => GroupsChanged?.Invoke(this, EventArgs.Empty));
        }

        public void RemoveContactFromGroup(Contact contact, ContactGroup group)
        {
            lock (_contacts)
            {
                var g = _groups.FirstOrDefault(x => x.Guid == group.Guid);
                g?.Contacts.RemoveAll(c => c.Mail == contact.Mail);
                contact.ContactGroups.RemoveAll(x => x.Guid == group.Guid);
            }
            Marshal(() => GroupsChanged?.Invoke(this, EventArgs.Empty));
        }

        public void SetFavorite(Contact contact, bool favorite)
        {
            if (contact == null) return;
            lock (_contacts)
            {
                if (favorite) _favorites.Add(contact.Mail); else _favorites.Remove(contact.Mail);
            }
            Marshal(() => ContactStatusChanged?.Invoke(this, new ContactPresenceEventArgs(contact)));
        }

        public void SetDisplayName(string name)
        {
            _displayName = name;
            if (_currentUser != null) _currentUser.Name = name;
        }

        public void SetNowPlaying(string artist, string title)
        {
            // No presence publishing in this build.
        }

        // ---- presence ----
        public void SetStatus(PresenceStatus status)
        {
            if (_currentUser != null) _currentUser.Status = status;
            Marshal(() => ContactStatusChanged?.Invoke(this, new ContactPresenceEventArgs(_currentUser!)));
        }

        public void SendTyping(Contact to)
        {
            // No typing indicators in this build.
        }

        public void SendNudge(Contact to)
        {
            // Nudges are local-only in this build.
        }

        // ---- IBA session history ----
        public async Task<DoryNative.Models.SessionHistoryResponse?> GetSessionHistoryAsync(string sessionId)
        {
            try { return await _backend.GetSessionHistoryAsync(sessionId); }
            catch { return null; }
        }

        // ---- messaging ----
        public void SendMessage(Contact to, string text, string? session = null, string? workspace = null)
        {
            if (to == null || string.IsNullOrEmpty(text)) return;
            var contact = to;
            var sessionId = session ?? contact.Mail;
            _ = Task.Run(async () =>
            {
                try
                {
                    var streamId = "iba-" + Interlocked.Increment(ref _streamCounter);
                    var request = new ChatStreamRequest
                    {
                        Messages = new List<DoryNative.Models.ChatMessage>
                        {
                            new() { Role = "user", Content = text }
                        },
                        UseIba = true,
                        SessionId = sessionId,
                        VariableNewconv = true
                    };

                    await foreach (var evt in _backend.StreamChatAsync(request))
                    {
                        // Forward every event to the chat window for rich rendering
                        Marshal(() => IbaEventReceived?.Invoke(this,
                            new IbaEventArgs(contact, evt.Name, evt.Data, streamId)));

                        // Answers and errors render as ordinary chat bubbles.
                        // Emit per-event (not just the last) so parallel tasks each
                        // get their own reply bubble.
                        if (evt.Name == "answer" && evt.Data.ValueKind == JsonValueKind.Object
                            && evt.Data.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                        {
                            var text = c.GetString();
                            if (!string.IsNullOrEmpty(text))
                                Marshal(() => MessageReceived?.Invoke(this,
                                    new IncomingMessageEventArgs(contact, text)));
                        }
                        else if (evt.Name == "error" && evt.Data.ValueKind == JsonValueKind.Object
                            && evt.Data.TryGetProperty("error", out var errEl))
                        {
                            Marshal(() => MessageReceived?.Invoke(this,
                                new IncomingMessageEventArgs(contact, "[Error] " + errEl.GetString())));
                        }
                        else if (evt.Name == "done")
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Marshal(() => MessageReceived?.Invoke(this,
                        new IncomingMessageEventArgs(contact, "[Connection error] " + ex.Message)));
                }
            });
        }

        public void SendNudge(Contact to, string? session = null)
        {
            SendNudge(to);
        }

        public void SendWink(Contact to, string winkName)
        {
            // Winks are local-only visuals in this build.
        }

        // ---- personal message / display picture ----
        public string GetPersonalMessage() => _personalMessage;

        public void SetPersonalMessage(string text)
        {
            _personalMessage = text ?? string.Empty;
            if (_currentUser != null)
                _currentUser.PersonalMessage = new PersonalMessage { Message = _personalMessage };
        }

        public ImageSource? GetCurrentUserDisplayImage()
        {
            return _currentUser?.Tag as ImageSource;
        }

        public void SetCurrentUserDisplayPicture(string imagePath)
        {
            try
            {
                var bmp = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                bmp.Freeze();
                if (_currentUser != null) _currentUser.Tag = bmp;
                Marshal(() => ContactDisplayImageChanged?.Invoke(this, new ContactPresenceEventArgs(_currentUser!)));
            }
            catch { }
        }

        public void SetCurrentUserDisplayPicture(byte[] imageData)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new System.IO.MemoryStream(imageData);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                if (_currentUser != null) _currentUser.Tag = bmp;
                Marshal(() => ContactDisplayImageChanged?.Invoke(this, new ContactPresenceEventArgs(_currentUser!)));
            }
            catch { }
        }

        public void SetDisplayPicture(string imagePath) => SetCurrentUserDisplayPicture(imagePath);
        public void SetDisplayPicture(byte[] imageData) => SetCurrentUserDisplayPicture(imageData);

        public void RequestDisplayPicture(Contact contact, bool force = false)
        {
            // Display images are populated during contact refresh.
        }

        public static ImageSource? TryGetDisplayImage(Contact? contact)
        {
            if (contact?.Tag is ImageSource src) return src;
            if (contact?.DisplayImage != null && contact.DisplayImage.ImageData != null)
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new System.IO.MemoryStream(contact.DisplayImage.ImageData);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch { }
            }
            return null;
        }

        public static string NormalizeWinkLocation(string location)
        {
            if (string.IsNullOrEmpty(location)) return location ?? string.Empty;
            var s = location.Trim();
            if (!s.EndsWith(".mct", StringComparison.OrdinalIgnoreCase))
                s += ".mct";
            return s;
        }

        private void Marshal(Action action)
        {
            if (_uiDispatcher.CheckAccess()) action();
            else _uiDispatcher.BeginInvoke(action);
        }
    }

    public class MessengerErrorEventArgs : EventArgs
    {
        public string Message { get; }
        public Exception Exception { get; }
        public MessengerErrorEventArgs(string message, Exception ex)
        {
            Message = message;
            Exception = ex;
        }
    }

    public class ContactPresenceEventArgs : EventArgs
    {
        public Contact Contact { get; }
        public ContactPresenceEventArgs(Contact contact) { Contact = contact; }
    }

    public class IncomingMessageEventArgs : EventArgs
    {
        public Contact From { get; }
        public string Text { get; }
        public string? StreamId { get; init; }
        public bool IsComplete { get; init; } = true;

        public IncomingMessageEventArgs(Contact from, string text)
        {
            From = from;
            Text = text;
        }
    }

    /// <summary>
    /// Carries one raw IBA SSE event (thought, tool_start, tool_result, wait...)
    /// to the chat window for rich rendering.
    /// </summary>
    public class IbaEventArgs : EventArgs
    {
        public Contact From { get; }
        public string EventName { get; }
        public JsonElement Data { get; }
        public string StreamId { get; }

        public IbaEventArgs(Contact from, string eventName, JsonElement data, string streamId)
        {
            From = from;
            EventName = eventName;
            Data = data;
            StreamId = streamId;
        }
    }

    public class WinkReceivedEventArgs : EventArgs
    {
        public Contact From { get; }
        public string Location { get; }
        public WinkReceivedEventArgs(Contact from, Wink? wink)
        {
            From = from;
            Location = wink?.Location;
        }
    }
}
