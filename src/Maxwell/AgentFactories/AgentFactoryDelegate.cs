using AgentFrameworkToolkit;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using static AgentFrameworkToolkit.MiddlewareDelegates;

namespace Maxwell;

public delegate AIAgent AgentFactoryDelegate(
    AgentDefinition agentDefinition,
    string agentInstructions,
    IList<AITool>? tools = null,
    IEnumerable<AIContextProvider>? aIContextProviders = null,
    ChatHistoryProvider? chatHistoryProvider = null,
    IServiceProvider? services = null,
    ILoggerFactory? loggerFactory = null, 
    LoggingMiddleware? loggingMiddleware=null,
    ToolCallingMiddlewareDelegate? toolCallingMiddelware=null,
    Action<ToolCallingDetails>?  toolCallingDetails=null,
    Action<RawCallDetails>? rawCallDetails=null);