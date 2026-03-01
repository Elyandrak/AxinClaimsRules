using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using AxinClaimsRules.Data.Registry;

namespace AxinClaimsRules.Data.Registry.Sync
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.4d): TP ensure helper (write-once unless overwrite=true).
    /// </summary>
    internal static class TpEnsureService
    {
                public static void EnsureTp(ICoreServerAPI api, string ownerPlayerUid, string axinClaimId, BlockPos pos, string setByUid, bool overwrite)
                {
                    if (api == null) return;
                    var reg = AxinClaimsRulesMod.RegistryCfg;
                    if (reg?.players == null) return;
                    if (string.IsNullOrWhiteSpace(ownerPlayerUid)) ownerPlayerUid = "unknown";
                    if (string.IsNullOrWhiteSpace(axinClaimId)) return;

                    if (!reg.players.TryGetValue(ownerPlayerUid, out var player)) return;
                    if (player?.claims == null) return;
                    if (!player.claims.TryGetValue(axinClaimId, out var ce) || ce == null) return;

                    if (ce.tp == null) ce.tp = new TpInfo();

                    if (!overwrite)
                    {
                        // si ya existe TP, no tocar
                        if (!(ce.tp.x == 0 && ce.tp.y == 0 && ce.tp.z == 0) && !string.IsNullOrWhiteSpace(ce.tp.setAtUtc))
                            return;
                    }

                    ce.tp.x = pos.X;
                    ce.tp.y = pos.Y;
                    ce.tp.z = pos.Z;
                    ce.tp.setAtUtc = DateTime.UtcNow.ToString("o");
                    ce.tp.setByPlayerUid = setByUid ?? "";

                    reg.updatedAtUtc = DateTime.UtcNow.ToString("o");
                    RegistryStore.SaveClaimsRegistry(api, reg);
                }
    }
}
