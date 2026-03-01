using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AxinClaimsRules.Features.Commands.Handlers
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint D2):
    /// Handler dedicado para /ac flags.
    /// Objetivo: sacar parsing/privilegios del ModSystem sin cambiar comportamiento.
    /// </summary>
    internal static class FlagsCommand
    {
        internal static TextCommandResult Handle(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var sp = args.Caller.Player as IServerPlayer;
            if (sp == null) return TextCommandResult.Error(LangManager.T("err.players.only", "Players only."));

            if (!PrivilegeChecks.RequireCmd(sp, AxinClaimsRulesMod.CmdCfg, "flags"))
                return TextCommandResult.Error(LangManager.T("err.no.priv", "You don't have privileges for this command."));

            int page = 1;
            if (args.ArgCount >= 1)
            {
                var token = args[0]?.ToString();
                if (!string.IsNullOrWhiteSpace(token)) int.TryParse(token.Trim(), out page);
            }

            return AxinClaimCommands.CmdFlags(api, sp, page);
        }
    }
}
