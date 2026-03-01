using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Server;
using AxinClaimsRules.Data.Registry;

namespace AxinClaimsRules.Data.Registry.Sync
{
    /// <summary>
    /// AXIN-AI-ARCH (E7.4d): alias-related ensure/resolve helpers.
    /// Extracted from RegistrySync.cs.
    /// </summary>
    internal static class AliasEnsureService
    {
                public static string EnsureAliasForClaim(ICoreServerAPI api, string ownerPlayerUid, string ownerName, string axinClaimId)
                {
                    if (api == null) return "";
                    var reg = AxinClaimsRulesMod.RegistryCfg;
                    if (reg == null) return "";
                    if (string.IsNullOrWhiteSpace(ownerPlayerUid)) ownerPlayerUid = "unknown";

                    reg.players ??= new Dictionary<string, PlayerClaimsEntry>();
                    reg.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    if (!reg.players.TryGetValue(ownerPlayerUid, out var player))
                    {
                        player = new PlayerClaimsEntry();
                        reg.players[ownerPlayerUid] = player;
                    }

                    player.lastKnownName = ownerName ?? player.lastKnownName ?? "";

                    // If already mapped, return existing key (avoid duplicates)
                    try
                    {
                        foreach (var kv in reg.aliases)
                        {
                            if (string.Equals((kv.Value ?? "").Trim(), axinClaimId, StringComparison.OrdinalIgnoreCase))
                                return kv.Key;
                        }
                    }
                    catch { }

                    // Base alias from ownerName (strip "Player ", remove spaces/symbols)
                    string baseName = MakeOwnerBaseName(ownerName);

                    // Ensure unique alias in GLOBAL dictionary
                    int n = 1;
                    string alias;
                    do
                    {
                        alias = baseName + n;
                        n++;
                    }
                    while (reg.aliases.ContainsKey(alias));

                    reg.aliases[alias] = axinClaimId;

                    reg.updatedAtUtc = DateTime.UtcNow.ToString("o");
                    RegistryStore.SaveClaimsRegistry(api, reg);

                    return alias;
                }

                public static string GetAliasForClaim(PlayerClaimsEntry player, string axinClaimId)
                {
                    if (string.IsNullOrWhiteSpace(axinClaimId)) return "(noalias)";

                    var reg = AxinClaimsRulesMod.RegistryCfg;
                    if (reg?.aliases == null) return "(noalias)";

                    foreach (var kv in reg.aliases)
                    {
                        if (string.Equals(kv.Value, axinClaimId, StringComparison.OrdinalIgnoreCase))
                            return kv.Key;
                    }

                    return "(noalias)";
                }

                public static bool TryResolveAlias(string alias, out string ownerPlayerUid, out string ownerName, out string axinClaimId, out ClaimEntry entry)
                {
                    ownerPlayerUid = "";
                    ownerName = "";
                    axinClaimId = null;
                    entry = null;

                    var reg = AxinClaimsRulesMod.RegistryCfg;
                    if (reg == null) return false;

                    if (string.IsNullOrWhiteSpace(alias)) return false;
                    alias = alias.Trim();

                    // v2 format: aliases are global at root
                    if (reg.aliases == null) reg.aliases = new Dictionary<string, string>();

                    bool TryGetByKey(string key, out string axinId)
                    {
                        axinId = null;
                        if (reg.aliases == null) return false;
                        if (reg.aliases.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                        {
                            axinId = v;
                            return true;
                        }
                        return false;
                    }

                    // 1) exact key
                    if (TryGetByKey(alias, out var idExact))
                    {
                        axinClaimId = idExact;
                        return TryFindClaimEntry(reg, axinClaimId, out ownerPlayerUid, out ownerName, out entry);
                    }

                    // 2) fallback: match short alias inside a folder (FolderName/alias). If ambiguous, return false.
                    string suffix = "/" + alias;
                    string foundKey = null;
                    string foundId = null;

                    foreach (var kv in reg.aliases)
                    {
                        if (kv.Key != null && kv.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        {
                            if (foundKey != null)
                            {
                                // ambiguous
                                foundKey = null;
                                break;
                            }
                            foundKey = kv.Key;
                            foundId = kv.Value;
                        }
                    }

                    if (foundKey != null && !string.IsNullOrWhiteSpace(foundId))
                    {
                        axinClaimId = foundId;
                        return TryFindClaimEntry(reg, axinClaimId, out ownerPlayerUid, out ownerName, out entry);
                    }

                    return false;
                }

                private static bool TryFindClaimEntry(ClaimsRegistry reg, string axinClaimId, out string ownerUid, out string ownerName, out ClaimEntry entry)
                {
                    ownerUid = "";
                    ownerName = "";
                    entry = null;
                    if (reg?.players == null) return false;
                    if (string.IsNullOrWhiteSpace(axinClaimId)) return false;

                    foreach (var p in reg.players)
                    {
                        var pe = p.Value;
                        if (pe?.claims == null) continue;
                        if (pe.claims.TryGetValue(axinClaimId, out var ce) && ce != null)
                        {
                            ownerUid = p.Key;
                            ownerName = pe.lastKnownName ?? "";
                            entry = ce;
                            return true;
                        }
                    }
                    return false;
                }

                internal static string MakeOwnerBaseName(string ownerName)
                {
                    string s = (ownerName ?? "").Trim();
                    if (s.StartsWith("Player ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
                    s = s.Trim();
                    if (s.Length == 0) s = "player";

                    var sb = new StringBuilder();
                    foreach (var ch in s)
                    {
                        if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                    }
                    var outS = sb.ToString();
                    if (outS.Length == 0) outS = "player";
                    if (outS.Length > 16) outS = outS.Substring(0, 16);
                    return outS;
                }
    }
}
