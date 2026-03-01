using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Vintagestory.API.Config;

namespace AxinClaimsRules.Infra
{
    internal static class ConfigLanguageReader
    {
        internal static string TryReadLanguageFromConfigRaw(string configRelativePath)
        {
            try
            {
                var fullPath = Path.Combine(GamePaths.ModConfig, configRelativePath);
                if (!File.Exists(fullPath)) return null;

                var json = File.ReadAllText(fullPath, Encoding.UTF8);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (!prop.Name.Equals("Language", StringComparison.OrdinalIgnoreCase)) continue;
                    if (prop.Value.ValueKind != JsonValueKind.String) return null;

                    var lang = prop.Value.GetString();
                    return string.IsNullOrWhiteSpace(lang) ? null : lang.Trim();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
