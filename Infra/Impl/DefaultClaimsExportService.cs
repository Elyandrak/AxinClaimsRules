using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules;
using AxinClaimsRules.Contracts;
using AxinClaimsRules.Data.Registry.Export;

namespace AxinClaimsRules.Infra.Impl
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.3a):
    /// Implementación por defecto de IClaimsExportService.
    /// Internamente delega en ClaimsExportService (static) para minimizar cambios.
    /// </summary>
    internal sealed class DefaultClaimsExportService : IClaimsExportService
    {
        public TextCommandResult ExportWorldClaimsToRegistry(ICoreServerAPI api, ClaimsRegistry reg, bool includeTraders)
            => ClaimsExportService.ExportWorldClaimsToRegistry(api, reg, includeTraders);
    }
}
