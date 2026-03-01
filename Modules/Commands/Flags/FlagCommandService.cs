using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using AxinClaimsRules.Data.Registry;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using AxinClaimsRules.Infra;

namespace AxinClaimsRules.Features.Commands.Flags
{
    /// <summary>
    /// AXIN-IA-ARCH (E7.4b):
    /// Servicio extraído desde AxinClaimCommands.CmdFlags/CmdFlagSet/CmdFlagHelp para adelgazar CmdFlags legacy.
    /// Mantiene comportamiento (VTML, privilegios, EnsureCurrentClaim, Store/Reload).
    /// </summary>
    internal static class FlagCommandService
    {

        private static AxinClaimsRules.Contracts.ICommandRenderer R()
        {
            // Ultra-conservador: si por algún motivo no está inicializado aún, caemos a un renderer nuevo.
            return AxinClaimsRulesMod.RendererSvc ?? new AxinClaimsRules.Features.Commands.Rendering.ChatVtmlRenderer();
        }

        private static string StripOuterQuotes(string s)
        {
            if (s == null) return null;
            s = s.Trim();
            if (s.Length >= 2)
            {
                char a = s[0];
                char b = s[s.Length - 1];
                if ((a == '"' && b == '"') || (a == '\'' && b == '\''))
                {
                    return s.Substring(1, s.Length - 2);
                }
            }
            return s;
        }

        
        // /ac flag <aliasZona> <flag> <value>
        // value puede ser true/false (bool) o valores especiales (p.ej. claimFlight.mode).
        public static TextCommandResult CmdFlagSet(ICoreServerAPI api, IServerPlayer sp, string aliasZona, string flagKey, string boolText)
        {
            if (api == null || sp == null) return TextCommandResult.Error(LangManager.T("err.players.only", "Players only."));

            if (string.IsNullOrWhiteSpace(aliasZona)) return CmdFlagHelp(api, sp);
            if (string.IsNullOrWhiteSpace(flagKey)) return CmdFlagHelp(api, sp);
            if (string.IsNullOrWhiteSpace(boolText)) return CmdFlagHelp(api, sp);

            // Alias puede venir entrecomillado desde VTML (por carpetas con '/').
            aliasZona = StripOuterQuotes(aliasZona);
            string valueText = (boolText ?? "").Trim();

            if (AxinClaimsRulesMod.CmdCfg?.debugFlagCommands == true)
            {
                try
                {
                    api.Logger.Notification("[AxinClaimsRules][FLAGDBG] CmdFlagSet | player={0}/{1} | alias='{2}' | flag='{3}' | value='{4}'",
                        sp.PlayerName, sp.PlayerUID, aliasZona, flagKey, valueText);
                }
                catch { }
            }

            // Privileges: flag modifications are controlled by commandPrivileges["flag"] (default controlserver).
            // Optional per-flag allowlist key: "flag" + <flagKey> (e.g. "flagfireSpread.enabled").
            if (!PrivilegeChecks.RequireCmd(sp, AxinClaimsRulesMod.CmdCfg, "flag", "flag" + flagKey.Trim()))
            {
                return TextCommandResult.Error(LangManager.T("err.no.priv", "No tienes privilegios para usar este comando."));
            }


            // Reload registry from disk BEFORE write (non-destructive, AXIN-N 6.3.2)
            try
            {
                var disk = RegistryStore.TryLoadClaimsRegistry(api);
                if (disk != null) AxinClaimsRulesMod.RegistryCfg = disk;
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[AxinClaimsRules] Reload registry before set failed: {0}", ex);
            }

            // Resolve alias -> claim entry
            if (!RegistrySync.TryResolveAlias(aliasZona.Trim(), out string ownerUid, out string ownerName, out string axinClaimId, out ClaimEntry entry))
            {
                return TextCommandResult.Error($"Alias no encontrado: {aliasZona}");
            }

            // Ensure claimRules exists
            if (entry.claimRules == null)
            {
                entry.claimRules = ClaimRules.CreateDefault(AxinClaimsRulesMod.GlobalCfg);
            }

            // Normalize + apply flag
            string norm = NormalizeFlagKey(flagKey);
// 1) boolean flags
            bool value;
            if (!bool.TryParse(valueText, out value))
            {
                return TextCommandResult.Error(LangManager.T("err.flag.value", "Invalid value. Use true or false."));
            }

            if (!TryApplyFlag(entry.claimRules, norm, value, out string applied))
            {
                return TextCommandResult.Error("Flag inválida: " + flagKey);
            }

            // Persist
            try
            {
                AxinClaimsRulesMod.RegistryCfg.updatedAtUtc = DateTime.UtcNow.ToString("o");
                RegistryStore.SaveClaimsRegistry(api, AxinClaimsRulesMod.RegistryCfg);
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[AxinClaimsRules] Failed saving ClaimsRegistry.json after flag set: {0}", ex);
                return TextCommandResult.Error("No se pudo guardar ClaimsRegistry.json (ver server-debug.log).");
            }

            // Auto-reload to apply runtime changes
            try
            {
                AxinClaimsRulesMod.ReloadAllFromDisk(api);
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[AxinClaimsRules] Auto-reload after flag set failed: {0}", ex);
                // Still consider set OK (persisted) but warn user
                try { sp.SendMessage(0, LangManager.T("warn.flag.reload.failed", "[AxinClaimsRules] WARN: flag saved, but reload failed (see server-debug.log)."), EnumChatType.Notification); } catch { }
            }

            return TextCommandResult.Success(LangManager.Tf("flagset.ok", "OK: {0} {1}={2} (reload applied)", aliasZona, applied, value));
        }

