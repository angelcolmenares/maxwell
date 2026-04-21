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
        return await _store.LoadAsync(cancellationToken);
    }

    protected override async ValueTask StoreChatHistoryAsync(
        InvokedContext context, CancellationToken cancellationToken = default)
    {
        var responses =  context.ResponseMessages?.OrderBy(f=> f.CreatedAt).ToList() ?? [];
        var newMessages = context.RequestMessages.Concat(responses);
        await _store.SaveAsync(newMessages, cancellationToken);
    }
}
