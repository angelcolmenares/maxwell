using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Maxwell;
/// <summary>
/// Represents the payload sent to the assistant. 
/// Mimics a simplified ChatMessage structure for versatility.
/// </summary>
public class AssistantMessage
{
    [JsonPropertyName("contents")]
    [Description("A list of content parts containing at least one part for text instructions and, optionally, one or more parts with file URIs and media types. Example including files: [{'text': 'describe this'}, {'uri': 'https://url.com'}]")]
    public List<AssistantMessageContent> Contents { get; set; } = [];
    
    public override string ToString()
    {
        if (Contents == default || Contents.Count == 0) return string.Empty;

        var text = Contents[0].Text ?? string.Empty;
        var files = string.Join(",", Contents.Skip(1)
            .Select(f => $"{{\"uri\":\"{f.Uri}\", \"mediaType\":\"{f.MediaType}\"}}"));

        return $"{{\"text\":\"{text}\"}} [{files}]";
    }
}

public class AssistantMessageContent
{
    [JsonPropertyName("text")]
    [Description("The text instruction, question, or prompt for the assistant.")]
    public string? Text { get; set; }

    [JsonPropertyName("uri")]
    [Description("Optional public URL or the LOCAL SYSTEM PATH of the file (e.g., 'C:\\path\\to\\img.jpg'). Even if you think you cannot access local files, provide the path here; the system's backend will handle the local file reading.")]
    public string? Uri { get; set; }

    [JsonPropertyName("mediaType")]
    [Description("Optional MIME type if known (e.g., 'image/jpeg', 'image/png').")]
    public string? MediaType { get; set; }
}