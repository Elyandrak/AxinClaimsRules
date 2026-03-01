using System;
using Vintagestory.API.Common;

namespace AxinClaimsRules.Data.Registry
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.2b):
    /// Store/IO concentrado para ClaimsRegistry.json y Alias.json.
    /// Objetivo: separar load/save del resto del mod sin cambiar comportamiento.
    /// </summary>
    internal static class RegistryStore
    {
        /// <summary>
        /// Try to load ClaimsRegistry from disk. Returns null if missing/corrupt.
        /// Does NOT create or write anything.
        /// </summary>
        internal static ClaimsRegistry TryLoadClaimsRegistry(ICoreAPI api)
        {
            if (api == null) return null;
            try { return api.LoadModConfig<ClaimsRegistry>(AxinClaimsRulesMod.RegistryFile); }
            catch (Exception e)
            {
                try { api.Logger.Warning("[AxinClaimsRules] Failed to load {0}: {1}", AxinClaimsRulesMod.RegistryFile, e.Message); } catch { }
                return null;
            }
        }

        internal static ClaimsRegistry LoadOrCreateClaimsRegistry(ICoreAPI api)
        {
            return LoadOrCreate(api, AxinClaimsRulesMod.RegistryFile, ClaimsRegistry.CreateDefault);
        }

        /// <summary>
        /// Try to load Alias.json from disk. Returns null if missing/corrupt.
        /// Does NOT create or write anything.
        /// </summary>
        internal static CommandAliasConfig TryLoadAliasConfig(ICoreAPI api)
        {
            if (api == null) return null;
            try { return api.LoadModConfig<CommandAliasConfig>(AxinClaimsRulesMod.AliasFile); }
            catch (Exception e)
            {
                try { api.Logger.Warning("[AxinClaimsRules] Failed to load {0}: {1}", AxinClaimsRulesMod.AliasFile, e.Message); } catch { }
                return null;
            }
        }

        internal static CommandAliasConfig LoadOrCreateAliasConfig(ICoreAPI api)
        {
            return LoadOrCreate(api, AxinClaimsRulesMod.AliasFile, CommandAliasConfig.CreateDefault);
        }

        internal static void SaveClaimsRegistry(ICoreAPI api, ClaimsRegistry registry)
        {
            if (api == null || registry == null) return;
            try { api.StoreModConfig(registry, AxinClaimsRulesMod.RegistryFile); }
            catch (Exception e)
            {
                try { api.Logger.Warning("[AxinClaimsRules] Failed to write {0}: {1}", AxinClaimsRulesMod.RegistryFile, e.Message); } catch { }
            }
        }

        internal static void SaveAliasConfig(ICoreAPI api, CommandAliasConfig alias)
        {
            if (api == null || alias == null) return;
            try { api.StoreModConfig(alias, AxinClaimsRulesMod.AliasFile); }
            catch (Exception e)
            {
                try { api.Logger.Warning("[AxinClaimsRules] Failed to write {0}: {1}", AxinClaimsRulesMod.AliasFile, e.Message); } catch { }
            }
        }

        private static T LoadOrCreate<T>(ICoreAPI api, string file, Func<T> factory) where T : class
        {
            try
            {
                var cfg = api.LoadModConfig<T>(file);
                if (cfg != null) return cfg;
            }
            catch (Exception e)
            {
                try { api.Logger.Warning("[AxinClaimsRules] Failed to load {0}: {1}", file, e.Message); } catch { }
            }

            var created = factory();
            try
            {
                api.StoreModConfig(created, file);
                try { api.Logger.Notification("[AxinClaimsRules] Created default {0} in ModConfig", file); } catch { }
            }
            catch (Exception e)
            {
                try { api.Logger.Error("[AxinClaimsRules] Failed to write default {0}: {1}", file, e); } catch { }
            }

            return created;
        }
    }
}