        // Help/usage for /ac flag and /flag
        public static TextCommandResult CmdFlagHelp(ICoreServerAPI api, IServerPlayer sp)
        {
            return CmdFlagHelp(api, sp, 1);
        }

        // /ac flag [page]
        public static TextCommandResult CmdFlagHelp(ICoreServerAPI api, IServerPlayer sp, int page)
        {
            if (page < 1) page = 1;

            string root = (AxinClaimsRulesMod.AliasCfg?.rootAlias ?? "ac").Trim();
            if (string.IsNullOrWhiteSpace(root)) root = "ac";

            // Flags we currently accept in CmdFlagSet (must match TryApplyFlag)
            var known = new string[]
{
    "fireSpread.enabled",
    "fireIgnition.enabled",
    "allowTorches",
    "allowFirepit",
    "allowCharcoalPit",
    "allowFirestarterOnBlocks",
};

            // Player aliases (if any)
            string claimsLine;
            try
            {
                var reg = AxinClaimsRulesMod.RegistryCfg;
                if (reg?.players != null && sp != null && reg.players.TryGetValue(sp.PlayerUID, out var p) && p?.aliases != null && p.aliases.Count > 0)
                {
                    var keys = p.aliases.Keys.OrderBy(x => x).ToArray();
                    claimsLine = LangManager.T("flaghelp.youraliases", "Your claim aliases") + ": " + string.Join(", ", keys);
                }
                else
                {
                    claimsLine = LangManager.T("flaghelp.youraliases", "Your claim aliases") + ": " + LangManager.T("common.none", "(none)");
                }
            }
            catch
            {
                claimsLine = LangManager.T("flaghelp.youraliases", "Your claim aliases") + ": " + LangManager.T("common.error", "(error)");
            }

            string header = LangManager.T("flaghelp.header", "[AxinClaimsRules] Usage");
            string usage = LangManager.Tf(
                "flaghelp.usage",
                "/{0} flag (aliasZona) (flag) (true|false)",
                root
            );

            string flagsLabel = LangManager.T("flaghelp.available", "Available flags");

            // Build paginated lines (MAX 9 LINES)
            var lines = new List<string>
            {
                header,
                usage,
                flagsLabel + ":"
            };
            foreach (var k in known) lines.Add("- " + k);
            lines.Add(claimsLine);

            string baseCmd = "/" + root + " flag";

            // MAX 9 LINES: el chat se vuelve ilegible si imprimimos demasiado; la línea 10 se reserva para navegación.
            string pageText = CommandHelp.PaginateLines(lines, page, baseCmd, 9);
            return TextCommandResult.Success(pageText);
        }

        static string NormalizeFlagKey(string key)
        {
            key = (key ?? "").Trim();
            if (key.Length == 0) return "";
            key = key.Replace(" ", "");

            // IMPORTANT: do NOT strip "fireIgnition." for the canonical key "fireIgnition.enabled"
            // Otherwise it becomes "enabled" and will be rejected as invalid.
            if (string.Equals(key, "fireIgnition.enabled", StringComparison.OrdinalIgnoreCase))
            {
                return "fireIgnition.enabled";
            }

            // accept fireIgnition.allowTorches etc (strip prefix only for sub-keys)
            if (key.StartsWith("fireIgnition.", StringComparison.OrdinalIgnoreCase))
            {
                key = key.Substring("fireIgnition.".Length);
            }
            return key;
        }

