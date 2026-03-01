using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using AxinClaimsRules.Data.Registry;
using static AxinClaimsRules.AxinClaimsRulesMod;
using Vintagestory.API.MathTools;

namespace AxinClaimsRules
{
    internal static partial class AxinClaimCommands
    {
		private static AxinClaimsRules.Contracts.ICommandRenderer R()
		{
			return AxinClaimsRulesMod.RendererSvc
				?? new AxinClaimsRules.Features.Commands.Rendering.ChatVtmlRenderer();
		}

		private static string ResolveOwnerName(ICoreServerAPI api, string ownerPlayerUid, string fallback)
		{
			fallback ??= "";
			if (api == null) return fallback;
			if (string.IsNullOrWhiteSpace(ownerPlayerUid)) return fallback;

			// Prefer offline-safe player data manager
			try
			{
				var pdm = api.PlayerData;
				if (pdm != null && pdm.PlayerDataByUid != null && pdm.PlayerDataByUid.TryGetValue(ownerPlayerUid, out var pdata) && pdata != null)
				{
					var ln = pdata.LastKnownPlayername;
					if (!string.IsNullOrWhiteSpace(ln)) return ln;
				}
			}
			catch { }

			return fallback;
		}

		private static BlockPos ComputeTpAtClaimCenterOnGround(ICoreServerAPI api, BoundsInfo bounds)
		{
			int cx = (bounds.minX + bounds.maxX) / 2;
			int cz = (bounds.minZ + bounds.maxZ) / 2;

			int yTop = bounds.maxY;
			int yBottom = bounds.minY;
			if (yTop < yBottom) { int t = yTop; yTop = yBottom; yBottom = t; }

			// Default fallback (will be corrected if we find ground)
			int chosenX = cx;
			int chosenZ = cz;
			int chosenY = yTop + 1;

			try
			{
				var ba = api.World.BlockAccessor;
				bool IsAir(Block b) => b == null || b.Id == 0 || (b.Code?.Path == "air");
				bool IsSolid(Block b) => b != null && b.Id != 0 && !IsAir(b);

				bool TryColumn(int x, int z, int startY, int endY, out int yOut)
				{
					yOut = startY + 1;
					if (startY < endY) { int t = startY; startY = endY; endY = t; }
					for (int y = startY; y >= endY; y--)
					{
						var below = ba.GetBlock(new BlockPos(x, y, z));
						var above = ba.GetBlock(new BlockPos(x, y + 1, z));
						if (IsSolid(below) && IsAir(above))
						{
							yOut = y + 1;
							return true;
						}
					}
					return false;
				}

				// 1) Center column inside claim vertical range
				if (TryColumn(cx, cz, yTop, yBottom, out int yFound))
				{
					chosenY = yFound;
					return new BlockPos(chosenX, chosenY, chosenZ);
				}

				// 2) Expand search around center (small radius) within bounds.
				// Helps when claim center is in water/air or on structures.
				int maxRadius = 6;
				for (int r = 1; r <= maxRadius; r++)
				{
					for (int dx = -r; dx <= r; dx++)
					{
						int x1 = cx + dx;
						int z1 = cz + r;
						int z2 = cz - r;
						if (x1 < bounds.minX || x1 > bounds.maxX) continue;
						if (z1 >= bounds.minZ && z1 <= bounds.maxZ)
						{
							if (TryColumn(x1, z1, yTop, 0, out yFound)) { chosenX = x1; chosenZ = z1; chosenY = yFound; return new BlockPos(chosenX, chosenY, chosenZ); }
						}
						if (z2 >= bounds.minZ && z2 <= bounds.maxZ)
						{
							if (TryColumn(x1, z2, yTop, 0, out yFound)) { chosenX = x1; chosenZ = z2; chosenY = yFound; return new BlockPos(chosenX, chosenY, chosenZ); }
						}
					}
					for (int dz = -r + 1; dz <= r - 1; dz++)
					{
						int z1 = cz + dz;
						int x1 = cx + r;
						int x2 = cx - r;
						if (z1 < bounds.minZ || z1 > bounds.maxZ) continue;
						if (x1 >= bounds.minX && x1 <= bounds.maxX)
						{
							if (TryColumn(x1, z1, yTop, 0, out yFound)) { chosenX = x1; chosenZ = z1; chosenY = yFound; return new BlockPos(chosenX, chosenY, chosenZ); }
						}
						if (x2 >= bounds.minX && x2 <= bounds.maxX)
						{
							if (TryColumn(x2, z1, yTop, 0, out yFound)) { chosenX = x2; chosenZ = z1; chosenY = yFound; return new BlockPos(chosenX, chosenY, chosenZ); }
						}
					}
				}

				// 3) As a last resort: scan straight down from yTop to 0 at the center.
				if (TryColumn(cx, cz, yTop, 0, out yFound))
				{
					chosenY = yFound;
					return new BlockPos(chosenX, chosenY, chosenZ);
				}
			}
			catch { }

			return new BlockPos(chosenX, chosenY, chosenZ);
		}

