using Microsoft.Agents.AI;
using AgentFrameworkToolkit.OpenAI;
using AgentFrameworkToolkit.GitHub;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentFrameworkToolkit;
using static AgentFrameworkToolkit.MiddlewareDelegates;
using System.Text.Json;

namespace Maxwell;

public class AgentFactory(ConnectionDefinitionList connections)
{
    private readonly IReadOnlyDictionary<string, AgentFactoryDelegate> _delegates
        = BuildDelegates(connections);
    private readonly Dictionary<string, AIAgent> _agents = [];


    public async Task<AIAgent> Get(
        AgentDefinition agentDefinition,
        IAgentInstructions agentInstructions,
        IList<AITool>? tools = null,
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
        if (_agents.TryGetValue(agentDefinition.Name, out var agent))
        {
            return agent;
        }

        if (!_delegates.TryGetValue(agentDefinition.Connection, out var factory))
            throw new ArgumentException(
                $"No factory registered for connection '{agentDefinition.Connection}'. " +
                $"Available: {string.Join(", ", _delegates.Keys)}");

        agent = factory(
            agentDefinition,
            await agentInstructions.ReadAsync(agentDefinition, cancellationToken),
            tools,
            aiContextProviders,
            chatHistoryProvider,
            services,
            loggerFactory,
            loggingMiddleware,
            toolCallingMiddleware,
            toolCallingDetails,
            rawCallDetails);
        _agents.TryAdd(agentDefinition.Name, agent);
        return agent;
    }

    private static IReadOnlyDictionary<string, AgentFactoryDelegate> BuildDelegates(
        ConnectionDefinitionList connections)
    {
        var dict = new Dictionary<string, AgentFactoryDelegate>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in connections.Definitions)
        {
            var factory = definition.ClientType switch
            {
                var t when t.Equals("openai", StringComparison.OrdinalIgnoreCase)
                    => CreateOpenAIDelegate(definition),
                var t when t.Equals("github", StringComparison.OrdinalIgnoreCase)
                    => CreateGitHubDelegate(definition),
                _ => throw new ArgumentException(
                    $"Unknown ClientType '{definition.ClientType}' " +
                    $"in connection '{definition.Name}'.")
            };

            dict.Add(definition.Name, factory);
        }

        return dict.AsReadOnly();
    }

    // Resuelve TODO: "Map AgentDefinition into OpenAI.AgentOptions"
    // Options case-insensitive via extensión
    private static AgentFactoryDelegate CreateOpenAIDelegate(ConnectionDefinition definition)
    {
        var connection = new OpenAIConnection
        {
            ApiKey = definition.Options.GetString("apiKey", "no-key"),
            Endpoint = definition.Options.GetString("endpoint", ""),
        };
        var agentFactory = new OpenAIAgentFactory(connection);

        return (AgentDefinition agentDef,
        string instructions,
        IList<AITool>? tools = null,
        IEnumerable<AIContextProvider>? aIContextProviders = null,
        ChatHistoryProvider? chatHistoryProvider = null,
        IServiceProvider? services = null,
        ILoggerFactory? loggerFactory = null,
        LoggingMiddleware? loggingMiddleware = null,
        ToolCallingMiddlewareDelegate? toolCallingMiddelware = null,
         Action<ToolCallingDetails>? toolCallingDetails = null,
        Action<RawCallDetails>? rawCallDetails = null) => agentFactory.CreateAgent(new AgentOptions
        {
            Model = agentDef.Model,
            Name = agentDef.Name,
            Instructions = instructions,
            Description = agentDef.Description,
            //
            Tools = tools,
            AIContextProviders = aIContextProviders,
            ChatHistoryProvider = chatHistoryProvider,
            Services = services,
            LoggerFactory = loggerFactory,
            LoggingMiddleware = loggingMiddleware,
            ToolCallingMiddleware = GetToolCallingMiddlewareDelegate(toolCallingMiddelware),
            RawToolCallDetails = toolCallingDetails,
            RawHttpCallDetails = rawCallDetails,
            // 
            Temperature = agentDef.Options.GetFloat("temperature"),
            MaxOutputTokens = agentDef.Options.GetInt("maxOutputTokens"),

        });
    }

    private static ToolCallingMiddlewareDelegate? GetToolCallingMiddlewareDelegate(ToolCallingMiddlewareDelegate? toolCallingMiddleware)
    {
        return async (callingAgent, context, next, cancellationToken) =>
        {
            SanitizeMessage(context);
            string agentName = callingAgent.Name ?? "AgenteDesconocido";
            context.Arguments["agentName"] = agentName;

            if (toolCallingMiddleware != null)
            {
                return await toolCallingMiddleware(callingAgent, context, next, cancellationToken);
            }
            else
            {
                return await next(context, cancellationToken);
            }
        };
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
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[ERROR ToolCallingMiddleware]  {exception}");
                    throw;
                }
            }
        }
    }

    private static AgentFactoryDelegate CreateGitHubDelegate(ConnectionDefinition definition)
    {
        var connection = new GitHubConnection(definition.Options.GetString("personalToken"));
        var agentFactory = new GitHubAgentFactory(connection);

        return (AgentDefinition agentDef,
        string instructions,
        IList<AITool>? tools = null,
        IEnumerable<AIContextProvider>? aIContextProviders = null,
        ChatHistoryProvider? chatHistoryProvider = null,
        IServiceProvider? services = null,
        ILoggerFactory? loggerFactory = null,
        LoggingMiddleware? loggingMiddleware = null,
        ToolCallingMiddlewareDelegate? toolCallingMiddelware = null,
        Action<ToolCallingDetails>? toolCallingDetails = null,
        Action<RawCallDetails>? rawCallDetails = null) => agentFactory.CreateAgent(new GitHubAgentOptions
        {
            Model = agentDef.Model,
            Name = agentDef.Name,
            Instructions = instructions,
            Description = agentDef.Description,
            //
            Tools = tools,
            AIContextProviders = aIContextProviders,
            ChatHistoryProvider = chatHistoryProvider,
            Services = services,
            LoggerFactory = loggerFactory,
            LoggingMiddleware = loggingMiddleware,
            ToolCallingMiddleware = toolCallingMiddelware,
            RawToolCallDetails = toolCallingDetails,
            RawHttpCallDetails = rawCallDetails,
            //
            Temperature = agentDef.Options.GetFloat("temperature"),
            MaxOutputTokens = agentDef.Options.GetInt("maxOutputTokens"),

        });
    }
}

//--------------------------------------------------------------------------------------------------------------------
