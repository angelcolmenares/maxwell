using Maxwell;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Serilog;

Guid workspaceId = Guid.Parse(AppSettings.DefaultWorkspaceId);
Guid chatId = Guid.Parse(AppSettings.DefaultChatId);

JsonFileSystemAccessValidator fileSystemAccessValidator = new(AppSettings.GetFileSystemAccessJson(workspaceId));
McpClient mcpDockerClient = await CreateMcpDockerClient();
AiToolsDelegate aiFunctions = CreateAiFunctionsFactory(workspaceId, mcpDockerClient, fileSystemAccessValidator);
MyChatHistoryProvider myHistoryProvider = new(new JsonFileMessageStore(AppSettings.GetChatJsonStoreJsonFile(workspaceId, chatId)));
using ILoggerFactory loggerFactory = CreateLoggerFactory(workspaceId);

WorkspaceAgentFactory workspaceAgentFactory = new();
Workspace workspace = await Workspace.CreateAsync(
    workspaceId,
    GetChatStore,
    GetAgentDefinitionProvider,
    GetConnectionDefinitionProvider,
    GetAgentInstructionsProvider,
    GetSkillContextProvider,
    workspaceAgentFactory,
    aiFunctions,
    fileSystemAccessValidator,
    GetWorkspaceToolSelector,
    GetWorkspaceToolProxy,
    GetWorkspaceAssistantSelector,
    GetWorkspaceAssistantProxy,
    toolCallingMiddleware: ToolCallingMiddleware,
    loggerFactory: loggerFactory,
    chatHistoryProvider: myHistoryProvider);

ChatSession chat = await workspace.GetChatSession(chatId);
var session = await chat.CreateSessionAsync();
var hp = chat.GetService<MyChatHistoryProvider>();
do
{
    if (!GetUserInput(out var userQuery)) break;
    var runOptions = new AgentRunOptions();
    List<AgentResponseUpdate> updates = [];
    await foreach (var update in chat.RunStreamingAsync(userQuery, session: session, runOptions))
    {
        Console.Write(update.Text);
        updates.Add(update);
    }
    Console.WriteLine();
    Console.WriteLine("----------------");
    var agentResponse = updates.ToAgentResponse();
    Console.WriteLine($"updates: {agentResponse.Messages.Count}");
    Console.WriteLine("----------------");
    //var messages = hp?.GetMessages(session);
    Console.WriteLine(hp);
    //Console.WriteLine($"hp session messages.count: {messages?.Count ?? -99}");
    Console.WriteLine($"currentSession.Assistants : {(await chat.GetAssistants()).Count()}");
    Console.WriteLine($"agentResponse.Usage: {agentResponse.Usage}");
    Console.WriteLine($"agentResponse.Usage.CachedInputTokenCount: {agentResponse.Usage?.CachedInputTokenCount}");
    Console.WriteLine($"agentResponse.Usage.input: {agentResponse.Usage?.InputTokenCount}");
    Console.WriteLine($"agentResponse.Usage.output: {agentResponse.Usage?.OutputTokenCount}");
    Console.WriteLine($"agentResponse.Usage.reasoning: {agentResponse.Usage?.ReasoningTokenCount}");
    Console.WriteLine($"agentResponse.Usage.total: {agentResponse.Usage?.TotalTokenCount}");

} while (true);


//----------------------------------------------------------------------------------------------------------------------------
static AgenticToolSelector GetWorkspaceToolSelector(
    Workspace workspace)
{
    return new(workspace.AiToolsFunc, workspace.GetToolSelectorDelegate());
}

static ToolProxy GetWorkspaceToolProxy(
    Workspace workspace)
{
    return new(workspace.AiToolsFunc);
}

static AgenticAssistantSelector GetWorkspaceAssistantSelector(
    Workspace workspace)
{
    AgenticAssistantSelector agenticAssistantProxy = new(workspace.GetAssistantsDelegate(), workspace.GetAssistantSelectorDelegate());
    return agenticAssistantProxy;
}