        private static bool TryApplyFlag(ClaimRules rules, string key, bool value, out string applied)
        {
            applied = key;
            if (rules == null) return false;

            if (string.Equals(key, "fireSpread.enabled", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "fireSpread", StringComparison.OrdinalIgnoreCase))
            {
                rules.fireSpread ??= new ToggleRule { enabled = false };
                rules.fireSpread.enabled = value;
                applied = "fireSpread.enabled";
                return true;
            }

            if (string.Equals(key, "fireIgnition.enabled", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "fireIgnition", StringComparison.OrdinalIgnoreCase))
            {
                rules.fireIgnition ??= new FireIgnitionRule { enabled = true, allowTorches = true, allowFirepit = true, allowCharcoalPit = true, allowFirestarterOnBlocks = true };
                rules.fireIgnition.enabled = value;
                applied = "fireIgnition.enabled";
                return true;
            }

            rules.fireIgnition ??= new FireIgnitionRule { enabled = true, allowTorches = true, allowFirepit = true, allowCharcoalPit = true, allowFirestarterOnBlocks = true };

            if (string.Equals(key, "allowTorches", StringComparison.OrdinalIgnoreCase))
            {
                rules.fireIgnition.allowTorches = value; applied = "allowTorches"; return true;
            }
            if (string.Equals(key, "allowFirepit", StringComparison.OrdinalIgnoreCase))
            {
                rules.fireIgnition.allowFirepit = value; applied = "allowFirepit"; return true;
            }
            if (string.Equals(key, "allowCharcoalPit", StringComparison.OrdinalIgnoreCase))
            {
                rules.fireIgnition.allowCharcoalPit = value; applied = "allowCharcoalPit"; return true;
            }
            if (string.Equals(key, "allowFirestarterOnBlocks", StringComparison.OrdinalIgnoreCase))
            {
                rules.fireIgnition.allowFirestarterOnBlocks = value; applied = "allowFirestarterOnBlocks"; return true;
            }
            return false;
        }

        private static string FlagCmd(string root, string flagAlias, string aliasZona, string flagKey, string value)
        {
            // IMPORTANT: aliasZona puede incluir prefijos con '/', así que lo envolvemos en comillas.
            // Usamos comillas simples (') para minimizar escapes dentro de href="...".
            var az = (aliasZona ?? "").Replace("'", "\\'");
            return $"{root} {flagAlias} '{az}' {flagKey} {value}";
        }

        private static string ClickBool(ICoreServerAPI api, string aliasZona, string flagKey, bool value, bool isCurrent)
        {
            string root = (AxinClaimsRulesMod.AliasCfg?.rootAlias ?? "ac").Trim();
            if (string.IsNullOrWhiteSpace(root)) root = "ac";

            string flagAli = CommandAliases.AliasOr(AxinClaimsRulesMod.AliasCfg, "flag");
            if (string.IsNullOrWhiteSpace(flagAli)) flagAli = "flag";

            string v = value.ToString().ToLowerInvariant();
            if (isCurrent) return R().Strong(v);

            return UiTheme.LinkWithMarker("●", R().LinkText(v, FlagCmd(root, flagAli, aliasZona, flagKey, v)), value ? "lightgreen" : "lightcoral");
        }

        private static string ClickMode(ICoreServerAPI api, string aliasZona, string flagKey, string value, bool isCurrent)
        {
            string root = (AxinClaimsRulesMod.AliasCfg?.rootAlias ?? "ac").Trim();
            if (string.IsNullOrWhiteSpace(root)) root = "ac";

            string flagAli = CommandAliases.AliasOr(AxinClaimsRulesMod.AliasCfg, "flag");
            if (string.IsNullOrWhiteSpace(flagAli)) flagAli = "flag";

            var v = (value ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(v)) v = "all";
            if (isCurrent) return R().Strong(v);

            return UiTheme.LinkWithMarker("◈", R().LinkText(v, FlagCmd(root, flagAli, aliasZona, flagKey, v)));
        }

