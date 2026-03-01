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
using AxinClaimsRules.Data.Registry;

namespace AxinClaimsRules
{

    internal static class CommandHelp
    {
        /// <summary>
        /// Builds help lines with descriptions (localized). Chat-safe: no '<' or '>' characters.
        /// </summary>
        public static string BuildHelp(CommandAliasConfig cfg, CommandConfig cmdCfg)
        {
            string rootAlias = (cfg?.rootAlias ?? "ac").Trim();
            if (string.IsNullOrWhiteSpace(rootAlias)) rootAlias = "ac";
            string ac = "/" + rootAlias;
            string id = CommandAliases.AliasOr(cfg, "id");
            string list = CommandAliases.AliasOr(cfg, "list");
            string flags = CommandAliases.AliasOr(cfg, "flags");
            string flag = CommandAliases.AliasOr(cfg, "flag");
            if (string.IsNullOrWhiteSpace(flag)) flag = flags; // fallback
            string claims = CommandAliases.AliasOr(cfg, "claims");
            string folder = CommandAliases.AliasOr(cfg, "folder");
            string tp = CommandAliases.AliasOr(cfg, "tp");
            string settp = CommandAliases.AliasOr(cfg, "settp");
            string reload = CommandAliases.AliasOr(cfg, "reload");

            string Line(string canon, string ali, string desc, string args = "")
            {
                // AXIN v0.3.6-dev: show the configured alias in help output (not the canonical command name).
                string shown = string.IsNullOrWhiteSpace(ali) ? canon : ali;
                string shownLine = $"{ac} {shown}{args}";
                return $"- {shownLine} : {desc}";
            }
string header = LangManager.T("help.header", "AXIN CLAIMS — available commands");

            // Descriptions (localized)
            string dList  = LangManager.T("cmd.list.desc", "Shows all registered claims (alias and TP coords).");
            string dPlayer= LangManager.T("cmd.player.desc", "Shows registered claims for a player name.");
            string dId    = LangManager.T("cmd.id.desc", "Shows current claim id and registers it.");
            string dFlags = LangManager.T("cmd.flags.desc", "Shows effective flags/rules for the current claim.");
            string dFlagSet = LangManager.T("cmd.flagset.desc", "Sets a flag for a claim alias (and auto-reload).");
            string dClaims = LangManager.T("cmd.claims.desc", "Exports all world claims into ClaimsRegistry.json.");
            string dFolder = LangManager.T("cmd.folder.desc", "Creates and manages folders for claim aliases.");
            string dSettp = LangManager.T("cmd.settp.desc", "Sets TP for current claim to your position.");
            string dTp    = LangManager.T("cmd.tp.desc", "Teleports to the TP of a registered claim alias.");
            string dReload  = LangManager.T("cmd.reload.desc", "Reloads Config + Registry + Lang from disk (no rewrite).");

            return
                header + "\n" +
                Line("list", list, dList) + "\n" +
                $"- {ac} [name] : {dPlayer}\n" +
                Line("id", id, dId) + "\n" +
                Line("flags", flags, dFlags) + "\n" +
                Line("flag", flag, dFlagSet, " [aliasZona] [flag] [true|false]") + "\n" +
                Line("claims", claims, dClaims, " export") + "\n" +
                Line("folder", folder, dFolder, " add|move ...") + "\n" +
                                Line("settp", settp, dSettp) + "\n" +
                Line("tp", tp, dTp, " [alias]") + "\n" +
                Line("reload", reload, dReload);
        }

        public static string BuildHelp(CommandAliasConfig cfg)
        {
            return BuildHelp(cfg, AxinClaimsRulesMod.CmdCfg);
        }
    

        // MAX 9 LINES: AXIN chat pagination helper.
        // Why: avoid chat flooding and prevent UI glitches. Content is capped to maxLines per page.
        // The navigation control (Prev/Next) is printed as the NEXT line after the content (line maxLines+1).
        public static string Paginate(string fullText, int page, string rootCmd, string subCmd, int maxLines)
        {
            if (page < 1) page = 1;
            maxLines = maxLines <= 0 ? 9 : maxLines;

            var lines = (fullText ?? "")
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n')
                .Select(l => l ?? "")
                .Where(l => l.Length > 0)
                .ToList();

            return PaginateLines(lines, page, BuildCommandPrefix(rootCmd, subCmd), maxLines);
        }

