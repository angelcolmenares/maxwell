using Microsoft.Extensions.AI;

namespace Maxwell;

public static class StringExtensions
{
    extension(string source)
    {
        public ChatMessage ToChatMessage(string? authorName = null)
        => new(ChatRole.User, source)
        {
            AuthorName = authorName,
            CreatedAt = DateTimeOffset.UtcNow.RemoveMilliseconds()
        };
    }
}