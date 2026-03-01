using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace AxinClaimsRules
{
    public class PlayerClaimsEntry
    {
        public string lastKnownName { get; set; } = "";        // Alias corto (ej. elYandrack1) -> axinClaimId
        [JsonIgnore]
        public Dictionary<string, string> aliases { get; set; } = new Dictionary<string, string>();

        // v2 registry format: aliases are stored only once at root (ClaimsRegistry.aliases).
        // We still allow deserialization for v1 -> v2 migration, but we NEVER serialize per-player aliases again.
        public bool ShouldSerializealiases() => false; // Newtonsoft fallback (case-insensitive)
        public bool ShouldSerializeAliases() => false;


        // LEGACY: aliasOrder ya no forma parte del formato de ClaimsRegistry.json.
        // Se mantiene solo por compatibilidad en memoria (nunca se serializa).
        [JsonIgnore]
        public List<string> aliasOrder { get; set; } = new List<string>();

        // Orden de carpetas (carpetas SOLO aquí).
        [JsonIgnore]
        public List<string> foldersOrder { get; set; } = new List<string>();

        // v2 registry format: foldersOrder is stored only once at root (ClaimsRegistry.foldersOrder).
        // We still allow deserialization for migration, but we NEVER serialize per-player foldersOrder again.
        public bool ShouldSerializefoldersOrder() => false;
        public bool ShouldSerializeFoldersOrder() => false;


        // axinClaimId -> claim entry
        public Dictionary<string, ClaimEntry> claims { get; set; } = new Dictionary<string, ClaimEntry>();
    }

    public class ClaimEntry
    {
        public ClaimInfo info { get; set; } = new ClaimInfo();
        public ClaimRules claimRules { get; set; } = null; // editable; se crea si falta

        // TP del claim: por defecto se fija en el primer /axinclaim id dentro del claim.
        // Se puede sobrescribir con /axinclaim settp (admin).
        public TpInfo tp { get; set; } = null;
    }

    public class ClaimInfo
    {
        public string ownerPlayerUid { get; set; } = "";
        public string ownerGroupUid { get; set; } = "";
        public string lastKnownOwnerName { get; set; } = "";
        public string lastSeenUtc { get; set; } = "";
        public List<CuboidInfo> areas { get; set; } = new List<CuboidInfo>();
        public BoundsInfo bounds { get; set; } = new BoundsInfo();

        // Centro aproximado (fallback para TP si no existe tp)
        public CenterInfo center { get; set; } = new CenterInfo();
    }

    public class CuboidInfo
    {
        public int x1 { get; set; }
        public int y1 { get; set; }
        public int z1 { get; set; }
        public int x2 { get; set; }
        public int y2 { get; set; }
        public int z2 { get; set; }
    }

    public class BoundsInfo
    {
        public int minX { get; set; }
        public int minY { get; set; }
        public int minZ { get; set; }
        public int maxX { get; set; }
        public int maxY { get; set; }
        public int maxZ { get; set; }
    }

    public class CenterInfo
    {
        public int x { get; set; }
        public int y { get; set; }
        public int z { get; set; }
    }

    public class TpInfo
    {
        public int x { get; set; }
        public int y { get; set; }
        public int z { get; set; }
        public string setAtUtc { get; set; } = "";
        public string setByPlayerUid { get; set; } = "";
    }

    public class ClaimRules
    {
        public ToggleRule fireSpread { get; set; } = null;
        public FireIgnitionRule fireIgnition { get; set; } = null;

        // ClaimFlight: allow creative-like flight only inside this claim.
        public ClaimFlightRule claimFlight { get; set; } = null;

        public static ClaimRules CreateDefault(GlobalConfig g)
        {
            return new ClaimRules
            {
                fireSpread = g?.defaults?.fireSpread ?? new ToggleRule { enabled = false },
                fireIgnition = g?.defaults?.fireIgnition ?? new FireIgnitionRule { enabled = true, allowTorches = true, allowFirepit = true, allowCharcoalPit = true, allowFirestarterOnBlocks = true },
                claimFlight = AxinClaimsRules.Core.Extensions.ExtensionsState.IsLoaded("axinclaimsrulesflight")
                    ? new ClaimFlightRule { enabled = false, mode = "all", whitelist = new List<string>() }
                    : null
            };
        }
    }

    internal static class CommandPrivileges
    {
        public static string PrivOr(CommandConfig cfg, string canonCmd, string fallback)
        {
            try
            {
                if (cfg?.commandPrivileges != null && cfg.commandPrivileges.TryGetValue(canonCmd, out var p) && !string.IsNullOrWhiteSpace(p))
                    return p;
            }
            catch { /* ignore */ }
            return fallback;
        }
    }

    internal static class CommandParsing
    {
        public static string[] GetTokens(TextCommandCallingArgs args)
        {
            if (args == null) return Array.Empty<string>();

            var t = args.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            object raw =
                   t.GetProperty("RawArgs", flags)?.GetValue(args)
                ?? t.GetProperty("Arguments", flags)?.GetValue(args)
                ?? t.GetField("RawArgs", flags)?.GetValue(args)
                ?? t.GetField("Arguments", flags)?.GetValue(args);

            // Common shapes across VS versions:
            // - string[]
            // - List<string>
            // - object[]  (parsed args from WithArgs parsers)
            // - List<object>
            if (raw is string[] sa) return sa.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            if (raw is List<string> sl) return sl.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            if (raw is object[] oa)
            {
                return oa.Where(o => o != null)
                         .Select(o => o.ToString())
                         .Where(s => !string.IsNullOrWhiteSpace(s))
                         .ToArray();
            }

            if (raw is System.Collections.IEnumerable ie && raw is not string)
            {
                var list = new List<string>();
                foreach (var o in ie)
                {
                    if (o == null) continue;
                    var s = o.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
                if (list.Count > 0) return list.ToArray();
            }

            object rawStr =
                   t.GetProperty("RawArgString", flags)?.GetValue(args)
                ?? t.GetProperty("RawArgsString", flags)?.GetValue(args)
                ?? t.GetField("RawArgString", flags)?.GetValue(args)
                ?? t.GetField("RawArgsString", flags)?.GetValue(args);

            if (rawStr is string s2 && !string.IsNullOrWhiteSpace(s2))
            {
                return s2.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }

            // Fallback: attempt to pull named args a/b/c (from our WithArgs OptionalWord parsers)
            try
            {
                var a = GetNamedArg(args, "a");
                var b = GetNamedArg(args, "b");
                var c = GetNamedArg(args, "c");
                var arr = new[] { a, b, c }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                if (arr.Length > 0) return arr;
            }
            catch { /* ignore */ }

            return Array.Empty<string>();
        }

        private static string GetNamedArg(TextCommandCallingArgs args, string name)
        {
            var t = args.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Some versions expose an indexer or a dictionary of named args.
            // We'll try a few common patterns via reflection.
            var dictObj =
                   t.GetProperty("Args", flags)?.GetValue(args)
                ?? t.GetProperty("NamedArgs", flags)?.GetValue(args)
                ?? t.GetField("Args", flags)?.GetValue(args)
                ?? t.GetField("NamedArgs", flags)?.GetValue(args);

            if (dictObj is System.Collections.IDictionary d && d.Contains(name))
            {
                var v = d[name];
                return v?.ToString();
            }

            // Indexer: this[string]
            var idx = t.GetProperties(flags).FirstOrDefault(p =>
                p.GetIndexParameters().Length == 1 &&
                p.GetIndexParameters()[0].ParameterType == typeof(string));

            if (idx != null)
            {
                try
                {
                    var v = idx.GetValue(args, new object[] { name });
                    return v?.ToString();
                }
                catch { }
            }


            // Direct property/field named exactly like the arg (some VS versions store parsed args this way)
            try
            {
                var p2 = t.GetProperty(name, flags);
                if (p2 != null)
                {
                    var v2 = p2.GetValue(args);
                    if (v2 != null) return v2.ToString();
                }

                var f2 = t.GetField(name, flags);
                if (f2 != null)
                {
                    var v3 = f2.GetValue(args);
                    if (v3 != null) return v3.ToString();
                }

                // Also try capitalized name (e.g. "A" instead of "a")
                if (name.Length == 1)
                {
                    var cap = name.ToUpperInvariant();
                    var p3 = t.GetProperty(cap, flags);
                    if (p3 != null)
                    {
                        var v4 = p3.GetValue(args);
                        if (v4 != null) return v4.ToString();
                    }
                    var f3 = t.GetField(cap, flags);
                    if (f3 != null)
                    {
                        var v5 = f3.GetValue(args);
                        if (v5 != null) return v5.ToString();
                    }
                }
            }
            catch { /* ignore */ }

            return null;
        }
    }

    public class Defaults
    {
        public ToggleRule fireSpread { get; set; } = new ToggleRule();
        public FireIgnitionRule fireIgnition { get; set; } = new FireIgnitionRule();
    }

    public class ToggleRule
    {
        public string _description { get; set; } = "";
        public bool enabled { get; set; } = true;
    }

    public class OverrideEntry
    {
        public string _description { get; set; } = "";
        public string claimId { get; set; } = "";

        public ToggleRule fireSpread { get; set; } = null;
        public FireIgnitionRule fireIgnition { get; set; } = null;
    }
}