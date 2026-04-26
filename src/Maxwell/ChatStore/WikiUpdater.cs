using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maxwell;

/// <summary>
/// After each Q/A cycle:
///   1. Asks the LLM to produce a structured JSON result with summary, details, and reference.
///   2. Writes the detail file via IDetailStore.
///   3. Appends a row to index.md via IIndexStore.
///
/// IWikiStore is no longer used — index.md is the single source of memory.
/// </summary>
public class WikiUpdater(
    AIAgent chatClient,
    IIndexStore indexStore,
    IDetailStore detailStore)
{
    private const string SystemPrompt = """
        You are a knowledge distillation engine.
        Your job is to produce a structured record of a Q/A exchange.

        ## Output format — respond ONLY with valid JSON, no markdown fences, no preamble:
        {
          "summary":   "<one sentence describing what happened in this exchange>",
          "details":   "<detailed markdown notes — key facts, files mentioned, decisions, discoveries>",
          "reference": "<file path or URL the user referenced, or empty string if none>"
        }

        ## Details rules
        - Use compact markdown (headers, bullets, code blocks as needed).
        - Include the key facts, files, and discoveries from THIS exchange only.
        - Sections to use as relevant:
          ## Context
          ## Key Facts & Discoveries
          ## Decisions Made
          ## Open Questions
        - Be thorough but avoid padding.
        """;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task UpdateAsync(
        string userMessage,
        string agentResponse,
        CancellationToken ct = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        string prompt = $"""
            ## Exchange to Record
            **User:** {userMessage}
            **Agent:** {agentResponse}

            Respond with the JSON object described in the system prompt.
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, prompt)
        };

        bool success = true;
        string summary   = userMessage.Length > 80 ? userMessage[..80] + "…" : userMessage;
        string details   = agentResponse;
        string reference = string.Empty;

        try
        {
            AgentResponse result = await chatClient.RunAsync(messages, cancellationToken: ct);
            string raw = (result.Text ?? string.Empty).Trim();

            // Strip accidental markdown fences
            if (raw.StartsWith("```")) raw = raw[(raw.IndexOf('\n') + 1)..];
            if (raw.EndsWith("```"))   raw = raw[..raw.LastIndexOf("```")].TrimEnd();

            var parsed = JsonSerializer.Deserialize<WikiUpdateResult>(raw, _jsonOptions);
            if (parsed is not null)
            {
                summary   = parsed.Summary   ?? summary;
                details   = parsed.Details   ?? details;
                reference = parsed.Reference ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            success = false;
            Console.WriteLine($"[WIKI] LLM parse error: {ex.Message}");
        }

        // 1. Build detail file base name: "<topic> <yyyyMMdd_HHmmss>"
        string topicSlug     = BuildTopicSlug(userMessage, reference);
        string baseName      = $"{topicSlug} {now:yyyyMMdd_HHmmss}";

        // 2. Write detail file
        string detailFileName = await detailStore.WriteAsync(baseName, details, ct);

        // 3. Append row to index.md
        await indexStore.AppendAsync(new IndexEntry(
            Timestamp:      now,
            Success:        success,
            UserMessage:    userMessage,
            Summary:        summary,
            DetailFileName: detailFileName,
            Reference:      string.IsNullOrWhiteSpace(reference) ? null : reference), ct);
    }

    private static string BuildTopicSlug(string userMessage, string reference)
    {
        if (!string.IsNullOrWhiteSpace(reference))
        {
            string fileName = Path.GetFileName(reference.Replace('\\', '/'));
            if (!string.IsNullOrWhiteSpace(fileName))
                return $"{fileName} Description";
        }

        string slug = userMessage.Length > 50 ? userMessage[..50] : userMessage;
        return System.Text.RegularExpressions.Regex.Replace(slug, @"[^\w\s\-\.]", " ").Trim();
    }

    private sealed class WikiUpdateResult
    {
        [JsonPropertyName("summary")]   public string? Summary   { get; init; }
        [JsonPropertyName("details")]   public string? Details   { get; init; }
        [JsonPropertyName("reference")] public string? Reference { get; init; }
    }
}