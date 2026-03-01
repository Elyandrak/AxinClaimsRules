using Vintagestory.API.Server;
using AxinClaimsRules.Data.Registry;

namespace AxinClaimsRules.Data.Registry.Sync
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.4d): read-only rule accessors used by patches/services.
    /// </summary>
    internal static class RulesEnsureService
    {
                public static bool TryGetRule_FireSpread(string axinClaimId, out bool allow)
                {
                    allow = true;
                    var reg = AxinClaimsRulesMod.RegistryCfg;
                    if (reg?.players == null) return false;

                    foreach (var p in reg.players.Values)
                    {
                        if (p?.claims == null) continue;
                        if (!p.claims.TryGetValue(axinClaimId, out var ce)) continue;
                        if (ce?.claimRules?.fireSpread == null) return false; // existe claim pero sin regla
                        allow = ce.claimRules.fireSpread.enabled;
                        return true;
                    }

                    return false;
                }
    }
}
