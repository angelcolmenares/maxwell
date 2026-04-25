using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;


namespace Maxwell;

/// <summary>
/// After each Q/A cycle, calls the LLM to merge the new exchange
/// into the existing wiki, keeping context lean (Karpathy-style).
///
/// The wiki is a living markdown document that grows in QUALITY,
/// not in length — old facts get overwritten, new ones get added.
/// </summary>
public class WikiUpdater(AIAgent chatClient, IWikiStore wikiStore)
{
    private const string SystemPrompt = """
        You are a knowledge distillation engine.
        Your job is to maintain a concise, structured wiki that captures
        everything an AI agent needs to remember about an ongoing work session.

        ## Rules
        - Write in compact markdown (headers, bullets, code blocks as needed).
        - OVERWRITE outdated facts — never duplicate.
        - MERGE similar entries — no repetition.
        - PRESERVE all unique decisions, preferences, constraints, and discoveries.
        - The wiki must be SHORT but DENSE. Aim for <600 tokens.
        - Sections to maintain (add/remove as relevant):
          ## Context
          ## User Preferences & Style
          ## Decisions Made
          ## Key Facts & Discoveries
          ## Current Task State
          ## Open Questions

        Return ONLY the updated wiki in markdown. No preamble, no explanation.
        """;

    public async Task<string> UpdateAsync(
        string userMessage,
        string agentResponse,
        CancellationToken ct = default)
    {
        string currentWiki = await wikiStore.LoadAsync(ct);

        string prompt = $"""
            ## Current Wiki
            {(string.IsNullOrWhiteSpace(currentWiki) ? "(empty — first turn)" : currentWiki)}

            ## New Exchange to Integrate
            **User:** {userMessage}
            **Agent:** {agentResponse}

            Update the wiki to reflect any new knowledge from this exchange.
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, prompt)
        };

        AgentResponse result = await chatClient.RunAsync(messages, cancellationToken: ct);
        string updatedWiki = result.Text ?? currentWiki;

        await wikiStore.SaveAsync(updatedWiki, ct);
        return updatedWiki;
    }
}