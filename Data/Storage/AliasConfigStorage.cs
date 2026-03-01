using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace AxinClaimsRules.Data.Storage
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint E4):
    /// IO + migración de CommandAliasConfig (AliasFile) extraídos de ReloadService.
    /// </summary>
    internal static class AliasConfigStorage
    {
        internal static CommandAliasConfig LoadOrCreateAndMigrate(ICoreServerAPI api, string aliasFile, out bool wrote)
        {
            wrote = false;

            bool writeAlias = false;
            var cfg = api.LoadModConfig<CommandAliasConfig>(aliasFile);
            if (cfg == null)
            {
                cfg = CommandAliasConfig.CreateDefault();
                writeAlias = true;
            }
            else
            {
                var def = CommandAliasConfig.CreateDefault();

                if (cfg.schemaVersion < def.schemaVersion)
                {
                    cfg.schemaVersion = def.schemaVersion;
                    writeAlias = true;
                }

                // Ensure not-null
                cfg.subAliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (cfg.rootAlias == null) cfg.rootAlias = "";
            }

            if (writeAlias)
            {
                try { api.StoreModConfig(cfg, aliasFile); wrote = true; } catch { }
            }

            return cfg;
        }
    }
}
