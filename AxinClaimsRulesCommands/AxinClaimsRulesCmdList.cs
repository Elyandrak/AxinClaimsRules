using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Data.Registry;

namespace AxinClaimsRules
{
    internal static partial class AxinClaimCommands
    {
        private static AxinClaimsRules.Contracts.ICommandRenderer Ren()
        {
            // Ultra-conservador: si por algún motivo no está inicializado aún, caemos a un renderer nuevo.
            return AxinClaimsRulesMod.RendererSvc ?? new AxinClaimsRules.Features.Commands.Rendering.ChatVtmlRenderer();
        }

        // Mantener firma histórica para minimizar cambios.
        // NOTA: el color se ignora (limitación conocida del chat de VS).
        internal static string Link(string cmd, string text, string color)
        {
            return Ren().Link(text, cmd);
        }

        // /ac list
        // - /ac list
        // - /ac list [page]
        // - /ac list [folder] [page]
        // Chat rule: show max 9 lines per page + navigation.
        public static TextCommandResult CmdListAll(ICoreServerAPI api, IServerPlayer sp, string folderOrNull, int page)
        {
            // E7.1c: extracted to IA-ARCH service (Modules/Commands/Listing)
            return new AxinClaimsRules.Modules.Commands.Listing.ClaimsListService().BuildList(api, sp, folderOrNull, page);
        }

        internal static List<string> ExtractFoldersOrderedGlobal(ClaimsRegistry reg)
        {
            // E7.1c: moved to service, keep legacy wrapper for other handlers (Folder/ClaimUi)
            return AxinClaimsRules.Modules.Commands.Listing.ClaimsListService.ExtractFoldersOrderedGlobal(reg);
        }

        internal static PlayerClaimsEntry GetOrCreateViewer(IServerPlayer sp)
        {
            if (sp == null) return null;
            var reg = AxinClaimsRulesMod.RegistryCfg;
            if (reg == null) return null;

            reg.players ??= new Dictionary<string, PlayerClaimsEntry>();
            if (!reg.players.TryGetValue(sp.PlayerUID, out var viewer) || viewer == null)
            {
                viewer = new PlayerClaimsEntry();
                reg.players[sp.PlayerUID] = viewer;
            }

            viewer.claims ??= new Dictionary<string, ClaimEntry>();

            // v2 registry format: aliases live at root (reg.aliases).
            // Build an in-memory view of "my aliases" by filtering global aliases to only those that point to my claims.
            viewer.aliases ??= new Dictionary<string, string>();
            viewer.aliases.Clear();

            reg.aliases ??= new Dictionary<string, string>();
            foreach (var kv in reg.aliases)
            {
                var akey = (kv.Key ?? "").Trim();
                var cid = (kv.Value ?? "").Trim();
                if (akey.Length == 0 || cid.Length == 0) continue;

                if (viewer.claims.ContainsKey(cid))
                {
                    if (!viewer.aliases.ContainsKey(akey))
                        viewer.aliases[akey] = cid;
                }
            }

            // legacy in-memory only
            viewer.aliasOrder ??= new List<string>();

            return viewer;
        }

        internal static bool MoveKey(List<string> list, string key, int delta)
        {
            if (list == null) return false;
            int idx = list.FindIndex(x => x != null && x.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return false;
            int nidx = idx + delta;
            if (nidx < 0 || nidx >= list.Count) return false;
            var tmp = list[idx];
            list[idx] = list[nidx];
            list[nidx] = tmp;
            return true;
        }

        public static TextCommandResult CmdListPlayer(ICoreServerAPI api, string nameQuery)
        {
            if (string.IsNullOrWhiteSpace(nameQuery)) return TextCommandResult.Error("Nombre vacío.");

            var reg = AxinClaimsRulesMod.RegistryCfg;
            if (reg?.players == null || reg.players.Count == 0) return TextCommandResult.Success("No hay claims en el registry todavía.");

            string q = NameUtil.Normalize(nameQuery).ToLowerInvariant();

            var sb = new StringBuilder();
            sb.AppendLine($"AXIN CLAIMS :: {nameQuery}");

            int foundPlayers = 0;
            foreach (var p in reg.players)
            {
                var pe = p.Value;
                string pname = NameUtil.Normalize(pe?.lastKnownName ?? "").ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(pname)) continue;
                if (!pname.Contains(q)) continue;

                foundPlayers++;
                sb.AppendLine($"Jugador: {pe.lastKnownName} ({p.Key})");

                foreach (var kv in pe.claims)
                {
                    var axinId = kv.Key;
                    var ce = kv.Value;
                    var alias = RegistrySync.GetAliasForClaim(pe, axinId);
                    bool fire = ce?.claimRules?.fireSpread?.enabled ?? false;
                    string tp = (ce?.tp != null) ? $"{ce.tp.x},{ce.tp.y},{ce.tp.z}" : "-";

                    sb.AppendLine($"  - {alias} :: {axinId} :: fireSpread={fire} :: tp={tp}");
                }
            }

            if (foundPlayers == 0) return TextCommandResult.Success("No se encontró ningún jugador en el registry con ese nombre.");
            return TextCommandResult.Success(sb.ToString());
        }
    }
}
