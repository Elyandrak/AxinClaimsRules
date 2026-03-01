using System;
using AxinClaimsRules.Contracts.Extensions;
using Vintagestory.API.Server;

namespace AxinClaimsRules.Core
{
    /// <summary>
    /// Singleton API for expansion mods. Initialized once on StartServerSide.
    /// </summary>
    public static class AxinClaimsRulesApi
    {
        public static IAxinClaimsRulesApi Instance { get; private set; }

        internal static void Initialize(
            ICoreServerAPI sapi,
            CommandConfig cmdCfg,
            GlobalConfig globalCfg,
            ClaimsOverrides overridesCfg,
            ClaimsRegistry registryCfg,
            CommandAliasConfig aliasCfg)
        {
            Instance = new Impl(sapi, cmdCfg, globalCfg, overridesCfg, registryCfg, aliasCfg);
        }

        private sealed class Impl : IAxinClaimsRulesApi
        {
            public ICoreServerAPI Sapi { get; }
            public CommandConfig CmdCfg { get; }
            public GlobalConfig GlobalCfg { get; }
            public ClaimsOverrides OverridesCfg { get; }
            public ClaimsRegistry RegistryCfg { get; }
            public CommandAliasConfig AliasCfg { get; }

            public Impl(
                ICoreServerAPI sapi,
                CommandConfig cmdCfg,
                GlobalConfig globalCfg,
                ClaimsOverrides overridesCfg,
                ClaimsRegistry registryCfg,
                CommandAliasConfig aliasCfg)
            {
                Sapi = sapi;
                CmdCfg = cmdCfg;
                GlobalCfg = globalCfg;
                OverridesCfg = overridesCfg;
                RegistryCfg = registryCfg;
                AliasCfg = aliasCfg;
            }

            public string LangT(string key, string fallback) => LangManager.T(key, fallback);

            public string LangTf(string key, string fallback, params object[] args)
                => LangManager.Tf(key, fallback, args);
        }
    }
}
