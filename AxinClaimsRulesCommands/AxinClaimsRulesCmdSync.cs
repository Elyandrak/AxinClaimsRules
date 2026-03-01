using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace AxinClaimsRules
{
    internal static partial class AxinClaimCommands
    {

public static TextCommandResult 
CmdSync(ICoreServerAPI api, IServerPlayer sp)
{
    // /ac sync: recarga TODO desde disco (Config + Registry + Lang + Global/Overrides/Alias) de forma NO destructiva.
    // No resetea valores del admin: solo lee y normaliza nulos en memoria.
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

        api.Logger.Notification("[AxinClaimsRules] /ac sync: reloaded Config + Registry + Lang. players={0} claims={1} lang={2}", players, claims, lang);
        return TextCommandResult.Success($"Sync OK. lang={lang} players={players} claims={claims}");
    }
    catch (Exception e)
    {
        api.Logger.Warning("[AxinClaimsRules] /ac sync failed: {0}", e);
        return TextCommandResult.Error("Error en /ac sync: " + e.Message);
    }
}
    }
}
