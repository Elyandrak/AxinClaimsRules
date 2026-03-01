using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Domain;

namespace AxinClaimsRules.Data.Registry.Export
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.2d):
    /// Extrae la lógica de /ac claims export fuera del comando para reducir tamaño/acoplamiento.
    /// 
    /// Reglas:
    /// - No destructivo: nunca sobrescribir flags/aliases/folders existentes.
    /// - Auto-folder SOLO para claims NUEVOS exportados.
    /// </summary>
    internal static class ClaimsExportService
    {
        internal static TextCommandResult ExportWorldClaimsToRegistry(ICoreServerAPI api, ClaimsRegistry reg, bool includeTraders)
        {
            if (api == null || reg == null) return TextCommandResult.Error("Invalid caller.");

            // Ensure containers
            reg.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            reg.foldersOrder ??= new List<string>();
            reg.players ??= new Dictionary<string, PlayerClaimsEntry>();

            if (!includeTraders)
            {
                try { global::AxinClaimsRules.AxinClaimsRulesMod.PurgeTraderClaimsFromRegistry(reg); } catch { /* ignore */ }
            }
            bool purgeGhosts = global::AxinClaimsRules.AxinClaimsRulesMod.CmdCfg?.purgeGhostCustomMessageClaims ?? true;
            if (purgeGhosts)
            {
                // Purge persisted ghost claims from registry (physical purge) to prevent bloat and re-export.
                try { global::AxinClaimsRules.AxinClaimsRulesMod.PurgeGhostCustomMessageClaimsFromRegistry(reg); } catch { /* ignore */ }
            }


            int total = 0;
            int touched = 0;

            var newlyAddedClaimIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newClaimIdToOwnerKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var diag = new List<string>();
                var claimObjects = EnumerateAllClaimObjects(api, diag);
                foreach (var claimObj in claimObjects)
                {
                    if (claimObj == null) continue;

                    try
                    {
                        // Extract owner identity once for filters (trader / ghost)
ClaimIdentity.TryExtractOwnerAndAreas(claimObj,
    out string ownerPlayerUid,
    out string ownerGroupUid,
    out string lastKnownOwnerName,
    out var _areas,
    out var _bounds);

// Filter trader claims if needed
if (!includeTraders)
{
    if (global::AxinClaimsRules.ClaimOwnerSignature.IsTraderClaim(ownerPlayerUid, ownerGroupUid, lastKnownOwnerName)) continue;
}

// Filter CustomMessage ghost claims (server-history bloat)
if (purgeGhosts)
{
    if (global::AxinClaimsRules.ClaimOwnerSignature.IsGhostCustomMessageClaim(ownerPlayerUid, ownerGroupUid, lastKnownOwnerName)) continue;
}

string axinClaimId;
                        try { axinClaimId = ClaimIdentity.ComputeAxinClaimId(claimObj); }
                        catch (Exception exId)
                        {
                            api.Logger.Warning("[AxinClaimsRules] claims export: failed to compute axinClaimId for {0}: {1}", claimObj.GetType().FullName, exId);
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(axinClaimId)) continue;
                        total++;

                        bool isNew = MergeClaimIntoRegistry(reg, claimObj, axinClaimId, out var mergedOwnerKey);
                        if (isNew)
                        {
                            newlyAddedClaimIds.Add(axinClaimId);
                            if (!string.IsNullOrWhiteSpace(mergedOwnerKey)) newClaimIdToOwnerKey[axinClaimId] = mergedOwnerKey;
                        }

                        // Ensure a global alias exists for this claim (ownerName+counter pattern)
                        ClaimIdentity.TryExtractOwnerAndAreas(claimObj, out string ownerUid, out string ownerGroupUid2, out string ownerName, out _, out _);
                        var ownerKey = ResolveOwnerKey(ownerUid, ownerGroupUid2);
                        var nameForAlias = string.IsNullOrWhiteSpace(ownerName)
                            ? (ownerKey.StartsWith("group:", StringComparison.OrdinalIgnoreCase) ? ("Group " + ownerKey.Substring(6)) : "unknown")
                            : ownerName;
                        try { RegistrySync.EnsureAliasForClaim(api, ownerKey, nameForAlias, axinClaimId); } catch { /* ignore */ }

                        touched++;
                    }
                    catch (Exception exOne)
                    {
                        api.Logger.Warning("[AxinClaimsRules] claims export: skipping claim due to error: {0}", exOne);
                    }
                }

                if (diag.Count > 0)
                {
                    api.Logger.VerboseDebug("[AxinClaimsRules] claims export diag:\n" + string.Join("\n", diag));
                }
            }
            catch (Exception e)
            {
                api.Logger.Warning("[AxinClaimsRules] claims export failed: {0}", e);
                return TextCommandResult.Error(LangManager.T("claims.export.fail", "Claims export failed (see server-debug.log)."));
            }

            // Ensure foldersOrder contains folders detected from aliases ("Folder/Alias")
            try
            {
                foreach (var key in reg.aliases.Keys.ToList())
                {
                    int slash = key.IndexOf('/');
                    if (slash > 0)
                    {
                        var folder = key.Substring(0, slash).Trim();
                        if (!string.IsNullOrWhiteSpace(folder) && !reg.foldersOrder.Any(x => x.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                        {
                            reg.foldersOrder.Add(folder);
                        }
                    }
                }
            }
            catch { /* ignore */ }

            // Auto-folder ONLY for newly exported claims
            try { ApplyAutoFolderForNewClaims(reg, newlyAddedClaimIds, newClaimIdToOwnerKey); } catch { /* ignore */ }

            // Never allow "0 folders" UI state
            if (reg.foldersOrder.Count == 0) reg.foldersOrder.Add("Example");

            reg.updatedAtUtc = DateTime.UtcNow.ToString("o");

            try
            {
                RegistryStore.SaveClaimsRegistry(api, reg);
            }
            catch (Exception e)
            {
                api.Logger.Warning("[AxinClaimsRules] claims export failed writing ClaimsRegistry.json: {0}", e);
                return TextCommandResult.Error(LangManager.T("claims.export.fail.write", "Failed writing ClaimsRegistry.json (see server-debug.log)."));
            }

            return TextCommandResult.Success(LangManager.Tf(
                "claims.export.ok",
                "OK: exported {0} world claims (touched {1}). ClaimsRegistry.json updated.",
                total, touched
            ));
        }

        private static string ResolveOwnerKey(string ownerPlayerUid, string ownerGroupUid)
        {
            ownerPlayerUid = (ownerPlayerUid ?? "").Trim();
            ownerGroupUid = (ownerGroupUid ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(ownerPlayerUid)) return ownerPlayerUid;
            if (!string.IsNullOrWhiteSpace(ownerGroupUid) && ownerGroupUid != "0") return "group:" + ownerGroupUid;
            return "unknown";
        }

        private static bool MergeClaimIntoRegistry(ClaimsRegistry reg, object claimObj, string axinClaimId, out string ownerKey)
        {
            ownerKey = "";
            bool isNew = false;
            if (reg == null || claimObj == null) return false;
            if (string.IsNullOrWhiteSpace(axinClaimId)) return false;

            ClaimIdentity.TryExtractOwnerAndAreas(claimObj,
                out string ownerPlayerUid,
                out string ownerGroupUid,
                out string lastKnownOwnerName,
                out var areas,
                out var bounds);

            ownerKey = ResolveOwnerKey(ownerPlayerUid, ownerGroupUid);

            reg.players ??= new Dictionary<string, PlayerClaimsEntry>();
            if (!reg.players.TryGetValue(ownerKey, out var player))
            {
                player = new PlayerClaimsEntry();
                reg.players[ownerKey] = player;
            }

            if (!string.IsNullOrWhiteSpace(lastKnownOwnerName))
                player.lastKnownName = lastKnownOwnerName;

            player.claims ??= new Dictionary<string, ClaimEntry>();
            if (!player.claims.TryGetValue(axinClaimId, out var ce))
            {
                ce = new ClaimEntry();
                isNew = true;
                player.claims[axinClaimId] = ce;
            }

            ce.info ??= new ClaimInfo();
            ce.info.ownerPlayerUid = ownerPlayerUid ?? "";
            ce.info.ownerGroupUid = ownerGroupUid ?? "";
            ce.info.lastKnownOwnerName = lastKnownOwnerName ?? "";
            ce.info.lastSeenUtc = DateTime.UtcNow.ToString("o");
            ce.info.areas = areas;
            ce.info.bounds = bounds;

            try
            {
                ce.info.center.x = (bounds.minX + bounds.maxX) / 2;
                ce.info.center.y = (bounds.minY + bounds.maxY) / 2;
                ce.info.center.z = (bounds.minZ + bounds.maxZ) / 2;
            }
            catch { /* ignore */ }

            // Do NOT overwrite user flags / rules
            if (ce.claimRules == null)
                ce.claimRules = ClaimRules.CreateDefault(global::AxinClaimsRules.AxinClaimsRulesMod.GlobalCfg);

            return isNew;
        }

        private static void ApplyAutoFolderForNewClaims(ClaimsRegistry reg, HashSet<string> newClaimIds, Dictionary<string, string> newClaimIdToOwnerKey)
        {
            if (reg == null || reg.players == null || reg.aliases == null) return;
            if (newClaimIds == null || newClaimIds.Count == 0) return;

            reg.foldersOrder ??= new List<string>();
            reg.outsideOrder ??= new List<string>();
            reg.folderOrders ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            string OwnerBase(string ownerName)
            {
                string s = (ownerName ?? "").Trim();
                if (s.StartsWith("Player ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
                s = s.Trim();
                if (s.Length == 0) s = "player";

                var sb = new System.Text.StringBuilder();
                foreach (var ch in s)
                {
                    if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                }
                var outS = sb.ToString();
                if (outS.Length == 0) outS = "player";
                if (outS.Length > 16) outS = outS.Substring(0, 16);
                return outS;
            }

            var outsideAliasByClaim = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in reg.aliases)
            {
                var key = (kv.Key ?? "").Trim();
                if (key.Length == 0) continue;
                if (key.Contains("/")) continue; // only outside keys
                var cid = (kv.Value ?? "").Trim();
                if (cid.Length == 0) continue;

                if (!outsideAliasByClaim.TryGetValue(cid, out var list))
                {
                    list = new List<string>();
                    outsideAliasByClaim[cid] = list;
                }
                list.Add(key);
            }

            foreach (var pk in reg.players)
            {
                var ownerKey = (pk.Key ?? "").Trim();
                var pe = pk.Value;
                if (pe?.claims == null) continue;

                int ownedOutside = 0;
                foreach (var ck in pe.claims.Keys)
                {
                    if (outsideAliasByClaim.ContainsKey(ck)) ownedOutside++;
                }
                if (ownedOutside <= 4) continue;

                var newOutsideAliasMoves = new List<(string oldKey, string newKey, string cid)>();
                foreach (var cid in newClaimIds)
                {
                    if (newClaimIdToOwnerKey != null && newClaimIdToOwnerKey.TryGetValue(cid, out var ok)
                        && !string.IsNullOrWhiteSpace(ok) && !ok.Equals(ownerKey, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!pe.claims.ContainsKey(cid)) continue;
                    if (!outsideAliasByClaim.TryGetValue(cid, out var outsideKeys) || outsideKeys == null) continue;

                    foreach (var oldKey in outsideKeys)
                    {
                        if (string.IsNullOrWhiteSpace(oldKey)) continue;

                        string folder = "folder" + OwnerBase(pe.lastKnownName ?? "player");
                        if (folder.Length > 24) folder = folder.Substring(0, 24);

                        string newKey = folder + "/" + oldKey;
                        if (reg.aliases.ContainsKey(newKey)) continue;
                        newOutsideAliasMoves.Add((oldKey, newKey, cid));

                        if (!reg.foldersOrder.Any(x => x.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                        {
                            reg.foldersOrder.Add(folder);
                        }

                        if (!reg.folderOrders.TryGetValue(folder, out var order) || order == null)
                        {
                            order = new List<string>();
                            reg.folderOrders[folder] = order;
                        }

                        if (!order.Any(x => x.Equals(newKey, StringComparison.OrdinalIgnoreCase)))
                        {
                            order.Add(newKey);
                        }

                        reg.outsideOrder.RemoveAll(x => x != null && x.Equals(oldKey, StringComparison.OrdinalIgnoreCase));
                    }
                }

                foreach (var mv in newOutsideAliasMoves)
                {
                    if (reg.aliases.Remove(mv.oldKey))
                    {
                        reg.aliases[mv.newKey] = mv.cid;
                    }
                }
            }
        }

        private static IEnumerable<object> EnumerateAllClaimObjects(ICoreServerAPI api, List<string> diag)
        {
            var found = new List<object>();
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);

            void AddClaim(object claim)
            {
                if (claim == null) return;
                if (!seen.Add(claim)) return;
                found.Add(claim);
            }

            bool LooksLikeClaim(object o)
            {
                if (o == null) return false;
                var t = o.GetType();
                bool hasAreas = t.GetProperty("Areas") != null || t.GetProperty("areas") != null || t.GetField("Areas") != null || t.GetField("areas") != null;
                bool hasOwner = t.GetProperty("OwnerUID") != null || t.GetProperty("OwnerPlayerUid") != null || t.GetField("OwnerUID") != null || t.GetField("OwnerPlayerUid") != null || t.GetField("OwnerGroupUid") != null;
                return hasAreas || hasOwner;
            }

            void ScanObject(object root, string label, int depth)
            {
                if (root == null) return;
                if (depth > 4) return;

                if (root is IEnumerable ie && root is not string)
                {
                    int c = 0;
                    foreach (var item in ie)
                    {
                        if (item == null) continue;
                        c++;
                        if (LooksLikeClaim(item)) AddClaim(item);
                        if (depth < 2) ScanObject(item, label, depth + 1);
                    }
                    if (c > 0) diag?.Add($"{label}: enumerable items={c}");
                    return;
                }

                if (root is IDictionary dict)
                {
                    int c = 0;
                    foreach (var item in dict.Values)
                    {
                        if (item == null) continue;
                        c++;
                        if (LooksLikeClaim(item)) AddClaim(item);
                        if (depth < 2) ScanObject(item, label, depth + 1);
                    }
                    if (c > 0) diag?.Add($"{label}: dict values={c}");
                    return;
                }

                var t = root.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                foreach (var f in t.GetFields(flags))
                {
                    if (f.FieldType == typeof(string)) continue;
                    if (f.Name.IndexOf("claim", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    object v = null;
                    try { v = f.GetValue(root); } catch { }
                    if (v == null) continue;
                    ScanObject(v, $"{label}.{f.Name}", depth + 1);
                }
                foreach (var p in t.GetProperties(flags))
                {
                    if (!p.CanRead) continue;
                    if (p.PropertyType == typeof(string)) continue;
                    if (p.Name.IndexOf("claim", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    object v = null;
                    try { v = p.GetValue(root); } catch { }
                    if (v == null) continue;
                    ScanObject(v, $"{label}.{p.Name}", depth + 1);
                }
            }

            try { ScanObject(api?.World?.Claims, "World.Claims", 0); }
            catch (Exception e) { diag?.Add("World.Claims scan error: " + e.Message); }
            try { ScanObject(api?.WorldManager?.SaveGame, "WorldManager.SaveGame", 0); }
            catch (Exception e) { diag?.Add("SaveGame scan error: " + e.Message); }
            try { ScanObject(api?.WorldManager, "WorldManager", 0); }
            catch (Exception e) { diag?.Add("WorldManager scan error: " + e.Message); }

            var ok = new List<object>();
            foreach (var c in found)
            {
                try
                {
                    var id = ClaimIdentity.ComputeAxinClaimId(c);
                    if (!string.IsNullOrWhiteSpace(id)) ok.Add(c);
                }
                catch { /* ignore */ }
            }

            diag?.Add($"EnumerateAllClaimObjects: candidates={found.Count} valid={ok.Count}");
            return ok;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}