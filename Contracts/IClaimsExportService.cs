using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules;

namespace AxinClaimsRules.Contracts
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.3a / Contratos A5):
    /// Frontera mínima para exportar claims del mundo al ClaimsRegistry.
    /// </summary>
    internal interface IClaimsExportService
    {
        TextCommandResult ExportWorldClaimsToRegistry(ICoreServerAPI api, ClaimsRegistry reg, bool includeTraders);
    }
}
