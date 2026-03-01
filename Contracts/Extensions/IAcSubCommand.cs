using Vintagestory.API.Server;
using Vintagestory.API.Common;

namespace AxinClaimsRules.Contracts.Extensions
{
    /// <summary>
    /// Addon-provided subcommand under /ac.
    /// Core will register it as: /ac &lt;Key&gt;
    /// </summary>
    public interface IAcSubCommand
    {
        /// <summary>Canonical key (e.g. "addonping").</summary>
        string Key { get; }

        /// <summary>Description shown in command help (if used).</summary>
        string Description { get; }

        /// <summary>Execute command. Addon parses args itself.</summary>
        TextCommandResult Execute(ICoreServerAPI api, IServerPlayer sp, TextCommandCallingArgs args);
    }
}
