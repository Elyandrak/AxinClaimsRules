using System.Collections.Generic;

namespace AxinClaimsRules
{
    public class CommandAliasConfig
    {
        public string _description { get; set; } =
            "AxinClaimsRules - Alias de comandos. Edita este archivo para abreviar subcomandos. Root alias por defecto: \'ac\'.";
        public int schemaVersion { get; set; } = 2;

        // Alias adicional del root comando (además de /ac fijo). Si está vacío, se ignora.
        public string rootAlias { get; set; } = "ac";

        // Alias de subcomandos: clave = canónico (id,list,claims,tp,settp,flags,folder,reload), valor = alias.
        // Si no quieres alias, deja igual que el comando.
        public Dictionary<string, string> subAliases { get; set; } = new Dictionary<string, string>()
        {
            { "id", "id" },
            { "list", "list" },
            { "claims", "claims" },
            { "tp", "tp" },
            { "settp", "settp" },
            { "flags", "flags" },
            { "flag", "flag" },
            { "folder", "folder" },
            // UX: mostrar "refresh" en lugar de "sync".
            // El comando canónico es "reload", pero el alias por defecto es "refresh".
            { "reload", "refresh" }
        };

        // Alias de comandos raíz extra (DESHABILITADO en v0.3.5+): no se registran shortcuts raíz (ej. /flag).
        // Se mantiene el campo para compatibilidad con Alias.json antiguos, pero se ignora.
        public Dictionary<string, string> extraRootAliases { get; set; } = new Dictionary<string, string>();

public static CommandAliasConfig CreateDefault() => new CommandAliasConfig();
    }

}
