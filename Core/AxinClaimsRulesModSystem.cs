using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using AxinClaimsRules.Data.Registry;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Text.Json;
using AxinClaimsRules.Contracts;
using AxinClaimsRules.Infra.Impl;
using AxinClaimsRules.Features.Commands.Rendering;
using AxinClaimsRules.Features.Claims.Resolver;
using AxinClaimsRules.Features.Commands.Handlers;
using AxinClaimsRules.Features.Commands;
using AxinClaimsRules.Infra;
using AxinClaimsRules.Core;
#if DEBUG
using AxinClaimsRules.Debugging;
#endif


/*
AXIN — AxinClaimsRules
CORE MODULAR v0.6.4-8FILES (compat VS 1.21.6)

Este proyecto está dividido en módulos para reducir riesgo de regresión y facilitar mantenimiento.
IMPORTANTE (IA / continuidad): normalmente solo compartirás este archivo (AxinClaimsRulesCore.cs).
Por eso, aquí se documenta qué contiene cada módulo y cómo se conectan.

Mapa de archivos:
- AxinClaimsRulesCore.cs (este archivo): entrada principal del mod. Ciclo de vida del ModSystem, carga de config/registry/idiomas y registro de comandos + Harmony.
- AxinClaimsRulesCommands.cs: handlers de /axinclaims (/ac) y subcomandos (help, list, flags, flag, id, settp, tp, reload…). 
- AxinClaimsRulesConfig.cs: modelo + IO de Config.json (schemaVersion, language, defaults) + migración no destructiva.
- AxinClaimsRulesRegistry.cs: modelo + IO de ClaimsRegistry.json y Alias.json. Registro no destructivo (no sobrescribir claimRules).
- AxinClaimsRulesLang.cs: carga de assets/axinclaimsrules/lang/*.json + T(key) con fallback a EN.
- AxinClaimsRulesClaims.cs: resolver claim en pos + cálculo de axinClaimId determinista.
- AxinClaimsRulesFirePatch.cs: Harmony patch (p.ej. fireSpread).
- AxinClaimsRulesJson.cs: utilidades (JSON safe IO, helpers, create-if-missing).

Reglas clave:
- Chat: evitar caracteres '<' y '>' en output (rompen el parser del chat).
- Para que /ac muestre texto, devolver SIEMPRE TextCommandResult.Success("...") con texto.
- Registro: cualquier comando /ac dentro de claim asegura alias + flags en ClaimsRegistry (no destructivo).
- Idiomas: Config.json contiene 'language'; JSON en assets/axinclaimsrules/lang/*.json.

Autor: elYandrack (AXIN)
*/


namespace AxinClaimsRules
{
    public class AxinClaimsRulesMod : ModSystem
    {
        internal static ICoreServerAPI Sapi;
        internal static IRegistryStore RegistryStoreSvc;
        internal static IRegistryMigration RegistryMigrationSvc;
        internal static IClaimsExportService ClaimsExportSvc;
        internal static IRegistrySync RegistrySyncSvc;
        internal static ICommandRenderer RendererSvc;
        internal static IClaimsResolver ClaimsResolverSvc;
        internal static GlobalConfig GlobalCfg;
        internal static ClaimsOverrides OverridesCfg;
        internal static ClaimsRegistry RegistryCfg;
        internal static CommandAliasConfig AliasCfg;
        internal static CommandConfig CmdCfg;

