using Vintagestory.API.Common;

namespace AxinClaimsRules.Contracts
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint E6-A / A5):
    /// Contrato mínimo de frontera para acceso a Registry/Alias persistentes.
    /// Regla: el resto del mod debe depender de esta interfaz, no del IO concreto.
    /// </summary>
    internal interface IRegistryStore
    {
        /// <summary>
        /// Try to load ClaimsRegistry.json from disk. Returns null if missing/corrupt.
        /// Must NOT create or write anything.
        /// </summary>
        ClaimsRegistry TryLoadClaimsRegistry(ICoreAPI api);

        /// <summary>
        /// Try to load Alias.json from disk. Returns null if missing/corrupt.
        /// Must NOT create or write anything.
        /// </summary>
        CommandAliasConfig TryLoadAliasConfig(ICoreAPI api);

        ClaimsRegistry LoadOrCreateClaimsRegistry(ICoreAPI api);
        CommandAliasConfig LoadOrCreateAliasConfig(ICoreAPI api);

        void SaveClaimsRegistry(ICoreAPI api, ClaimsRegistry registry);
        void SaveAliasConfig(ICoreAPI api, CommandAliasConfig alias);
    }
}
