using System;
using AxinClaimsRules.Modules.Commands.Listing;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AxinClaimsRules.Features.Commands.Handlers
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint D1):
    /// Handler dedicado para /ac list.
    /// Objetivo: sacar lógica de parsing/privilegios del ModSystem sin cambiar comportamiento.
    /// </summary>
    internal static class ListCommand
    {
        internal static TextCommandResult Handle(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var sp = args.Caller.Player as IServerPlayer;
            if (sp == null) return TextCommandResult.Error(LangManager.T("err.only.players", "Players only."));

            RegistrySync.EnsureCurrentClaim(api, sp);
            if (!PrivilegeChecks.RequireCmd(sp, AxinClaimsRulesMod.CmdCfg, "list"))
                return TextCommandResult.Error(LangManager.T("err.no.priv", "You don't have privileges for this command."));

            string folder = null;
            int page = 1;

            if (args.ArgCount >= 1)
            {
                var t0 = args[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(t0))
                {
                    if (int.TryParse(t0, out int p)) page = p;
                    else folder = t0;
                }
            }

            if (args.ArgCount >= 2)
            {
                var t1 = args[1]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(t1) && int.TryParse(t1, out int p2)) page = p2;
            }

            // E7.1b: route through IA-ARCH service boundary.
            // IMPORTANT: behavior remains identical in this micro-step because
            // ClaimsListService still delegates to AxinClaimCommands.CmdListAll (E7.1a).
            return new ClaimsListService().BuildList(api, sp, folder, page);
        }
    }
}
