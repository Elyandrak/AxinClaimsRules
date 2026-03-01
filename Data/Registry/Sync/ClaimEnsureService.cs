using System;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using AxinClaimsRules.Data.Registry;
using AxinClaimsRules.Core.Extensions;

namespace AxinClaimsRules.Data.Registry.Sync
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.4d): claim-related "ensure" operations.
    /// Extracted from RegistrySync.cs to reduce monolith risk and keep responsibilities small.
    /// </summary>
    internal static class ClaimEnsureService
    {
                public static void EnsureClaimEntry(ICoreServerAPI api, object claimObj, string axinClaimId)
                {
                    if (api == null) return;
                    if (string.IsNullOrWhiteSpace(axinClaimId)) return;
                    // AXIN: nunca abortar por RegistryCfg null. Cargar desde disco o crear default.
                    if (AxinClaimsRulesMod.RegistryCfg == null)
                    {
                        try
                        {
                            AxinClaimsRulesMod.RegistryCfg = RegistryStore.TryLoadClaimsRegistry(api);
                        }
                        catch { /* ignore */ }

                        AxinClaimsRulesMod.RegistryCfg ??= ClaimsRegistry.CreateDefault();
                    }

                    ClaimIdentity.TryExtractOwnerAndAreas(claimObj,
                        out string ownerPlayerUid,
                        out string ownerGroupUid,
                        out string lastKnownOwnerName,
                        out var areas,
                        out var bounds);

                    if (string.IsNullOrWhiteSpace(ownerPlayerUid) && string.IsNullOrWhiteSpace(ownerGroupUid))
                    {
                        // si no podemos extraer owner, igual registramos bajo clave "unknown"
                        ownerPlayerUid = "unknown";
                    }


                    // AXIN: Blindaje contra ediciones manuales.
                    // Antes de escribir ClaimsRegistry.json, recargar el archivo desde disco para no pisar cambios hechos a mano.
                    try
                    {
                        var disk = RegistryStore.TryLoadClaimsRegistry(api);
                        if (disk != null) AxinClaimsRulesMod.RegistryCfg = disk;
                    }
                    catch { /* ignore */ }

                    var reg = AxinClaimsRulesMod.RegistryCfg ?? ClaimsRegistry.CreateDefault();
                    AxinClaimsRulesMod.RegistryCfg = reg;
                    if (reg.players == null) reg.players = new Dictionary<string, PlayerClaimsEntry>();
                    if (!reg.players.TryGetValue(ownerPlayerUid, out var player))
                    {
                        player = new PlayerClaimsEntry();
                        reg.players[ownerPlayerUid] = player;
                    }
                    MigrateLegacyFolders(player);
                    if (player.claims == null) player.claims = new Dictionary<string, ClaimEntry>();


                    if (!string.IsNullOrWhiteSpace(lastKnownOwnerName))
                        player.lastKnownName = lastKnownOwnerName;

                    if (!player.claims.TryGetValue(axinClaimId, out var ce))
                    {
                        ce = new ClaimEntry();
                        player.claims[axinClaimId] = ce;
                    }

                    // Update info (informativo)
                    ce.info.ownerPlayerUid = ownerPlayerUid ?? "";
                    ce.info.ownerGroupUid = ownerGroupUid ?? "";
                    ce.info.lastKnownOwnerName = lastKnownOwnerName ?? "";
                    ce.info.lastSeenUtc = DateTime.UtcNow.ToString("o");

                    ce.info.areas = areas;
                    ce.info.bounds = bounds;

                    // Update center (aprox)
                    try
                    {
                        ce.info.center.x = (bounds.minX + bounds.maxX) / 2;
                        ce.info.center.y = (bounds.minY + bounds.maxY) / 2;
                        ce.info.center.z = (bounds.minZ + bounds.maxZ) / 2;
                    }
                    catch { }

                    // Create claimRules if missing (pero NO sobreescribir si ya existe)
                    if (ce.claimRules == null)
                        ce.claimRules = ClaimRules.CreateDefault(AxinClaimsRulesMod.GlobalCfg);

                    // Ensure nested rules exist for existing claims (do NOT overwrite existing values)
                    ce.claimRules.fireSpread ??= (AxinClaimsRulesMod.GlobalCfg?.defaults?.fireSpread ?? new ToggleRule { enabled = false });
                    ce.claimRules.fireIgnition ??= (AxinClaimsRulesMod.GlobalCfg?.defaults?.fireIgnition ?? new FireIgnitionRule { enabled = true, allowTorches = true, allowFirepit = true, allowCharcoalPit = true, allowFirestarterOnBlocks = true });

                    // AXIN-IA-ARCH (P1.2): ClaimFlight is addon-owned.
                    // - If addon is NOT loaded, do NOT create claimFlight in new JSON.
                    // - If claimFlight exists (manual edit or previous addon), preserve and just normalize nested fields.
                    // - If addon IS loaded, ensure defaults exist.
                    if (ce.claimRules.claimFlight != null)
                    {
                        ce.claimRules.claimFlight.whitelist ??= new List<string>();
                        if (string.IsNullOrWhiteSpace(ce.claimRules.claimFlight.mode)) ce.claimRules.claimFlight.mode = "all";
                    }
                    else if (ExtensionsState.IsLoaded("axinclaimsrulesflight"))
                    {
                        ce.claimRules.claimFlight = new ClaimFlightRule { enabled = false, mode = "all", whitelist = new List<string>() };
                    }


                    reg.updatedAtUtc = DateTime.UtcNow.ToString("o");

                    // Store
                    RegistryStore.SaveClaimsRegistry(api, reg);
                }

                public static void EnsureCurrentClaim(ICoreServerAPI api, IServerPlayer sp)
                {
                    if (api == null || sp?.Entity == null) return;

                    var pos = sp.Entity.Pos.AsBlockPos;

                    if (!ClaimResolver.TryGetClaimAt(api, pos, out object claimObj, out string axinClaimId, out string status))
                        return;

                    if (claimObj == null || string.IsNullOrWhiteSpace(axinClaimId)) return;

                    EnsureClaimEntry(api, claimObj, axinClaimId);

                    // owner + alias
                    ClaimIdentity.TryExtractOwnerAndAreas(claimObj, out string ownerUid, out _, out string ownerName, out _, out _);
                    if (string.IsNullOrWhiteSpace(ownerUid)) ownerUid = "unknown";

                    var alias = AliasEnsureService.EnsureAliasForClaim(api, ownerUid, ownerName, axinClaimId);

                    // default TP (first time any command runs in the claim)
                    try { TpEnsureService.EnsureTp(api, ownerUid, axinClaimId, pos, sp.PlayerUID, overwrite: false); } catch { }

                    // Also ensure flags snapshot exists (if your registry has it)
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(alias))
                        {
                            // No-op: flags are stored inside claimRules which EnsureClaimEntry creates.
                        }
                    }
                    catch { }
                }

        private static void MigrateLegacyFolders(PlayerClaimsEntry player)
        {
            if (player == null) return;

            if (player.aliases == null) player.aliases = new Dictionary<string, string>();
            if (player.foldersOrder == null) player.foldersOrder = new List<string>();
            if (player.aliasOrder == null) player.aliasOrder = new List<string>();

            // mover aliases legacy de tipo folder a foldersOrder
            var legacy = player.aliases
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key)
                             && !kv.Key.Contains("/")
                             && string.Equals((kv.Value ?? "").Trim(), "folder", StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key.Trim())
                .ToList();

            foreach (var fn in legacy)
            {
                if (!player.foldersOrder.Any(x => x.Equals(fn, StringComparison.OrdinalIgnoreCase)))
                    player.foldersOrder.Add(fn);

                player.aliases.Remove(fn);
            }

            // aliasOrder ya no se usa: limpiarlo si existe contenido
            if (player.aliasOrder.Count > 0) player.aliasOrder.Clear();
        }

        public static bool TryFindClaimEntry(string axinClaimId, out string ownerUid, out PlayerClaimsEntry player, out ClaimEntry claimEntry)
        {
            ownerUid = null;
            player = null;
            claimEntry = null;

            var reg = AxinClaimsRulesMod.RegistryCfg;
            if (reg?.players == null) return false;

            foreach (var kv in reg.players)
            {
                var puid = kv.Key;
                var pe = kv.Value;
                if (pe?.claims == null) continue;

                if (pe.claims.TryGetValue(axinClaimId, out var ce) && ce != null)
                {
                    ownerUid = puid;
                    player = pe;
                    claimEntry = ce;
                    return true;
                }
            }
            return false;
        }
    }
}
