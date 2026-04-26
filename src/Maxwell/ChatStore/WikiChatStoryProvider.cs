using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maxwell;

/// <summary>
/// Chat history provider that keeps a SHORT sliding window of raw messages,
/// and prepends the full index.md as the agent's session memory.
///
///   [SESSION INDEX] + [last N raw messages]
/// </summary>
public class WikiChatHistoryProvider(
    IMessageStore messageStore,
    IIndexStore indexStore,
    int slidingWindowSize = 10) : ChatHistoryProvider
{
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        string index = await indexStore.LoadAsync(cancellationToken);
        List<ChatMessage> rawMessages = await messageStore.LoadAsync(cancellationToken);

        var result = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(index))
        {
            result.Add(new ChatMessage(ChatRole.System,
                $"""
                ## Session Index
                The following is the full log of Q/A exchanges in this session.
                Use it to maintain continuity and answer questions about what was discussed.

                {index}
                """));
        }

        var window = rawMessages.Count > slidingWindowSize
            ? rawMessages[^slidingWindowSize..]
            : rawMessages;

        result.AddRange(window);
        return result;
    }

    protected override async ValueTask StoreChatHistoryAsync(
        InvokedContext context, CancellationToken cancellationToken = default)
    {
        var responses = context.ResponseMessages?.OrderBy(f => f.CreatedAt).ToList() ?? [];
        var newMessages = context.RequestMessages.Concat(responses);
        await messageStore.SaveAsync(newMessages, cancellationToken);
    }
}