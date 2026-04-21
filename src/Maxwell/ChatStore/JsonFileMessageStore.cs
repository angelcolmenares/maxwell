using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Maxwell;

public class JsonFileMessageStore : IMessageStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new TruncatedDateTimeOffsetConverter() }
    };

    public JsonFileMessageStore(string filePath)
    {
        _filePath = filePath;
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }

    public async ValueTask<List<ChatMessage>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath)) return new List<ChatMessage>();

        try
        {
            using FileStream stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<List<ChatMessage>>(stream, _options, ct) ?? new();
        }
        catch (JsonException) { return new(); }
    }

    public async ValueTask SaveAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default)
    {
        // Cargamos existentes para merge (evitar duplicados)
        var existing = await LoadAsync(ct);
        
        foreach (var msg in messages)
        {
            // Verificación simple para no duplicar mensajes en el archivo
            if (!existing.Any(e => e.Role == msg.Role && e.Contents.SequenceEqual(msg.Contents)))
            {
                existing.Add(msg);
            }
        }

        await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(existing, _options), ct);
    }
}
