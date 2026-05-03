using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maxwell;


public static class AIAgentExtensions
{
    extension(ChatMessage source)
    {
        public string? TargetAgent => source.AdditionalProperties != null && source.AdditionalProperties.TryGetValue("targetAgent", out var agent) ? agent?.ToString() : null;
    }

    extension(AIAgent agent)
    {
                
        public string ToolName => $"call_{agent.SanitizedName}";
        public string ToolDescription => $"Invoke {agent.Name}. {agent.Description ?? ""}".Trim();

        public string? SanitizedName => Sanitize(agent.Name);

        private static string? Sanitize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return default;

            // 1. Normalizar para separar la tilde de la letra (e.g., 'á' -> 'a' + '´')
            string normalized = input.Normalize(NormalizationForm.FormD);

            // 2. Filtrar solo los caracteres que no sean "marcas" de acentuación
            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                if (char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            string result = sb.ToString();

            // 3. Aplicar limpieza de caracteres no permitidos (alfanuméricos, $, _)
            result = Regex.Replace(result, @"[^a-zA-Z0-9$_]", "_");

            // 4. Asegurar que no empiece con número
            if (char.IsDigit(result[0])) result = "_" + result;

            return result;
        }

    }
}