using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace AxinClaimsRules
{
    internal static partial class AxinClaimCommands
    {

        public static TextCommandResult CmdSetTp(ICoreServerAPI api, IServerPlayer sp)
        {
        // AXIN: Any /ac command executed inside a claim must ensure registry (alias + flags) non-destructively.
        try { RegistrySync.EnsureCurrentClaim(api, sp); } catch { }

            var pos = sp.Entity.Pos.AsBlockPos;

            bool inClaim = ClaimResolver.TryGetClaimAt(api, pos, out object claimObj, out string axinClaimId, out string status);
            if (!inClaim || claimObj == null || string.IsNullOrWhiteSpace(axinClaimId))
                return TextCommandResult.Success($"No estás dentro de ningún claim. claimsAPI={status}");

            RegistrySync.EnsureClaimEntry(api, claimObj, axinClaimId);
            ClaimIdentity.TryExtractOwnerAndAreas(claimObj, out string ownerUid, out _, out string ownerName, out _, out _);

            var alias = RegistrySync.EnsureAliasForClaim(api, ownerUid, ownerName, axinClaimId);

            RegistrySync.EnsureTp(api, ownerUid, axinClaimId, pos, sp.PlayerUID, overwrite: true);

            return TextCommandResult.Success($"TP actualizado: alias={alias} axinClaimId={axinClaimId} tp={pos.X},{pos.Y},{pos.Z}");
        }

        public static TextCommandResult CmdTp(ICoreServerAPI api, IServerPlayer sp, string alias)
        {
        // AXIN: Any /ac command executed inside a claim must ensure registry (alias + flags) non-destructively.
        try { RegistrySync.EnsureCurrentClaim(api, sp); } catch { }

            if (string.IsNullOrWhiteSpace(alias)) return TextCommandResult.Error("Alias vacío.");

            if (!RegistrySync.TryResolveAlias(alias.Trim(), out string ownerUid, out string ownerName, out string axinClaimId, out ClaimEntry entry))
                return TextCommandResult.Error($"No se encontró el alias '{alias}'.");

            int x = 0, y = 0, z = 0;

            if (entry?.tp != null)
            {
                x = entry.tp.x; y = entry.tp.y; z = entry.tp.z;
            }
            else if (entry?.info?.center != null)
            {
                x = entry.info.center.x; y = entry.info.center.y; z = entry.info.center.z;
            }

            if (x == 0 && y == 0 && z == 0) return TextCommandResult.Error("Ese claim no tiene TP ni center.");

            if (!TeleportUtil.TryTeleport(sp.Entity, x + 0.5, y + 0.5, z + 0.5))
                return TextCommandResult.Error("No pude teletransportar (API desconocida).");

            return TextCommandResult.Success($"TP OK : {alias} ({ownerName}) axinClaimId={axinClaimId} pos={x},{y},{z}");
        }
    }
}
