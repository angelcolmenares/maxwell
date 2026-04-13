namespace Maxwell;

using Microsoft.Extensions.AI;

public static class AssistantMessageExtensions
{
    /// <summary>
    /// Converts the custom AssistantMessage (from the tool call) 
    /// into a native ChatMessage that the LLM/Framework understands.
    /// </summary>
    public static async Task<ChatMessage> ToChatMessage(
        this AssistantMessage message, 
        string? authorName = null, 
        ChatRole? role = null, 
        CancellationToken cancellationToken =default)
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

            // If it's a file or image URL
            if (!string.IsNullOrEmpty(item.Uri))
            {     
                string? mediaType= string.IsNullOrEmpty(item.MediaType)? null: item.MediaType;           
                // 2. Handle Local Files (Convert to Data URI / Base64)
                if (!item.Uri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(item.Uri))
                    {                        
                        DataContent dataContent =await  DataContent.LoadFromAsync(item.Uri, mediaType, cancellationToken);                        
                        nativeContents.Add(dataContent);
                    }
                }
                else
                {
                    // 3. Handle Web URLs
                    nativeContents.Add(new UriContent(new Uri(item.Uri), mediaType));
                }
            }
        }

        // 3. Return the ChatMessage with the aggregated contents
        return new ChatMessage(role ?? ChatRole.User, nativeContents) { AuthorName = authorName };
    }
}
