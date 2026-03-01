using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Data.Registry;

namespace AxinClaimsRules.Features.Commands.Handlers
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint D6):
    /// Handler dedicado para /ac folder (add/up/down/pick/assign).
    /// Objetivo: sacar lógica de botones/orden de carpetas del CommandRegistration.
    /// Nota: implementación copiada 1:1 del legacy AxinClaimCommands (CmdList).
    /// </summary>
    internal static class FolderCommand
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

            if (action == "add") return CmdFolderAdd(api, sp, a);
            if (action == "pick") return CmdFolderPick(api, sp, a, page);
            if (action == "assign") return CmdFolderAssign(api, sp, a, b);
            if (action == "up" || action == "down") return CmdFolderMove(api, sp, action, a);

            return TextCommandResult.Success(LangManager.T("help.folder", "Use: /ac folder add|up|down|pick|assign"));
        }

                public static TextCommandResult CmdFolderAdd(ICoreAPI api, IServerPlayer sp, string folderNameRaw)
                {
                    var folderName = (folderNameRaw ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(folderName))
                        return TextCommandResult.Error(LangManager.T("err.folder.name", "Missing folder name."));
                    if (folderName.Contains("/"))
                        return TextCommandResult.Error(LangManager.T("err.folder.slash", "Folder name cannot contain '/'"));
                    if (folderName.Equals("Outside", StringComparison.OrdinalIgnoreCase))
                        return TextCommandResult.Error(LangManager.T("err.folder.reserved", "Reserved folder."));
        
                    var reg = AxinClaimsRulesMod.RegistryCfg;
                    if (reg == null) return TextCommandResult.Error("Registry not loaded.");
        
                    reg.foldersOrder ??= new List<string>();
        
                    if (reg.foldersOrder.Any(x => x.Equals(folderName, StringComparison.OrdinalIgnoreCase)))
                        return TextCommandResult.Success(LangManager.T("ok.folder.exists", "Folder already exists."));
        
                    reg.foldersOrder.Add(folderName);
        
                    RegistryStore.SaveClaimsRegistry(api, reg);
                    return TextCommandResult.Success(LangManager.T("ok.folder.added", "Folder created."));
                }

                public static TextCommandResult CmdFolderPick(ICoreAPI api, IServerPlayer sp, string claimAliasRaw, int page = 1)
                {
                    var claimAlias = (claimAliasRaw ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(claimAlias))
                        return TextCommandResult.Error(LangManager.T("err.alias.missing", "Missing alias."));
        
                    var viewer = AxinClaimsRules.AxinClaimCommands.GetOrCreateViewer(sp);
                    if (viewer == null) return TextCommandResult.Error("Registry not loaded.");
        
                    var reg = AxinClaimsRulesMod.RegistryCfg;
                    if (reg == null) return TextCommandResult.Error("Registry not loaded.");
        
                    var folders = AxinClaimsRules.AxinClaimCommands.ExtractFoldersOrderedGlobal(reg);
        
        	            string folderColor = AxinClaimsRulesMod.CmdCfg?.GetFolderColorOrDefault() ?? "#b07a3a";
        	            var header = $"Carpetas — {LangManager.T("list.page", "Listado")} {page}  {AxinClaimsRules.AxinClaimCommands.Link("ac list 1", $"[{LangManager.T("list.outside","Outside")}]", folderColor)}";
                    var content = new List<string>();
                    foreach (var f in folders)
                    {
        	                content.Add($"- {AxinClaimsRules.AxinClaimCommands.Link($"ac folder assign {f} {claimAlias}", f, folderColor)}");
                    }
        
                    var msg = header + "\n" + CommandHelp.PaginateLines(content, page, $"ac folder pick {claimAlias}", 9);
                    return TextCommandResult.Success(msg);
                }

                public static TextCommandResult CmdFolderAssign(ICoreAPI api, IServerPlayer sp, string folderNameRaw, string claimAliasRaw)
                {
                    var folderName = (folderNameRaw ?? "").Trim();
                    var claimAlias = (claimAliasRaw ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(claimAlias))
                        return TextCommandResult.Error("Missing args.");
        
                    var reg = AxinClaimsRulesMod.RegistryCfg;
                    if (reg == null) return TextCommandResult.Error("Registry not loaded.");
        
                    reg.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    reg.foldersOrder ??= new List<string>();
        
                    if (!reg.foldersOrder.Any(x => x.Equals(folderName, StringComparison.OrdinalIgnoreCase)))
                        return TextCommandResult.Error(LangManager.T("err.folder.unknown", "Unknown folder."));
        
                    var viewer = AxinClaimsRules.AxinClaimCommands.GetOrCreateViewer(sp);
                    if (viewer == null) return TextCommandResult.Error("Viewer not loaded.");
        
                    // Only allow assigning aliases that belong to this player's claims
                    if (!reg.aliases.TryGetValue(claimAlias, out var claimVal) || string.IsNullOrWhiteSpace(claimVal) || !viewer.claims.ContainsKey(claimVal))
                        return TextCommandResult.Error(LangManager.T("err.alias.unknown", "Unknown alias."));
        
                    if (claimAlias.Contains("/"))
                        return TextCommandResult.Error(LangManager.T("err.alias.infolder", "Alias is already in a folder."));
        
                    var newKey = folderName + "/" + claimAlias;
                    if (reg.aliases.ContainsKey(newKey))
                        return TextCommandResult.Error(LangManager.T("err.alias.exists", "Target alias already exists."));
        
                    reg.aliases.Remove(claimAlias);
                    reg.aliases[newKey] = claimVal;
        
                    RegistryStore.SaveClaimsRegistry(api, reg);
                    return TextCommandResult.Success(LangManager.T("ok.folder.assigned", "Added to folder."));
                }

        	        public static TextCommandResult CmdFolderMove(ICoreServerAPI api, IServerPlayer sp, string direction, string folderName)
        	        {
        	            if (sp == null) return TextCommandResult.Error(LangManager.T("err.only.players", "Players only."));
        	            var reg = AxinClaimsRulesMod.RegistryCfg;
        	            if (reg == null) return TextCommandResult.Error("Registry empty.");
        
        	            if (string.IsNullOrWhiteSpace(direction) || string.IsNullOrWhiteSpace(folderName))
        	                return TextCommandResult.Error("Usage: /ac folder up|down <folderName>");
        
        	            string fn = folderName.Trim();
        	            if (fn.Equals("Outside", StringComparison.OrdinalIgnoreCase))
        	                return TextCommandResult.Error(LangManager.T("err.folder.reserved", "Reserved folder."));
        
        	            reg.foldersOrder ??= new List<string>();
        
        	            // Ensure folder exists
        	            if (!reg.foldersOrder.Any(x => x.Equals(fn, StringComparison.OrdinalIgnoreCase)))
        	                return TextCommandResult.Error(LangManager.T("err.folder.unknown", "Unknown folder."));
        
        	            bool up = direction.Trim().Equals("up", StringComparison.OrdinalIgnoreCase);
        	            bool down = direction.Trim().Equals("down", StringComparison.OrdinalIgnoreCase);
        	            if (!up && !down) return TextCommandResult.Error("Direction must be up/down.");
        
        	            bool changed = AxinClaimsRules.AxinClaimCommands.MoveKey(reg.foldersOrder, fn, up ? -1 : +1);
        	            if (!changed) return TextCommandResult.Success("No change.");
        
	            RegistryStore.SaveClaimsRegistry(api, reg);
        
        	            return AxinClaimsRules.AxinClaimCommands.CmdListAll(api, sp, "folders", 1);
        	        }
    }
}
