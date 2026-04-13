namespace Maxwell;

using Microsoft.Extensions.AI;

public static class AssistantMessageExtensions
{
    /// <summary>
    /// Converts the custom AssistantMessage (from the tool call) 
    /// into a native ChatMessage that the LLM/Framework understands.
    /// </summary>
    public static ChatMessage ToChatMessage(
        this AssistantMessage message, 
        string? authorName = null, 
        ChatRole? role = null)
    {
        // 1. Initialize the list of content items for the native message
        var nativeContents = new List<AIContent>();

        // 2. Map each item from our custom structure to the framework's types
        foreach (var item in message.Contents)
        {
            // If it's a text instruction
            if (!string.IsNullOrEmpty(item.Text))
            {
                nativeContents.Add(new TextContent(item.Text));
            }            
        }

        // 3. Return the ChatMessage with the aggregated contents
        return new ChatMessage(role ?? ChatRole.User, nativeContents) { AuthorName = authorName };
    }
}
