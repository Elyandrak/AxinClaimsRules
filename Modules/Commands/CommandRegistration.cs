using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Features.Commands.Handlers;
using AxinClaimsRules.Core.Extensions;
using AxinClaimsRules.Contracts.Extensions;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.Json;

namespace AxinClaimsRules.Features.Commands
{
    /// <summary>
    /// AXIN-AI-ARCH (Checkpoint E1 - REDO):
    /// Registro de comandos extraído del ModSystem.
    /// Código copiado 1:1 del antiguo RegisterAxinClaimCommands.
    /// </summary>
    internal static class CommandRegistration
    {
        internal static void RegisterAll(ICoreServerAPI api)
        {
                        var parsers = api.ChatCommands.Parsers;

                        string rootAlias = (AxinClaimsRulesMod.AliasCfg?.rootAlias ?? "ac").Trim();
                        if (string.IsNullOrWhiteSpace(rootAlias)) rootAlias = "ac";

                        string SubAlias(string key)
                        {
                            if (AxinClaimsRulesMod.AliasCfg?.subAliases == null) return null;
                            if (!AxinClaimsRulesMod.AliasCfg.subAliases.TryGetValue(key, out var v)) return null;
                            v = (v ?? "").Trim();
                            if (v.Length == 0) return null;
                            if (v.Equals(key, StringComparison.OrdinalIgnoreCase)) return null; // no alias
                            return v;
                        }

                        // Root command name: prefer the configured alias (default /ac) so in-game errors/help show /ac.
                        // Keep /axinclaim as the secondary alias for backwards compatibility.
                        string primaryRoot = rootAlias;
                        if (string.IsNullOrWhiteSpace(primaryRoot)) primaryRoot = "ac";
                        string secondaryRoot = primaryRoot.Equals("axinclaim", StringComparison.OrdinalIgnoreCase)
                            ? "ac"
                            : "axinclaim";

                        // Extra alias fijo: /axinclaims (por si el jugador lo usa)
                        var root = api.ChatCommands
                            .Create(primaryRoot)
                            .WithDescription("AXIN CLAIMS — comandos del mod. Usa /" + primaryRoot + " para abreviar.")
                            .WithRootAlias(secondaryRoot)
                            .WithAlias("axinclaims")
                            .RequiresPlayer()
                            .RequiresPrivilege(Privilege.chat)
                            .HandleWith(args =>
                            {
                                var sp = args.Caller.Player as IServerPlayer;
                                try { RegistrySync.EnsureCurrentClaim(api, sp); }
                                catch (Exception ex)
                                {
                                    api.Logger.Warning("[AxinClaimsRules] EnsureCurrentClaim failed (help): {0}", ex);
                                    sp?.SendMessage(0,
                                        "[AxinClaimsRules] ERROR: no se pudo detectar el claim actual (ver server-debug.log).",
                                        EnumChatType.Notification);
                                }
                                return TextCommandResult.Success(CommandHelp.BuildHelp(AxinClaimsRulesMod.AliasCfg, AxinClaimsRulesMod.CmdCfg));
                            });

                        // /ac id
                        var idCmd = root.BeginSubCommand("id")
                            .WithDescription("Muestra el id del claim actual y lo registra en ClaimsRegistry.")
                            .RequiresPlayer()
                            .RequiresPrivilege(Privilege.chat)
                            .HandleWith(args =>
                            {
                                var sp = args.Caller.Player as IServerPlayer;
                                if (sp == null) return TextCommandResult.Error(LangManager.T("err.only.players","Players only."));

                                RegistrySync.EnsureCurrentClaim(api, sp);

                                if (!PrivilegeChecks.RequireCmd(sp, AxinClaimsRulesMod.CmdCfg, "id")) return TextCommandResult.Error("No tienes privilegios para usar id.");

                                return AxinClaimCommands.CmdId(api, sp);
                            });
                        var idAlias = SubAlias("id");
                        if (!string.IsNullOrWhiteSpace(idAlias)) idCmd.WithAlias(idAlias);
                        idCmd.EndSubCommand();

                        // /ac settp
                        var settpCmd = root.BeginSubCommand("settp")
                            .WithDescription("Guarda/actualiza el TP del claim actual (posición del jugador).")
                            .RequiresPlayer()
                            .RequiresPrivilege(Privilege.chat)
                            .HandleWith(args =>
                            {
                                var sp = args.Caller.Player as IServerPlayer;
                                if (sp == null) return TextCommandResult.Error(LangManager.T("err.only.players","Players only."));

                                RegistrySync.EnsureCurrentClaim(api, sp);

                                if (!PrivilegeChecks.RequireCmd(sp, AxinClaimsRulesMod.CmdCfg, "settp")) return TextCommandResult.Error("No tienes privilegios para usar settp.");

                                return AxinClaimCommands.CmdSetTp(api, sp);
                            });
                        var settpAlias = SubAlias("settp");
                        if (!string.IsNullOrWhiteSpace(settpAlias)) settpCmd.WithAlias(settpAlias);
                        settpCmd.EndSubCommand();

                        // /ac tp [alias]
                        var tpCmd = root.BeginSubCommand("tp")
                            .WithDescription("Teleport al TP guardado para un alias (ej: elYandrack1).")
                            .WithArgs(parsers.Word("alias"))
                            .RequiresPlayer()
                            .RequiresPrivilege(Privilege.chat)
                            .HandleWith(args =>
                            {
                                return TeleportCommand.Handle(api, args, rootAlias);
                            });
            var tpAlias = SubAlias("tp");
                        if (!string.IsNullOrWhiteSpace(tpAlias)) tpCmd.WithAlias(tpAlias);
                        tpCmd.EndSubCommand();

            	            // /ac list
            	            // - /ac list
            	            // - /ac list [page]
            	            // - /ac list [folder] [page]
            	            var listCmd = root.BeginSubCommand("list")
            	                .WithDescription(LangManager.T("cmd.list.desc", "Shows all registered claims (alias and TP coords)."))
            	                .WithArgs(api.ChatCommands.Parsers.OptionalWord("folderOrPage"), api.ChatCommands.Parsers.OptionalWord("page"))
            	                .RequiresPlayer()
            	                .RequiresPrivilege(Privilege.chat)
            	                .HandleWith(args =>
            	                {
            	                    return ListCommand.Handle(api, args);
            	                });
            var listAlias = SubAlias("list");
                        if (!string.IsNullOrWhiteSpace(listAlias)) listCmd.WithAlias(listAlias);
                        listCmd.EndSubCommand();

                        // /ac folder add <name>
                        // /ac folder up <name>
                        // /ac folder down <name>
                        // /ac folder pick <alias> [page]
                        // /ac folder assign <folder> <alias>
                        var folderCmd = root.BeginSubCommand("folder")
                            .WithDescription("Gestión de carpetas")
                            .WithArgs(parsers.OptionalWord("action"), parsers.OptionalWord("a"), parsers.OptionalWord("b"), parsers.OptionalInt("page"))
                            .RequiresPlayer()
                            .RequiresPrivilege(Privilege.chat)
                            .HandleWith(args =>
                            {
                                return FolderCommand.Handle(api, args);
                            });
                        folderCmd.EndSubCommand();


            // /ac claim up|down <aliasKey>
            // /ac claim pick <aliasKey> [page]
            // /ac claim movetofolder <aliasKey> <folderName>
            // /ac claim extract <folder>/<alias>
            var claimCmd = root.BeginSubCommand("claim")
                .WithDescription("Gestiona el orden y las carpetas de los claims (chat UI)")
                .WithArgs(parsers.OptionalWord("action"), parsers.OptionalWord("a"), parsers.OptionalWord("b"), parsers.OptionalInt("page"))
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args =>
                            {
                                return ClaimUiCommand.Handle(api, args);
                            });
            claimCmd.EndSubCommand();



