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
        CancellationToken cancellationToken = default)
    {
        // 1. Initialize the list of content items for the native message
        var nativeContents = new List<AIContent>();

        // If it's a text instruction
        if (!string.IsNullOrEmpty(message.Text))
        {
            nativeContents.Add(new TextContent(message.Text));
        }

        // If it's a file or image URL
        if (!string.IsNullOrEmpty(message.Uri))
        {
            // 2. Handle Local Files (Convert to Data URI / Base64)
            if (!message.Uri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(message.Uri))
                {
                    DataContent dataContent = await DataContent.LoadFromAsync(message.Uri, cancellationToken: cancellationToken);
                    nativeContents.Add(dataContent);
                }
            }
            else
            {
                // 3. Handle Web URLs
                nativeContents.Add(new UriContent(new Uri(message.Uri)));
            }
        }

        // 3. Return the ChatMessage with the aggregated contents
        return new ChatMessage(role ?? ChatRole.User, nativeContents)
        {
            AuthorName = authorName,
            CreatedAt = DateTimeOffset.UtcNow.RemoveMilliseconds()
        };
    }
}