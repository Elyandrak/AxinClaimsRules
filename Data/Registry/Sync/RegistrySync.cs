using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using System.Collections.Generic;
using AxinClaimsRules.Data.Registry;
using AxinClaimsRules.Data.Registry.Sync;

namespace AxinClaimsRules
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.4d):
    /// Facade/coordinator for Registry sync operations.
    /// Heavy logic has been split into small services under Data/Registry/Sync/*.
    /// Behavior must remain identical.
    /// </summary>
    internal static class RegistrySync
    {
                public static bool NormalizeRegistry(ICoreServerAPI api, ClaimsRegistry reg, bool exportTraderClaims)
                {
                    if (reg == null) return false;

                    reg.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    reg.foldersOrder ??= new List<string>();
                    reg.outsideOrder ??= new List<string>();
                    reg.folderOrders ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    reg.players ??= new Dictionary<string, PlayerClaimsEntry>();
        bool changed = false;

                    // 1) Build set of valid claim ids (and optionally trader filtering)
                    var validClaimIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var pk in reg.players)
                    {
                        var pe = pk.Value;
                        if (pe?.claims == null) continue;

                        foreach (var ck in pe.claims)
                        {
                            var cid = (ck.Key ?? "").Trim();
                            if (cid.Length == 0) continue;

                            var ce = ck.Value;
                            if (!exportTraderClaims && global::AxinClaimsRules.ClaimOwnerSignature.IsTraderClaim(ce?.info?.ownerPlayerUid, ce?.info?.ownerGroupUid, ce?.info?.lastKnownOwnerName))
                            {
                                // Keep claim entry but exclude from aliases listing
                                continue;
                            }

                            validClaimIds.Add(cid);
                        }
                    }

                    // 2) Remove aliases pointing to unknown/trader claims
                    if (reg.aliases.Count > 0)
                    {
                        var toRemove = new List<string>();
                        foreach (var kv in reg.aliases)
                        {
                            var akey = (kv.Key ?? "").Trim();
                            var cid = (kv.Value ?? "").Trim();
                            if (akey.Length == 0 || cid.Length == 0) { toRemove.Add(kv.Key); continue; }
                            if (!validClaimIds.Contains(cid)) toRemove.Add(kv.Key);
                        }
                        foreach (var k in toRemove)
                        {
                            reg.aliases.Remove(k);
                            changed = true;
                        }
                    }

                    // 3) Ensure 1 alias per claim (dedupe: if multiple keys map to same claim, keep first)
                    var seenClaim = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var dedupeRemove = new List<string>();
                    foreach (var kv in reg.aliases)
                    {
                        var cid = (kv.Value ?? "").Trim();
                        if (cid.Length == 0) { dedupeRemove.Add(kv.Key); continue; }
                        if (!seenClaim.Add(cid))
                        {
                            dedupeRemove.Add(kv.Key);
                        }
                    }
                    foreach (var k in dedupeRemove)
                    {
                        reg.aliases.Remove(k);
                        changed = true;
                    }

                    // 4) Generate missing aliases for all known claims (per player: ownerName + number)
                    var existingKeys = new HashSet<string>(reg.aliases.Keys, StringComparer.OrdinalIgnoreCase);

                    foreach (var pk in reg.players)
                    {
                        var pe = pk.Value;
                        if (pe?.claims == null) continue;

                        string baseName = AliasEnsureService.MakeOwnerBaseName(pe.lastKnownName ?? "player");

                        foreach (var ck in pe.claims)
                        {
                            var cid = (ck.Key ?? "").Trim();
                            if (cid.Length == 0) continue;
                            if (!validClaimIds.Contains(cid)) continue;

                            bool mapped = false;
                            foreach (var v in reg.aliases.Values)
                            {
                                if (string.Equals(v, cid, StringComparison.OrdinalIgnoreCase)) { mapped = true; break; }
                            }
                            if (mapped) continue;

                            int n = 1;
                            while (existingKeys.Contains(baseName + n)) n++;
                            string alias = baseName + n;
                            reg.aliases[alias] = cid;
                            existingKeys.Add(alias);
                            changed = true;
                        }
                    }


                    // 6) Derive foldersOrder from aliases (Folder/Alias)
                    var derived = new List<string>();
                    foreach (var k in reg.aliases.Keys)
                    {
                        if (string.IsNullOrWhiteSpace(k)) continue;
                        int idx = k.IndexOf('/');
                        if (idx <= 0) continue;
                        var folder = k.Substring(0, idx).Trim();
                        if (folder.Length == 0) continue;
                        if (!derived.Any(x => x.Equals(folder, StringComparison.OrdinalIgnoreCase))) derived.Add(folder);
                    }
                    foreach (var f in derived)
                    {
                        if (!reg.foldersOrder.Any(x => x.Equals(f, StringComparison.OrdinalIgnoreCase)))
                        {
                            reg.foldersOrder.Add(f);
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        reg.updatedAtUtc = DateTime.UtcNow.ToString("o");
                        RegistryStore.SaveClaimsRegistry(api, reg);
                    }

                    // --- Ensure at least one folder exists (example) ---
        if (reg.foldersOrder.Count == 0)
        {
            reg.foldersOrder.Add("Example");
            changed = true;
        }

        // --- Normalize claim ordering ---
        // Outside order: ensure it contains all outside aliases (no folder prefix) exactly once.
        var outsideAliases = reg.aliases.Keys.Where(k => !string.IsNullOrWhiteSpace(k) && !k.Contains("/"))
            .Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        reg.outsideOrder ??= new List<string>();
        // Remove missing
        reg.outsideOrder.RemoveAll(a => string.IsNullOrWhiteSpace(a) || !outsideAliases.Any(x => x.Equals(a, StringComparison.OrdinalIgnoreCase)));
        // Add new at end
        foreach (var a in outsideAliases)
        {
            if (!reg.outsideOrder.Any(x => x.Equals(a, StringComparison.OrdinalIgnoreCase)))
            {
                reg.outsideOrder.Add(a);
                changed = true;
            }
        }

        // Folder orders: ensure each folder has an order list that contains all aliases in that folder.
        reg.folderOrders ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var folderToAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in reg.aliases.Keys)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            int s = k.IndexOf('/');
            if (s <= 0) continue;
            var fn = k.Substring(0, s).Trim();
            if (fn.Length == 0) continue;
            folderToAliases.TryGetValue(fn, out var list);
            if (list == null) { list = new List<string>(); folderToAliases[fn] = list; }
            list.Add(k.Trim());
        }

        foreach (var kv in folderToAliases)
        {
            var fn = kv.Key;
            var aliasesInFolder = kv.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!reg.folderOrders.TryGetValue(fn, out var order) || order == null)
            {
                reg.folderOrders[fn] = new List<string>(aliasesInFolder);
                changed = true;
                continue;
            }

            // Remove missing
            order.RemoveAll(a => string.IsNullOrWhiteSpace(a) || !aliasesInFolder.Any(x => x.Equals(a, StringComparison.OrdinalIgnoreCase)));
            // Add new at end
            foreach (var a in aliasesInFolder)
            {
                if (!order.Any(x => x.Equals(a, StringComparison.OrdinalIgnoreCase)))
                {
                    order.Add(a);
                    changed = true;
                }
            }
        }

        return changed;
                }



        public static void EnsureClaimEntry(ICoreServerAPI api, object claimObj, string axinClaimId)
            => ClaimEnsureService.EnsureClaimEntry(api, claimObj, axinClaimId);

        public static void EnsureCurrentClaim(ICoreServerAPI api, IServerPlayer sp)
            => ClaimEnsureService.EnsureCurrentClaim(api, sp);

        public static string EnsureAliasForClaim(ICoreServerAPI api, string ownerPlayerUid, string ownerName, string axinClaimId)
            => AliasEnsureService.EnsureAliasForClaim(api, ownerPlayerUid, ownerName, axinClaimId);

        public static string GetAliasForClaim(PlayerClaimsEntry player, string axinClaimId)
            => AliasEnsureService.GetAliasForClaim(player, axinClaimId);

        public static bool TryResolveAlias(string alias, out string ownerPlayerUid, out string ownerName, out string axinClaimId, out ClaimEntry entry)
            => AliasEnsureService.TryResolveAlias(alias, out ownerPlayerUid, out ownerName, out axinClaimId, out entry);

        public static void EnsureTp(ICoreServerAPI api, string ownerPlayerUid, string axinClaimId, BlockPos pos, string setByUid, bool overwrite)
            => TpEnsureService.EnsureTp(api, ownerPlayerUid, axinClaimId, pos, setByUid, overwrite);

        public static bool TryGetRule_FireSpread(string axinClaimId, out bool allow)
            => RulesEnsureService.TryGetRule_FireSpread(axinClaimId, out allow);

        public static bool TryFindClaimEntry(string axinClaimId, out string ownerUid, out PlayerClaimsEntry player, out ClaimEntry claimEntry)
            => ClaimEnsureService.TryFindClaimEntry(axinClaimId, out ownerUid, out player, out claimEntry);
    }
}