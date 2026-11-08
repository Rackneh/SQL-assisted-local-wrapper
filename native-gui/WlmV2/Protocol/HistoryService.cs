using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WLMRevivalV2Desktop.Services;

namespace WLMRevivalV2Desktop.Protocol
{
    /// <summary>
    /// A persisted conversation-history entry. Mirrors the server-side
    /// t_message_history row and is also used for locally-cached live messages.
    /// </summary>
    public class HistoryMessage
    {
        public long Id { get; set; }
        public string ChatId { get; set; }
        public string SenderEmail { get; set; }
        public string SenderName { get; set; }
        public string RecipientEmail { get; set; }
        public string MessageType { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Read { get; set; }
        public bool Mine { get; set; }
        public string ChatSession { get; set; }
    }

    /// <summary>
    /// Local cache + server sync for conversation history.
    /// Messages are stored per contact under %AppData%\WLMRevivalV2Desktop\history
    /// so a user signing in on a new PC can pull the full history from the server.
    /// </summary>
    public static class HistoryService
    {
        private static readonly string BaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WLMRevivalV2Desktop", "history");

        private static string _currentUserEmail;
        private static readonly Dictionary<string, List<HistoryMessage>> _cache =
            new Dictionary<string, List<HistoryMessage>>(StringComparer.OrdinalIgnoreCase);

        public static string ServerBaseUrl
        {
            get { return (AppSettings.ApiServerUrl ?? "http://127.0.0.1").TrimEnd('/'); }
        }

        /// <summary>
        /// Call when the local user signs in/out.  This resets the in-memory cache
        /// and sets the directory used for local storage.
        /// </summary>
        public static void SetCurrentUser(string email)
        {
            _currentUserEmail = string.IsNullOrEmpty(email) ? null : email.ToLowerInvariant();
            _cache.Clear();
        }

        private static string SafeName(string email)
        {
            if (string.IsNullOrEmpty(email)) return "unknown";
            var sb = new StringBuilder();
            foreach (var c in email)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == '@')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }

