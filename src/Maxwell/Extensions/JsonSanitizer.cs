using System.Text;
using System.Text.Json;

namespace Maxwell;
public static class JsonSanitizer
{
    public static string Sanitize(string rawJson)
    {
        string unwrapped = UnwrapIfNeeded(rawJson);
        return SanitizeJsonString(unwrapped);
    }

    private static string UnwrapIfNeeded(string raw)
    {
        string trimmed = raw.Trim();
        if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
        {
            try
            {
                string? inner = JsonSerializer.Deserialize<string>(trimmed);
                if (inner != null) return inner;
            }
            catch { }
        }
        return trimmed;
    }

    /// <summary>
    /// Walks through the JSON character by character and escapes unescaped quotes
    /// inside string values.
    /// </summary>
    private static string SanitizeJsonString(string json)
    {
        var sb = new StringBuilder(json.Length);
        int i = 0;

        while (i < json.Length)
        {
            char c = json[i];

            if (c == '"')
            {
                // Start of a JSON string — copy the opening quote
                sb.Append('"');
                i++;

                // Read the string content until we find the real closing quote
                while (i < json.Length)
                {
                    char sc = json[i];

                    if (sc == '\\')
                    {
                        // Escape sequence — copy both characters as-is
                        sb.Append(sc);
                        i++;
                        if (i < json.Length)
                        {
                            sb.Append(json[i]);
                            i++;
                        }
                        continue;
                    }

                    if (sc == '"')
                    {
                        // Is this quote the real closing quote of the string?
                        if (IsClosingQuote(json, i))
                        {
                            sb.Append('"');
                            i++;
                            break; // end of JSON string
                        }
                        else
                        {
                            // Stray quote inside the content — escape it
                            sb.Append("\\\"");
                            i++;
                            continue;
                        }
                    }

                    // Escape literal control characters
                    if (sc == '\n') { sb.Append("\\n"); i++; continue; }
                    if (sc == '\r') { sb.Append("\\r"); i++; continue; }
                    if (sc == '\t') { sb.Append("\\t"); i++; continue; }

                    sb.Append(sc);
                    i++;
                }
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Determines whether the quote at position <paramref name="pos"/> is the real
    /// closing quote of a JSON string. A closing quote is always followed (ignoring
    /// whitespace) by a valid JSON token: ':', ',', '}', ']', or end of input.
    /// </summary>
    private static bool IsClosingQuote(string json, int pos)
    {
        int next = pos + 1;

        // Skip whitespace
        while (next < json.Length && json[next] is ' ' or '\t' or '\n' or '\r')
            next++;

        if (next >= json.Length) return true;

        char after = json[next];
        return after is ':' or ',' or '}' or ']';
    }
}