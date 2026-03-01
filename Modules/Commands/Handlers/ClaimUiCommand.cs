using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Data.Registry;
using System.Collections.Generic;
using System.Linq;

namespace AxinClaimsRules.Features.Commands.Handlers
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint D6):
    /// Handler dedicado para /ac claim (up/down/pick/movetofolder/extract).
    /// Objetivo: separar la lógica de botones/orden/mover/sacar de los claims (chat UI).
    /// Implementación copiada 1:1 del legacy AxinClaimCommands (CmdList).
    /// </summary>
    internal static class ClaimUiCommand
    {
        internal static TextCommandResult Handle(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var sp = args.Caller.Player as IServerPlayer;
            if (sp == null) return TextCommandResult.Error(LangManager.T("err.only.players", "Players only."));

            string action = args.ArgCount >= 1 ? args[0]?.ToString() : null;
            string a = args.ArgCount >= 2 ? args[1]?.ToString() : null;
            string b = args.ArgCount >= 3 ? args[2]?.ToString() : null;
            int page = args.ArgCount >= 4 ? (int)args[3] : 1;

            action = (action ?? "").Trim().ToLowerInvariant();

            if (action == "up" || action == "down") return CmdClaimMove(api, sp, action, a);
            if (action == "pick") return CmdClaimFolderPick(api, sp, a, page);
            if (action == "movetofolder") return CmdClaimMoveToFolder(api, sp, a, b);
            if (action == "extract") return CmdClaimExtract(api, sp, a);

            return TextCommandResult.Success(LangManager.T("help.claimui", "Use: /ac claim up|down|pick|movetofolder|extract"));
        }

        public static TextCommandResult CmdClaimMove(ICoreServerAPI api, IServerPlayer sp, string direction, string aliasKeyRaw)
        {
            if (sp == null) return TextCommandResult.Error(LangManager.T("err.only.players", "Players only."));
            var reg = AxinClaimsRulesMod.RegistryCfg;
            if (reg == null) return TextCommandResult.Error("Registry empty.");
        
            string aliasKey = (aliasKeyRaw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(direction) || string.IsNullOrWhiteSpace(aliasKey))
                return TextCommandResult.Error("Usage: /ac claim up|down <alias>");
        
            bool up = direction.Trim().Equals("up", StringComparison.OrdinalIgnoreCase);
            bool down = direction.Trim().Equals("down", StringComparison.OrdinalIgnoreCase);
            if (!up && !down) return TextCommandResult.Error("Direction must be up/down.");
        
            // Determine list to move in (outsideOrder or folderOrders[folder])
            if (!aliasKey.Contains("/"))
            {
                reg.outsideOrder ??= new List<string>();
                // Ensure present
                if (!reg.outsideOrder.Any(x => x.Equals(aliasKey, StringComparison.OrdinalIgnoreCase)))
                    reg.outsideOrder.Add(aliasKey);
        
                bool changed = AxinClaimsRules.AxinClaimCommands.MoveKey(reg.outsideOrder, aliasKey, up ? -1 : +1);
                if (changed) { RegistryStore.SaveClaimsRegistry(api, reg); }
                return AxinClaimsRules.AxinClaimCommands.CmdListAll(api, sp, null, 1);
            }
            else
            {
                int idx = aliasKey.IndexOf('/');
                string folder = aliasKey.Substring(0, idx);
                reg.folderOrders ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                if (!reg.folderOrders.TryGetValue(folder, out var list) || list == null)
                {
                    list = new List<string>();
                    reg.folderOrders[folder] = list;
                }
                if (!list.Any(x => x.Equals(aliasKey, StringComparison.OrdinalIgnoreCase)))
                    list.Add(aliasKey);
        
                bool changed = AxinClaimsRules.AxinClaimCommands.MoveKey(list, aliasKey, up ? -1 : +1);
                if (changed) { RegistryStore.SaveClaimsRegistry(api, reg); }
                return AxinClaimsRules.AxinClaimCommands.CmdListAll(api, sp, folder, 1);
            }
        }

        public static TextCommandResult CmdClaimFolderPick(ICoreServerAPI api, IServerPlayer sp, string aliasKeyRaw, int page = 1)
        {
            var aliasKey = (aliasKeyRaw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(aliasKey))
                return TextCommandResult.Error(LangManager.T("err.alias.missing", "Missing alias."));
        
            var reg = AxinClaimsRulesMod.RegistryCfg;
            if (reg == null) return TextCommandResult.Error("Registry not loaded.");
        
            string root = AxinClaimsRulesMod.AliasCfg?.rootAlias;
            if (string.IsNullOrWhiteSpace(root)) root = "ac";
        
            var folders = AxinClaimsRules.AxinClaimCommands.ExtractFoldersOrderedGlobal(reg);
        
            string folderColor = AxinClaimsRulesMod.CmdCfg?.GetFolderColorOrDefault() ?? "#b07a3a";
            var title = LangManager.T("folder.pick.title", "Selecciona carpeta");
            var outsideLabel = LangManager.T("list.outside", "Outside");
            var outsideCmd = root + " list";
            var header = AxinClaimsRulesMod.RendererSvc.Strong(title) + "  " + AxinClaimsRulesMod.RendererSvc.Link(outsideLabel, outsideCmd);
            var content = new List<string>();
        
            foreach (var f in folders)
            {
                var add = AxinClaimsRules.AxinClaimCommands.Link($"{root} claim movetofolder {aliasKey} {f}", $"[{f}]", folderColor);
                content.Add($"- {add}");
            }
        
            var msg = header + "\n" + CommandHelp.PaginateLines(content, page, $"ac claim pick {aliasKey}", 9);
            return TextCommandResult.Success(msg);
        }

        public static TextCommandResult CmdClaimMoveToFolder(ICoreServerAPI api, IServerPlayer sp, string aliasKeyRaw, string folderNameRaw)
        {
            var aliasKey = (aliasKeyRaw ?? "").Trim();
            var folderName = (folderNameRaw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(aliasKey) || string.IsNullOrWhiteSpace(folderName))
                return TextCommandResult.Error("Missing args.");
        
            var reg = AxinClaimsRulesMod.RegistryCfg;
            if (reg == null) return TextCommandResult.Error("Registry not loaded.");
        
            reg.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            reg.foldersOrder ??= new List<string>();
            reg.outsideOrder ??= new List<string>();
            reg.folderOrders ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        
            if (!reg.foldersOrder.Any(x => x.Equals(folderName, StringComparison.OrdinalIgnoreCase)))
                return TextCommandResult.Error(LangManager.T("err.folder.unknown", "Unknown folder."));
        
            if (!reg.aliases.TryGetValue(aliasKey, out var claimVal) || string.IsNullOrWhiteSpace(claimVal))
                return TextCommandResult.Error(LangManager.T("err.alias.unknown", "Unknown alias."));
        
            if (aliasKey.Contains("/"))
                return TextCommandResult.Error(LangManager.T("err.alias.infolder", "Alias is already in a folder."));
        
            var newKey = folderName + "/" + aliasKey;
            if (reg.aliases.ContainsKey(newKey))
                return TextCommandResult.Error(LangManager.T("err.alias.exists", "Target alias already exists."));
        
            // Move alias mapping
            reg.aliases.Remove(aliasKey);
            reg.aliases[newKey] = claimVal;
        
            // Update orders
            reg.outsideOrder.RemoveAll(a => a != null && a.Equals(aliasKey, StringComparison.OrdinalIgnoreCase));
        
            if (!reg.folderOrders.TryGetValue(folderName, out var list) || list == null)
            {
                list = new List<string>();
                reg.folderOrders[folderName] = list;
            }
            // put at end
            if (!list.Any(x => x.Equals(newKey, StringComparison.OrdinalIgnoreCase)))
                list.Add(newKey);
        
            RegistryStore.SaveClaimsRegistry(api, reg);
        
            return AxinClaimsRules.AxinClaimCommands.CmdListAll(api, sp, null, 1);
        }

        public static TextCommandResult CmdClaimExtract(ICoreServerAPI api, IServerPlayer sp, string folderAliasKeyRaw)
        {
            var key = (folderAliasKeyRaw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key))
                return TextCommandResult.Error("Missing alias.");
        
            if (!key.Contains("/"))
                return TextCommandResult.Error("Alias is not in a folder.");
        
            var reg = AxinClaimsRulesMod.RegistryCfg;
            if (reg == null) return TextCommandResult.Error("Registry not loaded.");
        
            reg.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            reg.outsideOrder ??= new List<string>();
            reg.folderOrders ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        
            if (!reg.aliases.TryGetValue(key, out var claimVal) || string.IsNullOrWhiteSpace(claimVal))
                return TextCommandResult.Error(LangManager.T("err.alias.unknown", "Unknown alias."));
        
            int idx = key.IndexOf('/');
            string folder = key.Substring(0, idx);
            string shortAlias = key.Substring(idx + 1);
        
            // Move alias mapping back to outside (position 1)
            reg.aliases.Remove(key);
            if (reg.aliases.ContainsKey(shortAlias))
            {
                // Find a unique name
                int n = 2;
                string cand = shortAlias + n;
                while (reg.aliases.ContainsKey(cand)) { n++; cand = shortAlias + n; }
                shortAlias = cand;
            }
            reg.aliases[shortAlias] = claimVal;
        
            // Remove from folder order
            if (reg.folderOrders.TryGetValue(folder, out var list) && list != null)
            {
                list.RemoveAll(a => a != null && a.Equals(key, StringComparison.OrdinalIgnoreCase));
            }
        
            // Insert at position 1 in outside order
            reg.outsideOrder.RemoveAll(a => a != null && a.Equals(shortAlias, StringComparison.OrdinalIgnoreCase));
            reg.outsideOrder.Insert(0, shortAlias);
        
            RegistryStore.SaveClaimsRegistry(api, reg);
        
            return AxinClaimsRules.AxinClaimCommands.CmdListAll(api, sp, null, 1);
        }
    }
}
