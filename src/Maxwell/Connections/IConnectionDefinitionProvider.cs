namespace Maxwell;

public interface IConnectionDefinitionProvider
{
    Task<ConnectionDefinitionList> BuildAsync(CancellationToken cancellationToken = default);
}