		private static void EnsureAliasInMemory(ClaimsRegistry reg, PlayerClaimsEntry player, string ownerName, string axinClaimId)
{
    if (reg == null) return;
    reg.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(axinClaimId)) return;

    // If there is already an alias pointing to this claim, keep it.
    foreach (var kv in reg.aliases)
    {
        if (string.Equals(kv.Value, axinClaimId, StringComparison.OrdinalIgnoreCase)) return;
    }

    // Create a default alias: <OwnerName>_<shortId>
    ownerName = (ownerName ?? "claim").Trim();
    if (string.IsNullOrWhiteSpace(ownerName)) ownerName = "claim";

    string shortId = axinClaimId;
    int idx = axinClaimId.IndexOf(':');
    if (idx >= 0 && idx + 1 < axinClaimId.Length) shortId = axinClaimId[(idx + 1)..];
    if (shortId.Length > 6) shortId = shortId.Substring(0, 6);

    string baseAlias = $"{ownerName}_{shortId}";
    string alias = baseAlias;
    int n = 2;
    while (reg.aliases.ContainsKey(alias))
    {
        alias = $"{baseAlias}{n}";
        n++;
    }

    reg.aliases[alias] = axinClaimId;
}

private static string FindAliasForClaim(ClaimsRegistry reg, string axinClaimId)
		{
            if (reg?.aliases == null || string.IsNullOrWhiteSpace(axinClaimId)) return null;
            foreach (var kv in reg.aliases)
            {
                if (string.Equals(kv.Value, axinClaimId, StringComparison.OrdinalIgnoreCase)) return kv.Key;
            }
            return null;
        }

private static void EnsureFolderAndMoveAliases(ClaimsRegistry reg, string ownerPlayerUid, string resolvedOwnerName, string folderName, List<string> aliasesToMove)
{
    if (reg == null) return;

    reg.aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    reg.foldersOrder ??= new List<string>();

    folderName = (folderName ?? "").Trim();
    if (string.IsNullOrWhiteSpace(folderName)) folderName = (resolvedOwnerName ?? "player") + "Claims";
    if (folderName.Equals("Outside", StringComparison.OrdinalIgnoreCase)) folderName = "TraderClaims";

    if (!reg.foldersOrder.Any(x => x.Equals(folderName, StringComparison.OrdinalIgnoreCase)))
        reg.foldersOrder.Add(folderName);

    if (aliasesToMove == null) return;

    foreach (var alias in aliasesToMove)
    {
        if (string.IsNullOrWhiteSpace(alias)) continue;
        if (!reg.aliases.TryGetValue(alias, out var claimId)) continue;
        if (string.IsNullOrWhiteSpace(claimId) || !claimId.StartsWith("axin:", StringComparison.OrdinalIgnoreCase)) continue;

        string newKey = folderName + "/" + alias;
        if (reg.aliases.ContainsKey(newKey)) continue;

        reg.aliases.Remove(alias);
        reg.aliases[newKey] = claimId;
    }
}

public static TextCommandResult CmdClaimsHelp(ICoreServerAPI api, IServerPlayer sp)
{
    // E6-B.3: Help UX con link clicable (renderer blindado)
    string root = AxinClaimsRulesMod.AliasCfg?.rootAlias;
    if (string.IsNullOrWhiteSpace(root)) root = "ac";

    string cmdExport = root + " claims export";
    string cmdReload = root + " reload";

    // Nota: en VTML command:/// se ejecuta el comando sin "/" inicial.
    string line1 = LangManager.T(
        "claims.help",
        "Uso: /ac claims export  — Exporta los claims del mundo a ClaimsRegistry.json (sin pisar flags/aliases/folders)."
    );

    // Añadimos links clicables sin introducir entidades (&apos;) en el href.
    string line2 =
        R().LinkText("/" + cmdExport, cmdExport) +
        "  |  " +
        R().LinkText("/" + cmdReload, cmdReload);

    return TextCommandResult.Success(line1 + "\n" + line2);
}

public static TextCommandResult CmdClaimsExport(ICoreServerAPI api, IServerPlayer sp)
{
    if (api == null || sp == null) return TextCommandResult.Error("Invalid caller.");

    // Load latest from disk to avoid overwriting admin edits
    ClaimsRegistry reg = null;
    reg = RegistryStore.TryLoadClaimsRegistry(api);
    reg ??= AxinClaimsRulesMod.RegistryCfg ?? ClaimsRegistry.CreateDefault();
    AxinClaimsRulesMod.RegistryCfg = reg;

    // NOTE: trader export is controlled by Config.json (CommandConfig), not GlobalConfig.json.
    bool includeTraders = AxinClaimsRulesMod.CmdCfg?.exportTraderClaims ?? false;
    return ClaimsExportSvc.ExportWorldClaimsToRegistry(api, reg, includeTraders);
}
// (E7.2d) Export logic extracted to Data/Registry/Export/ClaimsExportService.cs
    }
}
