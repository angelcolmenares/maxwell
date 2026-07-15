using Microsoft.Extensions.AI;

namespace Maxwell;

public class ToolProxy(AiToolsDelegate aiToolsDelegate): IToolProxy
{
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

    private async Task<object?> InvokeTool(
        string toolName, 
        string agentName, 
        Dictionary<string, object?>? arguments,
        CancellationToken cancellationToken=default)
    {        
        Console.WriteLine($"Invoking tool: {toolName}. Caller: {agentName} ...");
        Console.WriteLine(string.Join("\n", arguments?.Select(f => $"{f.Key}:{f.Value}") ?? []));
        var aiTools = await aiToolsDelegate(cancellationToken) ;
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