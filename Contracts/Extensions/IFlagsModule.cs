using System.Collections.Generic;
using Vintagestory.API.Server;

namespace AxinClaimsRules.Contracts.Extensions
{
    /// <summary>
    /// Addon-provided flags module.
    /// - Contributes keys to /ac flags list.
    /// - Handles set operations for its keys.
    /// - Renders its lines in /ac flags output.
    /// </summary>
    public interface IFlagsModule
    {
        /// <summary>Unique module id (usually same as modid).</summary>
        string Id { get; }

        /// <summary>Keys handled by this module (e.g. "claimFlight.enabled").</summary>
        IEnumerable<string> KnownKeys();

        /// <summary>
        /// Try apply a value to claim rules.
        /// Return true if handled (even if error), false if not recognized.
        /// </summary>
        bool TryApply(ClaimRules rules, string normalizedKey, string valueText, out string error, out string appliedKey);

        /// <summary>Render lines for /ac flags.</summary>
        void Render(List<string> lines, ClaimRules rules, ICoreServerAPI api, IServerPlayer sp);
    }
}
