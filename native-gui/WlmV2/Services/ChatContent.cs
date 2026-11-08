using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WLMRevivalV2Desktop.Services
{
    public enum ChatSegmentKind { Text, Gif, File, Think, Tool, Mcq, Ghost, Subagent, SubagentProgress }

    public class ChatSegment
    {
        public ChatSegmentKind Kind;
        public string Text;       // Text / Think / Tool body
        public string Url;        // Gif / File segments
        public string FileName;   // File segments
        public long FileSize;     // File segments
        public string FileType;   // File segments (MIME)

        // Tool / MCQ / Ghost / Subagent attributes
        public string ToolName;
        public string ToolStatus;
        public string ToolTitle;
        public string ToolSummary;
        public string ToolError;
        public string McqId;
        public string McqQuestion;
        public string[] McqOptions;
        public string GhostId;

        // Subagent attributes
        public string SubagentRole;
        public string SubagentEmail;
        public string SubagentTask;

        // SubagentProgress: a single live-progress line for an open PiP window.
        // (Email is carried in SubagentEmail; the line text is in Text.)
    }

    /// <summary>
    /// Wire format for rich chat content (GIFs, file attachments) embedded inside
    /// ordinary MSN text messages. This survives the normal switchboard channel
    /// and is also readable by bot accounts, because the literal URL is always
    /// present in the message text:
    ///   GIF : [wlmgif]URL[/wlmgif]
    ///   FILE: [wlmfile name="..." size="123" type="mime"]URL[/wlmfile]
    /// A client that doesn't understand the tokens still shows a usable URL.
    /// </summary>
    public static class ChatContent
    {
        public static string BuildGif(string url)
        {
            return "[wlmgif]" + (url ?? string.Empty) + "[/wlmgif]";
        }

        public static string BuildFile(string url, string name, long size, string type)
        {
            return string.Format(
                "[wlmfile name=\"{0}\" size=\"{1}\" type=\"{2}\"]{3}[/wlmfile]",
                Escape(name), size, Escape(type), url ?? string.Empty);
        }

        private static readonly Regex Token = new Regex(
            @"\[wlmgif\](?<gurl>.*?)\[/wlmgif\]" +
            @"|\[wlmfile(?<attrs>[^\]]*)\](?<furl>.*?)\[/wlmfile\]" +
            @"|\[wlmthink\](?<think>.*?)\[/wlmthink\]" +
            @"|\[wlmtool(?<toolattrs>[^\]]*)\](?<toolbody>.*?)\[/wlmtool\]" +
            @"|\[wlmmcq(?<mcqattrs>[^\]]*)\](?<mcqbody>.*?)\[/wlmmcq\]" +
            @"|\[wlmghost(?<ghostattrs>[^\]]*)\](?<ghostbody>.*?)\[/wlmghost\]" +
            @"|\[wlmsubprogress(?<subprogattrs>[^\]]*)\](?<subprogbody>.*?)\[/wlmsubprogress\]" +
            @"|\[wlmsubagent(?<subattrs>[^\]]*)\]",
            RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);



        private static readonly Regex Attr = new Regex(
            "(?<k>\\w+)\\s*=\\s*\"(?<v>.*?)\"",
            RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool HasRich(string text)
        {
            return !string.IsNullOrEmpty(text) &&
                   (text.IndexOf("[wlmgif]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[wlmfile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[wlmthink]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[wlmtool", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[wlmmcq", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[wlmghost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[wlmsubagent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[wlmsubprogress", StringComparison.OrdinalIgnoreCase) >= 0);
        }


        public static List<ChatSegment> Parse(string text)
        {
            var segments = new List<ChatSegment>();
            if (string.IsNullOrEmpty(text)) return segments;

            int last = 0;
            foreach (Match m in Token.Matches(text))
            {
                if (m.Index > last)
                    segments.Add(new ChatSegment
                    {
                        Kind = ChatSegmentKind.Text,
                        Text = text.Substring(last, m.Index - last)
                    });

                if (m.Groups["gurl"].Success)
                {
                    segments.Add(new ChatSegment
                    {
                        Kind = ChatSegmentKind.Gif,
                        Url = m.Groups["gurl"].Value.Trim()
                    });
                }
                else if (m.Groups["furl"].Success)
                {
                    var seg = new ChatSegment
                    {
                        Kind = ChatSegmentKind.File,
                        Url = m.Groups["furl"].Value.Trim()
                    };
                    foreach (Match a in Attr.Matches(m.Groups["attrs"].Value))
                    {
                        string v = a.Groups["v"].Value;
                        switch (a.Groups["k"].Value.ToLowerInvariant())
                        {
                            case "name": seg.FileName = v; break;
                            case "size": long.TryParse(v, out seg.FileSize); break;
                            case "type": seg.FileType = v; break;
                        }
                    }
                    segments.Add(seg);
                }
                else if (m.Groups["think"].Success)
                {
                    segments.Add(new ChatSegment
                    {
                        Kind = ChatSegmentKind.Think,
                        Text = m.Groups["think"].Value.Trim()
                    });
                }
                else if (m.Groups["toolbody"].Success)
                {
                    var seg = new ChatSegment
                    {
                        Kind = ChatSegmentKind.Tool,
                        Text = m.Groups["toolbody"].Value.Trim()
                    };
                    foreach (Match a in Attr.Matches(m.Groups["toolattrs"].Value))
                    {
                        string v = a.Groups["v"].Value;
                        switch (a.Groups["k"].Value.ToLowerInvariant())
                        {
                            case "name": seg.ToolName = v; break;
                            case "status": seg.ToolStatus = v; break;
                            case "title": seg.ToolTitle = v; break;
                            case "summary": seg.ToolSummary = v; break;
                            case "error": seg.ToolError = v; break;
                        }
                    }
                    segments.Add(seg);
                }
                else if (m.Groups["mcqbody"].Success)
                {
                    var seg = new ChatSegment
                    {
                        Kind = ChatSegmentKind.Mcq
                    };
                    foreach (Match a in Attr.Matches(m.Groups["mcqattrs"].Value))
                    {
                        string v = a.Groups["v"].Value;
                        switch (a.Groups["k"].Value.ToLowerInvariant())
                        {
                            case "id": seg.McqId = v; break;
                            case "question": seg.McqQuestion = Unescape(v); break;
                            case "options": seg.McqOptions = Unescape(v).Split('|'); break;
                        }
                    }
                    segments.Add(seg);
                }
                else if (m.Groups["ghostbody"].Success)
                {
                    var seg = new ChatSegment
                    {
                        Kind = ChatSegmentKind.Ghost,
                        Text = m.Groups["ghostbody"].Value
                    };
                    foreach (Match a in Attr.Matches(m.Groups["ghostattrs"].Value))
                    {
                        if (a.Groups["k"].Value.ToLowerInvariant() == "id")
                        {
                            seg.GhostId = a.Groups["v"].Value;
                            break;
                        }
                    }
                    segments.Add(seg);
                }

                else if (m.Groups["subprogbody"].Success)
                {
                    var seg = new ChatSegment
                    {
                        Kind = ChatSegmentKind.SubagentProgress,
                        Text = m.Groups["subprogbody"].Value
                    };
                    foreach (Match a in Attr.Matches(m.Groups["subprogattrs"].Value))
                    {
                        string v = a.Groups["v"].Value;
                        switch (a.Groups["k"].Value.ToLowerInvariant())
                        {
                            case "email": seg.SubagentEmail = v; break;
                            case "role": seg.SubagentRole = v; break;
                        }
                    }
                    segments.Add(seg);
                }

                else if (m.Groups["subattrs"].Success)
                {
                    var seg = new ChatSegment
                    {
                        Kind = ChatSegmentKind.Subagent
                    };
                    foreach (Match a in Attr.Matches(m.Groups["subattrs"].Value))
                    {
                        string v = a.Groups["v"].Value;
                        switch (a.Groups["k"].Value.ToLowerInvariant())
                        {
                            case "role": seg.SubagentRole = v; break;
                            case "email": seg.SubagentEmail = v; break;
                            case "task": seg.SubagentTask = Unescape(v); break;
                        }
                    }
                    segments.Add(seg);
                }

                last = m.Index + m.Length;
            }

            if (last < text.Length)
                segments.Add(new ChatSegment
                {
                    Kind = ChatSegmentKind.Text,
                    Text = text.Substring(last)
                });

            return segments;
        }

        public static bool IsIntermediateMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf("[wlmthink]", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("[wlmtool", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsGhostMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf("[wlmghost", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsSubagentMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            // Covers both the open marker ([wlmsubagent ...]) and live-progress
            // markers ([wlmsubprogress ...]). Note: "[wlmsubagent" is not a prefix
            // of "[wlmsubprogress", so both must be checked explicitly.
            return text.IndexOf("[wlmsubagent", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("[wlmsubprogress", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// True when the message contains ONLY subagent markers (open/progress)
        /// plus whitespace, so it should be routed to the PiP without rendering a
        /// bubble (and empty "Name says:" header) in the main conversation.
        /// </summary>
        public static bool IsSubagentOnly(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var segs = Parse(text);
            if (segs.Count == 0) return false;
            foreach (var s in segs)
            {
                if (s.Kind == ChatSegmentKind.Subagent || s.Kind == ChatSegmentKind.SubagentProgress)
                    continue;
                if (s.Kind == ChatSegmentKind.Text && string.IsNullOrWhiteSpace(s.Text))
                    continue;
                return false;
            }
            return true;
        }

        public static bool IsMcqMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf("[wlmmcq", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsImage(string nameOrType)
        {
            if (string.IsNullOrEmpty(nameOrType)) return false;
            string s = nameOrType.ToLowerInvariant();
            return s.Contains("image/")
                || s.EndsWith(".png") || s.EndsWith(".jpg") || s.EndsWith(".jpeg")
                || s.EndsWith(".gif") || s.EndsWith(".bmp") || s.EndsWith(".webp");
        }

        public static bool IsGifUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string s = url.ToLowerInvariant();
            return s.Contains(".gif") || s.Contains("tenor.");
        }

        // Tokens carry name/size/type inside quotes/brackets; neutralise any of
        // those characters in the values so they can't break the token grammar.
        private static string Escape(string s)
        {
            return (s ?? string.Empty)
                .Replace("\"", "'")
                .Replace("[", "(")
                .Replace("]", ")");
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s
                .Replace("&quot;", "\"")
                .Replace("&#91;", "[")
                .Replace("&#93;", "]")
                .Replace("&#124;", "|")
                .Replace("&amp;", "&");
        }
    }
}
