using AgentFrameworkToolkit;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using static AgentFrameworkToolkit.MiddlewareDelegates;

namespace Maxwell;

public class Workspace
{
    private readonly IChatStore _chatStore;
    private readonly IConnectionDefinitionProvider _connectionDefitionProvider;
    private readonly IAgentDefinitionProvider _agentDefinitionProvider;
    private readonly IAgentInstructions _agentInstructions;
    private readonly AgentFactory _agentFactory;
    private readonly Func<Task<List<AIFunction>>> _aiToolsFunc;
    private readonly Guid _workspaceId;

    private readonly IEnumerable<AIContextProvider> _aiContextProviders;
    ChatHistoryProvider? _chatHistoryProvider;
    private readonly IServiceProvider? _services;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly LoggingMiddleware? _loggingMiddleware;
    private readonly ToolCallingMiddlewareDelegate? _toolCallingMiddleware;
    private readonly Action<ToolCallingDetails>? _toolCallingDetails;
    private readonly Action<RawCallDetails>? _rawCallDetails;
    private readonly Func<Workspace, ToolSelector> _toolSelectorFunc;
    private readonly Func<Workspace, AssistantSelector> _assistantSelectorFunc;
    private readonly ToolSelector _toolSelector;

    private readonly AssistantSelector _assistantSelector;
    private readonly IFileSystemAccessValidator _fileSystemAccessValidator;

    private Workspace(
        Guid workspaceId,
        IChatStore chatStore,
        IConnectionDefinitionProvider connectionDefitionProvider,
        IAgentDefinitionProvider agentDefinitionProvider,
        IAgentInstructions agentInstructions,
        AgentFactory agentFactory,
        Func<Task<List<AIFunction>>> aiToolsFunc,
        IFileSystemAccessValidator fileSystemAccessValidator,
        Func<Workspace, ToolSelector> toolSelectorFunc,
        Func<Workspace, AssistantSelector> assistantSelectorFunc,
        IEnumerable<AIContextProvider>? aiContextProviders = null,
        ChatHistoryProvider? chatHistoryProvider = null,
        IServiceProvider? services = null,
        ILoggerFactory? loggerFactory = null,
        LoggingMiddleware? loggingMiddleware = null,
        ToolCallingMiddlewareDelegate? toolCallingMiddleware = null,
        Action<ToolCallingDetails>? toolCallingDetails = null,
        Action<RawCallDetails>? rawCallDetails = null
        )
    {
        _chatStore = chatStore;
        _agentDefinitionProvider = agentDefinitionProvider;
        _agentInstructions = agentInstructions;
        _connectionDefitionProvider = connectionDefitionProvider;
        _agentFactory = agentFactory;
        _aiToolsFunc = aiToolsFunc;
        _workspaceId = workspaceId;
        _aiContextProviders = aiContextProviders ?? [];
        _chatHistoryProvider = chatHistoryProvider;
        _services = services;
        _loggerFactory = loggerFactory;
        _loggingMiddleware = loggingMiddleware;
        _toolCallingMiddleware = toolCallingMiddleware;
        _toolCallingDetails = toolCallingDetails;
        _rawCallDetails = rawCallDetails;
        _toolSelectorFunc = toolSelectorFunc;
        _assistantSelectorFunc = assistantSelectorFunc;
        _toolSelector = _toolSelectorFunc(this);
        _assistantSelector = _assistantSelectorFunc(this);
        _fileSystemAccessValidator= fileSystemAccessValidator;

    }
    private AssistantsDelegate? assistantDelegate = null;

    public Guid WorkspaceId => _workspaceId;

    public Func<Task<List<AIFunction>>> AiToolsFunc => _aiToolsFunc;

    public async Task<AgentDefinition?> GetAgentDefinitionByRole(string role, CancellationToken cancellationToken = default)
    {
        AgentDefintionList agents = await _agentDefinitionProvider.BuildAsync(cancellationToken);
        return agents.FirstOrDefaultByRole(role);
    }

    public async Task<IReadOnlyList<AgentFrontmatter>> GetAgentFrontmatters(CancellationToken cancellationToken = default)
    {
        AgentDefintionList agents = await _agentDefinitionProvider.BuildAsync(cancellationToken);
        return agents.AgentFrontmatters;
    }

    public async Task<bool> ValidateAccessAsync(string path, CancellationToken cancellationToken=default)
    => await _fileSystemAccessValidator.ValidateAccessAsync(path, cancellationToken);

    private async Task<ChatDefinition?> GetChatDefinition(Guid chatId, CancellationToken cancellationToken = default)
    => await _chatStore.GetByIdAsync(chatId, cancellationToken);

    private List<AITool> ToolSelectorDelegates => [_toolSelector.FindToolsDelegate, _toolSelector.InvokeToolDelegate];
    private List<AITool> AssistantSelectorDelegates => [_assistantSelector.FindAssistantsDelegate, _assistantSelector.InvokeAssistantDelegate];

    internal  async Task<AIAgent> GetAgent(
        AgentDefinition agentDefinition,
        CancellationToken cancellationToken = default)
    {
        return await GetAgent(agentDefinition, [], cancellationToken);
    }

