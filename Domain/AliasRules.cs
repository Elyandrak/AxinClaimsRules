using System;
using System.Collections.Generic;

namespace AxinClaimsRules.Domain
{
    internal static class AliasRules
    {
        private static readonly string[] CanonKeys =
        {
            "id","list","claims","tp","settp","flags","flag","folder","reload"
        };

        /// <summary>
        /// E5-B: Normalize in-memory and report applied fixes (IA-friendly).
        /// Returns true if any mutation occurred.
        /// </summary>
        internal static bool NormalizeInMemory(CommandAliasConfig cfg, List<string> fixes)
        {
            if (cfg == null) return false;
            bool changed = false;

            cfg.rootAlias = (cfg.rootAlias ?? "").Trim();
            if (cfg.rootAlias.Length == 0)
            {
                cfg.rootAlias = "ac";
                changed = true;
                AddFix(fixes, "rootAlias defaulted to 'ac'");
            }

            if (cfg.subAliases == null)
            {
                cfg.subAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                changed = true;
                AddFix(fixes, "subAliases created");
            }

            if (cfg.extraRootAliases == null)
            {
                cfg.extraRootAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                changed = true;
                AddFix(fixes, "extraRootAliases created");
            }

            foreach (var key in CanonKeys)
            {
                if (!cfg.subAliases.ContainsKey(key))
                {
                    cfg.subAliases[key] = key == "reload" ? "refresh" : key;
                    changed = true;
                    AddFix(fixes, $"missing subAlias added: {key}");
                }
            }

            var toRemove = new List<string>();
            var toAdd = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in cfg.subAliases)
            {
                var k = (kv.Key ?? "").Trim();
                var v = (kv.Value ?? "").Trim();

                if (k.Length == 0)
                {
                    toRemove.Add(kv.Key);
                    changed = true;
                    AddFix(fixes, "empty subAlias key removed");
                    continue;
                }

                if (v.Length == 0)
                {
                    v = k;
                    changed = true;
                    AddFix(fixes, $"empty subAlias value fixed: {k}");
                }

                if (!string.Equals(k, kv.Key, StringComparison.Ordinal) || !string.Equals(v, kv.Value, StringComparison.Ordinal))
                {
                    toRemove.Add(kv.Key);
                    if (!toAdd.ContainsKey(k)) toAdd[k] = v;
                    changed = true;
                    AddFix(fixes, $"subAlias normalized: {k}");
                }
            }

            foreach (var k in toRemove)
            {
                if (k != null) cfg.subAliases.Remove(k);
            }

            foreach (var kv in toAdd)
            {
                if (!cfg.subAliases.ContainsKey(kv.Key)) cfg.subAliases[kv.Key] = kv.Value;
            }

            return changed;
        }

        /// <summary>
        /// Back-compat: old signature (E5-A).
        /// </summary>
        internal static bool NormalizeInMemory(CommandAliasConfig cfg)
        {
            return NormalizeInMemory(cfg, fixes: null);
        }

        private static void AddFix(List<string> fixes, string msg)
        {
            if (fixes == null) return;
            if (fixes.Count >= 12) return; // keep logs compact
            fixes.Add(msg);
        }
    }
}
