using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Server;
using AxinClaimsRules.Contracts.Extensions;

namespace AxinClaimsRules.Core.Extensions
{
    public static class ExtensionsLoader
    {
        public static void Load(ICoreServerAPI api)
        {
            var modSystems = EnumerateModSystems(api?.ModLoader);

            List<IRuleExtension> extensions = modSystems
                .OfType<IRuleExtension>()
                .ToList();

            if (extensions.Count == 0)
            {
                api?.Logger?.Notification("[AxinClaimsRules] No rule extensions detected.");
                return;
            }

            var host = new DefaultRulesHost(api);

            foreach (var ext in extensions)
            {
                try
                {
                    api.Logger.Notification("[AxinClaimsRules] Loading extension: {0}", ext.Id);
                    ext.Register(host, api);
                }
                catch (Exception ex)
                {
                    api.Logger.Warning("[AxinClaimsRules] Extension '{0}' failed: {1}", ext.Id, ex);
                }
            }
        }

        private static IEnumerable<object> EnumerateModSystems(object modLoader)
        {
            if (modLoader == null) yield break;

            var t = modLoader.GetType();

            // 1) Method GetModSystems()
            var mi = t.GetMethod("GetModSystems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (mi != null)
            {
                object result = null;
                try { result = mi.Invoke(modLoader, null); } catch { }
                foreach (var o in EnumerateUnknownEnumerable(result)) yield return o;
                yield break;
            }

            // 2) Property ModSystems
            var pi = t.GetProperty("ModSystems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pi != null)
            {
                object result = null;
                try { result = pi.GetValue(modLoader); } catch { }
                foreach (var o in EnumerateUnknownEnumerable(result)) yield return o;
                yield break;
            }

            // 3) Field modSystems
            var fi = t.GetField("modSystems", BindingFlags.Instance | BindingFlags.NonPublic)
                  ?? t.GetField("_modSystems", BindingFlags.Instance | BindingFlags.NonPublic)
                  ?? t.GetField("modsSystems", BindingFlags.Instance | BindingFlags.NonPublic);

            if (fi != null)
            {
                object result = null;
                try { result = fi.GetValue(modLoader); } catch { }
                foreach (var o in EnumerateUnknownEnumerable(result)) yield return o;
                yield break;
            }
        }

        private static IEnumerable<object> EnumerateUnknownEnumerable(object maybeEnumerable)
        {
            if (maybeEnumerable == null) yield break;

            if (maybeEnumerable is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item != null) yield return item;
                }
            }
        }

        internal class DefaultRulesHost : IRulesHost
        {
            private readonly ICoreServerAPI api;

            public DefaultRulesHost(ICoreServerAPI api) => this.api = api;

            public void RegisterInfo(string message)
            {
                api?.Logger?.Notification("[AxinClaimsRules][Extension] {0}", message);
            }

            public void RegisterAcSubCommand(IAcSubCommand cmd)
            {
                ExtensionsState.AddSubCommand(cmd);
            }

            public void RegisterFlagsModule(IFlagsModule module)
            {
                ExtensionsState.AddFlagsModule(module);
            }
        }
    }
}
