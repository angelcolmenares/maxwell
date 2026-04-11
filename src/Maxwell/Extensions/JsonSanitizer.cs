using System.Text.RegularExpressions;

public static class JsonSanitizer
{
    // Encuentra todos los strings JSON (entre comillas) y sana solo su contenido
    private static readonly Regex JsonStringPattern = new(
        @"""((?:[^""\\]|\\.)*)""",
        RegexOptions.Compiled
    );

    public static string Sanitize(string rawJson)
    {
        // Reemplaza cada string encontrado, escapando las barras invertidas
        // que NO estén ya escapadas dentro de él
        return JsonStringPattern.Replace(rawJson, match =>
        {
            string inner = match.Groups[1].Value;
            
            // Escapamos backslashes que no sean ya secuencias de escape válidas
            string sanitized = SanitizeStringValue(inner);
            
            return $"\"{sanitized}\"";
        });
    }

    private static readonly Regex UnescapedBackslash = new(
        @"\\(?![""\\\/bfnrtu])",  // \ NOT seguido de char de escape válido JSON
        RegexOptions.Compiled
    );

    private static string SanitizeStringValue(string value)
    {
        // Reemplaza \c, \U, \p, etc. → \\c, \\U, \\p (los escapa correctamente)
        return UnescapedBackslash.Replace(value, @"\\");
    }
}