using System;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Data.Registry.Migration;
using AxinClaimsRules.Data.Registry.Sync;

namespace AxinClaimsRules.Infra.Diagnostics
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.3b):
    /// No-op regression harness for Registry normalization/migration.
    ///
    /// Goal:
    /// - Detect accidental non-idempotent migrations/normalization (rehash/reorder/save loops).
    /// - Zero impact when disabled (guarded by config flag).
    ///
    /// Design:
    /// - Clone current in-memory registry and re-run migrations + normalization.
    /// - If the second pass reports changes, log a warning (regression signal).
    /// </summary>
    internal static class RegistryNoOpHarness
    {
        internal static void Run(ICoreServerAPI api, ClaimsRegistry registry, bool exportTraderClaims)
        {
            if (api == null || registry == null) return;

            try
            {
                // Deep clone via JSON (stable enough for a debug-only harness).
                var clone = DeepClone(registry);

                bool changed = false;

                // Re-run migrations + normalization on the clone.
                if (RegistryMigration.ApplyRegistryMigrations(api, clone)) changed = true;
                if (RegistrySync.NormalizeRegistry(api, clone, exportTraderClaims)) changed = true;

                if (changed)
                {
                    api.Logger.Warning(
                        "[AxinClaimsRules][E7.3b] Registry No-Op Harness: second pass reported CHANGES. " +
                        "This indicates non-idempotent migration/normalization or missing normalization before save.");
                }
            }
            catch (Exception e)
            {
                try { api.Logger.Warning("[AxinClaimsRules][E7.3b] Registry No-Op Harness failed: {0}", e.Message); } catch { }
            }
        }

        private static ClaimsRegistry DeepClone(ClaimsRegistry src)
        {
            var json = JsonConvert.SerializeObject(src);
            return JsonConvert.DeserializeObject<ClaimsRegistry>(json) ?? ClaimsRegistry.CreateDefault();
        }
    }
}
