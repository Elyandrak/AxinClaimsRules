using System;

namespace AxinClaimsRules.Contracts
{
    /// <summary>
    /// AXIN-AI-ARCH (E6-B / Contratos A5):
    /// Frontera mínima para renderizar VTML/links en chat.
    /// Objetivo: que los handlers NO generen strings/VTML "a mano".
    /// </summary>
    public interface ICommandRenderer
    {
        string Escape(string raw);
        string Strong(string innerText);
        /// <summary>
        /// Enlace VTML sin corchetes automáticos (para alias/TP, etc.).
        /// </summary>
        string LinkText(string text, string command);
        string Link(string label, string command);
    }
}
