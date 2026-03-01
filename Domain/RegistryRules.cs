using System;
using System.Collections.Generic;

namespace AxinClaimsRules.Domain
{
    internal static class RegistryRules
    {
        /// <summary>
        /// E5-B: Normalize in-memory and report applied fixes (IA-friendly).
        /// Returns true if any mutation occurred.
        /// </summary>
        internal static bool NormalizeInMemory(ClaimsRegistry reg, List<string> fixes)
        {
            if (reg == null) return false;
            bool changed = false;

            if (reg.aliases == null)
            {
                reg.aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                changed = true;
                AddFix(fixes, "aliases created");
            }

            if (reg.foldersOrder == null)
            {
                reg.foldersOrder = new List<string>();
                changed = true;
                AddFix(fixes, "foldersOrder created");
            }

            if (reg.outsideOrder == null)
            {
                reg.outsideOrder = new List<string>();
                changed = true;
                AddFix(fixes, "outsideOrder created");
            }

            if (reg.folderOrders == null)
            {
                reg.folderOrders = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                changed = true;
                AddFix(fixes, "folderOrders created");
            }

            if (reg.players == null)
            {
                reg.players = new Dictionary<string, PlayerClaimsEntry>(StringComparer.OrdinalIgnoreCase);
                changed = true;
                AddFix(fixes, "players created");
            }

            // Trim + remove empties in aliases
            var toRemove = new List<string>();
            var toAdd = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in reg.aliases)
            {
                var k = (kv.Key ?? "").Trim();
                var v = (kv.Value ?? "").Trim();
                if (k.Length == 0 || v.Length == 0)
                {
                    toRemove.Add(kv.Key);
                    changed = true;
                    AddFix(fixes, "empty alias removed");
                    continue;
                }

                if (!string.Equals(k, kv.Key, StringComparison.Ordinal) || !string.Equals(v, kv.Value, StringComparison.Ordinal))
                {
                    toRemove.Add(kv.Key);
                    if (!toAdd.ContainsKey(k)) toAdd[k] = v;
                    changed = true;
                    AddFix(fixes, $"alias normalized: {k}");
                }
            }

            foreach (var k in toRemove)
            {
                if (k != null && reg.aliases.Remove(k)) { /* already counted */ }
            }

            foreach (var kv in toAdd)
            {
                if (!reg.aliases.ContainsKey(kv.Key))
                {
                    reg.aliases[kv.Key] = kv.Value;
                }
            }

            if (DedupInPlace(reg.foldersOrder, StringComparer.OrdinalIgnoreCase, fixes, "foldersOrder"))
            {
                changed = true;
            }

            if (DedupInPlace(reg.outsideOrder, StringComparer.OrdinalIgnoreCase, fixes, "outsideOrder"))
            {
                changed = true;
            }

            foreach (var fk in reg.folderOrders)
            {
                if (fk.Value == null)
                {
                    reg.folderOrders[fk.Key] = new List<string>();
                    changed = true;
                    AddFix(fixes, $"folderOrders[{fk.Key}] created");
                    continue;
                }

                if (DedupInPlace(fk.Value, StringComparer.OrdinalIgnoreCase, fixes, $"folderOrders[{fk.Key}]"))
                {
                    changed = true;
                }
            }

            // Safety: foldersOrder should not contain reserved view name "Outside"
            for (int i = reg.foldersOrder.Count - 1; i >= 0; i--)
            {
                if (string.Equals(reg.foldersOrder[i], "Outside", StringComparison.OrdinalIgnoreCase))
                {
                    reg.foldersOrder.RemoveAt(i);
                    changed = true;
                    AddFix(fixes, "reserved folder removed from foldersOrder: Outside");
                }
            }

            return changed;
        }

        /// <summary>
        /// Back-compat: old signature (E5-A).
        /// </summary>
        internal static bool NormalizeInMemory(ClaimsRegistry reg)
        {
            return NormalizeInMemory(reg, fixes: null);
        }

        private static bool DedupInPlace(List<string> list, IEqualityComparer<string> cmp, List<string> fixes, string label)
        {
            if (list == null) return false;
            bool changed = false;
            var seen = new HashSet<string>(cmp);

            // forward preserve order
            for (int i = 0; i < list.Count; i++)
            {
                var v = (list[i] ?? "").Trim();
                if (v.Length == 0)
                {
                    list[i] = null;
                    changed = true;
                    continue;
                }
                if (v != list[i])
                {
                    list[i] = v;
                    changed = true;
                }
                if (!seen.Add(v))
                {
                    list[i] = null;
                    changed = true;
                }
            }

            if (changed)
            {
                list.RemoveAll(s => string.IsNullOrWhiteSpace(s));
                AddFix(fixes, $"dedup+trim applied: {label}");
            }

            return changed;
        }

        private static void AddFix(List<string> fixes, string msg)
        {
            if (fixes == null) return;
            if (fixes.Count >= 12) return;
            fixes.Add(msg);
        }
    }
}
