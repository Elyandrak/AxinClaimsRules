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
    internal static class LangManager
    {
        private static Dictionary<string, string> dict = new Dictionary<string, string>();
        public static string Current { get; private set; } = "en";

        public static void Load(ICoreAPI api, string languageCode)
        {
            string code = (languageCode ?? "en").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(code)) code = "en";

            // Try requested language, then fallback to en
            var loaded = TryLoadFromAssets(api, code) ?? (code != "en" ? TryLoadFromAssets(api, "en") : null);

            dict = loaded ?? new Dictionary<string, string>();
            Current = (loaded != null ? code : "en");

            try { api?.Logger?.Notification("[AxinClaimsRules] Lang loaded: {0}", Current); } catch { }
        }

        private static Dictionary<string, string> TryLoadFromAssets(ICoreAPI api, string code)
        {
            try
            {
                if (api?.Assets == null) return null;

                // assets/axinclaimsrules/lang/<code>.json  :  axinclaimsrules:lang/<code>.json
                var loc = new AssetLocation("axinclaimsrules", $"lang/{code}.json");
                var asset = api.Assets.Get(loc);
                if (asset == null) return null;

                string json = asset.ToText();
                if (string.IsNullOrWhiteSpace(json)) return null;

                // Use the game JsonUtil if available? To stay compatible we parse minimal JSON ourselves via System.Text.Json.
                var opts = new System.Text.Json.JsonSerializerOptions
                {
                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var d = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json, opts);
                return d;
            }
            catch { return null; }
        }

        public static string T(string key, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(key) && dict != null && dict.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                return v;

            return fallback ?? key ?? "";
        }

        public static string Tf(string key, string fallback, params object[] args)
        {
            string fmt = T(key, fallback);
            if (args == null || args.Length == 0) return fmt;
            try { return string.Format(fmt, args); } catch { return fmt; }
        }
    }

}
