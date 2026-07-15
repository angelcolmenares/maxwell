using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maxwell;

public interface IAssistantSelector
{
    AIFunction FindAssistantsDelegate { get; }    
}
public class AgenticAssistantSelector(
    Workspace workspace
    ) :  IAssistantSelector
{
  
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

    
    private async Task<string> FindAssistants(string query, string agentName, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[DEBUG] FindAssistants called with query: {query}, agentName: {agentName}");
        AgentDefinition? assistantSelectorDefinition = await workspace.GetAgentDefinitionByRole("AssistantSelector", cancellationToken);

        if (assistantSelectorDefinition == default) return string.Empty;

        AIAgent selector = await workspace.GetAgent(assistantSelectorDefinition, cancellationToken);

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
        var selectorResponse = await selector.RunAsync(message.ToChatMessage(agentName, selector.Name??"AgentSelector", ChatRole.User), cancellationToken: cancellationToken);
        var selectedNames = selectorResponse.Text.Split(',', StringSplitOptions.TrimEntries);
        AgentFrontmatter[] toolProxyResponse = [.. agentFrontmatters.Where(t => selectedNames.Contains(t.Name) && t.Name != agentName)];
        var  filteredResults = toolProxyResponse
            .Select(t => new { t.Name, t.Description, t.Model, ReturnType = "string" })
            .ToList();
        return string.Format(_instrucctionsTemplate, filteredResults.ToMarkdownTable());
        
    }
        
}
