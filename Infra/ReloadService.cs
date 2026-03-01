using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Domain;
using AxinClaimsRules.Data.Storage;
using static AxinClaimsRules.AxinClaimsRulesMod;
using AxinClaimsRules.Infra.Diagnostics;

namespace AxinClaimsRules.Infra
{
    internal static class ReloadService
    {
        internal static void ReloadAllFromDisk(ICoreServerAPI api)
        {
            try
            {
                // Reload config (auto-migrate + rewrite when schemaVersion changes)
                bool writeConfig = false;
                var cfg = api.LoadModConfig<CommandConfig>(ConfigFile);
                if (cfg == null)
                {
                    cfg = CommandConfig.CreateDefault();
                    writeConfig = true;
                }
                else
                {
                    var def = CommandConfig.CreateDefault();
                    if (cfg.schemaVersion < def.schemaVersion)
                    {
                        cfg.schemaVersion = def.schemaVersion;
                        writeConfig = true;
                    }

                    cfg.commandPrivileges ??= new Dictionary<string, string>();
                    foreach (var kv in def.commandPrivileges)
                    {
                        if (!cfg.commandPrivileges.ContainsKey(kv.Key)) { cfg.commandPrivileges[kv.Key] = kv.Value; writeConfig = true; }
                    }
                    // Ensure allowlist object contains all keys (non-destructive)
                    foreach (var kv in def.commandPrivilegeAllowPlayers)
                    {
                        if (!cfg.commandPrivilegeAllowPlayers.ContainsKey(kv.Key))
                        {
                            cfg.commandPrivilegeAllowPlayers[kv.Key] = kv.Value ?? "";
                            writeConfig = true;
                        }
                    }
                }

                if (writeConfig)
                {
                    try { api.StoreModConfig(cfg, ConfigFile); } catch { }
                }

                CmdCfg = cfg;

                var detected = ConfigLanguageReader.TryReadLanguageFromConfigRaw(ConfigFile);
                if (!string.IsNullOrWhiteSpace(detected)) CmdCfg.language = detected;

                // Reload global config + overrides
                GlobalCfg = api.LoadModConfig<GlobalConfig>(GlobalConfigFile) ?? GlobalCfg ?? GlobalConfig.CreateDefault();
                OverridesCfg = api.LoadModConfig<ClaimsOverrides>(OverridesFile) ?? OverridesCfg ?? ClaimsOverrides.CreateDefault();

                // Reload registry + alias (auto-migrate + rewrite when needed)
                bool writeRegistry = false;
                RegistryCfg = RegistryStoreSvc.TryLoadClaimsRegistry(api);
                if (RegistryCfg == null)
                {
                    RegistryCfg = ClaimsRegistry.CreateDefault();
                    writeRegistry = true;
                }
                else
                {
                    // E7.2c: migrations extracted
                    if (RegistryMigrationSvc.ApplyRegistryMigrations(api, RegistryCfg)) writeRegistry = true;
                }

                bool writeAlias = false;
                AliasCfg = RegistryStoreSvc.TryLoadAliasConfig(api);
                if (AliasCfg == null)
                {
                    AliasCfg = CommandAliasConfig.CreateDefault();
                    writeAlias = true;
                }
                else
                {
                    // E7.2c: migrations extracted
                    if (RegistryMigrationSvc.ApplyAliasMigrations(api, AliasCfg)) writeAlias = true;
                }
                // Normalize + migrate registry
                if (RegistryCfg.schemaVersion < 2)
                {
                    RegistryCfg.schemaVersion = 2;
                    writeRegistry = true;
                }

                // AXIN: v2 registry requires global aliases/foldersOrder fully consistent.
                // - 1 alias por claim conocido
                // - NO auto-folder en reload (AXIN: no destructivo). Auto-folder solo en /ac claims export para claims nuevos.
                // - eliminar aliases duplicados o inválidos
                // - excluir traders de aliases si exportTraderClaims=false
                try
                {
                    bool norm = RegistrySyncSvc.NormalizeRegistry(api, RegistryCfg, CmdCfg?.exportTraderClaims ?? false);
                    if (norm) writeRegistry = true;
                }
                catch { /* ignore */ }

                // E7.3b: optional no-op regression harness (disabled by default)
                if (CmdCfg?.debugRegistryNoOpHarness == true)
                {
                    RegistryNoOpHarness.Run(api, RegistryCfg, CmdCfg?.exportTraderClaims ?? false);
                }

                                // AXIN-AI-ARCH E5-B: Domain normalization (IA-friendly) + controlled persistence
                bool normRegChanged = false;
                bool normAliasChanged = false;
                var regFixes = new List<string>();
                var aliasFixes = new List<string>();

                try { normRegChanged = RegistryRules.NormalizeInMemory(RegistryCfg, regFixes); } catch { /* ignore */ }
                try { normAliasChanged = AliasRules.NormalizeInMemory(AliasCfg, aliasFixes); } catch { /* ignore */ }

                if (normRegChanged) writeRegistry = true;
                if (normAliasChanged) writeAlias = true;
                // Per-player legacy folder markers also handled inside ApplyRegistryMigrations.

                if (writeRegistry)
                {
                    try
                    {
                        RegistryCfg.updatedAtUtc = DateTime.UtcNow.ToString("o");
                        RegistryStoreSvc.SaveClaimsRegistry(api, RegistryCfg);
                    }
                    catch { }
                }

        
                
                if (writeAlias)
                {
                    try
                    {
                        RegistryStoreSvc.SaveAliasConfig(api, AliasCfg);
                    }
                    catch { }
                }

                // One-line normalize summary (anti-spam, IA-friendly)
                try
                {
                    api.Logger.Notification("[AxinClaimsRules] Normalize: registry={0}({1}) alias={2}({3})",
                        (normRegChanged ? "changed" : "ok"), regFixes.Count,
                        (normAliasChanged ? "changed" : "ok"), aliasFixes.Count);
                }
                catch { }

// If trader claims export is disabled, purge any previously exported trader claims on reload.
                try
                {
                    if (CmdCfg != null && CmdCfg.exportTraderClaims == false)
                    {
                        bool purged = PurgeTraderClaimsFromRegistry(RegistryCfg);
                        if (purged)
                        {
                            RegistryStoreSvc.SaveClaimsRegistry(api, RegistryCfg);
                            try { api.Logger.Notification("[AxinClaimsRules] Purged trader claims from ClaimsRegistry.json (exportTraderClaims=false)"); } catch { }
                        }
                    }
                }
                catch { /* ignore */ }

// Option B: physical purge on reload (only when detected). Controlled by Config.json.
try
{
    bool purgeGhosts = CmdCfg?.purgeGhostCustomMessageClaims ?? true;
    if (purgeGhosts)
    {
        bool purgedGhosts = PurgeGhostCustomMessageClaimsFromRegistry(RegistryCfg);
        if (purgedGhosts)
        {
            RegistryStoreSvc.SaveClaimsRegistry(api, RegistryCfg);
            try { api.Logger.Notification("[AxinClaimsRules] Purged CustomMessage ghost claims from ClaimsRegistry.json (purgeGhostCustomMessageClaims=true)"); } catch { }
        }
    }
}
catch { /* ignore */ }


        // Reload lang
                LangManager.Load(api, CmdCfg?.language ?? "en");
                try { api.Logger.Notification("[AxinClaimsRules] /ac reload: reloaded Config + Registry + Lang. lang={0}", LangManager.Current); } catch { }
            }
            catch (Exception e)
            {
                try { api.Logger.Warning("[AxinClaimsRules] ReloadAllFromDisk failed: {0}", e); } catch { }
            }
        }
    }
}