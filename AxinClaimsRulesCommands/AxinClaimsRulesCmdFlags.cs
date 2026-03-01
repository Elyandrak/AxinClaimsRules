using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Features.Commands.Flags;

namespace AxinClaimsRules
{
    // AXIN-IA-ARCH (E7.4b): coordinator fino. Implementación real en FlagCommandService.
    internal static partial class AxinClaimCommands
    {
        public static TextCommandResult CmdFlagSet(ICoreServerAPI api, IServerPlayer sp, string aliasZona, string flagKey, string boolText)
            => FlagCommandService.CmdFlagSet(api, sp, aliasZona, flagKey, boolText);

        public static TextCommandResult CmdFlagHelp(ICoreServerAPI api, IServerPlayer sp)
            => FlagCommandService.CmdFlagHelp(api, sp);

        public static TextCommandResult CmdFlagHelp(ICoreServerAPI api, IServerPlayer sp, int page)
            => FlagCommandService.CmdFlagHelp(api, sp, page);

        public static TextCommandResult CmdFlags(ICoreServerAPI api, IServerPlayer sp, int page)
            => FlagCommandService.CmdFlags(api, sp, page);
    }
}
