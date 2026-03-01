using AxinClaimsRules.Contracts;

namespace AxinClaimsRules.Features.Commands.Rendering
{
    /// <summary>
    /// Implementación VTML conservadora + blindada para atributos.
    /// - No intenta colorear (limitación conocida del chat de VS).
    /// - Escapa texto y también el atributo href (comando) para evitar romper VTML.
    /// </summary>
    public sealed class ChatVtmlRenderer : ICommandRenderer
    {
        public string Escape(string raw)
        {
            if (raw == null) return string.Empty;

            // Text-node escaping (seguro para contenido entre tags)
            return raw
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static string EscapeCmdAttr(string raw)
        {
            if (raw == null) return string.Empty;

            // IMPORTANTE:
            // - Este atributo contiene el *comando* que VS ejecutará literalmente.
            // - Si convertimos comillas simples a &apos; (o & a &amp;), el parser del comando
            //   recibirá esa entidad como texto y romperá aliases (ej: '&apos;folder/...' ).
            //
            // Por tanto: escapamos SOLO lo mínimo para no romper el VTML:
            // - eliminamos CR/LF/TAB (reales y secuencias literales)
            // - escapamos comillas dobles y < >
            // - NO escapamos comillas simples ni '&'

            var s = raw
                // caracteres reales:
                .Replace("\r\n", " ")
                .Replace("\n\r", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ");

            // por si llegan secuencias literales \\r \\n \\t (dos caracteres)
            s = s.Replace("\\r", " ").Replace("\\n", " ").Replace("\\t", " ");

            return s
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        public string Strong(string innerText)
        {
            return $"<strong>{Escape(innerText)}</strong>";
        }

        public string LinkText(string text, string command)
        {
            // VS chat soporta enlaces command:/// mediante atributo href.
            // El comando va en atributo: debe escaparse como atributo.
            var safeCmd = EscapeCmdAttr(command ?? string.Empty).Trim();

            // Nota: el texto es nodo de texto, no atributo.
            return $"<a href=\"command:///{safeCmd}\">{Escape(text)}</a>";
        }

        public string Link(string label, string command)
        {
            // Convención UX del mod: botones/acciones en corchetes.
            return LinkText($"[{label}]", command);
        }
    }
}
