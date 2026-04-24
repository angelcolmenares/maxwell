using System.Text.Json;

namespace Maxwell;

public class AgentMessage : Dictionary<string, JsonElement>
{                

    public string Sender
    {
        get => TryGetValue("sender", out var value) ? value.GetString() ?? string.Empty : string.Empty;
        set => SetString("sender", value);
    }


    /// <summary>
    /// "invoke_assistant", "find_assistants",  "find_tools", "show_to_user" 
    /// </summary>
    public string? ActionName
    {
        get => GetString("actionName");
        set => SetString("actionName", value);
    }

    public string? Text
    {
        get => GetString("text");
        set => SetString("text", value);
    }

    public string? Uri
    {
        get => GetString("uri");
        set => SetString("uri", value);
    }
    public string? AssistantName
    {
        get => GetString("assistantName");
    }

    public string? Query
    {
        get => GetString("query");
    }

    private string? GetString(string key)
        => TryGetValue(key, out var value) ? value.GetString() ?? null : null;

    private void SetString(string key, string? value) 
        => this[key] = JsonSerializer.SerializeToElement(value);
    
    
}