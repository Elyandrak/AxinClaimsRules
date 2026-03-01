using Vintagestory.API.Server;

namespace AxinClaimsRules.Contracts.Extensions
{
    /// <summary>
    /// Stable API exposed by AxinClaimsRules CORE for expansion mods.
    /// Rules:
    /// - Plugins must NOT write JSON directly.
    /// - Data is preserved even if plugin is removed.
    /// </summary>
    public interface IAxinClaimsRulesApi
    {
        ICoreServerAPI Sapi { get; }
        CommandConfig CmdCfg { get; }
        GlobalConfig GlobalCfg { get; }
        ClaimsOverrides OverridesCfg { get; }
        ClaimsRegistry RegistryCfg { get; }
        CommandAliasConfig AliasCfg { get; }

        string LangT(string key, string fallback);
        string LangTf(string key, string fallback, params object[] args);
    }
}