        private static string HistoryDir()
        {
            var dir = Path.Combine(BaseDir, SafeName(_currentUserEmail ?? "anonymous"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string HistoryPath(string contactEmail)
        {
            return Path.Combine(HistoryDir(), SafeName(contactEmail) + ".json");
        }

        /// <summary>
        /// Load the locally-cached history for a contact, optionally filtered to a
        /// bot chat session.
        /// </summary>
        public static List<HistoryMessage> LoadHistory(string contactEmail, string sessionId = null)
        {
            if (string.IsNullOrEmpty(contactEmail)) return new List<HistoryMessage>();

            lock (_cache)
            {
                List<HistoryMessage> cached;
                if (_cache.TryGetValue(contactEmail, out cached))
                {
                    var all = cached.Select(m => Clone(m)).ToList();
                    if (string.IsNullOrEmpty(sessionId))
                        return all;
                    return all.Where(m => string.Equals(m.ChatSession, sessionId, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var list = new List<HistoryMessage>();
                try
                {
                    var path = HistoryPath(contactEmail);
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        list = JsonConvert.DeserializeObject<List<HistoryMessage>>(json) ?? list;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("HistoryService.LoadHistory: " + ex.Message);
                }

                _cache[contactEmail] = list;
                var result = list.Select(m => Clone(m)).ToList();
                if (string.IsNullOrEmpty(sessionId))
                    return result;
                return result.Where(m => string.Equals(m.ChatSession, sessionId, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        /// <summary>
        /// Store a live message locally (sent or received).  Does not touch the server.
        /// </summary>
        public static void AppendMessage(string contactEmail, string senderEmail, string senderName,
            string recipientEmail, string messageType, string content, DateTime createdAt, bool mine, string chatSession = null)
        {
            if (string.IsNullOrEmpty(contactEmail)) return;

            var message = new HistoryMessage
            {
                Id = 0, // local-only until sync assigns a server id
                SenderEmail = senderEmail ?? string.Empty,
                SenderName = senderName ?? senderEmail ?? string.Empty,
                RecipientEmail = recipientEmail ?? string.Empty,
                MessageType = messageType ?? ClassifyContent(content),
                Content = content ?? string.Empty,
                CreatedAt = createdAt,
                Read = mine,
                Mine = mine,
                ChatSession = chatSession,
            };

            lock (_cache)
            {
                List<HistoryMessage> list;
                if (!_cache.TryGetValue(contactEmail, out list))
                {
                    list = LoadHistory(contactEmail);
                    _cache[contactEmail] = list;
                }
                list.Add(message);
                SaveUnlocked(contactEmail, list);
            }
        }

        /// <summary>
        /// Convenience overload for a plain text/rich message sent by the local user.
        /// </summary>
        public static void AppendLocalMessage(string contactEmail, string content, bool isMine, string chatSession = null)
        {
            AppendMessage(contactEmail,
                isMine ? _currentUserEmail : contactEmail,
                isMine ? null : null,
                isMine ? contactEmail : _currentUserEmail,
                ClassifyContent(content),
                content,
                DateTime.UtcNow,
                isMine,
                chatSession);
        }

        /// <summary>
        /// Convenience overload for nudges and winks.
        /// </summary>
        public static void AppendLocalEvent(string contactEmail, string messageType, string content, bool isMine, string chatSession = null)
        {
            AppendMessage(contactEmail,
                isMine ? _currentUserEmail : contactEmail,
                null,
                isMine ? contactEmail : _currentUserEmail,
                messageType,
                content,
                DateTime.UtcNow,
                isMine,
                chatSession);
        }

        /// <summary>
        /// Assign the current chat session to any local-only messages that were
        /// created before the session id was known (e.g. first message sent while
        /// sessions were still loading).
        /// </summary>
        public static void AssignSessionToLocalMessages(string contactEmail, string sessionId)
        {
            if (string.IsNullOrEmpty(contactEmail) || string.IsNullOrEmpty(sessionId)) return;

            lock (_cache)
            {
                List<HistoryMessage> list;
                if (!_cache.TryGetValue(contactEmail, out list))
                {
                    list = LoadHistory(contactEmail);
                    _cache[contactEmail] = list;
                }
                bool changed = false;
                foreach (var m in list)
                {
                    if (m.Id == 0 && string.IsNullOrEmpty(m.ChatSession))
                    {
                        m.ChatSession = sessionId;
                        changed = true;
                    }
                }
                if (changed)
                    SaveUnlocked(contactEmail, list);
            }
        }

        /// <summary>
        /// Fetch server-side history for a contact and merge it into the local cache.
        /// Returns the newly fetched messages (already merged into the cache).
        /// </summary>
        public static async Task<List<HistoryMessage>> SyncAsync(string contactEmail, string sessionId = null)
        {
            if (string.IsNullOrEmpty(contactEmail) || string.IsNullOrEmpty(_currentUserEmail))
                return new List<HistoryMessage>();

            var lastId = GetLastSyncId(contactEmail);
            var url = ServerBaseUrl + "/api/v1/history/" + Uri.EscapeDataString(contactEmail)
                + "?limit=500"
                + (lastId > 0 ? "&after_id=" + lastId : string.Empty)
                + (!string.IsNullOrEmpty(sessionId) ? "&session=" + Uri.EscapeDataString(sessionId) : string.Empty);

            try
            {
                var json = await GetJsonAsync(url);
                if (json == null || !json.Value<bool>("success"))
                    return new List<HistoryMessage>();

                var fetched = ParseMessages(json["messages"] as JArray);
                if (fetched.Count > 0)
                {
                    MergeMessages(contactEmail, fetched);
                    var maxId = fetched.Max(m => m.Id);
                    if (maxId > 0)
                        SetLastSyncId(contactEmail, maxId);
                }
                return fetched;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("HistoryService.SyncAsync: " + ex.Message);
                return new List<HistoryMessage>();
            }
        }

        /// <summary>
        /// Fetch all conversations that have new messages since the last sync.
        /// This is used at sign-in to catch up on messages sent from other PCs.
        /// </summary>
        public static async Task<List<HistoryMessage>> SyncAllAsync()
        {
            if (string.IsNullOrEmpty(_currentUserEmail))
                return new List<HistoryMessage>();

            var overallLastId = GetOverallLastSyncId();
            var url = ServerBaseUrl + "/api/v1/history/sync"
                + "?limit=1000"
                + (overallLastId > 0 ? "&after_id=" + overallLastId : string.Empty);

            try
            {
                var json = await GetJsonAsync(url);
                if (json == null || !json.Value<bool>("success"))
                    return new List<HistoryMessage>();

                var fetched = ParseMessages(json["messages"] as JArray);
                if (fetched.Count > 0)
                {
                    // Group by contact and merge.
                    var byContact = fetched.GroupBy(m =>
                        string.Equals(m.SenderEmail, _currentUserEmail, StringComparison.OrdinalIgnoreCase)
                            ? m.RecipientEmail
                            : m.SenderEmail);

                    foreach (var group in byContact)
                    {
                        MergeMessages(group.Key, group.ToList());
                        var maxId = group.Max(m => m.Id);
                        if (maxId > GetLastSyncId(group.Key))
                            SetLastSyncId(group.Key, maxId);
                    }

                    var overallMax = fetched.Max(m => m.Id);
                    if (overallMax > 0)
                        SetOverallLastSyncId(overallMax);
                }
                return fetched;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("HistoryService.SyncAllAsync: " + ex.Message);
                return new List<HistoryMessage>();
            }
        }

        /// <summary>
        /// Tell the server that messages from a contact have been read up to a
        /// given server id.  Best-effort: failures are ignored.
        /// </summary>
        public static async Task MarkReadAsync(string contactEmail, long upToId, string sessionId = null)
        {
            if (string.IsNullOrEmpty(contactEmail) || upToId <= 0)
                return;

            try
            {
                var url = ServerBaseUrl + "/api/v1/history/read";
                var body = JsonConvert.SerializeObject(new
                {
                    contact_uuid = contactEmail,
                    up_to_id = upToId,
                    session = sessionId,
                });
                await PostJsonAsync(url, body);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("HistoryService.MarkReadAsync: " + ex.Message);
            }
        }

        public static long GetLastSyncId(string contactEmail)
        {
            return AppSettings.GetHistoryLastSyncId(contactEmail);
        }

        public static void SetLastSyncId(string contactEmail, long id)
        {
            AppSettings.SetHistoryLastSyncId(contactEmail, id);
        }

        public static long GetOverallLastSyncId()
        {
            return AppSettings.GetLong("HistoryOverallLastSyncId", 0);
        }

        public static void SetOverallLastSyncId(long id)
        {
            AppSettings.SetLong("HistoryOverallLastSyncId", id);
        }

        private static void MergeMessages(string contactEmail, List<HistoryMessage> serverMessages)
        {
            lock (_cache)
            {
                List<HistoryMessage> list;
                if (!_cache.TryGetValue(contactEmail, out list))
                {
                    list = LoadHistory(contactEmail);
                    _cache[contactEmail] = list;
                }

                foreach (var serverMsg in serverMessages)
                {
                    // Match by server id if the local message already has one.
                    if (serverMsg.Id > 0)
                    {
                        var existing = list.FirstOrDefault(m => m.Id == serverMsg.Id);
                        if (existing != null)
                        {
                            existing.Read = serverMsg.Read;
                            existing.Mine = serverMsg.Mine;
                            continue;
                        }
                    }

                    // De-duplicate local-only messages that were created while offline.
                    if (serverMsg.Id > 0 && serverMsg.Mine)
                    {
                        var localDup = list.FirstOrDefault(m =>
                            m.Id == 0
                            && m.Mine
                            && string.Equals(m.Content, serverMsg.Content, StringComparison.Ordinal)
                            && Math.Abs((m.CreatedAt - serverMsg.CreatedAt).TotalMinutes) < 5);
                        if (localDup != null)
                        {
                            localDup.Id = serverMsg.Id;
                            localDup.Read = serverMsg.Read;
                            if (!string.IsNullOrEmpty(serverMsg.ChatSession))
                                localDup.ChatSession = serverMsg.ChatSession;
                            continue;
                        }
                    }

                    list.Add(serverMsg);
                }

                list.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
                SaveUnlocked(contactEmail, list);
            }
        }

        private static void SaveUnlocked(string contactEmail, List<HistoryMessage> list)
        {
            try
            {
                var path = HistoryPath(contactEmail);
                var json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("HistoryService.Save: " + ex.Message);
            }
        }

        private static HistoryMessage Clone(HistoryMessage source)
        {
            return new HistoryMessage
            {
                Id = source.Id,
                ChatId = source.ChatId,
                SenderEmail = source.SenderEmail,
                SenderName = source.SenderName,
                RecipientEmail = source.RecipientEmail,
                MessageType = source.MessageType,
                Content = source.Content,
                CreatedAt = source.CreatedAt,
                Read = source.Read,
                Mine = source.Mine,
                ChatSession = source.ChatSession,
            };
        }

        private static List<HistoryMessage> ParseMessages(JArray array)
        {
            var result = new List<HistoryMessage>();
            if (array == null) return result;

            foreach (var item in array)
            {
                try
                {
                    var m = new HistoryMessage
                    {
                        Id = item.Value<long>("id"),
                        ChatId = item.Value<string>("chat_id"),
                        SenderEmail = item.Value<string>("sender_email") ?? item.Value<string>("sender_uuid"),
                        SenderName = item.Value<string>("sender_name"),
                        RecipientEmail = item.Value<string>("recipient_uuid"),
                        MessageType = item.Value<string>("message_type") ?? "chat",
                        Content = item.Value<string>("content") ?? string.Empty,
                        Read = item.Value<bool?>("read") ?? false,
                        Mine = item.Value<bool>("mine"),
                        ChatSession = item.Value<string>("chat_session"),
                    };

                    var created = item.Value<string>("created_at");
                    if (!string.IsNullOrEmpty(created))
                    {
                        DateTime dt;
                        // Server timestamps are naive UTC (datetime.utcnow().isoformat(), no
                        // offset suffix). Parsing them plain yields DateTimeKind.Unspecified,
                        // and dt.ToUniversalTime() then *assumes local time* and shifts the
                        // value by the machine's UTC offset. That desync (hours, for any
                        // non-UTC user) blew past the 5-minute de-dup windows in
                        // ChatWindowLive, so the history-sync copy of every just-rendered
                        // live message was treated as new and rendered a second time.
                        // AssumeUniversal treats a no-offset string as UTC; AdjustToUniversal
                        // returns a UTC-kind value, keeping it aligned with DateTime.UtcNow.
                        if (DateTime.TryParse(created,
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.AssumeUniversal
                                    | System.Globalization.DateTimeStyles.AdjustToUniversal,
                                out dt))
                            m.CreatedAt = dt;
                    }

                    result.Add(m);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("HistoryService.ParseMessages: " + ex.Message);
                }
            }
            return result;
        }

        public static string ClassifyContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return "chat";
            var lower = content.ToLowerInvariant();
            if (lower.Contains("[wlmgif]")) return "gif";
            if (lower.Contains("[wlmghost")) return "ghost";
            if (lower.Contains("[wlmfile"))
            {
                try
                {
                    var start = content.IndexOf("[wlmfile", StringComparison.OrdinalIgnoreCase);
                    var end = content.IndexOf("]", start);
                    if (end > start)
                    {
                        var attrs = content.Substring(start, end - start);
                        if (attrs.IndexOf("type=\"image/", StringComparison.OrdinalIgnoreCase) >= 0)
                            return "image";
                    }
                }
                catch { }
                return "file";
            }
            return "chat";
        }

        private static async Task<JObject> GetJsonAsync(string url)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "GET";
                    request.Timeout = 30000;
                    request.Accept = "application/json";
                    if (!string.IsNullOrEmpty(ContactStyleManager.CurrentUserEmail))
                        request.Headers["X-Desktop-User"] = ContactStyleManager.CurrentUserEmail;

                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var text = reader.ReadToEnd();
                        return JObject.Parse(text);
                    }
                }
                catch (WebException ex)
                {
                    var status = (ex.Response as HttpWebResponse)?.StatusCode ?? 0;
                    ClientLog.Warn("HistoryService.GetJsonAsync", $"HTTP {(int)status}: {ex.Message}");
                    if (AppSettings.DebugMode)
                        ClientLog.Error("HistoryService.GetJsonAsync", ex.ToString());
                    return null;
                }
                catch (Exception ex)
                {
                    ClientLog.Warn("HistoryService.GetJsonAsync", ex.Message);
                    if (AppSettings.DebugMode)
                        ClientLog.Error("HistoryService.GetJsonAsync", ex.ToString());
                    return null;
                }
            });
        }

        private static async Task<JObject> PostJsonAsync(string url, string body)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "POST";
                    request.ContentType = "application/json";
                    request.Timeout = 30000;
                    request.Accept = "application/json";
                    if (!string.IsNullOrEmpty(ContactStyleManager.CurrentUserEmail))
                        request.Headers["X-Desktop-User"] = ContactStyleManager.CurrentUserEmail;

                    var bytes = Encoding.UTF8.GetBytes(body);
                    request.ContentLength = bytes.Length;
                    using (var stream = request.GetRequestStream())
                        stream.Write(bytes, 0, bytes.Length);

                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var text = reader.ReadToEnd();
                        return JObject.Parse(text);
                    }
                }
                catch (WebException ex)
                {
                    var status = (ex.Response as HttpWebResponse)?.StatusCode ?? 0;
                    ClientLog.Warn("HistoryService.PostJsonAsync", $"HTTP {(int)status}: {ex.Message}");
                    if (AppSettings.DebugMode)
                        ClientLog.Error("HistoryService.PostJsonAsync", ex.ToString());
                    return null;
                }
                catch (Exception ex)
                {
                    ClientLog.Warn("HistoryService.PostJsonAsync", ex.Message);
                    if (AppSettings.DebugMode)
                        ClientLog.Error("HistoryService.PostJsonAsync", ex.ToString());
                    return null;
                }
            });
        }
    }
}
