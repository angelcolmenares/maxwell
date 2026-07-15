using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maxwell;

public interface IToolSelector
{
    AIFunction FindToolsDelegate { get; }    
}
public class AgenticToolSelector(AiToolsDelegate aiToolsDelegate, ToolSelectorDelegate toolSelectorDelegate): IToolSelector
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

    
    private async Task<string> FindTools(
        string query, 
        string agentName, 
        CancellationToken cancellationToken=default)
    {
                        
        AIAgent? toolSelector = await toolSelectorDelegate(cancellationToken);
        if( toolSelector==default) return string.Empty;
            
        var aiTools = await aiToolsDelegate(cancellationToken);
        var allToolsMD = aiTools
            .Select(t => new { t.Name, t.Description })
            .ToMarkdownTable();
        Console.WriteLine($"finding tools: {query} ");
        var message =
        $"""        
        <find-tools-request>
        <instructions>Given the query find the best tools from the available-tools.
        Return only the names of the tools separated by comma. 
        If not sure include the tool name.
        DO NOT ADD any comments or extra info just the names of the best tools separated by coma.
        <query>{query}</query>
        <available-tools>{allToolsMD}</available-tools>
        </find-tools-request>
        """;        
        var routerResponse = await toolSelector.RunAsync(message.ToChatMessage(agentName, toolSelector.Name??"ToolSelector", ChatRole.User), cancellationToken:cancellationToken);
        var selectedNames = routerResponse.Text.Split(',', StringSplitOptions.TrimEntries);
        var tools = aiTools
            .Where(t => selectedNames.Contains(t.Name))            
            .ToList();
        Console.WriteLine($"found tools: {string.Join(',', tools.Select(f => f.Name))}");
                
        var filteredResults = tools
            .Select(t => new { t.Name, t.Description, t.JsonSchema, t.ReturnJsonSchema })
            .ToList();
        return string.Format(_toolsUsageTemplate, filteredResults.ToMarkdownTable());
    }

    
}