        public static TextCommandResult CmdFlags(ICoreServerAPI api, IServerPlayer sp, int page)
        {
            if (page < 1) page = 1;

            // AXIN: Any /ac command executed inside a claim must ensure registry (alias + flags) non-destructively.
            try { RegistrySync.EnsureCurrentClaim(api, sp); }
            catch (Exception ex)
            {
                api.Logger.Warning("[AxinClaimsRules] EnsureCurrentClaim failed (flags): {0}", ex.Message);
                try
                {
                    sp.SendMessage(0,
                        LangManager.T("err.claim.detect.flags", "[AxinClaimsRules] ERROR: could not detect current claim (flags)."),
                        EnumChatType.Notification);
                }
                catch { }
            }

            var pos = sp.Entity.Pos.AsBlockPos;

            bool inClaim = ClaimResolver.TryGetClaimAt(api, pos, out object claimObj, out string axinClaimId, out string status);
            if (!inClaim || claimObj == null || string.IsNullOrWhiteSpace(axinClaimId))
                return TextCommandResult.Success(LangManager.T("flags.notinclaim", "You are not inside any claim.") + " claimsAPI=" + status);

            // Asegura que existe entrada en registry (pero NO sobreescribe reglas si ya existen)
            RegistrySync.EnsureClaimEntry(api, claimObj, axinClaimId);

            ClaimIdentity.TryExtractOwnerAndAreas(claimObj, out string ownerUid, out _, out string ownerName, out _, out _);

            // reglas efectivas: si no existe en registry, usa defaults globales
            ClaimRules eff = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(ownerUid)
                    && AxinClaimsRulesMod.RegistryCfg?.players != null
                    && AxinClaimsRulesMod.RegistryCfg.players.TryGetValue(ownerUid, out var pEntry)
                    && pEntry?.claims != null
                    && pEntry.claims.TryGetValue(axinClaimId, out var cEntry)
                    && cEntry?.claimRules != null)
                {
                    eff = cEntry.claimRules;
                }
            }
            catch { /* ignore */ }

            eff ??= ClaimRules.CreateDefault(AxinClaimsRulesMod.GlobalCfg);

            var fs = eff.fireSpread ?? new ToggleRule { enabled = false };
            var fi = eff.fireIgnition ?? new FireIgnitionRule { enabled = true, allowTorches = true, allowFirepit = true, allowCharcoalPit = true, allowFirestarterOnBlocks = true };

            // Salida compacta en chat (sin "< >" ni markup raro)
            // Build clickable VTML output (links) for quick toggle.
            string aliasZona = "(noalias)";
            try
            {
                if (!string.IsNullOrWhiteSpace(ownerUid)
                    && AxinClaimsRulesMod.RegistryCfg?.players != null
                    && AxinClaimsRulesMod.RegistryCfg.players.TryGetValue(ownerUid, out var pEntry2)
                    && pEntry2 != null)
                {
                    aliasZona = RegistrySync.GetAliasForClaim(pEntry2, axinClaimId);
                    if (string.IsNullOrWhiteSpace(aliasZona) || aliasZona == "(noalias)")
                    {
                        // Ensure alias exists
                        aliasZona = RegistrySync.EnsureAliasForClaim(api, ownerUid, ownerName, axinClaimId);
                    }
                }
            }
            catch { }

            // VTML clickable true/false
            string Line(string key, bool current)
            {
                string cur = UiTheme.Bool(R(), current);
                string t = ClickBool(api, aliasZona, key, true, current == true);
                string f = ClickBool(api, aliasZona, key, false, current == false);
                return $"{key}={cur}  [ {t} | {f} ]";
            }



            var lines = new List<string>
            {
                UiTheme.Header(R(), LangManager.T("flags.header", "Claim flags") + $": axinClaimId={axinClaimId} ownerName={ownerName ?? "-"} alias={aliasZona}")
            };
            lines.Add(Line("fireSpread.enabled", fs.enabled));
            lines.Add(Line("fireIgnition.enabled", fi.enabled));
            lines.Add(Line("allowTorches", fi.allowTorches));
            lines.Add(Line("allowFirepit", fi.allowFirepit));
            lines.Add(Line("allowCharcoalPit", fi.allowCharcoalPit));
            lines.Add(Line("allowFirestarterOnBlocks", fi.allowFirestarterOnBlocks));

            string root = (AxinClaimsRulesMod.AliasCfg?.rootAlias ?? "ac").Trim();
            if (string.IsNullOrWhiteSpace(root)) root = "ac";
            string flagsAli = CommandAliases.AliasOr(AxinClaimsRulesMod.AliasCfg, "flags");
            if (string.IsNullOrWhiteSpace(flagsAli)) flagsAli = "flags";
            string baseCmd = "/" + root + " " + flagsAli;

            // MAX 9 LINES: el chat se vuelve ilegible si imprimimos demasiado; la línea 10 se reserva para navegación.
            var pageText = CommandHelp.PaginateLines(lines, page, baseCmd, 9);
            return TextCommandResult.Success(pageText);
        }

    }
}