        // MAX 9 LINES: paginates a list of lines.
        public static string PaginateLines(List<string> lines, int page, string commandPrefix, int maxLines)
        {
            if (page < 1) page = 1;
            maxLines = maxLines <= 0 ? 9 : maxLines;
            lines ??= new List<string>();

            int total = lines.Count;
            int pages = (int)Math.Ceiling(total / (double)maxLines);
            if (pages < 1) pages = 1;
            if (page > pages) page = pages;

            int start = (page - 1) * maxLines;
            var pageLines = lines.Skip(start).Take(maxLines).ToList();

            // If only one page, return as-is (<= maxLines).
            if (pages == 1)
            {
                return string.Join("\n", pageLines);
            }

            // Navigation line (line maxLines+1)
            string prevText = LangManager.T("nav.prev", "Prev");
            string nextText = LangManager.T("nav.next", "Next");

            // E6-B.1: usar renderer blindado (escape de atributo + texto)
            var r = AxinClaimsRulesMod.RendererSvc;
            string prevLink = page > 1
                ? (r != null ? r.LinkText(prevText, $"{commandPrefix} {page - 1}") : $"<a href=\"command:///{commandPrefix} {page - 1}\">{prevText}</a>")
                : prevText;

            string nextLink = page < pages
                ? (r != null ? r.LinkText(nextText, $"{commandPrefix} {page + 1}") : $"<a href=\"command:///{commandPrefix} {page + 1}\">{nextText}</a>")
                : nextText;

            // Use Tf because LangManager.T only supports (key, fallback)
            string navLine = LangManager.Tf("nav.line", "{0} | {1}", prevLink, nextLink);

            return string.Join("\n", pageLines) + "\n" + navLine;
        }

        private static string BuildCommandPrefix(string rootCmd, string subCmd)
        {
            string r = (rootCmd ?? "").Trim();
            string s = (subCmd ?? "").Trim();
            if (r.StartsWith("/")) r = r.Substring(1);
            if (string.IsNullOrWhiteSpace(s)) return r;
            return (r + " " + s).Trim();
        }
}

internal static class CommandAliases
    {
        public static string ResolveSubcommand(CommandAliasConfig cfg, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return "";
            var t = token.Trim();

            foreach (var canon in new[] { "id", "list", "flags", "flag", "claims", "folder", "tp", "settp", "reload" })
            {
                if (t.Equals(canon, StringComparison.OrdinalIgnoreCase)) return canon;
            }

            if (cfg?.subAliases != null)
            {
                foreach (var kv in cfg.subAliases)
                {
                    var canon = kv.Key?.Trim();
                    var alias = kv.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(canon) || string.IsNullOrWhiteSpace(alias)) continue;
                    if (t.Equals(alias, StringComparison.OrdinalIgnoreCase)) return canon;
                }
            }

            return t;
        }

        public static string AliasOr(CommandAliasConfig cfg, string canon)
        {
            if (cfg?.subAliases != null && cfg.subAliases.TryGetValue(canon, out var a) && !string.IsNullOrWhiteSpace(a))
                return a;
            return canon;
        }
    }

    internal static class RulesEngine
    {
        public static bool EffectiveAllow_FireSpread(string claimId)
        {
            var g = AxinClaimsRulesMod.GlobalCfg;
            var o = AxinClaimsRulesMod.OverridesCfg;

            // fuera de claim => no decidimos aquí (se permite en vanilla)
            if (string.IsNullOrWhiteSpace(claimId)) return true;

            bool allow = g?.defaults?.fireSpread?.enabled ?? true;

            // 1) ClaimsRegistry (editable) tiene prioridad
            if (RegistrySync.TryGetRule_FireSpread(claimId, out bool regAllow))
            {
                return regAllow;
            }

            // 2) ClaimsOverrides (legacy) por claimId (ahora tratamos claimId=AxinClaimId)
            var entry = o?.overrides?.FirstOrDefault(x =>
                string.Equals(x?.claimId, claimId, StringComparison.OrdinalIgnoreCase)
            );

            if (entry?.fireSpread != null)
            {
                allow = entry.fireSpread.enabled;
            }

            return allow;
        }
    }

    // =========================
    // CLAIM RESOLVER (mínimo)
    // =========================

}