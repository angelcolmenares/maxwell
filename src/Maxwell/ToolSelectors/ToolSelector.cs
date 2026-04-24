namespace Maxwell;

public class ToolSelector(IToolProxy toolProxy )
{
    private const string _toolsUsageTemplate =
        """
        <tools_usage>
        You have access to the available_tools.
        <available_tools>
        {0}
        </available_tools>        

        When a tool aligns with a specific task:
        1. Use `invoke_tool` passing the toolName and the arguments as a dictionary[string,object]
        
        Only call what is needed, when it is needed.
        </tools_usage>
        """;
    

    public async Task<string> FindTools( 
        string query, 
        string agentName, 
        CancellationToken cancellationToken=default)
    {
        var toolProxyResponse = await  toolProxy.FindTools(query, agentName,cancellationToken );        
        var filteredResults = toolProxyResponse        
            .Select(t => new { t.Name, t.Description, t.JsonSchema, t.ReturnJsonSchema })
            .ToList();
        return string.Format(_toolsUsageTemplate, filteredResults.ToMarkdownTable());
    }

    public async Task<object?> InvokeTool(
        string toolName, 
        string agentName, 
        Dictionary<string, object?>? arguments,
        CancellationToken cancellationToken=default)
    {        
        return await toolProxy.InvokeTool(toolName, agentName, arguments, cancellationToken);
    }
}
