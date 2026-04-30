using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maxwell;

public class AgenticToolProxy(
    Workspace workspace) : IToolProxy
{
    public Workspace Workspace => workspace;
    public async Task<IList<AIFunction>> FindTools(string query, string agentName, CancellationToken cancellationToken = default)
    {
        AgentDefinition? toolSelectorDefinition = await workspace.GetAgentDefinitionByRole("ToolSelector");
        
        if (toolSelectorDefinition==default) return[];

        AIAgent toolSelector = await workspace.GetAgent(toolSelectorDefinition, cancellationToken);
            
        var aiTools = await workspace.AiToolsFunc();
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
        var routerResponse = await toolSelector.RunAsync(message, cancellationToken:cancellationToken);
        var selectedNames = routerResponse.Text.Split(',', StringSplitOptions.TrimEntries);
        var filteredResults = aiTools
            .Where(t => selectedNames.Contains(t.Name))            
            .ToList();
        Console.WriteLine($"found tools: {string.Join(',', filteredResults.Select(f => f.Name))}");
        return filteredResults;
    }

    public async Task<object?> InvokeTool(string toolName, string agentName, Dictionary<string, object?>? arguments, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Invoking tool: {toolName}. Caller: {agentName} ...");
        Console.WriteLine(string.Join("\n", arguments?.Select(f => $"{f.Key}:{f.Value}") ?? []));
        var aiTools = await workspace.AiToolsFunc();
        var tool = aiTools.FirstOrDefault(f => f.Name == toolName);
        if (tool == default)
        {
            var error = $"ERROR_FATAL: tool '{toolName}' not found.";
            Console.WriteLine($"tool failed: {toolName}. Caller: {agentName} {error}");
            return error;
        }
        try
        {
            var result = await tool.InvokeAsync(new AIFunctionArguments(arguments), cancellationToken);
            Console.WriteLine($"Invoked tool {tool.Name}. Caller: {agentName}: {(result?.ToString() ?? "").PadRight(200, ' ').Substring(0, 200)} ...");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed invoking tool '{toolName}' . Caller: {agentName} : {ex.Message}");
            return $"ERROR_FATAL: Failed invoking tool '{toolName}': {ex.Message}.";
        }
    }
}
