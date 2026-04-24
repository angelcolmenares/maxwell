using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maxwell;

public class AgenticAssistantProxy(
    Workspace workspace
    ) : IAssistantProxy
{
    public Workspace Workspace => workspace;
    private const string _instrucctionsTemplate =
        """
        <assistants_usage>
        You have access to assistants containing task-specific knowledge and capabilities.
        Each assistant is a specialized smart agent for and specific task.

        <available_assistants>
        {0}
        </available_assistants>        

        When an assistant aligns with a specific task:
        1. Use `invoke_assistant` to invoke an assistant passing the assistantName and your instructions
        
        Only call what is needed, when it is needed.
        </assistants_usage>
        """;

    public AIFunction FindAssistantsDelegate =>
        AIFunctionFactory.Create(
                this.FindAssistants,
                name: "find_assistants",
                description:
                """
                Finds assistants according to your instructions. 
                Receives your instructions. 
                Returns names and descriptions of the assistants that best fit your instructions.
                """);


    public AIFunction InvokeAssistantDelegate =>
        AIFunctionFactory.Create(
                this.InvokeAssistant,
                name: "invoke_assistant",
                description:
                """
                Invokes an smart assistant with capabilities of following instructions. 
                Receives  assistantName and the message containing the instructions.
                Returns response as string
                """);


    public async Task<string> InvokeAssistant(string assistantName, string agentName, AssistantMessage message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Invoking :{assistantName}. Caller:{agentName} Instructions:{message} ...");
        AssistantsDelegate assistantsDelegate = workspace.GetAssistantsDelegate();
        var assistants = await assistantsDelegate(cancellationToken);
        AIAgent? assistant = assistants.FirstOrDefault(f => f.Name == assistantName);
        if (assistant == default)
        {
            return $"ERROR_FATAL: Smart Assistant '{assistantName}' not found.";
        }
        try
        {
            var chatMessage = await message.ToChatMessage(authorName: agentName, cancellationToken:cancellationToken);
            var result = await assistant.RunAsync(chatMessage, cancellationToken: cancellationToken);

            if (result == null || string.IsNullOrEmpty(result.Text))
            {
                return "ERROR_FATAL: Assistant returns no result";
            }
            Console.WriteLine($"[DEBUG] Invoked {assistant.Name} result.Text length: {result.Text.Length}");
            Console.WriteLine($"[DEBUG] Invoked {assistant.Name} result.Text first 200 chars: {result.Text.Substring(0, Math.Min(200, result.Text.Length))}");
            return result.Text;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] InvokeAssistant failed: {ex.Message}");
            return $"ERROR_FATAL: Failed invoking '{assistantName}': {ex.Message}.";
        }
    }


    public async Task<IList<AgentFrontmatter>> FindAssistants(string query, string agentName, CancellationToken cancellationToken = default)
    {
        AgentDefinition? assistantSelectorDefinition = await workspace.GetAgentDefinitionByRole("AssistantSelector", cancellationToken);

        if (assistantSelectorDefinition == default) return [];

        AIAgent selector = await workspace.GetAgent(
            assistantSelectorDefinition,
            cancellationToken: cancellationToken);

        AssistantsDelegate assistantsDelegate = workspace.GetAssistantsDelegate();
        var assistants = await assistantsDelegate(cancellationToken);
        var agentFrontmatters = assistants.Definitions.AgentFrontmatters;

        var availableAssistants = agentFrontmatters
            .Select(t => new { t.Name, t.Description, t.Model })
            .ToMarkdownTable();
        var message =
        $"""                  
        <find-assistants-request>
        <instructions>Find the best assistants from the available-assistants  according to the query</instructions>
        <query>{query}</query>
        <available-assistants>{availableAssistants}</available-assistants>
        </find-assistants-request>
        """;
        var selectorResponse = await selector.RunAsync(message, cancellationToken: cancellationToken);
        var selectedNames = selectorResponse.Text.Split(',', StringSplitOptions.TrimEntries);
        return [.. agentFrontmatters.Where(t => selectedNames.Contains(t.Name) && t.Name != agentName)];
    }

    public async Task<Assistants> GetAssistants(CancellationToken cancellationToken = default)
    => await workspace.GetAssistantsDelegate()(cancellationToken);
}
