using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Infra;
using AxinClaimsRules.Features.Commands.Rendering;

namespace AxinClaimsRules.Features.Commands.Handlers
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint D4):
    /// Handler dedicado para /ac reload.
    /// Objetivo: sacar privilegios/validación del ModSystem sin cambiar comportamiento.
    /// Nota: NO llamar EnsureCurrentClaim aquí (el propio comando recarga desde disco).
    /// </summary>
    internal static class ReloadCommand
    {
        internal static TextCommandResult Handle(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var sp = args.Caller.Player as IServerPlayer;
            if (sp == null) return TextCommandResult.Error(LangManager.T("err.only.players", "Players only."));

            // NOTE: /ac reload must NOT touch registry before reload; avoid EnsureCurrentClaim here.

            if (!PrivilegeChecks.RequireCmd(sp, AxinClaimsRulesMod.CmdCfg, "reload"))
                return TextCommandResult.Error(UiTheme.Error(new ChatVtmlRenderer(), "No tienes privilegios para usar reload."));

            return AxinClaimCommands.CmdReload(api, sp);
        }
    }
}
