using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AxinClaimsRules
{
    internal static partial class AxinClaimCommands
    {
        // /ac reload : reload Config + Registry + Lang from disk (non-destructive)
        public static TextCommandResult CmdReload(ICoreServerAPI api, IServerPlayer sp)
        {
            if (api == null) return TextCommandResult.Error("No api.");

            try
            {
                AxinClaimsRulesMod.ReloadAllFromDisk(api);

                var reg = AxinClaimsRulesMod.RegistryCfg;
                int players = reg?.players?.Count ?? 0;
                int claims = 0;
                if (reg?.players != null)
                {
                    foreach (var p in reg.players.Values)
                    {
                        if (p?.claims != null) claims += p.claims.Count;
                    }
                }

                string lang = "en";
                try { lang = AxinClaimsRulesMod.CmdCfg?.language ?? "en"; } catch { }

                api.Logger.Notification("[AxinClaimsRules] /ac reload: reloaded Config + Registry + Lang. players={0} claims={1} lang={2}", players, claims, lang);

                // i18n
                string msg = LangManager.Tf(
                    "reload.ok",
                    "Reload OK. lang={0} players={1} claims={2}",
                    lang, players, claims
                );

                return TextCommandResult.Success(msg);
            }
            catch (Exception e)
            {
                api.Logger.Warning("[AxinClaimsRules] /ac reload failed: {0}", e);

                string msg = LangManager.Tf(
                    "reload.err",
                    "Reload ERROR: {0}",
                    e.Message ?? "unknown"
                );

                return TextCommandResult.Error(msg);
            }
        }
    }
}
