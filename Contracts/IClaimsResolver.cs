using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace AxinClaimsRules.Contracts
{
    /// <summary>
    /// AXIN-AI-ARCH (E6-B / Contratos A5):
    /// Frontera mínima para resolver claims desde una posición.
    /// Adaptador sobre el resolver legacy (reflexión) para aislar dependencias.
    /// </summary>
    public interface IClaimsResolver
    {
        bool TryGetClaimAt(ICoreServerAPI api, BlockPos pos, out object claimObj, out string claimId, out string status);
    }
}
