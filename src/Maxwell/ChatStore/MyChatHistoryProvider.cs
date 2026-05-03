using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maxwell;

public class MyChatHistoryProvider : ChatHistoryProvider
{
    private readonly IMessageStore _store;

    public MyChatHistoryProvider(IMessageStore store)
    {
        _store = store;
    }

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        var history = await _store.LoadAsync(cancellationToken);
        Console.WriteLine($"[MyChatHistoryProvider] Loaded {history.Count} messages from store.");
        Console.WriteLine($"[MyChatHistoryProvider] Agent:{context.Agent.Name}");
        
        history.ForEach(h => h.Contents = [
            .. h.Contents.Where(c=> c is TextContent  || c is UriContent || c is DataContent)]);
        history = [
            .. history
                .Where(h=> h.Contents.Count> 0 )
                .Where(h=> h.AuthorName =="user" && h.TargetAgent == context.Agent.Name
                           || (h.AuthorName==context.Agent.Name && h.Role == ChatRole.Assistant))];

        Console.WriteLine($"[MyChatHistoryProvider] Loaded {history.Count} messages from store.");
        Console.WriteLine($"[MyChatHistoryProvider] Agent:{context.Agent.Name}");
        return history.OrderBy(m => m.CreatedAt);
    }

    protected override async ValueTask StoreChatHistoryAsync(
        InvokedContext context, CancellationToken cancellationToken = default)
    {
        var responses = context.ResponseMessages ?? [];
        var newMessages = context.RequestMessages.Concat(responses).OrderBy(f => f.CreatedAt).ToList();
        await _store.SaveAsync(newMessages, cancellationToken);
    }
}