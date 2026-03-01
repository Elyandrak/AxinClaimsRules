using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AxinClaimsRules.Features.Commands.Handlers
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint D3):
    /// Handler dedicado para /ac tp.
    /// Objetivo: sacar parsing/privilegios del ModSystem sin cambiar comportamiento.
    /// </summary>
    internal static class TeleportCommand
    {
        internal static TextCommandResult Handle(ICoreServerAPI api, TextCommandCallingArgs args, string rootAlias)
        {
            var sp = args.Caller.Player as IServerPlayer;
            if (sp == null) return TextCommandResult.Error(LangManager.T("err.only.players", "Players only."));

            RegistrySync.EnsureCurrentClaim(api, sp);

            if (!PrivilegeChecks.RequireCmd(sp, AxinClaimsRulesMod.CmdCfg, "tp"))
                return TextCommandResult.Error("No tienes privilegios para usar tp.");

            var aliasObj = args.ArgCount >= 1 ? args[0] : null;
            var alias = aliasObj?.ToString();

            if (string.IsNullOrWhiteSpace(alias))
            {
                return TextCommandResult.Error("Falta alias. Uso: /" + rootAlias + " tp [alias]");
            }

            return AxinClaimCommands.CmdTp(api, sp, alias.Trim());
        }
    }
}
