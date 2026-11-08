using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WLMRevivalV2Desktop
{
    /// <summary>
    /// Detects whether a contact is a bot account by querying the revival server's
    /// /api/bots endpoint. The result is cached for the lifetime of the application
    /// so chat-window opening stays synchronous.
    /// </summary>
    public static class BotContactHelper
    {
        private static readonly HashSet<string> _botEmails =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static bool _fetchAttempted;
        private static readonly object _lock = new object();

        public static string ServerBaseUrl { get; set; } = "http://127.0.0.1";

        /// <summary>
        /// Returns true if the contact is a bot. Falls back to false if the server
        /// cannot be reached or has not been queried yet.
        /// </summary>
        public static bool IsBot(string email)
        {
            if (string.IsNullOrEmpty(email)) return false;
            EnsureFetchAsync();
            lock (_lock)
            {
                return _botEmails.Contains(email);
            }
        }

        /// <summary>
        /// Refresh the bot list from the server in the background.
        /// </summary>
        public static void Refresh()
        {
            _fetchAttempted = false;
            EnsureFetchAsync();
        }

        private static void EnsureFetchAsync()
        {
            if (_fetchAttempted) return;
            lock (_lock)
            {
                if (_fetchAttempted) return;
                _fetchAttempted = true;
            }
            Task.Run(FetchAsync);
        }

        private static async Task FetchAsync()
        {
            try
            {
                var guiUrl = AppSettings.Gui5000Url.TrimEnd('/');
                if (string.IsNullOrEmpty(guiUrl)) guiUrl = ServerBaseUrl.TrimEnd('/');
                var url = guiUrl + "/api/bots";

                using (var wc = new WebClient())
                {
                    var json = await Task.Run(() => wc.DownloadString(url));
                    var arr = JArray.Parse(json);
                    lock (_lock)
                    {
                        _botEmails.Clear();
                        if (arr != null)
                        {
                            foreach (var item in arr)
                            {
                                var email = item["email"]?.ToString();
                                if (!string.IsNullOrEmpty(email))
                                    _botEmails.Add(email);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("BotContactHelper.FetchAsync failed: " + ex.Message);
            }
        }
    }
}
