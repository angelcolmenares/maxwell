using Microsoft.Extensions.AI;

namespace Maxwell;

public static class StringExtensions
{
    extension(string source)
    {
        public ChatMessage ToChatMessage(string authorName, ChatRole? role = null)
        => new( role ?? ChatRole.User, source)
        {
            AuthorName = authorName,
            CreatedAt = DateTimeOffset.UtcNow.RemoveMilliseconds()
        };
    }
}