static AssistantProxy GetWorkspaceAssistantProxy(
    Workspace workspace)
{
    return new(workspace.GetAssistantsDelegate(), workspace.ValidateAccessAsync);
}

//----------------------------------------------------------------------------------------------------------------------------
static bool GetUserInput(out string userQuery)
{
    Console.WriteLine("User:");
    userQuery = Console.ReadLine()!;
    if (string.IsNullOrEmpty(userQuery) || string.Compare(userQuery, "q", StringComparison.OrdinalIgnoreCase) == 0)
    {
        return false;
    }
    return true;
}
//----------------------------------------------------------------------------------------------------------------------------
static async Task<McpClient> CreateMcpDockerClient()
{
    var transport = new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "DockerMCPServer",
        Command = "docker",
        Arguments = ["mcp", "gateway", "run"]
    });
    return await McpClient.CreateAsync(transport);
}
static async Task<List<AIFunction>> GetMcpDockerFunctions(McpClient mcpClient, CancellationToken cancellationToken = default)
{
    var mcpTools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
    return [.. mcpTools.Cast<AIFunction>()];
}
//----------------------------------------------------------------------------------------------------------------------------

static FileAgentInstructions GetAgentInstructionsProvider(Guid workspaceId) =>
    new(AppSettings.GetInstructionsDirectory(workspaceId));

static JsonAgentDefinitionProvider GetAgentDefinitionProvider(Guid workspaceId) =>
    new(AppSettings.AgentsJsonFile(workspaceId));

static JsonConnectionDefinitionProvider GetConnectionDefinitionProvider(Guid workspaceId) =>
    new(AppSettings.ConnectionsJsonFile(workspaceId));

static JsonChatStore GetChatStore(Guid workspaceId) => new(AppSettings.ChatsJsonFile(workspaceId));

static FileAgentSkillContextProvider GetSkillContextProvider(
    Guid workspaceId,
    IEnumerable<AgentFrontmatter> agentFrontmatterList, ILoggerFactory? loggerFactory = null)
    => new(AppSettings.GetSkillDirectory(workspaceId), agentFrontmatterList, loggerFactory: loggerFactory);

static async ValueTask<object?> ToolCallingMiddleware(
    AIAgent callingAgent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"[DEBUG ToolCallingMiddleware] Invoked for agent: {callingAgent.Name} Function: {context.Function.Name}");
    return await next(context, cancellationToken);
}



static ILoggerFactory CreateLoggerFactory(Guid workspaceId)
{
    return LoggerFactory.Create(
        builder =>
        {
            builder.AddSerilog(
                 new LoggerConfiguration()
                     .MinimumLevel.Debug()
                     .WriteTo.File(
                        Path.Combine(AppSettings.GetLogsDirectory(workspaceId), $"{DateTime.Today:yyyyMMdd}.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7)
                     .CreateLogger());
            builder.SetMinimumLevel(LogLevel.Debug);
        });
}

static AiToolsDelegate CreateAiFunctionsFactory(
    Guid workspaceId,
    McpClient mcpDockerClient,
    IFileSystemAccessValidator fileSystemAccessValidator)
{
    FileSystemAIFunctions fileSystemAIFunctions = new(fileSystemAccessValidator);

    var git = new GitAIFunctions(validator: fileSystemAccessValidator, personalAccessToken: PatResolver.Resolve(config: null));
    var md = new MarkItDownAIFunctions(fileSystemAccessValidator, new MarkItDownCliRunner());
    var images = new ImageAIFunctions(fileSystemAccessValidator);

    return async (CancellationToken cancellationToken = default) => [
        .. fileSystemAIFunctions.GetAllFunctions(),
        .. git.GetAllFunctions(),
        .. md.GetAllFunctions(),
        .. images.GetAllFunctions(),
        .. await GetMcpDockerFunctions(mcpDockerClient, cancellationToken)
    ];
}