using Microsoft.Extensions.AI;

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
    
    public AIFunction FindToolsDelegate =>
        AIFunctionFactory.Create(
            this.FindTools,
            name: "find_tools",
            description:
            """
            <find_tools_description>
            Finds best tools according to your query. 
            <tool-arguments>
            <argument name="query" type="string">your query describing the task yoo need to find tools for</argument>
            <tool-arguments>
            <returns>names and descriptions of the tools that best fit your instructions.</returns>                
            </find_tools_description>
            """);

    public AIFunction InvokeToolDelegate =>
        AIFunctionFactory.Create(
            this.InvokeTool,
            name: "invoke_tool",
            description:
            """
            <invoke_tool_description>
            Invokes a tool. 
            <tool-arguments>
            <argument name="toolName" type="string"/>
            <argument name="arguments" type="nullable dictionary where key is type of string an value is type of nullable object"/>
            <tool-arguments>
            <returns>a nullable object</returns>
            <invoke_tool_description>
            """);


    private async Task<string> FindTools(
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

    private async Task<object?> InvokeTool(
        string toolName, 
        string agentName, 
        Dictionary<string, object?>? arguments,
        CancellationToken cancellationToken=default)
    {        
        return await toolProxy.InvokeTool(toolName, agentName, arguments, cancellationToken);
    }
}