            // /ac flags
            var flagsCmd = root.BeginSubCommand("flags")
                .WithDescription("Muestra los flags/reglas efectivos del claim donde estás.")
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("page")) // Optional -> prevents chat 'incomplete command' lock
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args =>
                {
                    return FlagsCommand.Handle(api, args);
                });
            var flagsAlias = SubAlias("flags");
            if (!string.IsNullOrWhiteSpace(flagsAlias)) flagsCmd.WithAlias(flagsAlias);
            flagsCmd.EndSubCommand();



            // /ac flag <aliasZona> <flag> <true|false>
            var flagCmd = root.BeginSubCommand("flag")
                .WithDescription("Cambia un flag de un claim por alias y aplica reload automático.")
                // IMPORTANT: Optional args prevent chat "incomplete command" lock when user runs '/ac flag' with no args.
                .WithArgs(parsers.OptionalWord("aliasZona"), parsers.OptionalWord("flag"), parsers.OptionalWord("value"))
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args =>
                {
                    var sp = args.Caller.Player as IServerPlayer;
                    if (sp == null) return TextCommandResult.Error("Solo jugadores.");

                    if (!PrivilegeChecks.RequireCmd(sp, AxinClaimsRulesMod.CmdCfg, "flags"))
                        return TextCommandResult.Error("No tienes privilegios para cambiar flags.");

                    // 0-2 args => show help + list of flags + player's known aliases (paged)
                    // If only 1 arg and it's a number, treat it as help page (for VTML pagination links)
                    if (args.ArgCount < 3)
                    {
                        int page = 1;
                        if (args.ArgCount == 1)
                        {
                            var token = args[0]?.ToString();
                            if (!string.IsNullOrWhiteSpace(token)) int.TryParse(token.Trim(), out page);
                        }
                        return AxinClaimCommands.CmdFlagHelp(api, sp, page);
                    }

                    var a = args.ArgCount >= 1 ? args[0]?.ToString() : "";
                    var f = args.ArgCount >= 2 ? args[1]?.ToString() : "";
                    var v = args.ArgCount >= 3 ? args[2]?.ToString() : "";

                    try
                    {
                        return AxinClaimCommands.CmdFlagSet(api, sp, a, f, v);
                    }
                    catch (Exception ex)
                    {
                        api.Logger.Warning("[AxinClaimsRules] /ac flag failed: {0}", ex);
                        try { sp.SendMessage(0, "[AxinClaimsRules] ERROR: fallo en /ac flag (ver server-debug.log).", EnumChatType.Notification); } catch { }
                        return TextCommandResult.Error("Error en /ac flag: " + ex.Message);
                    }
                });
            var flagAlias = SubAlias("flag");
            if (!string.IsNullOrWhiteSpace(flagAlias)) flagCmd.WithAlias(flagAlias);
            flagCmd.EndSubCommand();

            // /ac reload
                        var reloadCmd = root.BeginSubCommand("reload")
                            .WithDescription("Recarga Config + Registry + Alias + Lang desde disco (aplica cambios manuales tras editar JSON).")
                            .RequiresPlayer()
                            .RequiresPrivilege(Privilege.chat)
                            .HandleWith(args =>
                            {
                                return ReloadCommand.Handle(api, args);
                            });
            var reloadAlias = SubAlias("reload");
                        if (!string.IsNullOrWhiteSpace(reloadAlias)) reloadCmd.WithAlias(reloadAlias);
                        reloadCmd.EndSubCommand();

                        // /ac claims export
                        var claimsCmd = root.BeginSubCommand("claims")
                            .WithDescription(LangManager.T("cmd.claims.desc", "Exports the full list of world claims into ClaimsRegistry.json (merging without overwriting flags, aliases or TP)."))
                            .RequiresPlayer()
                            .RequiresPrivilege(Privilege.chat)
                            .HandleWith(args =>
                            {
                                return ClaimsCommand.Handle(api, args);
                            });
                        claimsCmd.WithArgs(api.ChatCommands.Parsers.OptionalWord("action"));
                        var claimsAlias = SubAlias("claims");
                        if (!string.IsNullOrWhiteSpace(claimsAlias)) claimsCmd.WithAlias(claimsAlias);
                        claimsCmd.EndSubCommand();


            // IMPORTANT: DO NOT register a root '/flag' shortcut. Brief requires ONLY '/ac flag'.
            // Any old Alias.json extraRootAliases["flag"] is ignored for safety and to prevent chat lock.

            // Validación para evitar "Incomplete command"
                        root.Validate();

                        api.Logger.Notification("[AxinClaimsRules] /axinclaim REGISTERED (alias /" + rootAlias + ").");

        

                        // AXIN-IA-ARCH (P1.2): register addon subcommands under /ac
                        try
                        {
                            foreach (var extCmd in ExtensionsState.SubCommands)
                            {
                                if (extCmd == null) continue;
                                var key = (extCmd.Key ?? "").Trim();
                                if (key.Length == 0) continue;

                                var sc = root.BeginSubCommand(key)
                                    .WithDescription(extCmd.Description ?? ("Addon command: " + key))
                                    .RequiresPlayer()
                                    .RequiresPrivilege(Privilege.chat)
                                    .HandleWith(args =>
                                    {
                                        var sp = args.Caller.Player as IServerPlayer;
                                        if (sp == null) return TextCommandResult.Error(LangManager.T("err.only.players","Players only."));
                                        return extCmd.Execute(api, sp, args);
                                    });

                                // Note: aliases for addon commands can be handled by the addon itself (or extended later).
                                sc.EndSubCommand();
                            }
                        }
                        catch (Exception ex)
                        {
                            api.Logger.Warning("[AxinClaimsRules] Addon command registration failed: {0}", ex);
                        }

}
    }
}
