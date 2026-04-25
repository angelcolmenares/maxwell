using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maxwell;

/// <summary>
/// Chat history provider that keeps a SHORT sliding window of raw messages,
/// but always prepends the compressed wiki as a system-level memory block.
///
/// This keeps the LLM context clean:
///   [WIKI SYSTEM MSG] + [last N raw messages]
/// instead of the full unbounded history.
/// </summary>
public class WikiChatHistoryProvider(
    IMessageStore messageStore,
    IWikiStore wikiStore,
    int slidingWindowSize = 10) : ChatHistoryProvider
{
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        string wiki = await wikiStore.LoadAsync(cancellationToken);
        List<ChatMessage> rawMessages = await messageStore.LoadAsync(cancellationToken);

        var result = new List<ChatMessage>();

        // 1. Prepend wiki as a compressed memory block (system role)
        if (!string.IsNullOrWhiteSpace(wiki))
        {
            result.Add(new ChatMessage(ChatRole.System,
                $"""
                ## Agent Memory (Compressed Wiki)
                The following is your compressed knowledge base from previous interactions.
                Use it to maintain continuity without needing the full conversation history.

                {wiki}
                """));
        }

        // 2. Append only the last N messages (sliding window)
        var window = rawMessages.Count > slidingWindowSize
            ? rawMessages[^slidingWindowSize..]
            : rawMessages;

        result.AddRange(window);
        return result;
    }

    protected override async ValueTask StoreChatHistoryAsync(
        InvokedContext context, CancellationToken cancellationToken = default)
    {
        var responses =  context.ResponseMessages?.OrderBy(f=> f.CreatedAt).ToList() ?? [];
        var newMessages = context.RequestMessages.Concat(responses);
        await messageStore.SaveAsync(newMessages, cancellationToken);
    }
}