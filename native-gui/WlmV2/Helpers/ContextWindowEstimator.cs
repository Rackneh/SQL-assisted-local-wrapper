using System;
using System.Collections.Generic;
using System.Linq;
using WLMRevivalV2Desktop.Protocol;

namespace WLMRevivalV2Desktop.Helpers
{
    /// <summary>
    /// Lightweight, local estimate of how much of the model's context window the
    /// current conversation is consuming. Used to drive the realtime progress bar
    /// below the session/workspace switcher in bot chat windows.
    /// </summary>
    public static class ContextWindowEstimator
    {
        // A rough working context ceiling. This is intentionally conservative so
        // the bar gives early warning before the server-side limit is reached.
        public const int DefaultContextWindow = 120000;

        // Tokens per character for English/ASCII-heavy code/text is about 0.25;
        // mixed text averages closer to 0.3. We use 0.3 to err on the side of
        // showing more usage rather than less.
        private const double TokensPerChar = 0.3;

        // Base overhead for each message in the conversation (role, formatting,
        // MSN MIME headers, etc.).
        private const int OverheadTokensPerMessage = 40;

        // Average characters per line of code/text. Used to translate file I/O
        // line counts into token estimates when the actual content is not present
        // in the conversation history.
        private const int AvgCharsPerLine = 50;

        /// <summary>
        /// Estimate the number of tokens currently represented by the given
        /// conversation messages.
        /// </summary>
        public static int EstimateTokens(IEnumerable<HistoryMessage> messages)
        {
            return EstimateTokens(messages, 0, 0);
        }

        /// <summary>
        /// Estimate tokens including file I/O line counts (lines read/written by
        /// the bot's tools that are not already visible as chat messages).
        /// </summary>
        public static int EstimateTokens(IEnumerable<HistoryMessage> messages, int linesRead, int linesWritten)
        {
            if (messages == null) messages = Enumerable.Empty<HistoryMessage>();

            int total = 0;
            int count = 0;
            foreach (var msg in messages)
            {
                if (msg == null) continue;
                var text = msg.Content ?? string.Empty;
                total += (int)Math.Ceiling(text.Length * TokensPerChar) + OverheadTokensPerMessage;
                count++;
            }

            // Lines read/written by tools add context even when the full content
            // is not rendered as a chat message (e.g. truncated file reads).
            total += (int)Math.Ceiling(linesRead * AvgCharsPerLine * TokensPerChar);
            total += (int)Math.Ceiling(linesWritten * AvgCharsPerLine * TokensPerChar);

            return total;
        }

        /// <summary>
        /// Return a 0-100 percentage of the default context window.
        /// </summary>
        public static double EstimateUsagePercent(IEnumerable<HistoryMessage> messages)
        {
            return EstimateUsagePercent(messages, 0, 0, DefaultContextWindow);
        }

        /// <summary>
        /// Return a 0-100 percentage of the default context window, including
        /// file I/O line counts.
        /// </summary>
        public static double EstimateUsagePercent(IEnumerable<HistoryMessage> messages, int linesRead, int linesWritten)
        {
            return EstimateUsagePercent(messages, linesRead, linesWritten, DefaultContextWindow);
        }

        /// <summary>
        /// Return a 0-100 percentage of the supplied context window.
        /// </summary>
        public static double EstimateUsagePercent(IEnumerable<HistoryMessage> messages, int linesRead, int linesWritten, int contextWindowTokens)
        {
            if (contextWindowTokens <= 0) return 0;
            var tokens = EstimateTokens(messages, linesRead, linesWritten);
            double pct = (tokens / (double)contextWindowTokens) * 100.0;
            return Math.Min(pct, 100.0);
        }
    }
}
