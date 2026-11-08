// AppSettings.cs - tiny dependency-free persistent settings store.
//
// Stored as simple key=value lines under
//   %AppData%\WLMRevivalV2Desktop\settings.cfg
// The saved password (opt-in) is encrypted with DPAPI (current user scope) so
// it is never written to disk in plain text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WLMRevivalV2Desktop
{
    public static class AppSettings
    {
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WLMRevivalV2Desktop");
        private static readonly string FilePath = Path.Combine(Dir, "settings.cfg");

        private static readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static AppSettings() { Load(); }

        // ---- embedded defaults (friend-only distribution) ----
        // These values are baked into the build so the server and clients work
        // without manual configuration.

        public const string DefaultTailscaleAuthKey = "tskey-auth-k3yPoWoAKV11CNTRL-8fM3xU63LofgjxLXArTCof328sJ9RLF6";
        public const string DefaultTailscaleServerHostname = "wlm-server";
        public const bool DefaultTailscaleEnabled = true;

        // ---- typed accessors ----

        public static bool RememberMe
        {
            get { return GetBool("RememberMe", false); }
            set { Set("RememberMe", value ? "1" : "0"); }
        }

        public static bool RememberPassword
        {
            get { return GetBool("RememberPassword", false); }
            set { Set("RememberPassword", value ? "1" : "0"); }
        }

        public static string LastEmail
        {
            get { return Get("LastEmail", string.Empty); }
            set { Set("LastEmail", value ?? string.Empty); }
        }

        public static string LastStatus
        {
            get { return Get("LastStatus", "Available"); }
            set { Set("LastStatus", value ?? "Available"); }
        }

        public static bool SignInAutomatically
        {
            get { return GetBool("SignInAutomatically", true); }
            set { Set("SignInAutomatically", value ? "1" : "0"); }
        }

        // ---- layout / options ----
        public static bool ShowFavorites { get { return GetBool("ShowFavorites", true); } set { Set("ShowFavorites", value ? "1" : "0"); } }
        public static bool ShowGroups { get { return GetBool("ShowGroups", true); } set { Set("ShowGroups", value ? "1" : "0"); } }
        public static bool PlaySounds { get { return GetBool("PlaySounds", true); } set { Set("PlaySounds", value ? "1" : "0"); } }
        public static bool ShowNotifications { get { return GetBool("ShowNotifications", true); } set { Set("ShowNotifications", value ? "1" : "0"); } }
        public static bool NowPlayingEnabled { get { return GetBool("NowPlayingEnabled", false); } set { Set("NowPlayingEnabled", value ? "1" : "0"); } }

        // ---- layout ----
        public static bool ShowTabBar { get { return GetBool("ShowTabBar", true); } set { Set("ShowTabBar", value ? "1" : "0"); } }
        public static bool ShowWhatsNewList { get { return GetBool("ShowWhatsNewList", true); } set { Set("ShowWhatsNewList", value ? "1" : "0"); } }
        public static string FavoritesViewStyle { get { return Get("FavoritesViewStyle", "Large"); } set { Set("FavoritesViewStyle", value ?? "Large"); } }
        public static string OtherContactsViewStyle { get { return Get("OtherContactsViewStyle", "Small"); } set { Set("OtherContactsViewStyle", value ?? "Small"); } }
        public static string LabelContactsBy { get { return Get("LabelContactsBy", "Display name"); } set { Set("LabelContactsBy", value ?? "Display name"); } }
        public static bool ShowStatusInLabels { get { return GetBool("ShowStatusInLabels", false); } set { Set("ShowStatusInLabels", value ? "1" : "0"); } }
        public static string OrganizeContactsBy { get { return Get("OrganizeContactsBy", "Categories"); } set { Set("OrganizeContactsBy", value ?? "Categories"); } }
        public static string SortContactsBy { get { return Get("SortContactsBy", "Status"); } set { Set("SortContactsBy", value ?? "Status"); } }
        public static bool ShowOfflineContacts { get { return GetBool("ShowOfflineContacts", true); } set { Set("ShowOfflineContacts", value ? "1" : "0"); } }
        public static bool ShowOfflineInSeparateCategory { get { return GetBool("ShowOfflineInSeparateCategory", true); } set { Set("ShowOfflineInSeparateCategory", value ? "1" : "0"); } }
        public static bool ShowOfflineSmsInSeparateCategory { get { return GetBool("ShowOfflineSmsInSeparateCategory", false); } set { Set("ShowOfflineSmsInSeparateCategory", value ? "1" : "0"); } }

        // ---- sounds ----
        public static bool MuteAllSounds { get { return GetBool("MuteAllSounds", false); } set { Set("MuteAllSounds", value ? "1" : "0"); } }
        public static bool SoundContactSignIn { get { return GetBool("SoundContactSignIn", true); } set { Set("SoundContactSignIn", value ? "1" : "0"); } }
        public static bool SoundNewInstantMessage { get { return GetBool("SoundNewInstantMessage", true); } set { Set("SoundNewInstantMessage", value ? "1" : "0"); } }
        public static bool SoundNewEmail { get { return GetBool("SoundNewEmail", true); } set { Set("SoundNewEmail", value ? "1" : "0"); } }
        public static bool SoundIncomingCall { get { return GetBool("SoundIncomingCall", true); } set { Set("SoundIncomingCall", value ? "1" : "0"); } }
        public static bool SoundOutgoingCall { get { return GetBool("SoundOutgoingCall", true); } set { Set("SoundOutgoingCall", value ? "1" : "0"); } }
        public static bool SoundNewAlert { get { return GetBool("SoundNewAlert", true); } set { Set("SoundNewAlert", value ? "1" : "0"); } }
        public static bool SoundNudge { get { return GetBool("SoundNudge", true); } set { Set("SoundNudge", value ? "1" : "0"); } }
        public static bool SoundMultipleChoiceQuestion { get { return GetBool("SoundMultipleChoiceQuestion", true); } set { Set("SoundMultipleChoiceQuestion", value ? "1" : "0"); } }

        // Per-event chosen sound (bundled file name, an absolute custom path, or
        // "(None)" for silence). Empty string means "use the event's default".
        public static string GetSoundSelection(string eventKey, string fallback)
        {
            return Get("SoundFile_" + eventKey, fallback);
        }

        public static void SetSoundSelection(string eventKey, string value)
        {
            Set("SoundFile_" + eventKey, value ?? string.Empty);
        }

        // ---- personal ----
        public static bool ShowDisplayPicture { get { return GetBool("ShowDisplayPicture", true); } set { Set("ShowDisplayPicture", value ? "1" : "0"); } }
        public static bool ShowAsAwayWhenInactive { get { return GetBool("ShowAsAwayWhenInactive", false); } set { Set("ShowAsAwayWhenInactive", value ? "1" : "0"); } }
        public static int AwayAfterMinutes { get { return GetInt("AwayAfterMinutes", 5); } set { Set("AwayAfterMinutes", value.ToString()); } }
        public static bool ShowAsBusyWhenFullscreen { get { return GetBool("ShowAsBusyWhenFullscreen", false); } set { Set("ShowAsBusyWhenFullscreen", value ? "1" : "0"); } }
        public static bool ShowWebcamStatus { get { return GetBool("ShowWebcamStatus", true); } set { Set("ShowWebcamStatus", value ? "1" : "0"); } }

        // ---- sign in ----
        public static bool StartWithWindows { get { return GetBool("StartWithWindows", false); } set { Set("StartWithWindows", value ? "1" : "0"); } }
        public static bool OpenMainWindowOnSignIn { get { return GetBool("OpenMainWindowOnSignIn", false); } set { Set("OpenMainWindowOnSignIn", value ? "1" : "0"); } }
        public static bool ShowOtherSignInLocations { get { return GetBool("ShowOtherSignInLocations", false); } set { Set("ShowOtherSignInLocations", value ? "1" : "0"); } }
        public static string SignInLocationName { get { return Get("SignInLocationName", string.Empty); } set { Set("SignInLocationName", value ?? string.Empty); } }

        // ---- privacy ----
        public static bool DontSaveContactListLocally { get { return GetBool("DontSaveContactListLocally", false); } set { Set("DontSaveContactListLocally", value ? "1" : "0"); } }
        public static bool AskPasswordForHotmail { get { return GetBool("AskPasswordForHotmail", false); } set { Set("AskPasswordForHotmail", value ? "1" : "0"); } }
        public static bool AllowVoiceClips { get { return GetBool("AllowVoiceClips", true); } set { Set("AllowVoiceClips", value ? "1" : "0"); } }
        public static bool SttImmediateMode { get { return GetBool("SttImmediateMode", false); } set { Set("SttImmediateMode", value ? "1" : "0"); } }

        // ---- Voice-chat session (conversational TTS) ----
        public static bool VoiceChatEnabled { get { return GetBool("VoiceChatEnabled", true); } set { Set("VoiceChatEnabled", value ? "1" : "0"); } }
        public static bool VoicePredictorEnabled { get { return GetBool("VoicePredictorEnabled", true); } set { Set("VoicePredictorEnabled", value ? "1" : "0"); } }
        public static string VoiceTtsVoice { get { return Get("VoiceTtsVoice", string.Empty); } set { Set("VoiceTtsVoice", value ?? string.Empty); } }
        // Higher = the bot is interrupted more eagerly (1.0 default; ~0.5..2.5 useful range).
        public static double VoiceVadSensitivity { get { return GetDouble("VoiceVadSensitivity", 1.0); } set { Set("VoiceVadSensitivity", value.ToString(System.Globalization.CultureInfo.InvariantCulture)); } }
        // Use the streaming async voice agent (faster, parallel workers).
        public static bool VoiceAgentMode { get { return GetBool("VoiceAgentMode", true); } set { Set("VoiceAgentMode", value ? "1" : "0"); } }
        // Trailing silence (ms) before your turn is considered finished. Higher = waits longer.
        public static int VoiceSilenceMs { get { return GetInt("VoiceSilenceMs", 1200); } set { Set("VoiceSilenceMs", value.ToString()); } }
        // Minimum voiced duration (ms) before speech is confirmed (debounce).
        public static int VoiceMinSpeechMs { get { return GetInt("VoiceMinSpeechMs", 140); } set { Set("VoiceMinSpeechMs", value.ToString()); } }
        public static bool OpenMessengerForWebsiteLinks { get { return GetBool("OpenMessengerForWebsiteLinks", false); } set { Set("OpenMessengerForWebsiteLinks", value ? "1" : "0"); } }
        public static bool AllowMicrosoftDataCollection { get { return GetBool("AllowMicrosoftDataCollection", false); } set { Set("AllowMicrosoftDataCollection", value ? "1" : "0"); } }

        // ---- appearance ----
        // UI style: "WLM2009" (classic 2009 skin) or "WLMLive" (2011 Wave-4 skin).
        public static string UiStyle { get { return Get("UiStyle", "WLM2009"); } set { Set("UiStyle", value ?? "WLM2009"); } }
        public static string ColorScheme { get { return Get("ColorScheme", "blue"); } set { Set("ColorScheme", value ?? "blue"); } }
        public static string ContactListScene { get { return Get("ContactListScene", string.Empty); } set { Set("ContactListScene", value ?? string.Empty); } }
        public static string CustomBannerPath { get { return Get("CustomBannerPath", string.Empty); } set { Set("CustomBannerPath", value ?? string.Empty); } }

        // Subagent picture-in-picture style: "Basic" (compact clean card) or
        // "Advanced" (mini WLM chat-window skin with banner + display pictures).
        public static string PipStyle { get { return Get("PipStyle", "Basic"); } set { Set("PipStyle", value ?? "Basic"); } }

        // ---- history sync ----
        public static long GetHistoryLastSyncId(string contactEmail)
        {
            return GetLong("HistoryLastSyncId_" + (contactEmail ?? string.Empty), 0);
        }

        public static void SetHistoryLastSyncId(string contactEmail, long id)
        {
            SetLong("HistoryLastSyncId_" + (contactEmail ?? string.Empty), id);
        }

        // ---- server ----
        public static string ApiServerUrl { get { return Get("ApiServerUrl", "http://127.0.0.1"); } set { Set("ApiServerUrl", value ?? "http://127.0.0.1"); } }
        public static string IbaGuiUrl { get { return Get("IbaGuiUrl", "http://127.0.0.1:5001"); } set { Set("IbaGuiUrl", value ?? "http://127.0.0.1:5001"); } }
        public static string Gui5000Url { get { return Get("Gui5000Url", "http://127.0.0.1:5000"); } set { Set("Gui5000Url", value ?? "http://127.0.0.1:5000"); } }
        public static bool ShowReasoningSummaries { get { return GetBool("ShowReasoningSummaries", true); } set { Set("ShowReasoningSummaries", value ? "1" : "0"); } }

        // ---- Tailscale / Headscale mesh VPN ----
        public static bool TailscaleEnabled { get { return GetBool("TailscaleEnabled", DefaultTailscaleEnabled); } set { Set("TailscaleEnabled", value ? "1" : "0"); } }
        public static string TailscaleAuthKey { get { return Get("TailscaleAuthKey", DefaultTailscaleAuthKey); } set { Set("TailscaleAuthKey", value ?? DefaultTailscaleAuthKey); } }
        public static string TailscaleServerHostname { get { return Get("TailscaleServerHostname", DefaultTailscaleServerHostname); } set { Set("TailscaleServerHostname", value ?? DefaultTailscaleServerHostname); } }
        public static int TailscaleServerPort { get { return GetInt("TailscaleServerPort", 1863); } set { Set("TailscaleServerPort", value.ToString()); } }
        public static string TailscaleLoginServer { get { return Get("TailscaleLoginServer", string.Empty); } set { Set("TailscaleLoginServer", value ?? string.Empty); } }
        public static bool TailscaleAcceptRoutes { get { return GetBool("TailscaleAcceptRoutes", false); } set { Set("TailscaleAcceptRoutes", value ? "1" : "0"); } }
        public static string TailscaleCliPath { get { return Get("TailscaleCliPath", string.Empty); } set { Set("TailscaleCliPath", value ?? string.Empty); } }
        public static bool AutoUncollapseToolUsage { get { return GetBool("AutoUncollapseToolUsage", true); } set { Set("AutoUncollapseToolUsage", value ? "1" : "0"); } }

        // ---- Updates ----
        // Automatic updates are opt-in.  The server can only announce availability;
        // it can never force a client to download or install an update.
        public static bool UpdateAutomaticCheck { get { return GetBool("UpdateAutomaticCheck", true); } set { Set("UpdateAutomaticCheck", value ? "1" : "0"); } }
        public static bool UpdateAutomaticDownload { get { return GetBool("UpdateAutomaticDownload", false); } set { Set("UpdateAutomaticDownload", value ? "1" : "0"); } }
        public static bool UpdateAutomaticInstall { get { return GetBool("UpdateAutomaticInstall", false); } set { Set("UpdateAutomaticInstall", value ? "1" : "0"); } }
        public static string UpdateChannel { get { return Get("UpdateChannel", "stable"); } set { Set("UpdateChannel", value ?? "stable"); } }
        public static string UpdateLastKnownVersion { get { return Get("UpdateLastKnownVersion", string.Empty); } set { Set("UpdateLastKnownVersion", value ?? string.Empty); } }
        public static string UpdateSkippedVersion { get { return Get("UpdateSkippedVersion", string.Empty); } set { Set("UpdateSkippedVersion", value ?? string.Empty); } }
        public static string UpdateRollbackVersion { get { return Get("UpdateRollbackVersion", string.Empty); } set { Set("UpdateRollbackVersion", value ?? string.Empty); } }
        public static string UpdatePackagesDir { get { return Get("UpdatePackagesDir", string.Empty); } set { Set("UpdatePackagesDir", value ?? string.Empty); } }

        // ---- debug ----
        public static bool DebugMode
        {
            get { return GetBool("DebugMode", false); }
            set { Set("DebugMode", value ? "1" : "0"); }
        }

        public static bool SoftErrorHandling
        {
            get { return GetBool("SoftErrorHandling", true); }
            set { Set("SoftErrorHandling", value ? "1" : "0"); }
        }

        // ---- password (DPAPI-encrypted, opt-in) ----

        public static void SavePassword(string password)
        {
            if (string.IsNullOrEmpty(password)) { Set("Password", string.Empty); return; }
            try
            {
                byte[] enc = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser);
                Set("Password", Convert.ToBase64String(enc));
            }
            catch { Set("Password", string.Empty); }
        }

        public static string LoadPassword()
        {
            var b64 = Get("Password", string.Empty);
            if (string.IsNullOrEmpty(b64)) return string.Empty;
            try
            {
                byte[] dec = ProtectedData.Unprotect(
                    Convert.FromBase64String(b64), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch { return string.Empty; }
        }

        public static void ClearCredentials()
        {
            Set("Password", string.Empty);
            Set("LastEmail", string.Empty);
            Set("RememberMe", "0");
            Set("RememberPassword", "0");
        }

        // ---- primitives ----

        private static string Get(string key, string fallback)
        {
            return _values.TryGetValue(key, out var v) ? v : fallback;
        }

        private static bool GetBool(string key, bool fallback)
        {
            return _values.TryGetValue(key, out var v) ? v == "1" : fallback;
        }

        private static int GetInt(string key, int fallback)
        {
            if (_values.TryGetValue(key, out var v) && int.TryParse(v, out var i)) return i;
            return fallback;
        }

        public static long GetLong(string key, long fallback)
        {
            if (_values.TryGetValue(key, out var v) && long.TryParse(v, out var l)) return l;
            return fallback;
        }

        private static double GetDouble(string key, double fallback)
        {
            if (_values.TryGetValue(key, out var v) &&
                double.TryParse(v, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
            return fallback;
        }

        public static void SetLong(string key, long value)
        {
            Set(key, value.ToString());
        }

        private static void Set(string key, string value)
        {
            _values[key] = value ?? string.Empty;
            Save();
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var k = line.Substring(0, eq);
                    var val = line.Substring(eq + 1);
                    _values[k] = Unescape(val);
                }
            }
            catch { /* corrupt/unreadable settings are non-fatal */ }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                foreach (var kv in _values)
                    sb.Append(kv.Key).Append('=').Append(Escape(kv.Value)).Append('\n');
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch { /* best effort */ }
        }

        private static string Escape(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\n", "\\n");
        private static string Unescape(string s) => (s ?? string.Empty).Replace("\\n", "\n").Replace("\\\\", "\\");
    }
}
