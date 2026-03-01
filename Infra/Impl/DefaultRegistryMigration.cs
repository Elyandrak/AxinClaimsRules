using Vintagestory.API.Common;
using AxinClaimsRules.Contracts;
using AxinClaimsRules.Data.Registry.Migration;

namespace AxinClaimsRules.Infra.Impl
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.3a):
    /// Implementación por defecto de IRegistryMigration.
    /// Internamente delega en RegistryMigration (static) para minimizar cambios.
    /// </summary>
    internal sealed class DefaultRegistryMigration : IRegistryMigration
    {
        public bool ApplyRegistryMigrations(ICoreAPI api, ClaimsRegistry registry) => RegistryMigration.ApplyRegistryMigrations(api, registry);

        public bool ApplyAliasMigrations(ICoreAPI api, CommandAliasConfig aliasCfg) => RegistryMigration.ApplyAliasMigrations(api, aliasCfg);
    }
}
