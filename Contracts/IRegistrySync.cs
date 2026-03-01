using Vintagestory.API.Server;

namespace AxinClaimsRules.Contracts
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.3a / Contratos A5):
    /// Frontera mínima para operaciones de sync/normalización relacionadas con claims actuales.
    /// Objetivo: permitir testear handlers sin depender de static globals.
    /// </summary>
    internal interface IRegistrySync
    {
        void EnsureCurrentClaim(ICoreServerAPI api, IServerPlayer player);

        bool NormalizeRegistry(ICoreServerAPI api, ClaimsRegistry registry, bool includeTraders);

        void EnsureAliasForClaim(ICoreServerAPI api, string ownerKey, string ownerName, string claimId);
    }
}
