using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace AxinClaimsRules.Data.Storage
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint E4):
    /// IO + migración del ClaimsRegistry (RegistryFile) extraídos de ReloadService.
    /// </summary>
    internal static class ClaimsRegistryStorage
    {
        internal static ClaimsRegistry LoadOrCreateAndMigrate(ICoreServerAPI api, string registryFile, out bool wrote)
        {
            wrote = false;

            bool writeRegistry = false;
            var reg = api.LoadModConfig<ClaimsRegistry>(registryFile);
            if (reg == null)
            {
                reg = ClaimsRegistry.CreateDefault();
                writeRegistry = true;
            }
            else
            {
                var defReg = ClaimsRegistry.CreateDefault();

                // v1 -> v2 migration (aliases/folders become root/global)
                if (reg.schemaVersion < defReg.schemaVersion)
                {
                    reg.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    reg.foldersOrder ??= new List<string>();
                    reg.outsideOrder ??= new List<string>();
                    reg.folderOrders ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                    if (reg.players != null)
                    {
                        foreach (var pk in reg.players)
                        {
                            var pe = pk.Value;
                            if (pe == null) continue;

                            // Merge legacy per-player aliases into global root aliases.
                            // NOTE: PlayerClaimsEntry.aliases is [JsonIgnore] in v2, but it may still be deserialized from old files.
                            try
                            {
                                if (pe.aliases != null)
                                {
                                    foreach (var ak in pe.aliases)
                                    {
                                        var akey = (ak.Key ?? "").Trim();
                                        var aval = (ak.Value ?? "").Trim();
                                        if (akey.Length == 0 || aval.Length == 0) continue;
                                        if (!reg.aliases.ContainsKey(akey))
                                        {
                                            reg.aliases[akey] = aval;
                                        }
                                    }
                                }
                            }
                            catch { }

                            // Cleanup legacy to avoid duplication (not serialized anyway, but keep clean in-memory)
                            try { pe.aliases = new Dictionary<string, string>(); } catch { }
                        }
                    }

                    reg.players ??= new Dictionary<string, PlayerClaimsEntry>(StringComparer.OrdinalIgnoreCase);

                    // bump schema
                    reg.schemaVersion = defReg.schemaVersion;
                    writeRegistry = true;
                }
                else
                {
                    // Ensure not-null for v2
                    reg.players ??= new Dictionary<string, PlayerClaimsEntry>(StringComparer.OrdinalIgnoreCase);
                    reg.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    reg.foldersOrder ??= new List<string>();
                    reg.outsideOrder ??= new List<string>();
                    reg.folderOrders ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                }
            }

            if (writeRegistry)
            {
                try { api.StoreModConfig(reg, registryFile); wrote = true; } catch { }
            }

            return reg;
        }
    }
}
