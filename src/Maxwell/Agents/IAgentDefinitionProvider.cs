
namespace Maxwell;

public interface IAgentDefinitionProvider
{
    Task<AgentDefintionList> BuildAsync(CancellationToken cancellationToken = default);
    Task<AgentDefintionList> FindByRole(string role, CancellationToken cancellationToken=default);
}