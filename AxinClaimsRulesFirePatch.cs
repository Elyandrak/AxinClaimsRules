using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace AxinClaimsRules
{
    [HarmonyPatch]
    public static class FireSpreadEnforcePatch
    {
        static MethodBase TargetMethod()
        {
            // VS 1.20.x+: BEBehaviorBurning.TrySpreadTo(...)
            var t = AccessTools.TypeByName("Vintagestory.GameContent.BEBehaviorBurning");
            if (t == null) return null;

            var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                           .Where(m => m.Name.Equals("TrySpreadTo", StringComparison.OrdinalIgnoreCase))
                           .ToList();

            // Prefer overload where arg0 is BlockPos
            var prefer = methods.FirstOrDefault(m =>
            {
                var ps = m.GetParameters();
                return ps.Length >= 1 && ps[0].ParameterType == typeof(BlockPos);
            });

            return prefer ?? methods.FirstOrDefault();
        }

        // Debug sampling is rate-limited and optional (see debugFireSpreadLog).
        private static readonly object LogLock = new object();
        private static long lastLogMs = 0;
        private static int callsSinceLast = 0;
        private static int blockedSinceLast = 0;
        private static string lastSample = "";

        static bool Prefix(MethodBase __originalMethod, object __instance, object[] __args, ref object __result)
        {
            var api = AxinClaimsRulesMod.Sapi;
            if (api == null) return true;

            BlockPos target = null;
            if (__args != null && __args.Length >= 1 && __args[0] is BlockPos bp) target = bp;
            if (target == null) return true;

            bool inClaim = ClaimResolver.TryGetClaimAt(api, target, out _, out string claimId, out string claimStatus);
            bool allow = inClaim ? RulesEngine.EffectiveAllow_FireSpread(claimId) : true;

            // Optional debug sampling (disabled by default to prevent log spam)
            // Enable via GlobalConfig.json: debugFireSpreadLog=true
            if (AxinClaimsRulesMod.CmdCfg?.debugFireSpreadLog == true)
            {
                try
                {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                lock (LogLock)
                {
                    callsSinceLast++;
                    if (inClaim && !allow) blockedSinceLast++;

                    lastSample = "target=" + target.X + "," + target.Y + "," + target.Z +
                                 " inClaim=" + inClaim +
                                 " claimId=" + (claimId ?? "-") +
                                 " allow=" + allow +
                                 " claimsAPI=" + (claimStatus ?? "-");

                    if (now - lastLogMs >= 15000)
                    {
                        api.Logger.VerboseDebug(
                            "[AxinClaimsRules][FireSpread] rate=15s calls={0} blocked={1} sample: {2}",
                            callsSinceLast, blockedSinceLast, lastSample
                        );

                        lastLogMs = now;
                        callsSinceLast = 0;
                        blockedSinceLast = 0;
                        lastSample = "";
                    }
                }
            }
                catch { /* never break gameplay */ }
            }

            // ENFORCE: if in claim and not allowed -> block spread
            if (inClaim && !allow)
            {
                if (__originalMethod is MethodInfo mi && mi.ReturnType == typeof(bool))
                {
                    __result = false;
                }

                return false;
            }

            return true;
        }
    }
}
