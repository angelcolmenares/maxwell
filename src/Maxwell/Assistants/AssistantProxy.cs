using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maxwell;

public delegate Task<bool> ValidateAccessDelegate(string path, CancellationToken cancellationToken = default);

public interface IAssistantProxy
{
    AIFunction InvokeAssistantDelegate { get; }
}
public class AssistantProxy(AssistantsDelegate assistantsDelegate, ValidateAccessDelegate validateAccessDelegate ): IAssistantProxy
{
    
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
        Console.WriteLine($"Invoking :{assistantName}. Caller:{agentName} Instructions:{message} ...");
        
        var assistants = await assistantsDelegate(cancellationToken);
        AIAgent? assistant = assistants.FirstOrDefault(f => f.Name == assistantName);
        if (assistant == default)
        {
            return $"ERROR_FATAL: Smart Assistant '{assistantName}' not found.";
        }
        try
        {
            if (!string.IsNullOrEmpty(message.Uri) && new Uri(message.Uri).IsFile)
            {
                if (!await validateAccessDelegate(message.Uri, cancellationToken))
                {
                    return AccessDenied(message.Uri);
                }
            }
            var chatMessage = await message.ToChatMessage(authorName: agentName, assistantName, cancellationToken: cancellationToken);
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

    private static string AccessDenied(string path) =>
        $"Access denied: the path '{path}' is not within an allowed directory.";
}