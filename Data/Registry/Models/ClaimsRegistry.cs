using System;
using System.Collections.Generic;

namespace AxinClaimsRules
{
    public class ClaimsRegistry
    {
        public string _description { get; set; } =
            "AxinClaimsRules - Registro de claims observado (info) + ClaimRules editables. Formato v2: aliases y foldersOrder son globales (raíz). Clave principal: playerUid.";

        public int schemaVersion { get; set; } = 2;

        public string updatedAtUtc { get; set; } = "";

        // Global: alias -> axinClaimId (stored only once in the file)
        public Dictionary<string, string> aliases { get; set; } = new Dictionary<string, string>();

        // Global: ordered folders list (stored only once in the file)
        public List<string> foldersOrder { get; set; } = new List<string>();

        // Global: ordered aliases for Outside view (aliases without folder prefix)
        public List<string> outsideOrder { get; set; } = new List<string>();

        // Global: ordered aliases per folder (folderName -> [aliasKey in that folder])
        public Dictionary<string, List<string>> folderOrders { get; set; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // playerUid -> entry
        public Dictionary<string, PlayerClaimsEntry> players { get; set; } = new Dictionary<string, PlayerClaimsEntry>();

        public static ClaimsRegistry CreateDefault() => new ClaimsRegistry();
    }

}
