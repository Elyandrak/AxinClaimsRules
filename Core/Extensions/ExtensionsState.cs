using System.Collections.Generic;
using System.Linq;
using AxinClaimsRules.Contracts.Extensions;

namespace AxinClaimsRules.Core.Extensions
{
    /// <summary>
    /// Runtime state populated by ExtensionsLoader.
    /// Used by CORE features to:
    /// - know what addons are loaded
    /// - integrate addon commands/flags
    /// </summary>
    public static class ExtensionsState
    {
        private static readonly HashSet<string> loadedIds = new HashSet<string>();
        private static readonly List<IAcSubCommand> subCommands = new List<IAcSubCommand>();
        private static readonly List<IFlagsModule> flagsModules = new List<IFlagsModule>();

        public static IReadOnlyCollection<string> LoadedIds => loadedIds;
        public static IReadOnlyList<IAcSubCommand> SubCommands => subCommands;
        public static IReadOnlyList<IFlagsModule> FlagsModules => flagsModules;

        public static bool IsLoaded(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            return loadedIds.Contains(id.Trim());
        }

        internal static void SetLoadedIds(IEnumerable<string> ids)
        {
            loadedIds.Clear();
            if (ids == null) return;
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                loadedIds.Add(id.Trim());
            }
        }

        internal static void ClearRegistrations()
        {
            subCommands.Clear();
            flagsModules.Clear();
        }

        internal static void AddSubCommand(IAcSubCommand cmd)
        {
            if (cmd == null) return;
            if (subCommands.Any(c => c.Key == cmd.Key)) return;
            subCommands.Add(cmd);
        }

        internal static void AddFlagsModule(IFlagsModule module)
        {
            if (module == null) return;
            if (flagsModules.Any(m => m.Id == module.Id)) return;
            flagsModules.Add(module);
        }
    }
}
