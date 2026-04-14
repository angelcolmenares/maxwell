using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Maxwell;
/// <summary>
/// Represents the payload sent to the assistant. 
/// </summary>
public class AssistantMessage
{

    [JsonPropertyName("text")]
    [Description("The text instruction, question, or prompt for the assistant.")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("uri")]
    [Description("Optional public URL or the LOCAL SYSTEM PATH of the file (e.g., 'C:\\path\\to\\img.jpg'). The system's backend will handle the local file reading.")]
    public string? Uri { get; set; }

    public override string ToString()
    {        
        return $"{{'text':'{Text}'" + (string.IsNullOrEmpty(Uri)? "": $"uri':'{Uri}'") + "}";
    }
}
