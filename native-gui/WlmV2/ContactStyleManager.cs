using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;

namespace WLMRevivalV2Desktop
{
    /// <summary>
    /// Per-contact style info fetched from the revival server.
    /// Includes banner, display picture, and color scheme.
    /// </summary>
    public class ContactStyle
    {
        // The server emits snake_case JSON keys (banner_url, display_picture_url,
        // color_scheme). Without these explicit JsonProperty maps Newtonsoft never
        // populates the properties, so HasBanner is always false and no banner or
        // colour ever renders. These attributes are the fix for that.
        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("banner_url")]
        public string BannerUrl { get; set; }

        [JsonProperty("display_picture_url")]
        public string DisplayPictureUrl { get; set; }

        [JsonProperty("color_scheme")]
        public string ColorScheme { get; set; }

        public bool HasBanner => !string.IsNullOrEmpty(BannerUrl);
        public bool HasDisplayPicture => !string.IsNullOrEmpty(DisplayPictureUrl);
    }

    /// <summary>
    /// Fetches and caches contact style info (banners, colors, display pictures) from the server.
    /// Styles are cached in memory and persisted to disk. Images are cached in memory only.
    /// </summary>
    public static class ContactStyleManager
    {
        private static readonly Dictionary<string, ContactStyle> _cache =
            new Dictionary<string, ContactStyle>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, ImageSource> _bannerImageCache =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, ImageSource> _displayPictureCache =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> _inFlight =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly string _cacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WLMRevivalV2Desktop", "contact_styles.json");

        private static string _serverBaseUrl = "http://127.0.0.1";

        static ContactStyleManager()
        {
            LoadCache();
        }

        public static string ServerBaseUrl
        {
            get { return _serverBaseUrl; }
            set { _serverBaseUrl = value?.TrimEnd('/'); }
        }

        /// <summary>
        /// Email of the currently signed-in user. Sent as the X-Desktop-User header
        /// when uploading the user's own banner / colour scheme so the (loopback)
        /// server can authenticate the request. Set on sign-in.
        /// </summary>
        public static string CurrentUserEmail { get; set; }

        /// <summary>
        /// Get cached style for a contact (returns null if not cached).
        /// </summary>
        public static ContactStyle GetStyle(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;
            lock (_cache)
            {
                _cache.TryGetValue(email, out var style);
                return style;
            }
        }

        /// <summary>
        /// Get cached style or fetch from server if not present.
        /// Returns cached value immediately and triggers async fetch if missing/stale.
        /// </summary>
        public static ContactStyle GetOrFetchStyle(string email)
        {
            var style = GetStyle(email);
            if (style == null)
                FetchStyleAsync(email);
            return style;
        }

        /// <summary>
        /// Fetch style from server and update cache (async, fire-and-forget).
        /// </summary>
        public static void FetchStyleAsync(string email)
        {
            if (string.IsNullOrEmpty(email)) return;
            lock (_inFlight)
            {
                if (!_inFlight.Add(email)) return; // already in flight
            }
            Task.Run(() =>
            {
                try { FetchStyleInternal(email); }
                finally
                {
                    lock (_inFlight) { _inFlight.Remove(email); }
                }
            });
        }

        /// <summary>
        /// Fetch styles for multiple contacts in batch.
        /// </summary>
        public static void FetchStylesAsync(IEnumerable<string> emails)
        {
            foreach (var email in emails)
                FetchStyleAsync(email);
        }

        /// <summary>
        /// Preload styles for all given emails, fetching from cache or server.
        /// </summary>
        public static void PreloadStyles(IEnumerable<string> emails)
        {
            foreach (var email in emails)
                GetOrFetchStyle(email);
        }

        private static void FetchStyleInternal(string email)
        {
            try
            {
                var url = $"{_serverBaseUrl}/api/v1/style/{Uri.EscapeDataString(email)}";
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    var json = client.DownloadString(url);
                    var style = JsonConvert.DeserializeObject<ContactStyle>(json);
                    if (style != null)
                    {
                        lock (_cache)
                        {
                            _cache[email] = style;
                        }
                        SaveCache();
                        // Pre-load banner and display picture into image cache
                        if (style.HasBanner)
                            LoadBannerImage(style.BannerUrl);
                        if (style.HasDisplayPicture)
                            LoadDisplayPicture(style.DisplayPictureUrl);
                        // Notify UI that style changed
                        StyleChanged?.Invoke(null, new ContactStyleEventArgs(email, style));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ContactStyleManager: failed to fetch style for {email}: {ex.Message}");
            }
        }

        public static event EventHandler<ContactStyleEventArgs> StyleChanged;

        private static void LoadCache()
        {
            try
            {
                if (!File.Exists(_cacheFilePath)) return;
                var json = File.ReadAllText(_cacheFilePath);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, ContactStyle>>(json);
                if (dict != null)
                {
                    lock (_cache)
                    {
                        _cache.Clear();
                        foreach (var kv in dict)
                            _cache[kv.Key] = kv.Value;
                    }
                }
            }
            catch { /* ignore corrupt cache */ }
        }

        private static void SaveCache()
        {
            try
            {
                var dir = Path.GetDirectoryName(_cacheFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Dictionary<string, ContactStyle> snapshot;
                lock (_cache)
                {
                    snapshot = new Dictionary<string, ContactStyle>(_cache, StringComparer.OrdinalIgnoreCase);
                }
                var json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
                File.WriteAllText(_cacheFilePath, json);
            }
            catch { /* best effort */ }
        }

        /// <summary>
        /// Return a banner image only if it is already in the in-memory cache.
        /// Never blocks (no network) - safe to call from the UI thread while
        /// building rows. Returns null if not yet downloaded.
        /// </summary>
        public static ImageSource GetCachedBannerImage(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            lock (_bannerImageCache)
            {
                _bannerImageCache.TryGetValue(url, out var cached);
                return cached;
            }
        }

        /// <summary>
        /// Ensure a banner image is downloaded into the cache (in the background),
        /// then raise StyleChanged for the owner so the contact list re-renders
        /// with the now-available image. No-op if already cached or in flight.
        /// </summary>
        public static void EnsureBannerImageAsync(string url, string email)
        {
            EnsureBannerImageAsync(url, email, null);
        }

        /// <summary>
        /// Ensure a banner image is downloaded into the cache (in the background),
        /// then raise StyleChanged for the owner so the contact list re-renders
        /// with the now-available image. The supplied style is included in the
        /// event args so callers that already hold the style don't lose it if the
        /// style cache hasn't been updated yet. No-op if already cached or in flight.
        /// </summary>
        public static void EnsureBannerImageAsync(string url, string email, ContactStyle knownStyle)
        {
            if (string.IsNullOrEmpty(url)) return;
            lock (_bannerImageCache)
            {
                if (_bannerImageCache.ContainsKey(url)) return;
            }
            var key = "banner:" + url;
            lock (_inFlight)
            {
                if (!_inFlight.Add(key)) return;
            }
            Task.Run(() =>
            {
                try
                {
                    var img = LoadBannerImage(url);
                    if (img != null && !string.IsNullOrEmpty(email))
                    {
                        var style = knownStyle ?? GetStyle(email);
                        StyleChanged?.Invoke(null, new ContactStyleEventArgs(email, style));
                    }
                }
                finally
                {
                    lock (_inFlight) { _inFlight.Remove(key); }
                }
            });
        }

        /// <summary>
        /// Return a contact's display picture (fetched over HTTP from the style
        /// API) if it is already cached. Never blocks. This is the fallback the
        /// contact list uses when the P2P picture transfer hasn't delivered an
        /// image (e.g. bots that don't serve P2P). Triggers a background download
        /// + StyleChanged refresh if the image isn't cached yet.
        /// </summary>
        public static ImageSource GetContactDisplayImage(string email)
        {
            var style = GetStyle(email);
            if (style == null || !style.HasDisplayPicture) return null;
            var url = style.DisplayPictureUrl;
            lock (_displayPictureCache)
            {
                if (_displayPictureCache.TryGetValue(url, out var cached))
                    return cached;
            }
            EnsureDisplayPictureAsync(url, email);
            return null;
        }

        /// <summary>
        /// Download a contact display picture into the cache in the background,
        /// then raise StyleChanged so the list re-renders with it.
        /// </summary>
        public static void EnsureDisplayPictureAsync(string url, string email)
        {
            if (string.IsNullOrEmpty(url)) return;
            lock (_displayPictureCache)
            {
                if (_displayPictureCache.ContainsKey(url)) return;
            }
            var key = "dp:" + url;
            lock (_inFlight)
            {
                if (!_inFlight.Add(key)) return;
            }
            Task.Run(() =>
            {
                try
                {
                    var img = LoadDisplayPicture(url);
                    if (img != null && !string.IsNullOrEmpty(email))
                        StyleChanged?.Invoke(null, new ContactStyleEventArgs(email, GetStyle(email)));
                }
                finally
                {
                    lock (_inFlight) { _inFlight.Remove(key); }
                }
            });
        }

        /// <summary>
        /// Try to load a banner image from URL, using the in-memory cache.
        /// Returns null on failure.
        /// </summary>
        public static ImageSource LoadBannerImage(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            lock (_bannerImageCache)
            {
                if (_bannerImageCache.TryGetValue(url, out var cached))
                    return cached;
            }
            try
            {
                using (var client = new WebClient())
                {
                    var data = client.DownloadData(url);
                    using (var ms = new MemoryStream(data))
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = ms;
                        bi.EndInit();
                        bi.Freeze();
                        lock (_bannerImageCache)
                        {
                            _bannerImageCache[url] = bi;
                        }
                        return bi;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ContactStyleManager: failed to load banner: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Try to load a display picture from URL, using the in-memory cache.
        /// Returns null on failure.
        /// </summary>
        public static ImageSource LoadDisplayPicture(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            lock (_displayPictureCache)
            {
                if (_displayPictureCache.TryGetValue(url, out var cached))
                    return cached;
            }
            try
            {
                using (var client = new WebClient())
                {
                    var data = client.DownloadData(url);
                    using (var ms = new MemoryStream(data))
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = ms;
                        bi.EndInit();
                        bi.Freeze();
                        lock (_displayPictureCache)
                        {
                            _displayPictureCache[url] = bi;
                        }
                        return bi;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ContactStyleManager: failed to load display picture: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clear all cached images to free memory. Styles remain cached on disk.
        /// </summary>
        public static void ClearImageCache()
        {
            lock (_bannerImageCache) { _bannerImageCache.Clear(); }
            lock (_displayPictureCache) { _displayPictureCache.Clear(); }
        }
    }

    public class ContactStyleEventArgs : EventArgs
    {
        public string Email { get; }
        public ContactStyle Style { get; }

        public ContactStyleEventArgs(string email, ContactStyle style)
        {
            Email = email;
            Style = style;
        }
    }
}
