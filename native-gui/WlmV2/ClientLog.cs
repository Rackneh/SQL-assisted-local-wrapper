using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WLMRevivalV2Desktop
{
    /// <summary>
    /// Lightweight diagnostic log. Writes to %AppData%\WLMRevivalV2Desktop\client.log
    /// by default, and switches to a per-account folder once the user signs in so
    /// multiple simultaneous client instances don't overwrite each other's logs.
    /// Also captures MSNPSharp's verbose Trace output (contact-sync, NOT
    /// notifications, SOAP) into the same file, so intermittent runtime issues
    /// (failed contact adds, "stale until relog") can be diagnosed from one place.
    /// All methods are best-effort and never throw.
    /// </summary>
    internal static class ClientLog
    {
        private static string _path;
        public static string LogPath => _path;

        private static readonly List<Action<LogEntry>> _listeners = new List<Action<LogEntry>>();
        private static readonly object _listenerLock = new object();

        public static event Action<LogEntry> OnLog
        {
            add
            {
                lock (_listenerLock) _listeners.Add(value);
                ReplayRecent(value);
            }
            remove { lock (_listenerLock) _listeners.Remove(value); }
        }

        private static readonly LinkedList<LogEntry> _recent = new LinkedList<LogEntry>();
        private const int MaxRecent = 250;

        public static void Init()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WLMRevivalV2Desktop");
                Directory.CreateDirectory(dir);
                _path = Path.Combine(dir, "client.log");

                // A file listener captures everything written via Trace, including
                // MSNPSharp's own Trace.WriteLineIf output.
                Trace.Listeners.Add(new TextWriterTraceListener(_path, "wlmfile"));
                Trace.AutoFlush = true;

                // Crank MSNPSharp tracing up so the address-book sync / NOT handling
                // / SOAP requests are all recorded.
                try { MSNPSharp.Settings.TraceSwitch.Level = TraceLevel.Verbose; } catch { }

                Write("==================== client started " + DateTime.Now + " pid=" + Process.GetCurrentProcess().Id + " ====================");
                Write("WLMRevivalV2Desktop build timestamp: " + File.GetLastWriteTime(Process.GetCurrentProcess().MainModule.FileName) + " (bundled-wink shortcut enabled)");
            }
            catch { /* logging must never break the app */ }
        }

        /// <summary>
        /// Switch logging to a folder named after the signed-in account so two
        /// clients running on the same PC don't clobber each other's logs.
        /// Each login session gets its own timestamped log file so history is preserved.
        /// </summary>
        public static void SetAccount(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email)) return;

                var baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WLMRevivalV2Desktop");
                var accountDir = Path.Combine(baseDir, SafeName(email));
                var sessionDir = Path.Combine(accountDir, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Directory.CreateDirectory(sessionDir);

                var newPath = Path.Combine(sessionDir, "client.log");
                if (string.Equals(_path, newPath, StringComparison.OrdinalIgnoreCase)) return;

                // Flush and replace the file trace listener so MSNPSharp output also lands
                // in the per-account log from now on.
                var oldListener = Trace.Listeners["wlmfile"] as TextWriterTraceListener;
                if (oldListener != null)
                {
                    oldListener.Flush();
                    oldListener.Dispose();
                    Trace.Listeners.Remove(oldListener);
                }

                _path = newPath;
                Trace.Listeners.Add(new TextWriterTraceListener(_path, "wlmfile"));
                Trace.AutoFlush = true;

                Write("==================== logging switched to account " + email + " pid=" + Process.GetCurrentProcess().Id + " ====================");
            }
            catch { /* logging must never break the app */ }
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

        public static void Write(string message)
        {
            try
            {
                var entry = new LogEntry(DateTime.Now, LogLevel.Info, null, message);
                Trace.WriteLine(entry.ToFileLine());
                PushRecent(entry);
            }
            catch { }
        }

        public static void Write(string context, Exception ex)
        {
            Write(LogLevel.Error, context, ex?.ToString());
        }

        public static void Write(LogLevel level, string context, string detail)
        {
            try
            {
                var entry = new LogEntry(DateTime.Now, level, context, detail);
                Trace.WriteLine(entry.ToFileLine());
                PushRecent(entry);
            }
            catch { }
        }

        public static void Info(string context, string detail) => Write(LogLevel.Info, context, detail);
        public static void Debug(string context, string detail) => Write(LogLevel.Debug, context, detail);
        public static void Warn(string context, string detail) => Write(LogLevel.Warn, context, detail);
        public static void Error(string context, string detail) => Write(LogLevel.Error, context, detail);

        private static void PushRecent(LogEntry entry)
        {
            lock (_listenerLock)
            {
                _recent.AddLast(entry);
                while (_recent.Count > MaxRecent)
                    _recent.RemoveFirst();
                foreach (var listener in _listeners)
                {
                    try { listener(entry); }
                    catch { }
                }
            }
        }

        private static void ReplayRecent(Action<LogEntry> listener)
        {
            lock (_listenerLock)
            {
                foreach (var entry in _recent)
                {
                    try { listener(entry); }
                    catch { }
                }
            }
        }
    }

    public enum LogLevel { Debug, Info, Warn, Error }

    public class LogEntry
    {
        public DateTime Timestamp { get; }
        public LogLevel Level { get; }
        public string Context { get; }
        public string Message { get; }

        public LogEntry(DateTime timestamp, LogLevel level, string context, string message)
        {
            Timestamp = timestamp;
            Level = level;
            Context = context;
            Message = message ?? string.Empty;
        }

        public string ToFileLine()
        {
            var ctx = string.IsNullOrEmpty(Context) ? "" : $"[{Context}] ";
            return $"[WLM {Timestamp:HH:mm:ss.fff}] [{Level}] {ctx}{Message}";
        }
    }
}
