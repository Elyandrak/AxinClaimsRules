using Vintagestory.API.Common;

namespace AxinClaimsRules.Contracts
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.3a / Contratos A5):
    /// Frontera mínima para migraciones in-memory de modelos persistentes.
    /// Regla: la migración NO debe ser destructiva.
    /// </summary>
    internal interface IRegistryMigration
    {
        /// <summary>
        /// Aplica migraciones al ClaimsRegistry cargado. Devuelve true si cambió y requiere persistencia.
        /// </summary>
        bool ApplyRegistryMigrations(ICoreAPI api, ClaimsRegistry registry);

        /// <summary>
        /// Aplica migraciones al Alias.json cargado. Devuelve true si cambió y requiere persistencia.
        /// </summary>
        bool ApplyAliasMigrations(ICoreAPI api, CommandAliasConfig aliasCfg);
    }
}
