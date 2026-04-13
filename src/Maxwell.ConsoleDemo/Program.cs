using System.Text.Json;
using Maxwell;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

Guid workspaceId = Guid.Parse(AppSettings.DefaultWorkspaceId);
Guid chatId = Guid.Parse(AppSettings.DefaultChatId);
JsonFileSystemAccessValidator fileSystemAccessValidator = new(AppSettings.GetFileSystemAccessJson(workspaceId));
FileSystemAIFunctions fileSystemAIFunctions = new(fileSystemAccessValidator);

var git = new GitAIFunctions(validator: fileSystemAccessValidator, personalAccessToken: PatResolver.Resolve(config: null));
var md = new MarkItDownAIFunctions(fileSystemAccessValidator, new MarkItDownCliRunner());

McpClient mcpDockerClient = await CreateMcpDockerClient();
Func<Task<List<AIFunction>>> aiFunctions = async () => [
    .. fileSystemAIFunctions.GetAllFunctions(),
    .. git.GetAllFunctions(),
    .. md.GetAllFunctions(),
    .. await GetMcpDockerFunctions(mcpDockerClient)];

using ILoggerFactory loggerFactory = LoggerFactory.Create(
    builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Debug);
    });


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
    GetWorkspaceAssistantSelector,
    toolCallingMiddleware: ToolCallingMiddleware,
    loggerFactory: loggerFactory
    );

ChatSession chat = await workspace.GetChatSession(chatId);
AIAgent leader = chat.Leader;
var session = await leader.CreateSessionAsync();
var hp = leader.GetService<InMemoryChatHistoryProvider>();
do
{
    if (!GetUserInput(out var userQuery)) break;
    var runOptions = new AgentRunOptions();
    List<AgentResponseUpdate> updates = [];
    await foreach (var update in leader.RunStreamingAsync(userQuery, session: session, runOptions))
    {
        Console.Write(update.Text);
        updates.Add(update);
    }
    Console.WriteLine();
    Console.WriteLine("----------------");
    Console.WriteLine($"updates: {updates.ToAgentResponse().Messages.Count}");
    Console.WriteLine("----------------");
    var messages = hp?.GetMessages(session);
    Console.WriteLine($"hp session messages.count: {messages?.Count ?? -99}");
    Console.WriteLine($"currentSession.Assistants : {(await chat.GetAssistants()).Count()}");

} while (true);


//----------------------------------------------------------------------------------------------------------------------------
static ToolSelector GetWorkspaceToolSelector(
    Workspace workspace)
{
    AgenticToolProxy agenticToolProxy = new(workspace);
    return new ToolSelector(agenticToolProxy);
}

static AssistantSelector GetWorkspaceAssistantSelector(
    Workspace workspace)
{
    AgenticAssistantProxy agenticAssistantProxy = new(workspace);
    return new(agenticAssistantProxy);
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
static async Task<List<AIFunction>> GetMcpDockerFunctions(McpClient mcpClient)
{
    var mcpTools = await mcpClient.ListToolsAsync();
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
    SanitizeMessage(context);
    string agentName = callingAgent.Name ?? "AgenteDesconocido";
    context.Arguments["agentName"] = agentName;    
    return await next(context, cancellationToken);
}

static void SanitizeMessage(FunctionInvocationContext context)
{
    if (context.Arguments.TryGetValue("message", out object? messageValue))
    {
        string? jsonString = messageValue switch
        {
            string s => s,
            JsonElement e when e.ValueKind == JsonValueKind.String => e.GetString(),
            _ => null
        };

        if (jsonString != null)
        {
            string healthyJson = JsonSanitizer.Sanitize(jsonString);
            try
            {
                var assistantMsg = JsonSerializer.Deserialize<object>(healthyJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                context.Arguments["message"] = assistantMsg;
                Console.WriteLine("[DEBUG ToolCallingMiddleware] Message sanitized and unpacked.");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[ERROR ToolCallingMiddleware]  {exception}");
                throw;
            }
        }
    }
}
