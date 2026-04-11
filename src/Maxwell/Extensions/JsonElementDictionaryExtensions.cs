
using System.Text.Json;

namespace Maxwell;

public static class JsonElementDictionaryExtensions
{
    extension(IReadOnlyDictionary<string, JsonElement> options)
    {
        public string GetString(string key, string defaultValue = "")
        {
            var match = options
                .FirstOrDefault(kv => kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            return match.Key is not null
                ? match.Value.GetString() ?? defaultValue
                : defaultValue;
        }

        public float? GetFloat(string key)
        {
            var match = options
                .FirstOrDefault(kv => kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            return match.Key is not null
                ? match.Value.GetSingle() : null;
        }

        public int? GetInt(string key)
        {
            var match = options
                .FirstOrDefault(kv => kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            return match.Key is not null
                ? match.Value.GetInt32() : null;
        }

    }

}
