using System;
using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Contracts;

namespace AxinClaimsRules.Debugging
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.3b) — Harness de regresión “no destructivo”
    ///
    /// Objetivo:
    /// - Detectar mutaciones inesperadas al aplicar Migration + Sync sobre modelos ya normalizados.
    /// - Evitar regresiones tipo "rehash/reorder" o guardado innecesario.
    ///
    /// Nota:
    /// - Se ejecuta SOLO en builds DEBUG.
    /// - No requiere framework de tests.
    /// - No toca disco: construye ejemplos in-memory.
    /// </summary>
    internal static class RegistryRegressionHarness
    {
        private static bool _ran;

        internal static void Run(ICoreServerAPI api, IRegistryMigration migration, IRegistrySync sync)
        {
            if (_ran) return;
            _ran = true;

            if (api == null || migration == null || sync == null) return;

            try
            {
                var (registry, aliasCfg) = BuildNormalizedExample();

                var beforeReg = SnapshotRegistry(registry);
                var beforeAlias = SnapshotAlias(aliasCfg);

                bool changedReg = migration.ApplyRegistryMigrations(api, registry);
                bool changedAlias = migration.ApplyAliasMigrations(api, aliasCfg);

                if (changedReg || changedAlias)
                {
                    api.Logger.Warning("[AxinClaimsRules][E7.3b] Harness: Migration reported changes on an already-normalized sample (reg={0}, alias={1}).", changedReg, changedAlias);
                }

                // Sync/Normalize should also be idempotent for already-normalized inputs.
                // IMPORTANT: our sample includes valid players/claims so NormalizeRegistry does NOT remove aliases.
                bool changedBySync = sync.NormalizeRegistry(api, registry, includeTraders: true);
                if (changedBySync)
                {
                    api.Logger.Warning("[AxinClaimsRules][E7.3b] Harness: Sync.NormalizeRegistry mutated an already-normalized sample.");
                }

                var afterReg = SnapshotRegistry(registry);
                var afterAlias = SnapshotAlias(aliasCfg);

                if (!string.Equals(beforeReg, afterReg, StringComparison.Ordinal))
                {
                    api.Logger.Error("[AxinClaimsRules][E7.3b] Harness FAIL: ClaimsRegistry changed after Migration+Sync. Potential non-destructive regression (reorder/rehash/mutation).\nBefore: {0}\nAfter: {1}", Trunc(beforeReg), Trunc(afterReg));
                }
                else
                {
                    api.Logger.Notification("[AxinClaimsRules][E7.3b] Harness OK: ClaimsRegistry remained identical after Migration+Sync (idempotent)." );
                }

                if (!string.Equals(beforeAlias, afterAlias, StringComparison.Ordinal))
                {
                    api.Logger.Error("[AxinClaimsRules][E7.3b] Harness FAIL: Alias config changed after Migration. Potential regression.\nBefore: {0}\nAfter: {1}", Trunc(beforeAlias), Trunc(afterAlias));
                }
                else
                {
                    api.Logger.Notification("[AxinClaimsRules][E7.3b] Harness OK: Alias config remained identical after Migration (idempotent)." );
                }
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[AxinClaimsRules][E7.3b] Harness exception (ignored): {0}", ex);
            }
        }

        private static (ClaimsRegistry reg, CommandAliasConfig alias) BuildNormalizedExample()
        {
            var reg = ClaimsRegistry.CreateDefault();

            // One claim, one alias, already consistent orders.
            const string playerUid = "player-uid-example";
            const string claimId = "axin:exampleclaimid";
            const string aliasKey = "Example/Player1";
            reg.players[playerUid] = new PlayerClaimsEntry
            {
                lastKnownName = "ExamplePlayer",
                claims = new System.Collections.Generic.Dictionary<string, ClaimEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    [claimId] = new ClaimEntry
                    {
                        info = new ClaimInfo { ownerPlayerUid = playerUid, lastKnownOwnerName = "ExamplePlayer" },
                        claimRules = new ClaimRules(),
                        tp = new TpInfo { x = 0, y = 100, z = 0 }
                    }
                }
            };

            reg.aliases[aliasKey] = claimId;
            reg.foldersOrder.Add("Example");
            reg.outsideOrder = new System.Collections.Generic.List<string>();
            reg.folderOrders["Example"] = new System.Collections.Generic.List<string> { aliasKey };

            var alias = CommandAliasConfig.CreateDefault();
            alias.schemaVersion = 2;

            // Ensure deterministic snapshots (updatedAtUtc shouldn't affect idempotency checks)
            reg.updatedAtUtc = "";

            return (reg, alias);
        }

        private static string SnapshotRegistry(ClaimsRegistry reg)
        {
            if (reg == null) return "null";

            // Ignore volatile timestamp fields for regression checks
            var saved = reg.updatedAtUtc;
            reg.updatedAtUtc = "";
            try
            {
                return JsonSerializer.Serialize(reg, new JsonSerializerOptions { WriteIndented = false });
            }
            finally
            {
                reg.updatedAtUtc = saved;
            }
        }

        private static string SnapshotAlias(CommandAliasConfig alias)
        {
            if (alias == null) return "null";
            return JsonSerializer.Serialize(alias, new JsonSerializerOptions { WriteIndented = false });
        }

        private static string Trunc(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            const int max = 400;
            return s.Length <= max ? s : (s.Substring(0, max) + "…");
        }
    }
}
