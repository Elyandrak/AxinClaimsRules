using AxinClaimsRules.Contracts;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace AxinClaimsRules.Features.Claims.Resolver
{
    /// <summary>
    /// Adaptador conservador sobre ClaimResolver legacy.
    /// </summary>
    public sealed class DefaultClaimsResolver : IClaimsResolver
    {
        public bool TryGetClaimAt(ICoreServerAPI api, BlockPos pos, out object claimObj, out string claimId, out string status)
        {
            return ClaimResolver.TryGetClaimAt(api, pos, out claimObj, out claimId, out status);
        }
    }
}
