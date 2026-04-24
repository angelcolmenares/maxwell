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
        1. Respond with a json message: `{'actionName': 'invoke_assistant', 'assistantName': 'THE_SELECTED_ASSISTANT', 'text':'YOUR_INSTRUCCTIONS', 'uri':'NORMALIZED_PATH_IF_NEEED', 'other_argument': OTHER_ARG_IF_REQUIRED}`
        
        Only call what is needed, when it is needed.
        </assistants_usage>
        """;
    
    public async Task<string> InvokeAssistant(string assistantName, string agentName, AssistantMessage message, CancellationToken cancellationToken = default)
    {      
         var uri = message.Uri;
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


    public async Task<string> FindAssistants(string query, string agentName, CancellationToken cancellationToken = default)
    {
        var toolProxyResponse = await assistantProxy.FindAssistants(query, agentName, cancellationToken);
        var filteredResults = toolProxyResponse
            .Select(t => new { t.Name, t.Description, t.Model, ReturnType = "string" })
            .ToList();
        return string.Format(_instrucctionsTemplate, filteredResults.ToMarkdownTable());
    }

    public async Task<Assistants> GetAssistants() => await assistantProxy.GetAssistants();

}
