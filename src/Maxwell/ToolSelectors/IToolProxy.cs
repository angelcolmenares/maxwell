using Microsoft.Extensions.AI;

namespace Maxwell;

public interface IToolProxy
{
    AIFunction GetFindToolsDelegate {get;}
    AIFunction GetInvokeToolDelegate {get;}
}

public interface IRealToolProxy
{
    Task<IList<AIFunction>> FindTools(string query, string agentName, CancellationToken cancellationToken = default);
    Task<object?> InvokeTool(string toolName, string agentName, Dictionary<string, object?>? arguments, CancellationToken cancellationToken = default);

    Workspace Workspace {get;}
}