    private async Task<AIAgent> GetAgent(
        AgentDefinition agentDefinition,
        IList<AITool>? tools ,
        CancellationToken cancellationToken = default)
    {
        return await _agentFactory.Get(
           agentDefinition,
           _agentInstructions,
           tools,
           _aiContextProviders,
           _chatHistoryProvider,
           _services,
           _loggerFactory,
           _loggingMiddleware,
           _toolCallingMiddleware,
           _toolCallingDetails,
           _rawCallDetails,
           cancellationToken);
    }

    public AssistantsDelegate GetAssistantsDelegate()
    {
        if (assistantDelegate != null)
        {
            return assistantDelegate;
        }

        assistantDelegate =
        async (CancellationToken cancellationToken = default) =>
        {
            List<AIAgent> assistants = [];
            var definitions = await _agentDefinitionProvider.FindByRole("Assistant", cancellationToken);
            foreach (var definition in definitions.Definitions)
            {
                AIAgent assistant = await _agentFactory.Get(
                    definition,
                    _agentInstructions,
                    tools: ToolSelectorDelegates,
                    _aiContextProviders,
                    _chatHistoryProvider,
                    _services,
                    _loggerFactory,
                    _loggingMiddleware,
                    _toolCallingMiddleware,
                    _toolCallingDetails,
                    _rawCallDetails,
                    cancellationToken);
                assistants.Add(assistant);
            }
            return new Assistants(definitions, assistants);
        };
        return assistantDelegate;
    }

    public async Task<ChatSession> GetChatSession(Guid chatId, CancellationToken cancellationToken = default)
    {        

        ChatDefinition? chatDefinition = await GetChatDefinition(chatId, cancellationToken)
                                               ??
                                               throw new ArgumentException($"chat not found chatId:{chatId} workspaceId {WorkspaceId} ");
        AgentDefinition? leaderDefinition = await GetAgentDefinitionByRole("Leader", cancellationToken)
                                                 ??
                                                 throw new ArgumentException($"Leader Definition not found workspaceId {WorkspaceId} ");

        List<AITool> leaderTools = [.. ToolSelectorDelegates, .. AssistantSelectorDelegates];
        AIAgent leader = await GetAgent(leaderDefinition, leaderTools, cancellationToken);
        return new(chatDefinition, new(leaderDefinition, leader), GetAssistantsDelegate());
    }

    public static async Task<Workspace> CreateAsync(
        Guid workspaceId,
        Func<Guid, IChatStore> chatStoreFunc,
        Func<Guid, IAgentDefinitionProvider> agentDefinitionProviderFunc,
        Func<Guid, IConnectionDefinitionProvider> connectionDefitionFunc,
        Func<Guid, IAgentInstructions> agentInstructionsFunc,
        Func<Guid, IEnumerable<AgentFrontmatter>, ILoggerFactory?, SkillContextProvider> skillContextProviderFunc,
        WorkspaceAgentFactory workspaceAgentFactory,
        Func<Task<List<AIFunction>>> aiTools,
        IFileSystemAccessValidator fileSystemAccessValidator,
        Func<Workspace, ToolSelector> toolSelectorFunc,
        Func<Workspace, AssistantSelector> assistantSelectorFunc,
        IEnumerable<AIContextProvider>? aiContextProviders = null,
        ChatHistoryProvider? chatHistoryProvider = null,
        IServiceProvider? services = null,
        ILoggerFactory? loggerFactory = null,
        LoggingMiddleware? loggingMiddleware = null,
        ToolCallingMiddlewareDelegate? toolCallingMiddleware = null,
        Action<ToolCallingDetails>? toolCallingDetails = null,
        Action<RawCallDetails>? rawCallDetails = null,
        CancellationToken cancellationToken = default)
    {
        IChatStore chatStore = chatStoreFunc(workspaceId);
        IAgentDefinitionProvider agentDefinitionProvider = agentDefinitionProviderFunc(workspaceId);
        IConnectionDefinitionProvider connectionDefitionProvider = connectionDefitionFunc(workspaceId);
        IAgentInstructions agentInstructions = agentInstructionsFunc(workspaceId);
        AgentDefintionList agents = await agentDefinitionProvider.BuildAsync(cancellationToken);
        SkillContextProvider skillContextProvider = skillContextProviderFunc(workspaceId, agents.AgentFrontmatters,loggerFactory);
        AgentFactory agentFactory = await workspaceAgentFactory.Create(workspaceId, connectionDefitionProvider, cancellationToken);
        return new(
            workspaceId,
            chatStore,
            connectionDefitionProvider,
            agentDefinitionProvider,
            agentInstructions,
            agentFactory,
            aiTools,
            fileSystemAccessValidator,
            toolSelectorFunc,
            assistantSelectorFunc,
            [skillContextProvider, .. aiContextProviders ?? []],
            chatHistoryProvider,
            services,
            loggerFactory,
            loggingMiddleware,
            toolCallingMiddleware,
            toolCallingDetails,
            rawCallDetails);
    }
}