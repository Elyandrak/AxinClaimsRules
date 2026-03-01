using Vintagestory.API.Server;
using AxinClaimsRules.Contracts;

namespace AxinClaimsRules.Infra.Impl
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.3a):
    /// Implementación por defecto de IRegistrySync.
    /// Internamente delega en RegistrySync (static) para minimizar cambios.
    /// </summary>
    internal sealed class DefaultRegistrySync : IRegistrySync
    {
        public void EnsureCurrentClaim(ICoreServerAPI api, IServerPlayer player) => RegistrySync.EnsureCurrentClaim(api, player);

        public bool NormalizeRegistry(ICoreServerAPI api, ClaimsRegistry registry, bool includeTraders)
            => RegistrySync.NormalizeRegistry(api, registry, includeTraders);

        public void EnsureAliasForClaim(ICoreServerAPI api, string ownerKey, string ownerName, string claimId)
            => RegistrySync.EnsureAliasForClaim(api, ownerKey, ownerName, claimId);
    }
}
