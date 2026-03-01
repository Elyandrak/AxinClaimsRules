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
    internal static class ClaimIdentity
    {
        // Devuelve un id estable basado en owner+áreas. No depende de IDs internos del engine.
        public static string ComputeAxinClaimId(object claimObj)
        {
            TryExtractOwnerAndAreas(claimObj, out string ownerPlayerUid, out string ownerGroupUid, out _, out var areas, out _);

            // si no tenemos owner ni áreas, fallback al tipo (debug)
            if ((ownerPlayerUid == null && ownerGroupUid == null) || areas == null || areas.Count == 0)
            {
                return claimObj?.GetType().Name ?? "UnknownClaim";
            }

            var sb = new StringBuilder();
            sb.Append(ownerPlayerUid ?? "");
            sb.Append("|");
            sb.Append(ownerGroupUid ?? "");
            sb.Append("|");

            // ordenar áreas para estabilidad
            foreach (var a in areas.OrderBy(a => a.x1).ThenBy(a => a.y1).ThenBy(a => a.z1).ThenBy(a => a.x2).ThenBy(a => a.y2).ThenBy(a => a.z2))
            {
                sb.Append(a.x1).Append(",").Append(a.y1).Append(",").Append(a.z1).Append(",")
                  .Append(a.x2).Append(",").Append(a.y2).Append(",").Append(a.z2).Append(";");
            }

            using var sha1 = SHA1.Create();
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hash = sha1.ComputeHash(bytes);

            // 12 bytes hex (24 chars) es suficiente y compacto
            var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return "axin:" + hex.Substring(0, 24);
        }

        public static void TryExtractOwnerAndAreas(object claimObj,
            out string ownerPlayerUid,
            out string ownerGroupUid,
            out string lastKnownOwnerName,
            out List<CuboidInfo> areasOut,
            out BoundsInfo boundsOut)
        {
            ownerPlayerUid = null;
            ownerGroupUid = null;
            lastKnownOwnerName = null;
            areasOut = new List<CuboidInfo>();
            boundsOut = new BoundsInfo();

            if (claimObj == null) return;

            // owner uids (props)
            ownerPlayerUid = GetStringProp(claimObj, "OwnedByPlayerUid") ?? GetStringProp(claimObj, "ownedByPlayerUid");
            ownerGroupUid = GetStringProp(claimObj, "OwnedByPlayerGroupUid") ?? GetStringProp(claimObj, "ownedByPlayerGroupUid");
            lastKnownOwnerName = GetStringProp(claimObj, "LastKnownOwnerName") ?? GetStringProp(claimObj, "lastKnownOwnerName");

            // areas
            var areasVal = GetProp(claimObj, "Areas") ?? GetProp(claimObj, "areas");
            if (areasVal is System.Collections.IEnumerable en)
            {
                foreach (var item in en)
                {
                    if (item == null) continue;

                    int x1 = GetIntProp(item, "X1", "x1");
                    int y1 = GetIntProp(item, "Y1", "y1");
                    int z1 = GetIntProp(item, "Z1", "z1");
                    int x2 = GetIntProp(item, "X2", "x2");
                    int y2 = GetIntProp(item, "Y2", "y2");
                    int z2 = GetIntProp(item, "Z2", "z2");

                    areasOut.Add(new CuboidInfo { x1 = x1, y1 = y1, z1 = z1, x2 = x2, y2 = y2, z2 = z2 });

                    // bounds
                    if (areasOut.Count == 1)
                    {
                        boundsOut.minX = Math.Min(x1, x2);
                        boundsOut.minY = Math.Min(y1, y2);
                        boundsOut.minZ = Math.Min(z1, z2);
                        boundsOut.maxX = Math.Max(x1, x2);
                        boundsOut.maxY = Math.Max(y1, y2);
                        boundsOut.maxZ = Math.Max(z1, z2);
                    }
                    else
                    {
                        boundsOut.minX = Math.Min(boundsOut.minX, Math.Min(x1, x2));
                        boundsOut.minY = Math.Min(boundsOut.minY, Math.Min(y1, y2));
                        boundsOut.minZ = Math.Min(boundsOut.minZ, Math.Min(z1, z2));
                        boundsOut.maxX = Math.Max(boundsOut.maxX, Math.Max(x1, x2));
                        boundsOut.maxY = Math.Max(boundsOut.maxY, Math.Max(y1, y2));
                        boundsOut.maxZ = Math.Max(boundsOut.maxZ, Math.Max(z1, z2));
                    }
                }
            }
        }

        static object GetProp(object obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name)) return null;
            var t = obj.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var p = t.GetProperty(name, flags);
            if (p != null) { try { return p.GetValue(obj); } catch { } }

            var f = t.GetField(name, flags);
            if (f != null) { try { return f.GetValue(obj); } catch { } }

            return null;
        }

        static string GetStringProp(object obj, string name)
        {
            try
            {
                var v = GetProp(obj, name);
                return v?.ToString();
            }
            catch { return null; }
        }

        static int GetIntProp(object obj, params string[] names)
        {
            foreach (var n in names)
            {
                try
                {
                    var v = GetProp(obj, n);
                    if (v == null) continue;
                    if (v is int i) return i;
                    if (int.TryParse(v.ToString(), out int parsed)) return parsed;
                }
                catch { }
            }
            return 0;
        }
    }

    // =========================
    // CONFIG MODELS (v0.3)
    // =========================

    public class ClaimsOverrides
    {
        public string _description { get; set; } = "Overrides por claim. Solo deltas respecto a defaults.";
        public string _note { get; set; } = "Sin claims anidados en vanilla. claimId debe ser estable.";
        public OverrideEntry[] overrides { get; set; } = Array.Empty<OverrideEntry>();

        public static ClaimsOverrides CreateDefault() => new ClaimsOverrides();
    }

    internal static class ClaimResolver
    {
        static PropertyInfo claimsProp;
        static MethodInfo getByPos;

        public static bool TryGetClaimAt(ICoreServerAPI api, BlockPos pos, out object claimObj, out string claimId, out string status)
        {
            claimObj = null;
            claimId = null;
            status = "unknown";

            try
            {
                var world = api.World;

                claimsProp ??= world.GetType().GetProperty("Claims", BindingFlags.Instance | BindingFlags.Public);
                if (claimsProp == null)
                {
                    status = "ClaimsPropNotFound";
                    return false;
                }

                var claimsApi = claimsProp.GetValue(world);
                if (claimsApi == null)
                {
                    status = "ClaimsObjNull";
                    return false;
                }

                getByPos ??= claimsApi.GetType().GetMethod("Get", new[] { typeof(BlockPos) });
                if (getByPos == null)
                {
                    status = "NoGet(BlockPos)";
                    return false;
                }

                var res = getByPos.Invoke(claimsApi, new object[] { pos });
                if (res == null)
                {
                    status = "OK:Get=null";
                    return false;
                }

                if (res is Array arr && arr.Length > 0)
                {
                    claimObj = arr.GetValue(0);
                }
                else
                {
                    status = "OK:Get=empty";
                    return false;
                }

                claimId = ClaimIdentity.ComputeAxinClaimId(claimObj);
                status = "OK:Get";
                return true;
            }
            catch (Exception e)
            {
                status = "Exception:" + e.GetType().Name;
                return false;
            }
        }

        private static string ExtractClaimId(object claimObj)
        {
            if (claimObj == null) return null;
            return ClaimIdentity.ComputeAxinClaimId(claimObj);
        }
}


    internal static class ClaimOwnerSignature
    {
        internal static bool IsUnownedOwner(string ownerPlayerUid, string ownerGroupUid)
        {
            return string.IsNullOrWhiteSpace(ownerPlayerUid)
                   && (string.IsNullOrWhiteSpace(ownerGroupUid) || (ownerGroupUid ?? "").Trim() == "0");
        }

        internal static bool IsTraderClaim(string ownerPlayerUid, string ownerGroupUid, string lastKnownOwnerName)
        {
            return IsUnownedOwner(ownerPlayerUid, ownerGroupUid)
                   && string.Equals((lastKnownOwnerName ?? "").Trim(), "Trader", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsGhostCustomMessageClaim(string ownerPlayerUid, string ownerGroupUid, string lastKnownOwnerName)
        {
            if (!IsUnownedOwner(ownerPlayerUid, ownerGroupUid)) return false;
            var n = (lastKnownOwnerName ?? "").Trim();
            return n.StartsWith("custommessage-", StringComparison.OrdinalIgnoreCase);
        }
    }

}
