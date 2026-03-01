using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Contracts;
using AxinClaimsRules.Features.Commands.Rendering;
using AxinClaimsRules.Infra;

namespace AxinClaimsRules.Modules.Commands.Listing
{
    /// <summary>
    /// E7.1c: /ac list logic extracted from legacy AxinClaimCommands.CmdListAll.
    /// 
    /// IMPORTANT: Keep behavior identical (copy 1:1) while relocating code.
    /// Legacy entrypoint now delegates to this service.
    /// </summary>
    internal sealed class ClaimsListService
    {
        public TextCommandResult BuildList(ICoreServerAPI api, IServerPlayer sp, string folderOrNull, int page)
        {
            var reg = AxinClaimsRulesMod.RegistryCfg;
            if (reg?.players == null || reg.players.Count == 0)
                return TextCommandResult.Success(LangManager.T("list.empty", "No claims registered yet."));

            string root = AxinClaimsRulesMod.AliasCfg?.rootAlias;
            if (string.IsNullOrWhiteSpace(root)) root = "ac";

            var headerLines = new List<string>();
            headerLines.Add(UiTheme.Header(Ren(), LangManager.T("list.header", "AXIN CLAIMS — registered claims")));

            // Build CONTENT (claims or folders), then paginate ONLY content to 9 lines.
            var contentLines = new List<string>();

            if (!string.IsNullOrWhiteSpace(folderOrNull))
            {
                // Special view: folders list
                if (string.Equals(folderOrNull.Trim(), "folders", StringComparison.OrdinalIgnoreCase))
                {
                    // Content: [Outside] first
                    contentLines.Add(UiTheme.LinkWithMarker("↩", Ren().Link(LangManager.T("list.back", "Outside"), $"{root} list 1"), "cornflowerblue"));

                    // v2: foldersOrder is global at root
                    var folders = ExtractFoldersOrderedGlobal(reg);
                    foreach (var fn in folders)
                    {
                        string up = LangManager.T("folder.up", "Arriba");
                        string down = LangManager.T("folder.down", "Abajo");

                        string open = UiTheme.LinkWithMarker("•", Ren().Link(fn, $"{root} list {fn} 1"));
                        string upLink = UiTheme.BracketedLinkWithMarker("^", Ren().Link(up, $"{root} folder up {fn}"), "cornflowerblue", "cornflowerblue");
                        string downLink = UiTheme.BracketedLinkWithMarker("v", Ren().Link(down, $"{root} folder down {fn}"), "lightgreen", "lightgreen");
                        contentLines.Add($"- {open}  {upLink}  {downLink}");
                    }
                }
                else
                {
                    // Folder view (global): show aliases whose key starts with "Folder/"
                    string folder = folderOrNull.Trim();
                    contentLines.AddRange(BuildAliasLinesFromGlobal(api, reg, root, folder));
                }
            }
            else
            {
                // OUTSIDE view: show a clickable [Folders] entry at the top (first item)
                contentLines.Add(UiTheme.LinkWithMarker("◆", Ren().Link(LangManager.T("list.folders", "Folders"), $"{root} list folders 1")));

                // Outside claims (global): aliases without folder prefix
                contentLines.AddRange(BuildAliasLinesFromGlobal(api, reg, root, folder: null));
            }

            // --- Pagination: 9 content lines + 1 nav/status line ---
            const int perPage = 9;
            int safePage = page <= 0 ? 1 : page;
            int total = contentLines.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)perPage));
            if (safePage > totalPages) safePage = totalPages;

            int startIndex = (safePage - 1) * perPage;
            int endIndex = Math.Min(total, startIndex + perPage);

            var outLines = new List<string>();
            outLines.AddRange(headerLines);

            for (int i = startIndex; i < endIndex; i++)
            {
                outLines.Add(contentLines[i]);
            }

            if (totalPages > 1)
            {
                string prevText = LangManager.T("nav.prev", "Anterior");
                string nextText = LangManager.T("nav.next", "Siguiente");

                string arg = string.IsNullOrWhiteSpace(folderOrNull) ? "" : (" " + folderOrNull.Trim());
                string prev = (safePage > 1)
                    ? UiTheme.LinkWithMarker("←", Ren().LinkText(prevText, $"{root} list{arg} {safePage - 1}"))
                    : prevText;
                string next = (safePage < totalPages)
                    ? UiTheme.LinkWithMarker("→", Ren().LinkText(nextText, $"{root} list{arg} {safePage + 1}"))
                    : nextText;

                int shownFrom = total == 0 ? 0 : startIndex + 1;
                int shownTo = endIndex;
                string status = string.Format(LangManager.T("nav.status", "{0}-{1} de {2}"), shownFrom, shownTo, total);

                outLines.Add($"{prev} - {status} - {next}");
            }

            return TextCommandResult.Success(string.Join("\n", outLines));
        }

        internal static List<string> ExtractFoldersOrderedGlobal(ClaimsRegistry reg)
        {
            var outList = new List<string>();
            if (reg?.foldersOrder != null)
            {
                foreach (var f in reg.foldersOrder)
                {
                    var fn = (f ?? "").Trim();
                    if (fn.Length == 0) continue;
                    if (!outList.Any(x => x.Equals(fn, StringComparison.OrdinalIgnoreCase))) outList.Add(fn);
                }
            }
            // Derive from aliases if needed
            if (reg?.aliases != null)
            {
                foreach (var k in reg.aliases.Keys)
                {
                    if (string.IsNullOrWhiteSpace(k)) continue;
                    int idx = k.IndexOf('/');
                    if (idx <= 0) continue;
                    var fn = k.Substring(0, idx).Trim();
                    if (fn.Length == 0) continue;
                    if (!outList.Any(x => x.Equals(fn, StringComparison.OrdinalIgnoreCase))) outList.Add(fn);
                }
            }
            return outList;
        }

        internal static List<string> BuildAliasLinesFromGlobal(ICoreServerAPI api, ClaimsRegistry reg, string root, string folder)
        {
            var lines = new List<string>();
            if (reg?.aliases == null || reg.aliases.Count == 0) return lines;

            bool inFolderView = !string.IsNullOrWhiteSpace(folder);
            string folderName = inFolderView ? folder.Trim() : "";

            // Determine ordered alias keys for this view
            List<string> orderedKeys;

            if (!inFolderView)
            {
                reg.outsideOrder ??= new List<string>();
                orderedKeys = reg.outsideOrder
                    .Where(k => !string.IsNullOrWhiteSpace(k) && reg.aliases.ContainsKey(k))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Add missing outside keys at end
                var outsideKeys = reg.aliases.Keys.Where(k => !string.IsNullOrWhiteSpace(k) && !k.Contains("/"))
                    .Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var k in outsideKeys)
                {
                    if (!orderedKeys.Any(x => x.Equals(k, StringComparison.OrdinalIgnoreCase)))
                        orderedKeys.Add(k);
                }
            }
            else
            {
                reg.folderOrders ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var prefix = folderName + "/";

                var keysInFolder = reg.aliases.Keys.Where(k => !string.IsNullOrWhiteSpace(k) && k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (!reg.folderOrders.TryGetValue(folderName, out var fo) || fo == null)
                    fo = new List<string>();

                orderedKeys = fo.Where(k => !string.IsNullOrWhiteSpace(k) && reg.aliases.ContainsKey(k))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                foreach (var k in keysInFolder)
                {
                    if (!orderedKeys.Any(x => x.Equals(k, StringComparison.OrdinalIgnoreCase)))
                        orderedKeys.Add(k);
                }
            }

            // Build lines
            foreach (var key in orderedKeys)
            {
                string display = key;

                if (inFolderView)
                {
                    int idx = key.IndexOf('/');
                    if (idx >= 0 && idx + 1 < key.Length) display = key.Substring(idx + 1);
                }

                // clickable TP (sin corchetes)
                string tpLink = UiTheme.LinkWrap(Ren().LinkText(display, $"{root} tp {display}"));

                string up = LangManager.T("folder.up", "Arriba");
                string down = LangManager.T("folder.down", "Abajo");

                // claim move buttons
                string upLink = UiTheme.BracketedLinkWithMarker("^", Ren().Link(up, $"{root} claim up {key}"), "cornflowerblue", "cornflowerblue");
                string downLink = UiTheme.BracketedLinkWithMarker("v", Ren().Link(down, $"{root} claim down {key}"), "lightgreen", "lightgreen");

                if (!inFolderView)
                {
                    // add folder picker
                    string addFolderText = LangManager.T("folder.addto", "Add to folder");
                    string addFolder = UiTheme.BracketedLinkWithMarker("#", Ren().Link(addFolderText, $"{root} claim pick {key} 1"), "yellow", "yellow");
                    lines.Add($"- {tpLink}  {upLink}  {downLink}  {addFolder}");
                }
                else
                {
                    // extract
                    string extractText = LangManager.T("folder.extract", "Sacar");
                    string extract = UiTheme.LinkWithMarker("↥", Ren().Link(extractText, $"{root} claim extract {key}"));
                    lines.Add($"- {tpLink}  {upLink}  {downLink}  {extract}");
                }
            }

            return lines;
        }

        private static ICommandRenderer Ren()
        {
            return AxinClaimsRulesMod.RendererSvc ?? new ChatVtmlRenderer();
        }
    }
}
