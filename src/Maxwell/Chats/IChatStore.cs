namespace Maxwell;
public interface IChatStore
{
    Task<IReadOnlyList<ChatDefinition>> GetAsync(CancellationToken cancellationToken=default);
    Task<ChatDefinition?> GetByIdAsync(Guid id,  CancellationToken cancellationToken=default);
}