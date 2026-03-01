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
using Newtonsoft.Json;

namespace AxinClaimsRules
{
    public class CommandConfig
    {
        public string _description { get; set; } =
            "AxinClaimsRules - Config principal. Incluye idioma (language) y permisos por comando (commandPrivileges). Edita este archivo; el mod lo genera si no existe.";
        public int schemaVersion { get; set; } = 5;

        // UI language for this mod. Allowed: en, es, fr, pt, de, ru, uk, zh, ja
        public string language { get; set; } = "en";


        // Include trader (NPC) claims when running "/ac claims export".
        // Default: false (trader claims are ignored and purged on /ac reload).
        public bool exportTraderClaims { get; set; } = false;

        // Ignore + purge ghost claims created by CustomMessage (server history bloat).
        // Signature: ownerPlayerUid=="" AND ownerGroupUid=="0" AND lastKnownOwnerName starts with "custommessage-".
        // When enabled:
        // - /ac claims export: skip those world claims (never exported)
        // - /ac reload and /ac claims export: purge them from ClaimsRegistry.json (claims + aliases + orders)
        // Default: true.
        public bool purgeGhostCustomMessageClaims { get; set; } = true;

        // UI: color for Up/Down buttons in chat (default: #66aaff)
        public string uiColorMoveButtons { get; set; } = "#66aaff";

        // UI: color for Folder-related buttons in chat (default: medium brown)
        public string uiColorFolderButtons { get; set; } = "#b07a3a";


        // DEBUG: log every /ac flag invocation (tokens, parsing and result) to server-debug.log.
        // Default: false (no overhead when disabled).
        public bool debugFlagCommands { get; set; } = false;

        // DEBUG: run a no-op harness after registry reload to detect non-idempotent migrations/normalization.
        // Default: false (zero overhead when disabled).
        public bool debugRegistryNoOpHarness { get; set; } = false;

        // DEBUG: log FireSpread patch sampling to server-debug.log.
        // Default: false (prevents log spam). If enabled, logs are rate-limited.
        public bool debugFireSpreadLog { get; set; } = false;

        // ClaimFlight: seconds before flight is disabled after leaving an eligible claim.
        // Default: 5. Set to 0 to disable immediately.
        public int claimFlightLeaveDelaySeconds { get; set; } = 5;

        // Privilegios por subcomando canónico:
        // - "chat" permite a jugadores normales
        // - "controlserver" normalmente solo admins
        // Si pones "" (vacío), se permitirá a cualquiera que pueda usar el chat command base.
        public Dictionary<string, string> commandPrivileges { get; set; } = new Dictionary<string, string>()
        {
            { "id", Privilege.chat },
            { "list", Privilege.chat },
            { "folder", Privilege.chat },
            { "claim", Privilege.chat },
            { "flags", Privilege.chat },
            { "flag", Privilege.controlserver },
            { "claims", Privilege.controlserver },
            { "player", Privilege.chat },

            { "tp", Privilege.controlserver },
            { "settp", Privilege.controlserver },
            { "reload", Privilege.controlserver },
            { "sync", Privilege.controlserver }
        };