        internal const string GlobalConfigFile = "AxinClaimsRules/GlobalConfig.json";
        internal const string OverridesFile = "AxinClaimsRules/ClaimsOverrides.json";
        internal const string RegistryFile  = "AxinClaimsRules/ClaimsRegistry.json";
        internal const string AliasFile     = "AxinClaimsRules/Alias.json";
        internal const string ConfigFile    = "AxinClaimsRules/Config.json";
        public override void Start(ICoreAPI api)
        {
            // Common (client+server): register network channel + packets
            try
            {
                // (moved to plugin) ClaimFlight.StartCommon(api);
            }
            catch { /* ignore */ }
        }

public override void StartServerSide(ICoreServerAPI api)
        {
            Sapi = api;
            RegistryStoreSvc = new DefaultRegistryStore();
            RegistryMigrationSvc = new DefaultRegistryMigration();
            ClaimsExportSvc = new DefaultClaimsExportService();
            RegistrySyncSvc = new DefaultRegistrySync();
            RendererSvc = new ChatVtmlRenderer();
            ClaimsResolverSvc = new DefaultClaimsResolver();

            GlobalCfg    = LoadOrCreate(api, GlobalConfigFile, GlobalConfig.CreateDefault);
            OverridesCfg = LoadIfExists<ClaimsOverrides>(api, OverridesFile);
            RegistryCfg  = RegistryStoreSvc.LoadOrCreateClaimsRegistry(api);
            AliasCfg     = RegistryStoreSvc.LoadOrCreateAliasConfig(api);
            CmdCfg       = LoadOrCreate(api, ConfigFile, CommandConfig.CreateDefault); // Load Config.json (do NOT rewrite). Also accept "Language"/"language" (case-insensitive).

            if (CmdCfg == null)
            {
                // If config missing/invalid, create default ONCE.
                CmdCfg = CommandConfig.CreateDefault();
                api.StoreModConfig(CmdCfg, ConfigFile);
            }
            else
            {
                // Read raw json to accept "Language" (uppercase) and other casing variants without rewriting file.
                var detected = TryReadLanguageFromConfigRaw();
                if (!string.IsNullOrWhiteSpace(detected))
                {
                    CmdCfg.language = detected;
                }
            }



// Auto-migrate Config.json ONLY when schemaVersion upgrades (non-destructive).
try
{
    bool writeConfig = false;
    var def = CommandConfig.CreateDefault();

    if (CmdCfg.schemaVersion < def.schemaVersion)
    {
        CmdCfg.schemaVersion = def.schemaVersion;
        writeConfig = true;
    }

    CmdCfg.commandPrivileges ??= new Dictionary<string, string>();
    foreach (var kv in def.commandPrivileges)
    {
        if (!CmdCfg.commandPrivileges.ContainsKey(kv.Key)) { CmdCfg.commandPrivileges[kv.Key] = kv.Value; writeConfig = true; }
    }

    CmdCfg.commandPrivilegeAllowPlayers ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var kv in def.commandPrivilegeAllowPlayers)
    {
        if (!CmdCfg.commandPrivilegeAllowPlayers.ContainsKey(kv.Key)) { CmdCfg.commandPrivilegeAllowPlayers[kv.Key] = kv.Value ?? ""; writeConfig = true; }
    }

    if (writeConfig)
    {
        api.StoreModConfig(CmdCfg, ConfigFile);
    }
}
catch { /* ignore */ }


// Load language strings from assets (assets/axinclaimsrules/lang/<code>.json)
            LangManager.Load(api, CmdCfg?.language ?? "en");

            // P2 — Public API for expansion mods (plugins). Read-only surface; CORE owns persistence.
            try
            {
                AxinClaimsRulesApi.Initialize(api, CmdCfg, GlobalCfg, OverridesCfg, RegistryCfg, AliasCfg);
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[AxinClaimsRules] AxinClaimsRulesApi.Initialize failed: {0}", ex);
            }



            api.Logger.Notification("[AxinClaimsRules] Loaded");
            api.Logger.Notification("[AxinClaimsRules] defaults.fireSpread.enabled={0}", GlobalCfg?.defaults?.fireSpread?.enabled);

            

            // Register chat commands (server-side)
            try
            {
                api.Logger.Notification("[AxinClaimsRules] Registering chat commands...");
                CommandRegistration.RegisterAll(api);
            }
            catch (Exception e)
            {
                api.Logger.Warning("[AxinClaimsRules] Failed to register chat commands: {0}", e);
            }
var harmony = new Harmony("axin.axinclaimsrules");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // ClaimFlight runtime (server): enforces flight inside claim based on claimRules.claimFlight
            try
            {
                // (moved to plugin) ClaimFlight.StartServer(api);
            }
            catch (Exception ex) { api.Logger.Warning("[AxinClaimsRules] ClaimFlight.StartServer failed: {0}", ex); }

#if DEBUG
            // E7.3b — Harness de regresión no destructivo (solo DEBUG)
            RegistryRegressionHarness.Run(api, RegistryMigrationSvc, RegistrySyncSvc);
#endif
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            // ClaimFlight runtime (client): applies FreeMove and fall-safety on packets from server
            try
            {
                // (moved to plugin) ClaimFlight.StartClient(api);
            }
            catch { /* ignore */ }
        }

