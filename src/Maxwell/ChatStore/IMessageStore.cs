using Microsoft.Extensions.AI;

namespace Maxwell;
public interface IMessageStore
{
    ValueTask<List<ChatMessage>> LoadAsync(CancellationToken ct = default);
    ValueTask SaveAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default);
}