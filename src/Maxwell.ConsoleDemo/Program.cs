using System.Text.Json;
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
Func<Task<List<AIFunction>>> aiFunctions = CreateAiFunctionsFactory(workspaceId, mcpDockerClient, fileSystemAccessValidator);
JsonFileMessageStore messageStore = new(AppSettings.GetChatStoreJson(workspaceId, chatId));
MyChatHistoryProvider historyProvider = new(messageStore);
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
    GetWorkspaceAssistantSelector,
    toolCallingMiddleware: ToolCallingMiddleware,
    loggerFactory: loggerFactory,
    chatHistoryProvider: historyProvider
    );

ChatSession chat = await workspace.GetChatSession(chatId);
AIAgent leader = chat.Leader;
var session = await leader.CreateSessionAsync();
var hp = leader.GetService<InMemoryChatHistoryProvider>();

do
{
    if (!GetUserInput(out var userQuery)) break;

    // Seed the chain: user message goes to the leader
    ChatMessage? currentMessage = userQuery.ToChatMessage(authorName: "user");
    AIAgent currentAgent = leader;

    while (currentMessage != default)
    {
        AgentMessage agentMessage = await RunAgentAsync(currentAgent, currentMessage, session);
        currentMessage = await GetNextMessage(agentMessage, workspace);
    }

} while (true);

//-------------------------------------------------------------------------------------------------

static async Task<AgentMessage> RunAgentAsync(
    AIAgent agent,
    ChatMessage message,
    AgentSession session)
{
    var runOptions = new AgentRunOptions();
    List<AgentResponseUpdate> updates = [];

    await foreach (var update in agent.RunStreamingAsync(message, session: session, runOptions))
    {
        Console.Write(update.Text);
        updates.Add(update);
    }
    Console.WriteLine();
    Console.WriteLine("----------------");

    AgentResponse agentResponse = updates.ToAgentResponse();

    return agentResponse.ToAgentMessage(agent.Name ?? "")
        ?? new AgentMessage
        {
            Sender = agent.Name ?? string.Empty,
            ActionName = "show_to_user",
            Text = agentResponse.Text
        };
}

static async Task<ChatMessage?> GetNextMessage(AgentMessage agentMessage, Workspace workspace)
{
    Console.WriteLine($"[ROUTING] Sender={agentMessage.Sender} Action={agentMessage.ActionName}");

    return agentMessage.ActionName switch
    {
        "show_to_user" => null,
        "invoke_assistant" => await InvokeAssistant(agentMessage, workspace),
        "find_assistants" => await FindAssistants(agentMessage, workspace),
        "find_tools" => await FindTools(agentMessage, workspace),
        _ => await HandleUnknownAction(agentMessage, workspace)
    };

    static async Task<ChatMessage> InvokeAssistant(AgentMessage agentMessage, Workspace workspace)
    {
        string assistantName = agentMessage.AssistantName ?? string.Empty;
        AssistantMessage assistantMessage = agentMessage.ToAssistantMessage();
        string assistantResponse = await workspace.AssistantSelector.InvokeAssistant(
                assistantName,
                agentMessage.Sender,
                assistantMessage);
        return assistantResponse.ToChatMessage(authorName: assistantName);
    }

    static async Task<ChatMessage> FindAssistants(AgentMessage agentMessage, Workspace workspace)
    {
        string query = agentMessage.Query ?? string.Empty;
        string response = await workspace.AssistantSelector.FindAssistants(query, agentMessage.Sender);
        return response.ToChatMessage();
    }

    static async Task<ChatMessage> FindTools(AgentMessage agentMessage, Workspace workspace)
    {

        string query = agentMessage.Query ?? string.Empty;
        string response = await workspace.ToolSelector.FindTools(query, agentMessage.Sender);
        return response.ToChatMessage();
    }

    static async Task<ChatMessage?> HandleUnknownAction(AgentMessage agentMessage, Workspace workspace)
    {
        await Task.CompletedTask;
        Console.WriteLine($"[WARNING] Unknown action '{agentMessage.ActionName}'. Stopping chain.");
        return null;
    }
}

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

static Func<Task<List<AIFunction>>> CreateAiFunctionsFactory(
    Guid workspaceId,
    McpClient mcpDockerClient,
    IFileSystemAccessValidator fileSystemAccessValidator)
{
    FileSystemAIFunctions fileSystemAIFunctions = new(fileSystemAccessValidator);

    var git = new GitAIFunctions(validator: fileSystemAccessValidator, personalAccessToken: PatResolver.Resolve(config: null));
    var md = new MarkItDownAIFunctions(fileSystemAccessValidator, new MarkItDownCliRunner());
    var images = new ImageAIFunctions(fileSystemAccessValidator);

    return async () => [
        .. fileSystemAIFunctions.GetAllFunctions(),
    .. git.GetAllFunctions(),
    .. md.GetAllFunctions(),
    .. images.GetAllFunctions(),
    .. await GetMcpDockerFunctions(mcpDockerClient)];
}
