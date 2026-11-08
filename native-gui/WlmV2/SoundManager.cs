using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace WLMRevivalV2Desktop
{
    /// <summary>
    /// One configurable Messenger sound event (shown as a row in Options &gt; Sounds).
    /// </summary>
    public class SoundEvent
    {
        public string Key;          // stable id used for the settings key
        public string Label;        // user-facing name
        public string Hint;         // grey "(...)" hint shown after the label
        public string DefaultFile;  // bundled file name (no extension needed)

        public SoundEvent(string key, string label, string hint, string defaultFile)
        {
            Key = key; Label = label; Hint = hint; DefaultFile = defaultFile;
        }
    }

    /// <summary>A selectable sound for the Options dropdowns.</summary>
    public class SoundChoice
    {
        public string Id;     // file name (bundled) or absolute path (custom); "(None)" for silence
        public string Label;  // friendly display text
        public override string ToString() => Label;
    }

    public static class SoundManager
    {
        private static readonly List<MediaPlayer> _players = new List<MediaPlayer>();
        private static readonly object _lock = new object();

        public const string NoneId = "(None)";

        // The configurable sound events, in the order shown in Options.
        public static readonly SoundEvent[] Events =
        {
            new SoundEvent("ContactSignIn",     "Contacts sign in",                "(contact online)", "online"),
            new SoundEvent("NewInstantMessage", "New instant message",             "(type)",           "type"),
            new SoundEvent("NewEmail",          "New e-mail message",              "(new e-mail)",     "newemail"),
            new SoundEvent("IncomingCall",      "Incoming voice or video call",    "(incoming call)",  "phone"),
            new SoundEvent("OutgoingCall",      "Outgoing voice or video call",    "(outgoing call)",  "outgoing"),
            new SoundEvent("NewAlert",          "New Windows Live Alert",          "(new alert)",      "newalert"),
            new SoundEvent("Nudge",             "Nudge",                           "(nudge)",          "nudge"),
            new SoundEvent("MultipleChoiceQuestion", "Multiple-choice question",   "(question)",       "type"),
        };

        // ---- high-level event hooks (called from the UI) ----
        public static void PlayNudge()    => PlayEvent("Nudge");
        public static void PlayMessage()  => PlayEvent("NewInstantMessage");
        public static void PlayOutgoing() => PlayEvent("OutgoingCall");
        public static void PlayOnline()   => PlayEvent("ContactSignIn");
        public static void PlayEmail()    => PlayEvent("NewEmail");
        public static void PlayPhone()    => PlayEvent("IncomingCall");
        public static void PlayAlert()    => PlayEvent("NewAlert");
        public static void PlayType()     => PlayEvent("NewInstantMessage");
        public static void PlayMultipleChoiceQuestion() => PlayEvent("MultipleChoiceQuestion");

        public static SoundEvent GetEvent(string key) => Events.FirstOrDefault(e => e.Key == key);

        // ---- core ----
        public static void PlayEvent(string eventKey)
        {
            var ev = GetEvent(eventKey);
            if (ev == null) return;
            if (AppSettings.MuteAllSounds || !AppSettings.PlaySounds)
            {
                ClientLog.Write($"SoundManager.PlayEvent({eventKey}) skipped: muted/all-sounds off");
                return;
            }
            if (!EnabledFor(eventKey))
            {
                ClientLog.Write($"SoundManager.PlayEvent({eventKey}) skipped: event disabled in settings");
                return;
            }

            string path = ResolveFile(eventKey, ev.DefaultFile);
            if (path == null)
            {
                ClientLog.Write($"SoundManager.PlayEvent({eventKey}) skipped: sound file not found (default '{ev.DefaultFile}')");
                return;
            }

            ClientLog.Write($"SoundManager.PlayEvent({eventKey}) -> {path}");
            RunOnUiThread(() => PlayFile(path));
        }

        /// <summary>Play a specific event's selection regardless of mute/enable (for the preview button).</summary>
        public static void Preview(string eventKey)
        {
            var ev = GetEvent(eventKey);
            if (ev == null) return;
            string path = ResolveFile(eventKey, ev.DefaultFile);
            if (path == null) return;
            RunOnUiThread(() => PlayFile(path));
        }

        /// <summary>Play an explicit file/selection id (bundled name or absolute path) for previewing.</summary>
        public static void PreviewSelection(string selectionId)
        {
            string path = ResolvePath(selectionId, null);
            if (path == null) return;
            RunOnUiThread(() => PlayFile(path));
        }

        private static bool EnabledFor(string key)
        {
            switch (key)
            {
                case "ContactSignIn":     return AppSettings.SoundContactSignIn;
                case "NewInstantMessage": return AppSettings.SoundNewInstantMessage;
                case "NewEmail":          return AppSettings.SoundNewEmail;
                case "IncomingCall":      return AppSettings.SoundIncomingCall;
                case "OutgoingCall":      return AppSettings.SoundOutgoingCall;
                case "NewAlert":          return AppSettings.SoundNewAlert;
                case "Nudge":             return AppSettings.SoundNudge;
                case "MultipleChoiceQuestion": return AppSettings.SoundMultipleChoiceQuestion;
                default:                  return true;
            }
        }

        // Resolve the file for an event given its saved selection (or default).
        private static string ResolveFile(string eventKey, string defaultFile)
        {
            string sel = AppSettings.GetSoundSelection(eventKey, string.Empty);
            return ResolvePath(sel, defaultFile);
        }

        // sel: "(None)" => silent; "" => fall back to default; absolute path => custom;
        // otherwise a bundled file name searched in the sounds folders.
        private static string ResolvePath(string sel, string defaultFile)
        {
            if (string.Equals(sel, NoneId, StringComparison.OrdinalIgnoreCase)) return null;
            if (string.IsNullOrEmpty(sel)) sel = defaultFile;
            if (string.IsNullOrEmpty(sel)) return null;

            if (Path.IsPathRooted(sel))
                return File.Exists(sel) ? sel : null;

            return FindByName(Path.GetFileNameWithoutExtension(sel));
        }

        private static IEnumerable<string> SoundDirs()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            yield return Path.Combine(baseDir, "Assets", "wlm", "sounds");
            yield return Path.Combine(baseDir, "Assets", "wlm", "sounds", "library");
        }

        private static string FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var dir in SoundDirs())
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var ext in new[] { ".mp3", ".wma", ".wav" })
                {
                    string path = Path.Combine(dir, name + ext);
                    if (File.Exists(path)) return path;
                }
            }
            return null;
        }

        /// <summary>All bundled sounds available for selection, de-duplicated by name.</summary>
        public static List<SoundChoice> AvailableSounds()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<SoundChoice>();
            foreach (var dir in SoundDirs())
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir)
                             .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                                      || f.EndsWith(".wma", StringComparison.OrdinalIgnoreCase)
                                      || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                             .OrderBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase))
                {
                    string nameNoExt = Path.GetFileNameWithoutExtension(f);
                    if (!seen.Add(nameNoExt)) continue;
                    list.Add(new SoundChoice { Id = Path.GetFileName(f), Label = Prettify(nameNoExt) });
                }
            }
            return list;
        }

        public static string Prettify(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var s = name.Replace('_', ' ').Replace('-', ' ').Trim();
            var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        // ---- playback plumbing ----
        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) { action(); return; }
            if (dispatcher.CheckAccess()) action();
            else dispatcher.Invoke(action);
        }

        private static void PlayFile(string path)
        {
            try
            {
                var player = new MediaPlayer { Volume = 1.0 };
                player.MediaFailed += (s, e) =>
                {
                    ClientLog.Write($"SoundManager.MediaFailed {path}: {e.ErrorException?.Message}");
                    CleanupPlayer(player);
                };
                player.MediaEnded += (s, e) => CleanupPlayer(player);
                lock (_lock) { _players.Add(player); }
                player.Open(new Uri(path, UriKind.Absolute));
                player.Play();
                ClientLog.Write($"SoundManager.PlayFile started: {path}");
            }
            catch (Exception ex)
            {
                ClientLog.Write("SoundManager.PlayFile", ex);
            }
        }

        private static void CleanupPlayer(MediaPlayer player)
        {
            lock (_lock) { _players.Remove(player); }
            player.Close();
        }
    }
}
