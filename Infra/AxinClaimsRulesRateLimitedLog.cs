using System;
using System.Collections.Concurrent;

namespace AxinClaimsRules
{
    /// <summary>
    /// Ultra-cheap per-key rate limiter for debug logging.
    /// Goal: avoid server-debug.log spam in hot paths (e.g., FireSpread),
    /// while still allowing TRACE when enabled.
    ///
    /// Design:
    /// - Per key -> at most 1 log per intervalMs.
    /// - Thread-safe (ConcurrentDictionary).
    /// - Opportunistic cleanup to avoid unbounded growth.
    ///
    /// Nivel N: logging must not affect performance when enabled; this keeps
    /// overhead to a dictionary lookup + timestamp compare.
    /// </summary>
    internal static class RateLimitedLog
    {
        private static readonly ConcurrentDictionary<string, long> LastLogMs = new(StringComparer.Ordinal);

        // Cleanup knobs (very cheap and rare)
        private const int SoftMaxKeys = 20000;
        private const int CleanupBatch = 2000;

        /// <summary>
        /// Returns true if a log line should be emitted for this key now.
        /// </summary>
        public static bool ShouldLog(string key, long nowMs, int intervalMs)
        {
            if (intervalMs <= 0) return true;
            if (string.IsNullOrEmpty(key)) return true;

            long last = LastLogMs.GetOrAdd(key, -1);
            if (last >= 0 && (nowMs - last) < intervalMs) return false;

            LastLogMs[key] = nowMs;

            // Opportunistic cleanup (only when dictionary is large)
            if (LastLogMs.Count > SoftMaxKeys)
            {
                int removed = 0;
                foreach (var kv in LastLogMs)
                {
                    // remove very old entries (5 minutes)
                    if ((nowMs - kv.Value) > 5 * 60 * 1000)
                    {
                        LastLogMs.TryRemove(kv.Key, out _);
                        removed++;
                        if (removed >= CleanupBatch) break;
                    }
                }
            }

            return true;
        }
    }
}
