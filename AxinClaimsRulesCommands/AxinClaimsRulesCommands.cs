using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using AxinClaimsRules.Data.Registry;

namespace AxinClaimsRules
{
    internal static class PrivilegeChecks
    {
        public static bool RequireAdmin(IServerPlayer sp)
        {
            if (sp == null) return false;
            try { return sp.HasPrivilege(Privilege.controlserver); }
            catch { return false; }
        }

        public static bool RequireCmd(IServerPlayer sp, CommandConfig cfg, string canonCmd, string extraAllowKey = null)
        {
            if (sp == null) return false;

            string priv = null;
            try
            {
                if (cfg?.commandPrivileges != null && cfg.commandPrivileges.TryGetValue(canonCmd, out var p) && !string.IsNullOrWhiteSpace(p))
                    priv = p;
            }
            catch { /* ignore */ }

            // Si no está definido, por defecto permitir (ya que el root command exige Privilege.chat)
            if (string.IsNullOrWhiteSpace(priv)) return true;

            // If it's not a controlserver command, respect the privilege as-is (chat, etc.)
            if (!priv.Equals(Privilege.controlserver, StringComparison.OrdinalIgnoreCase))
            {
                try { return sp.HasPrivilege(priv); }
                catch { return false; }
            }

            // controlserver: allow admins OR allowlisted players (only for this mode)
            try
            {
                if (sp.HasPrivilege(Privilege.controlserver)) return true;
            }
            catch { /* ignore */ }

            bool IsAllowedByKey(string key)
            {
                if (string.IsNullOrWhiteSpace(key)) return false;
                try
                {
                    if (cfg?.commandPrivilegeAllowPlayers == null) return false;
                    if (!cfg.commandPrivilegeAllowPlayers.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return false;

                    string uid = (sp.PlayerUID ?? "").Trim();
                    string name = "";
                    try { name = (sp.PlayerName ?? "").Trim(); } catch { }

                    // Accept separators: / , ; | whitespace
                    var tokens = raw.Split(new char[] { '/', ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var t in tokens)
                    {
                        var token = (t ?? "").Trim();
                        if (token.Length == 0) continue;

                        if (!string.IsNullOrWhiteSpace(uid) && token.Equals(uid, StringComparison.OrdinalIgnoreCase)) return true;
                        if (!string.IsNullOrWhiteSpace(name) && token.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
                catch { /* ignore */ }

                return false;
            }

            // 1) Per-command allowlist
            if (IsAllowedByKey(canonCmd)) return true;

            // 2) Optional extra allowlist (e.g. per-flag "flag<flagKey>")
            if (IsAllowedByKey(extraAllowKey)) return true;

            return false;
        }
    }

    internal static partial class AxinClaimCommands
    {
        public static TextCommandResult CmdId(ICoreServerAPI api, IServerPlayer sp)
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

            // TP por defecto = primera vez que se usa /axinclaim id dentro del claim
            RegistrySync.EnsureTp(api, ownerUid, axinClaimId, pos, sp.PlayerUID, overwrite: false);

            return TextCommandResult.Success($"alias={alias} axinClaimId={axinClaimId} ownerUid={ownerUid ?? "-"} ownerName={ownerName ?? "-"} pos={pos.X},{pos.Y},{pos.Z} claimsAPI={status}");
        }

    }
}
