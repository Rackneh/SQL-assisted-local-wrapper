// Lightweight stubs for optional V2 services that depended on third-party
// native/media packages (NAudio, IceLink, System.Speech) or external servers.
// They preserve the API surface used by the views so the UI compiles and the
// features degrade gracefully.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WLMRevivalV2Desktop.Services
{
    public class GifResult
    {
        public string Id { get; set; }
        public string Gif { get; set; }
        public string Preview { get; set; }
        public string Desc { get; set; }
    }

    public static class GifService
    {
        public static string ServerBaseUrl { get; set; } = "http://127.0.0.1:5000";

        public static List<GifResult> Search(string query, string pos, out string next, out bool noApiKey)
        {
            next = null;
            noApiKey = true;
            return new List<GifResult>();
        }
    }

    public static class GifFavorites
    {
        public static event EventHandler Changed;
        private static readonly List<GifResult> _items = new List<GifResult>();
        private static readonly object _lock = new object();

        public static List<GifResult> All()
        {
            lock (_lock) return new List<GifResult>(_items);
        }

        public static bool IsFavorite(string gifUrl)
        {
            lock (_lock) return _items.Exists(g => g.Gif == gifUrl);
        }

        public static void Add(GifResult gif)
        {
            lock (_lock)
            {
                if (!_items.Exists(g => g.Gif == gif.Gif)) _items.Add(gif);
            }
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Remove(string gifUrl)
        {
            lock (_lock) _items.RemoveAll(g => g.Gif == gifUrl);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool Toggle(GifResult gif)
        {
            if (gif == null) return false;
            if (IsFavorite(gif.Gif)) { Remove(gif.Gif); return false; }
            Add(gif); return true;
        }
    }

    public class FileUploadResult
    {
        public bool Success;
        public string Url;
        public string Name;
        public string Type;
        public long Size;
        public string Error;
    }

    public static class FileTransferService
    {
        public static string ServerBaseUrl { get; set; } = "http://127.0.0.1:5000";

        public static FileUploadResult Upload(string filePath)
        {
            try
            {
                var fi = new FileInfo(filePath);
                return new FileUploadResult
                {
                    Success = true,
                    Url = "file://" + fi.FullName,
                    Name = fi.Name,
                    Type = GuessContentType(fi.Name),
                    Size = fi.Length
                };
            }
            catch (Exception ex)
            {
                return new FileUploadResult { Success = false, Error = ex.Message };
            }
        }

        public static string HumanSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("0.0") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("0.0") + " GB";
        }

        public static string GuessContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif",
                ".bmp" => "image/bmp", ".txt" => "text/plain", ".pdf" => "application/pdf",
                ".zip" => "application/zip", _ => "application/octet-stream"
            };
        }
    }

    public class SttChunkResult
    {
        public bool Success;
        public string Text;
        public string Error;
    }

    public static class SttService
    {
        public static string ServerBaseUrl { get; set; } = "http://127.0.0.1:5000";

        public static string RecordAndTranscribe(TimeSpan duration, Action<string> onStatus = null)
        {
            onStatus?.Invoke("Voice input is not available in this build.");
            return string.Empty;
        }

        public static Task<string> RecordAndTranscribeAsync(TimeSpan duration, Action<string> onStatus = null)
        {
            return Task.Run(async () =>
            {
                onStatus?.Invoke("Voice input is not available in this build.");
                await Task.Delay(duration);
                return string.Empty;
            });
        }

        public static async Task RecordAndTranscribeChunksAsync(
            TimeSpan chunkDuration,
            Action<SttChunkResult> onChunk,
            Action<string> onStatus = null,
            CancellationToken cancellationToken = default)
        {
            onStatus?.Invoke("Voice input is not available in this build.");
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { }
        }

        public static string TranscribePcm(byte[] pcm, int sampleRate = 16000, string contactUuid = null)
        {
            return string.Empty;
        }
    }

    public class TailscaleService
    {
        public bool IsEnabled => AppSettings.TailscaleEnabled;
        public string LastError { get; private set; } = string.Empty;
        public string EmbeddedAuthKey => AppSettings.DefaultTailscaleAuthKey;
        public string EffectiveAuthKey => string.IsNullOrEmpty(AppSettings.TailscaleAuthKey) ? EmbeddedAuthKey : AppSettings.TailscaleAuthKey;
        public bool IsConnected => false;
        public string TailnetName => string.Empty;

        public Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            LastError = "Tailscale is not available in this build.";
            return Task.FromResult(false);
        }

        public string ResolveServerIp(string hostname = null) => null;
        public string GetMsnServerEndpoint(int defaultPort = 1863) => null;
        public void Disconnect() { }
        public string FindTailscaleCli() => null;
    }
}

namespace WLMRevivalV2Desktop
{
    public class UpdateCheckResult
    {
        public bool Available { get; set; }
        public string CurrentVersion { get; set; }
        public string NewVersion { get; set; }
        public string ReleaseDate { get; set; }
        public string ReleaseNotes { get; set; }
        public string DownloadUrl { get; set; }
        public string MinVersion { get; set; }
        public string Error { get; set; }
    }

    public class UpdateService
    {
        public static string CurrentVersion => "2.0.0-native";

        public string GetUpdateBaseUrl() => AppSettings.ApiServerUrl;

        public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UpdateCheckResult
            {
                Available = false,
                CurrentVersion = CurrentVersion,
                Error = string.Empty
            });
        }

        public Task<string> DownloadAsync(string url, string targetPath, IProgress<int> progress = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string>(null);
        }

        public Task<bool> InstallAsync(string packagePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public bool Rollback() => false;
        public string GetPackagesDirectory() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packages");
    }
}
