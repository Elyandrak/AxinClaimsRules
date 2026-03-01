using Vintagestory.API.Common;
using AxinClaimsRules.Contracts;
using AxinClaimsRules.Data.Registry;

namespace AxinClaimsRules.Infra.Impl
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint E6-A / A5):
    /// Implementación por defecto de IRegistryStore.
    /// Internamente delega en RegistryStore (static) para minimizar cambios.
    /// </summary>
    internal sealed class DefaultRegistryStore : IRegistryStore
    {
        public ClaimsRegistry TryLoadClaimsRegistry(ICoreAPI api) => RegistryStore.TryLoadClaimsRegistry(api);

        public CommandAliasConfig TryLoadAliasConfig(ICoreAPI api) => RegistryStore.TryLoadAliasConfig(api);

        public ClaimsRegistry LoadOrCreateClaimsRegistry(ICoreAPI api) => RegistryStore.LoadOrCreateClaimsRegistry(api);

        public CommandAliasConfig LoadOrCreateAliasConfig(ICoreAPI api) => RegistryStore.LoadOrCreateAliasConfig(api);

        public void SaveClaimsRegistry(ICoreAPI api, ClaimsRegistry registry) => RegistryStore.SaveClaimsRegistry(api, registry);

        public void SaveAliasConfig(ICoreAPI api, CommandAliasConfig alias) => RegistryStore.SaveAliasConfig(api, alias);
    }
}
