using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace AxinClaimsRules
{
    internal static class TeleportUtil
    {
        public static bool TryTeleport(object entityObj, double x, double y, double z)
        {
            if (entityObj == null) return false;
            var t = entityObj.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // TeleportTo(double,double,double)
            var m = t.GetMethod("TeleportTo", flags, null, new[] { typeof(double), typeof(double), typeof(double) }, null);
            if (m != null)
            {
                m.Invoke(entityObj, new object[] { x, y, z });
                return true;
            }

            // TeleportTo(Vec3d)
            var vec3dType = t.Assembly.GetType("Vintagestory.API.MathTools.Vec3d");
            if (vec3dType != null)
            {
                var ctor = vec3dType.GetConstructor(new[] { typeof(double), typeof(double), typeof(double) });
                if (ctor != null)
                {
                    var v = ctor.Invoke(new object[] { x, y, z });
                    var m2 = t.GetMethod("TeleportTo", flags, null, new[] { vec3dType }, null);
                    if (m2 != null)
                    {
                        m2.Invoke(entityObj, new[] { v });
                        return true;
                    }
                }
            }

            return false;
        }
    }

    internal static class NameUtil
    {
        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var n = name.Trim();

            if (n.StartsWith("Player ", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(7);

            n = string.Join(" ", n.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            return n;
        }
    }

    public class FireIgnitionRule
    {
        public string _description { get; set; } = "";
        public bool enabled { get; set; } = true;

        public bool allowTorches { get; set; } = true;
        public bool allowFirepit { get; set; } = true;
        public bool allowCharcoalPit { get; set; } = true;
        public bool allowFirestarterOnBlocks { get; set; } = true;
    }


    public class ClaimFlightRule
    {
        public string _description { get; set; } = "";
        public bool enabled { get; set; } = false;

        // Allowed: all, build, whitelist
        public string mode { get; set; } = "all";

        // If empty => all players can fly (when mode=whitelist).
        // Entries may be player names or PlayerUIDs.
        public List<string> whitelist { get; set; } = new List<string>();
    }

}