        // OPTIONAL: per-command allowlist for non-admin players.
        // Only applies when commandPrivileges[cmd] == "controlserver".
        // Values can be PlayerUIDs or player names (case-insensitive).
         [Newtonsoft.Json.JsonConverter(typeof(StringAllowlistDictConverter))]
        public Dictionary<string, string> commandPrivilegeAllowPlayers { get; set; } = new Dictionary<string, string>();

        
        // JSON converter: allows values to be either a string ("p1/p2") or a JSON array (["p1","p2"]).
        // It normalizes everything into a single string joined by "/".
        public class StringAllowlistDictConverter : JsonConverter<Dictionary<string, string>>
        {
            public override Dictionary<string, string> ReadJson(JsonReader reader, Type objectType, Dictionary<string, string> existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (reader.TokenType == JsonToken.Null) return dict;

                if (reader.TokenType != JsonToken.StartObject)
                {
                    // Unexpected; try deserialize normally
                    try { return serializer.Deserialize<Dictionary<string, string>>(reader) ?? dict; }
                    catch { return dict; }
                }

                var jo = Newtonsoft.Json.Linq.JObject.Load(reader);
                foreach (var prop in jo.Properties())
                {
                    var v = prop.Value;
                    if (v == null || v.Type == Newtonsoft.Json.Linq.JTokenType.Null)
                    {
                        dict[prop.Name] = "";
                        continue;
                    }

                    if (v.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                    {
                        var arr = v.Values<string>().Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
                        dict[prop.Name] = string.Join("/", arr);
                        continue;
                    }

                    dict[prop.Name] = (v.ToString() ?? "").Trim();
                }

                return dict;
            }

            public override void WriteJson(JsonWriter writer, Dictionary<string, string> value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value);
            }
        }

public static CommandConfig CreateDefault()
        {
            var cfg = new CommandConfig();

            // Build a fully-populated allowlist object with the same keys as commandPrivileges,
            // plus per-flag keys: "flag" + <flagKey> (e.g. "flagfireSpread.enabled").
            cfg.commandPrivilegeAllowPlayers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var k in cfg.commandPrivileges.Keys)
            {
                if (!cfg.commandPrivilegeAllowPlayers.ContainsKey(k))
                    cfg.commandPrivilegeAllowPlayers[k] = "";
            }

            // Known flags accepted by /ac flag (must match CmdFlagSet).
            var knownFlags = new string[]
            {
                "fireSpread.enabled",
                "fireIgnition.enabled",
                "allowTorches",
                "allowFirepit",
                "allowCharcoalPit",
                "allowFirestarterOnBlocks"
            };

            foreach (var f in knownFlags)
            {
                var key = "flag" + f;
                if (!cfg.commandPrivilegeAllowPlayers.ContainsKey(key))
                    cfg.commandPrivilegeAllowPlayers[key] = "";
            }

            // Example as requested
            if (cfg.commandPrivilegeAllowPlayers.ContainsKey("sync"))
                cfg.commandPrivilegeAllowPlayers["sync"] = "player1/player2";

            return cfg;
        }
        public string GetMoveColorOrDefault()
        {
            return NormalizeHexColor(uiColorMoveButtons, "#66aaff");
        }

        public string GetFolderColorOrDefault()
        {
            return NormalizeHexColor(uiColorFolderButtons, "#b07a3a");
        }

        public static string NormalizeHexColor(string value, string fallback)
        {
            string v = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(v)) return fallback;
            if (!v.StartsWith("#")) v = "#" + v;
            if (v.Length != 7) return fallback;
            for (int i = 1; i < 7; i++)
            {
                char c = v[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return fallback;
            }
            return v;
        }
}



    public class GlobalConfig
    {
        public string _description { get; set; } =
            "AxinClaimsRules - Configuración GLOBAL (defaults). Se aplica a TODOS los claims salvo override.";

        public string _note { get; set; } =
            "ignición ≠ propagación. No tocar .vcdbs. GlobalConfig define defaults; ClaimsOverrides define deltas por claim.";

        public string version { get; set; } = "0.3.0";

        public Defaults defaults { get; set; } = new Defaults();

        public static GlobalConfig CreateDefault()
        {
            return new GlobalConfig
            {
                defaults = new Defaults
                {
                    fireSpread = new ToggleRule
                    {
                        _description = "Por defecto en claims: ¿puede propagarse el fuego?",
                        enabled = false // ✅ tu regla por defecto
                    },
                    fireIgnition = new FireIgnitionRule
                    {
                        _description = "Ignición (antorchas/hoguera/etc.). Por defecto permitido.",
                        enabled = true,
                        allowTorches = true,
                        allowFirepit = true,
                        allowCharcoalPit = true,
                        allowFirestarterOnBlocks = true
                    }
                }
            };
        }
    }

}