using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
// NOTE: Models were extracted in E7.2a and intentionally kept in the root namespace (AxinClaimsRules)
// to avoid widespread using changes. Do NOT reference a Data.Registry.Models namespace.
using AxinClaimsRules;

namespace AxinClaimsRules.Data.Registry.Migration
{
    /// <summary>
    /// AXIN :: Registry migrations extracted from ReloadService (E7.2c).
    /// Goal: keep behavior identical while reducing ReloadService size.
    /// </summary>
    internal static class RegistryMigration
    {
        /// <summary>
        /// Apply in-memory migrations to ClaimsRegistry loaded from disk.
        /// Returns true if registry was changed and should be persisted.
        /// Non-destructive: preserves existing mappings when possible.
        /// </summary>
        internal static bool ApplyRegistryMigrations(ICoreAPI api, ClaimsRegistry registry)
        {
            if (registry == null) return false;

            bool changed = false;
            var defReg = ClaimsRegistry.CreateDefault();

            // Migrate older schema to current default schemaVersion
            if (registry.schemaVersion < defReg.schemaVersion)
            {
                // Migrate v1 -> v2: move aliases + foldersOrder to root, keep claims per-player.
                registry.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                registry.foldersOrder ??= new List<string>();

                if (registry.players != null)
                {
                    foreach (var pk in registry.players)
                    {
                        var pe = pk.Value;
                        if (pe == null) continue;

                        // Merge legacy per-player aliases into global root aliases
                        try
                        {
                            if (pe.aliases != null)
                            {
                                foreach (var ak in pe.aliases)
                                {
                                    var akey = (ak.Key ?? "").Trim();
                                    var aval = (ak.Value ?? "").Trim();
                                    if (akey.Length == 0 || aval.Length == 0) continue;

                                    // Non-destructive: if alias already exists, keep existing mapping
                                    if (!registry.aliases.ContainsKey(akey))
                                        registry.aliases[akey] = aval;
                                }
                            }
                        }
                        catch { }

                        // Merge legacy per-player foldersOrder into global foldersOrder
                        try
                        {
                            if (pe.foldersOrder != null)
                            {
                                foreach (var f in pe.foldersOrder)
                                {
                                    var fn = (f ?? "").Trim();
                                    if (fn.Length == 0) continue;
                                    if (!registry.foldersOrder.Any(x => x.Equals(fn, StringComparison.OrdinalIgnoreCase)))
                                        registry.foldersOrder.Add(fn);
                                }
                            }
                        }
                        catch { }
                    }
                }

                // If foldersOrder still empty, derive from aliases (Folder/Alias keys)
                try
                {
                    if (registry.foldersOrder.Count == 0 && registry.aliases != null)
                    {
                        foreach (var k in registry.aliases.Keys)
                        {
                            if (string.IsNullOrWhiteSpace(k)) continue;
                            var idxSlash = k.IndexOf('/');
                            if (idxSlash <= 0) continue;
                            var folder = k.Substring(0, idxSlash).Trim();
                            if (folder.Length == 0) continue;
                            if (!registry.foldersOrder.Any(x => x.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                                registry.foldersOrder.Add(folder);
                        }
                    }
                }
                catch { }

                registry.schemaVersion = defReg.schemaVersion;
                changed = true;
            }

            // Ensure containers (v2+)
            registry.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            registry.foldersOrder ??= new List<string>();

            // Legacy per-player folder markers migration (kept identical)
            registry.players ??= new Dictionary<string, PlayerClaimsEntry>();
            foreach (var p in registry.players.Values)
            {
                if (p == null) continue;
                p.claims ??= new Dictionary<string, ClaimEntry>();
                p.aliases ??= new Dictionary<string, string>();
                p.foldersOrder ??= new List<string>();

                // migrate legacy folder markers: aliases["Folder"]="folder"
                var legacyFolders = p.aliases
                    .Where(kv => !string.IsNullOrWhiteSpace(kv.Key)
                                 && !kv.Key.Contains("/")
                                 && string.Equals((kv.Value ?? "").Trim(), "folder", StringComparison.OrdinalIgnoreCase))
                    .Select(kv => kv.Key.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var fn in legacyFolders)
                {
                    if (!p.foldersOrder.Any(x => x.Equals(fn, StringComparison.OrdinalIgnoreCase)))
                    {
                        p.foldersOrder.Add(fn);
                        changed = true;
                    }
                    p.aliases.Remove(fn);
                    changed = true;
                }

                // detect folders by prefix in "Folder/Alias"
                foreach (var kv in p.aliases)
                {
                    var k = (kv.Key ?? "").Trim();
                    var v = (kv.Value ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(k) || !k.Contains("/")) continue;
                    if (!v.StartsWith("axin:", StringComparison.OrdinalIgnoreCase)) continue;

                    int slash = k.IndexOf('/');
                    if (slash <= 0) continue;
                    var fn = k.Substring(0, slash).Trim();
                    if (string.IsNullOrWhiteSpace(fn) || fn.Equals("Outside", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!p.foldersOrder.Any(x => x.Equals(fn, StringComparison.OrdinalIgnoreCase)))
                    {
                        p.foldersOrder.Add(fn);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        /// <summary>
        /// Apply in-memory migrations to Alias.json model loaded from disk.
        /// Returns true if alias config was changed and should be persisted.
        /// </summary>
        internal static bool ApplyAliasMigrations(ICoreAPI api, CommandAliasConfig aliasCfg)
        {
            if (aliasCfg == null) return false;

            bool changed = false;
            var def = CommandAliasConfig.CreateDefault();

            if (aliasCfg.schemaVersion < def.schemaVersion)
            {
                aliasCfg.schemaVersion = def.schemaVersion;
                changed = true;
            }

            aliasCfg.subAliases ??= new Dictionary<string, string>();
            foreach (var kv in def.subAliases)
            {
                if (!aliasCfg.subAliases.ContainsKey(kv.Key))
                {
                    aliasCfg.subAliases[kv.Key] = kv.Value;
                    changed = true;
                }
            }

            // Legacy: "sync" entry in Alias.json should become reload (default alias: refresh)
            if (aliasCfg.subAliases.TryGetValue("sync", out var syncAlias))
            {
                if (!aliasCfg.subAliases.ContainsKey("reload"))
                {
                    aliasCfg.subAliases["reload"] = string.IsNullOrWhiteSpace(syncAlias) ? def.subAliases["reload"] : syncAlias;
                }
                aliasCfg.subAliases.Remove("sync");
                changed = true;
            }

            // If reload alias is still the canonical name ("reload"), update to UX default ("refresh") only on schema upgrade.
            if (aliasCfg.schemaVersion >= 2 &&
                aliasCfg.subAliases.TryGetValue("reload", out var r) &&
                string.Equals(r, "reload", StringComparison.OrdinalIgnoreCase))
            {
                aliasCfg.subAliases["reload"] = def.subAliases["reload"]; // refresh
                changed = true;
            }

            return changed;
        }
    }
}