        private static T LoadOrCreate<T>(ICoreServerAPI api, string file, Func<T> factory) where T : class
        {
            try
            {
                var cfg = api.LoadModConfig<T>(file);
                if (cfg != null) return cfg;
            }
            catch (Exception e)
            {
                api.Logger.Warning("[AxinClaimsRules] Failed to load {0}: {1}", file, e.Message);
            }

            var created = factory();
            try
            {
                api.StoreModConfig(created, file);
                api.Logger.Notification("[AxinClaimsRules] Created default {0} in ModConfig", file);
            }
            catch (Exception e)
            {
                api.Logger.Error("[AxinClaimsRules] Failed to write default {0}: {1}", file, e);
            }

            return created;
        }

        private static T LoadIfExists<T>(ICoreServerAPI api, string file) where T : class
        {
            try
            {
                return api.LoadModConfig<T>(file);
            }
            catch (Exception e)
            {
                api.Logger.Warning("[AxinClaimsRules] Failed to load {0}: {1}", file, e.Message);
                return null;
            }
        }

    // =========================
    // CLAIMS REGISTRY (v1)
    // =========================

        private static string TryReadLanguageFromConfigRaw()
        {
            return ConfigLanguageReader.TryReadLanguageFromConfigRaw(ConfigFile);
        }


internal static bool PurgeTraderClaimsFromRegistry(ClaimsRegistry reg)
{
    if (reg?.players == null || reg.players.Count == 0) return false;

    bool changed = false;

    // Heuristic: trader claims are typically stored under playerUid "unknown" with lastKnownName "Trader"
    // and/or in a folder named "TraderClaims".
    foreach (var kv in reg.players.ToList())
    {
        string playerUid = kv.Key ?? "";
        var pe = kv.Value;
        if (pe == null) continue;

        string lastName = (pe.lastKnownName ?? "").Trim();
        bool looksTraderOwner = lastName.Equals("Trader", StringComparison.OrdinalIgnoreCase);

        bool hasTraderFolder =
            (pe.foldersOrder != null && pe.foldersOrder.Any(f => (f ?? "").Trim().Equals("TraderClaims", StringComparison.OrdinalIgnoreCase))) ||
            (pe.aliases != null && pe.aliases.Keys.Any(k =>
            {
                var kk = (k ?? "").Trim();
                return kk.Equals("TraderClaims", StringComparison.OrdinalIgnoreCase) ||
                       kk.StartsWith("TraderClaims/", StringComparison.OrdinalIgnoreCase);
            }));

        if (!looksTraderOwner && !hasTraderFolder) continue;

        // Collect trader claim IDs (axin:...) via aliases and claim info.
        var traderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (pe.aliases != null)
        {
            foreach (var a in pe.aliases)
            {
                var key = (a.Key ?? "").Trim();
                if (key.Equals("TraderClaims", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("TraderClaims/", StringComparison.OrdinalIgnoreCase))
                {
                    var val = (a.Value ?? "").Trim();
                    if (val.StartsWith("axin:", StringComparison.OrdinalIgnoreCase))
                        traderIds.Add(val);
                }
            }
        }

        if (pe.claims != null)
        {
            foreach (var c in pe.claims)
            {
                var ce = c.Value;
                var owner = (ce?.info?.lastKnownOwnerName ?? "").Trim();
                if (owner.Equals("Trader", StringComparison.OrdinalIgnoreCase))
                    traderIds.Add(c.Key);
            }
        }

        if (traderIds.Count == 0)
        {
            // If the entire player entry is clearly trader-owned, drop it.
            if (looksTraderOwner && playerUid.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                reg.players.Remove(kv.Key);
                changed = true;
            }
            continue;
        }

        // Remove claims
        if (pe.claims != null)
        {
            foreach (var id in traderIds) changed |= pe.claims.Remove(id);
        }

        // Remove aliases pointing to trader claims, and folder marker itself
        if (pe.aliases != null)
        {
            foreach (var a in pe.aliases.ToList())
            {
                var key = (a.Key ?? "").Trim();
                var val = (a.Value ?? "").Trim();

                if (key.Equals("TraderClaims", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("TraderClaims/", StringComparison.OrdinalIgnoreCase) ||
                    (val.StartsWith("axin:", StringComparison.OrdinalIgnoreCase) && traderIds.Contains(val)))
                {
                    pe.aliases.Remove(a.Key);
                    changed = true;
                }
            }
        }

        
        // Remove global (schema v2) aliases/folders for trader claims
        try
        {
            // Known trader folders
            string[] traderFolders = new[] { "TraderClaims", "folderTrader" };

            if (reg.aliases != null && reg.aliases.Count > 0)
            {
                foreach (var a in reg.aliases.ToList())
                {
                    var akey = (a.Key ?? "").Trim();
                    var aval = (a.Value ?? "").Trim();

                    bool keyInTraderFolder = traderFolders.Any(f =>
                        akey.Equals(f, StringComparison.OrdinalIgnoreCase) ||
                        akey.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase));

                    if (keyInTraderFolder || (aval.StartsWith("axin:", StringComparison.OrdinalIgnoreCase) && traderIds.Contains(aval)))
                    {
                        reg.aliases.Remove(a.Key);
                        changed = true;
                    }
                }
            }

            if (reg.folderOrders != null && reg.folderOrders.Count > 0)
            {
                foreach (var f in traderFolders)
                {
                    if (reg.folderOrders.Remove(f)) changed = true;
                }

                // Also remove any folderOrders entries that point to trader alias keys
                foreach (var key in reg.folderOrders.Keys.ToList())
                {
                    var list = reg.folderOrders[key];
                    int before = list?.Count ?? 0;
                    if (before == 0) continue;

                    var filtered = list
                        .Where(k =>
                        {
                            var kk = (k ?? "").Trim();
                            bool keyInTraderFolder = traderFolders.Any(f => kk.Equals(f, StringComparison.OrdinalIgnoreCase) || kk.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase));
                            if (keyInTraderFolder) return false;
                            if (reg.aliases != null && reg.aliases.TryGetValue(kk, out var cid))
                            {
                                cid = (cid ?? "").Trim();
                                if (cid.StartsWith("axin:", StringComparison.OrdinalIgnoreCase) && traderIds.Contains(cid)) return false;
                            }
                            return true;
                        })
                        .ToList();

                    if ((filtered?.Count ?? 0) != before)
                    {
                        reg.folderOrders[key] = filtered;
                        changed = true;
                    }
                }
            }

            if (reg.foldersOrder != null && reg.foldersOrder.Count > 0)
            {
                int before = reg.foldersOrder.Count;
                reg.foldersOrder = reg.foldersOrder
                    .Where(f => !traderFolders.Any(tf => (f ?? "").Trim().Equals(tf, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (reg.foldersOrder.Count != before) changed = true;
            }

            if (reg.outsideOrder != null && reg.outsideOrder.Count > 0)
            {
                int before = reg.outsideOrder.Count;
                reg.outsideOrder = reg.outsideOrder
                    .Where(k =>
                    {
                        var kk = (k ?? "").Trim();
                        bool keyInTraderFolder = traderFolders.Any(f => kk.Equals(f, StringComparison.OrdinalIgnoreCase) || kk.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase));
                        if (keyInTraderFolder) return false;
                        if (reg.aliases != null && reg.aliases.TryGetValue(kk, out var cid))
                        {
                            cid = (cid ?? "").Trim();
                            if (cid.StartsWith("axin:", StringComparison.OrdinalIgnoreCase) && traderIds.Contains(cid)) return false;
                        }
                        return true;
                    })
                    .ToList();
                if (reg.outsideOrder.Count != before) changed = true;
            }
        }
        catch { /* ignore */ }
// Remove folder order entry
        if (pe.foldersOrder != null)
        {
            int before = pe.foldersOrder.Count;
            pe.foldersOrder = pe.foldersOrder
                .Where(f => !(f ?? "").Trim().Equals("TraderClaims", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (pe.foldersOrder.Count != before) changed = true;
        }

        // Clean aliasOrder (legacy) if it contains trader paths
        if (pe.aliasOrder != null && pe.aliasOrder.Count > 0)
        {
            int before = pe.aliasOrder.Count;
            pe.aliasOrder = pe.aliasOrder
                .Where(a => !((a ?? "").Trim().Equals("TraderClaims", StringComparison.OrdinalIgnoreCase) ||
                               (a ?? "").Trim().StartsWith("TraderClaims/", StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (pe.aliasOrder.Count != before) changed = true;
        }

        // If player entry is now empty-ish, remove it
        bool emptyClaims = pe.claims == null || pe.claims.Count == 0;
        bool emptyAliases = pe.aliases == null || pe.aliases.Count == 0;
        bool emptyFolders = pe.foldersOrder == null || pe.foldersOrder.Count == 0;

        if (emptyClaims && emptyAliases && emptyFolders && playerUid.Equals("unknown", StringComparison.OrdinalIgnoreCase) && looksTraderOwner)
        {
            reg.players.Remove(kv.Key);
            changed = true;
        }
    }

    if (changed)
    {
        reg.updatedAtUtc = DateTime.UtcNow.ToString("o");
    }

    return changed;
}
        

internal static bool PurgeGhostCustomMessageClaimsFromRegistry(ClaimsRegistry reg)
{
    if (reg == null || reg.players == null || reg.players.Count == 0) return false;

    bool changed = false;
    int purgedClaims = 0;

    // Collect ghost claim IDs while removing claims from per-owner maps.
    var ghostClaimIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var pk in reg.players.ToList())
    {
        var playerUid = pk.Key ?? "";
        var pe = pk.Value;
        if (pe == null || pe.claims == null || pe.claims.Count == 0) continue;

        foreach (var ck in pe.claims.ToList())
        {
            var claimId = ck.Key ?? "";
            var ce = ck.Value;
            var info = ce?.info;

            if (info == null) continue;

            if (global::AxinClaimsRules.ClaimOwnerSignature.IsGhostCustomMessageClaim(info.ownerPlayerUid, info.ownerGroupUid, info.lastKnownOwnerName))
            {
                pe.claims.Remove(claimId);
                ghostClaimIds.Add(claimId);
                purgedClaims++;
                changed = true;
            }
        }
    }

    if (!changed || ghostClaimIds.Count == 0) return false;

    // Purge aliases pointing to ghost claim IDs
    var aliasKeysRemoved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    if (reg.aliases != null && reg.aliases.Count > 0)
    {
        foreach (var ak in reg.aliases.ToList())
        {
            var aliasKey = (ak.Key ?? "").Trim();
            var val = (ak.Value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(aliasKey)) continue;

            if (ghostClaimIds.Contains(val))
            {
                reg.aliases.Remove(ak.Key);
                aliasKeysRemoved.Add(aliasKey);
                changed = true;
            }
        }
    }

    // Purge order references (outside + per-folder)
    if (aliasKeysRemoved.Count > 0)
    {
        if (reg.outsideOrder != null && reg.outsideOrder.Count > 0)
        {
            int before = reg.outsideOrder.Count;
            reg.outsideOrder = reg.outsideOrder.Where(k => k != null && !aliasKeysRemoved.Contains(k.Trim())).ToList();
            if (reg.outsideOrder.Count != before) changed = true;
        }

        if (reg.folderOrders != null && reg.folderOrders.Count > 0)
        {
            foreach (var fk in reg.folderOrders.Keys.ToList())
            {
                var list = reg.folderOrders[fk];
                if (list == null || list.Count == 0) continue;

                int before = list.Count;
                var filtered = list.Where(k => k != null && !aliasKeysRemoved.Contains(k.Trim())).ToList();
                if (filtered.Count != before)
                {
                    reg.folderOrders[fk] = filtered;
                    changed = true;
                }
            }
        }
    }
    return changed;
}

internal static void ReloadAllFromDisk(ICoreServerAPI api)
        {
            ReloadService.ReloadAllFromDisk(api);
        }

}
}