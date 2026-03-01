using AxinClaimsRules.Contracts;
using AxinClaimsRules.Features.Commands.Rendering;

namespace AxinClaimsRules.Infra
{
    /// <summary>
    /// AXIN UI-1 — Mini identidad visual AXIN (VTML).
    /// Objetivo: centralizar VTML/colores para NO dispersar <font> por el código.
    /// NOTA: esto es SOLO capa visual (no cambia lógica).
    /// </summary>
    internal static class UiTheme
    {
        // Paleta AXIN (consensuada)
        private const string C_HEADER = "cornflowerblue";
        private const string C_TRUE = "lightgreen";
        private const string C_FALSE = "lightcoral";
        private const string C_LINK = "#c5893c";
        private const string C_ERROR = "red";
        private const string C_INFO = "cornflowerblue";
        private const string C_SUCCESS = "lightgreen";

        private static ICommandRenderer R(ICommandRenderer r)
        {
            return r ?? (AxinClaimsRulesMod.RendererSvc ?? new ChatVtmlRenderer());
        }

        internal static string Header(ICommandRenderer r, string text)
        {
            var ren = R(r);
            return Font(C_HEADER, ren.Strong(text));
        }

        internal static string Info(ICommandRenderer r, string text)
        {
            var ren = R(r);
            return Font(C_INFO, ren.Escape(text));
        }

        internal static string Success(ICommandRenderer r, string text)
        {
            var ren = R(r);
            return Font(C_SUCCESS, ren.Escape(text));
        }

        internal static string Error(ICommandRenderer r, string text)
        {
            var ren = R(r);
            return Font(C_ERROR, ren.Escape(text));
        }

        /// <summary>
        /// Envuelve un fragmento VTML ya-renderizado (ej. <a href="command:///...">...</a>)
        /// con el color de enlaces AXIN.
        /// </summary>
        internal static string LinkWrap(string vtmlAlreadyRendered)
        {
            return LinkWrapColor(vtmlAlreadyRendered, C_LINK);
        }

        /// <summary>
        /// Intenta aplicar color a un fragmento <a href="command:///...">...</a> sin romper la clicabilidad.
        /// Estrategia:
        /// 1) Inyectar atributo color en <a ...> (algunos clientes lo respetan).
        /// 2) Inyectar <font> *dentro* del <a> como segundo intento.
        /// 3) Fallback: sin tocar (mantener link default) / o envolver completo si no es link.
        /// </summary>
        internal static string LinkWrapColor(string vtmlAlreadyRendered, string color)
        {
            if (string.IsNullOrEmpty(vtmlAlreadyRendered)) return vtmlAlreadyRendered;
            if (string.IsNullOrWhiteSpace(color)) color = C_LINK;

            if (vtmlAlreadyRendered.Contains("<a ") && vtmlAlreadyRendered.Contains("</a>"))
            {
                string v = vtmlAlreadyRendered;

                // 1) intentar atributo color en el <a ...>
                int openEnd = v.IndexOf('>');
                if (openEnd > -1)
                {
                    string openTag = v.Substring(0, openEnd); // sin '>'
                    if (!openTag.Contains(" color=", System.StringComparison.OrdinalIgnoreCase))
                    {
                        v = openTag + $" color='{color}'>" + v.Substring(openEnd + 1);
                    }
                }

                // 2) intentar <font> dentro del <a> (algunos renderers solo respetan esto)
                int openEnd2 = v.IndexOf('>');
                if (openEnd2 > -1)
                {
                    string before = v.Substring(0, openEnd2 + 1);
                    string after = v.Substring(openEnd2 + 1);

                    if (!after.StartsWith("<font", System.StringComparison.OrdinalIgnoreCase))
                    {
                        // cerrar font justo antes de </a>
                        return before + $"<font color='{color}'>" + after.Replace("</a>", "</font></a>");
                    }
                }

                return v;
            }

            // No es link: envolver normal
            return Font(color, vtmlAlreadyRendered);
        }

        internal static string Bool(ICommandRenderer r, bool value)
        {
            var ren = R(r);
            var txt = value ? "true" : "false";
            return Font(value ? C_TRUE : C_FALSE, ren.Escape(txt));
        }

        
        /// <summary>
        /// VS chat no respeta colores dentro de <a>. Para mostrar color sin romper la clicabilidad,
        /// añadimos un "marker" coloreado FUERA del enlace.
        /// Ej: <font color='...'>↑</font> <a href='command:///...'>[Arriba]</a>
        /// </summary>
        internal static string LinkWithMarker(string marker, string linkVtmlAlreadyRendered, string markerColor = null)
        {
            if (string.IsNullOrEmpty(linkVtmlAlreadyRendered)) return linkVtmlAlreadyRendered;
            var c = string.IsNullOrWhiteSpace(markerColor) ? C_LINK : markerColor;
            var m = string.IsNullOrWhiteSpace(marker) ? "•" : marker;
            return Font(c, m) + " " + linkVtmlAlreadyRendered;
        }

        /// <summary>
        /// Variante compacta sin espacio extra (útil para envolver el marker como parte del "botón" visual).
        /// </summary>
        internal static string LinkWithMarkerCompact(string marker, string linkVtmlAlreadyRendered, string markerColor = null)
        {
            if (string.IsNullOrEmpty(linkVtmlAlreadyRendered)) return linkVtmlAlreadyRendered;
            var c = string.IsNullOrWhiteSpace(markerColor) ? C_LINK : markerColor;
            var m = string.IsNullOrWhiteSpace(marker) ? "•" : marker;
            return Font(c, m) + linkVtmlAlreadyRendered;
        }


        /// <summary>
        /// Variante robusta: colorea marker y los corchetes [ ] FUERA del enlace.
        /// Nota: El texto dentro del <a> seguirá con color forzado por el cliente, pero los corchetes/marker sí se verán.
        /// </summary>
        internal static string BracketedLinkWithMarker(string marker, string linkVtmlAlreadyRendered, string markerColor, string bracketColor)
        {
            if (string.IsNullOrEmpty(linkVtmlAlreadyRendered)) return linkVtmlAlreadyRendered;

            var m = string.IsNullOrWhiteSpace(marker) ? "•" : marker;
            var mc = string.IsNullOrWhiteSpace(markerColor) ? C_LINK : markerColor;
            var bc = string.IsNullOrWhiteSpace(bracketColor) ? mc : bracketColor;

            // Marker + [ + link + ]
            return Font(mc, m) + " " + Font(bc, "[") + linkVtmlAlreadyRendered + Font(bc, "]");
        }

internal static string BoolText(ICommandRenderer r, string valueLowerOrRaw)
        {
            var ren = R(r);
            var v = (valueLowerOrRaw ?? string.Empty).Trim();
            var low = v.ToLowerInvariant();
            if (low == "true") return Font(C_TRUE, ren.Escape(v));
            if (low == "false") return Font(C_FALSE, ren.Escape(v));
            return ren.Escape(v);
        }

        private static string Font(string color, string innerVtml)
        {
            return $"<font color='{color}'>{innerVtml}</font>";
        }
    }
}
