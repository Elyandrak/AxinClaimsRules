using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AxinClaimsRules.Features.Commands.Handlers
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint D5):
    /// Handler dedicado para /ac claims.
    /// Objetivo: sacar routing/parsing del registro de comandos sin cambiar comportamiento.
    /// </summary>
    internal static class ClaimsCommand
    {
        internal static TextCommandResult Handle(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var sp = args.Caller.Player as IServerPlayer;
            if (sp == null) return TextCommandResult.Error(LangManager.T("err.only.players", "Players only."));

            RegistrySync.EnsureCurrentClaim(api, sp);

            // NOTE: This can be heavy on huge servers, so restrict to operators by config.
            if (!PrivilegeChecks.RequireCmd(sp, AxinClaimsRulesMod.CmdCfg, "claims"))
                return TextCommandResult.Error(LangManager.T("err.no.priv", "You don't have permission."));

            string action = (args.Parsers[0].GetValue() as string ?? "").Trim();
            if (!string.Equals(action, "export", StringComparison.OrdinalIgnoreCase))
            {
                return AxinClaimCommands.CmdClaimsHelp(api, sp);
            }

            return AxinClaimCommands.CmdClaimsExport(api, sp);
        }
    }
}
