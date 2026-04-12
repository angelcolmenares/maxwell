using Microsoft.Extensions.AI;

namespace Maxwell;

public class AssistantSelector(IAssistantProxy assistantProxy)
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

    private async Task<string> InvokeAssistant(string assistantName, string agentName, AssistantMessage message, CancellationToken cancellationToken = default)
    {
        var uri = message.Contents.FirstOrDefault(f => !string.IsNullOrEmpty(f.Uri))?.Uri;
        if (!string.IsNullOrEmpty(uri) && new Uri(uri).IsFile)
        {
            if (!await assistantProxy.Workspace.ValidateAccessAsync(uri, cancellationToken))
            {
                return AccessDenied(uri);
            }
        }
        return await assistantProxy.InvokeAssistant(assistantName, agentName, message, cancellationToken);
    }

     private static string AccessDenied(string path) =>
        $"Access denied: the path '{path}' is not within an allowed directory.";


    private async Task<string> FindAssistants(string query, string agentName, CancellationToken cancellationToken = default)
    {
        var toolProxyResponse = await assistantProxy.FindAssistants(query, agentName, cancellationToken);
        var filteredResults = toolProxyResponse
            .Select(t => new { t.Name, t.Description, t.Model, ReturnType = "string" })
            .ToList();
        return string.Format(_instrucctionsTemplate, filteredResults.ToMarkdownTable());
    }

    public async Task<Assistants> GetAssistants() => await assistantProxy.GetAssistants();

}
