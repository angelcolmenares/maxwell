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
    [Description("A list of content parts containing at least one part for text instructions")]
    public List<AssistantMessageContent> Contents { get; set; } = [];
    
    public override string ToString()
    {
        if (Contents == default || Contents.Count == 0) return string.Empty;        
        return string.Join(",", Contents);                    
    }
}

public class AssistantMessageContent
{
    [JsonPropertyName("text")]
    [Description("The text instruction, question, or prompt for the assistant.")]
    public string? Text { get; set; }    
}