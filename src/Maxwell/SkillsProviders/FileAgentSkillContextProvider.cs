#pragma warning disable MAAI001
using System.Collections.ObjectModel;
using System.Security;
using System.Text;
using System.Xml.Linq;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Maxwell;

public abstract class SkillContextProvider : AIContextProvider
{
}

public class FileAgentSkillContextProvider(
    string skillPath,
    IEnumerable<AgentFrontmatter> agentFrontmatterList,
    AgentFileSkillScriptRunner? agentFileSkillScriptRunner = null,
        AgentSkillsProviderOptions? agentSkillsProviderOptions = null,
        AgentFileSkillsSourceOptions? agentFileSkillsSourceOptions = null,
    ILoggerFactory? loggerFactory = null) : SkillContextProvider
{

    private readonly ReadOnlyDictionary<string, AgentSkillsProvider> agentsSkillProvider = GetAgentsSkillProvider(
        skillPath,
        agentFrontmatterList,
        agentFileSkillScriptRunner: agentFileSkillScriptRunner,
        agentSkillsProviderOptions: agentSkillsProviderOptions,
        agentFileSkillsSourceOptions: agentFileSkillsSourceOptions,
        loggerFactory: loggerFactory);

    private static ReadOnlyDictionary<string, AgentSkillsProvider> GetAgentsSkillProvider(
        string skillPath,
        IEnumerable<AgentFrontmatter> agentFrontmatterList,
        AgentFileSkillScriptRunner? agentFileSkillScriptRunner = null,
        AgentSkillsProviderOptions? agentSkillsProviderOptions = null,
        AgentFileSkillsSourceOptions? agentFileSkillsSourceOptions = null,
        ILoggerFactory? loggerFactory = null)
    {
        return agentFrontmatterList.ToDictionary(k => k.Name, v => new AgentSkillsProvider(
            [Path.Combine(skillPath, "shared-skills"), Path.Combine(skillPath, "agents-skills", v.Name)],
            scriptRunner: agentFileSkillScriptRunner ?? SubprocessScriptRunner.RunAsync,
            fileOptions: agentFileSkillsSourceOptions,
            options: new AgentSkillsProviderOptions
            {
                SkillsInstructionPrompt = agentSkillsProviderOptions?.SkillsInstructionPrompt ??
            """
            <available_skills>
            {skills}
            <resource_instructions>
            {resource_instructions}
            </resource_instructions>
            <script_instructions>
            {script_instructions}
            </script_instructions> 
            </available_skills>            
            """,
                DisableCaching = agentSkillsProviderOptions?.DisableCaching ?? false,
                ScriptApproval = agentSkillsProviderOptions?.ScriptApproval ?? false

            },
            loggerFactory)).AsReadOnly();
    }


    private const string DefaultSkillsInstructionPrompt =  // TODO -- UPDATE
        """
        <skill-usage>
        You have access to skills containing domain-specific knowledge and capabilities.
        Each skill provides specialized instructions, reference documents, and assets for specific tasks.

        <available_skills>
        {skills}
        </available_skills>

        When a task aligns with a skill's domain, follow these steps in exact order:
        - Use `load_skill` to retrieve the skill's instructions.
        - Follow the provided guidance.
        {resource_instructions}
        {script_instructions}
        Only load what is needed, when it is needed.
        </skill-usage>
        """;

    protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        Console.WriteLine($"selecting skills for {context.Agent.Name}");
        var emptyContext = new InvokingContext(
           context.Agent,
           context.Session,
           new AIContext
           {
               Instructions = "",
               Messages = [],
               Tools = []
           });

        IEnumerable<AITool> toolsToFindSkills = [];
        Skills skills = new([], string.Empty, string.Empty);

        if (agentsSkillProvider.TryGetValue(context.Agent.Name ?? "", out var agentSkillsProvider))
        {
            var agentSkillsContext = await agentSkillsProvider.InvokingAsync(emptyContext, cancellationToken);
            toolsToFindSkills = agentSkillsContext.Tools ?? [];
            skills = ParseSkills(agentSkillsContext.Instructions ?? string.Empty);
        }

        var frontMatters = skills.FrontMatters;


        if (!toolsToFindSkills.Any() || frontMatters.Count == 0)
        {
            return await base.ProvideAIContextAsync(context, cancellationToken);
        }

        var instructionsToFindSkills = BuildSkillsInstructionPrompt(agentSkillsProviderOptions, skills);

        var cb = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("".PadRight(20, '-'));
        Console.WriteLine($"Agent:{context.Agent.Name}");
        Console.WriteLine($"tools: {string.Join(",", toolsToFindSkills.Select(f => f.Name))}");
        Console.WriteLine($"skills : {string.Join(",", skills.FrontMatters.Select(f => f.Name))}");
        Console.WriteLine($"resource_instructions : {skills.ResourceInstructions}");
        Console.WriteLine($"script_instructions : {skills.ScriptInstructions}");
        Console.WriteLine("".PadRight(20, '-'));
        Console.ForegroundColor = cb;
        List<AITool> wrappedTools = WrapTools(toolsToFindSkills);

        return new()
        {
            Tools = wrappedTools.Count == toolsToFindSkills.Count() ? wrappedTools : toolsToFindSkills,
            Instructions = instructionsToFindSkills,
        };

    }

    private static List<AITool> WrapTools(IEnumerable<AITool> toolsToFindSkills)
    {
        List<AITool> wrappedTools = [];
        var loadSkillTool = toolsToFindSkills.FirstOrDefault(f => f.Name == "load_skill") as AIFunction;
        if (loadSkillTool != default)
        {
            wrappedTools.Add(
            AIFunctionFactory.Create(
                method: (string skillName) => LoadSkillAsync(loadSkillTool, skillName),
                name: loadSkillTool.Name,
                description: loadSkillTool.Description,
                serializerOptions: loadSkillTool.JsonSerializerOptions));

            var readSkillResourceTool = toolsToFindSkills.FirstOrDefault(f => f.Name == "read_skill_resource") as AIFunction;

            if (readSkillResourceTool != default)
            {
                wrappedTools.Add(
                AIFunctionFactory.Create(
                    method: (string skillName, string resourceName, IServiceProvider? serviceProvider, CancellationToken cancellationToken = default)
                    => ReadSkillResourceAsync(readSkillResourceTool, skillName, resourceName, serviceProvider, cancellationToken),
                    name: readSkillResourceTool.Name,
                    description: readSkillResourceTool.Description,
                    serializerOptions: readSkillResourceTool.JsonSerializerOptions));
            }

            var runSkillScript = toolsToFindSkills.FirstOrDefault(f => f.Name == "run_skill_script") as AIFunction;
            if (runSkillScript != default)
            {
                wrappedTools.Add(
                    AIFunctionFactory.Create(
                    method: (string skillName, string scriptName, IDictionary<string, object?>? arguments = null, IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default)
                    => RunSkillScriptAsync(runSkillScript, skillName, scriptName, arguments),
                    name: runSkillScript.Name,
                    description: runSkillScript.Description,
                    serializerOptions: runSkillScript.JsonSerializerOptions));
            }

        }
        Console.WriteLine($"wrappedTools : {wrappedTools.Count}");
        return wrappedTools;
    }

    private static async Task<string> LoadSkillAsync(AIFunction loadSkillTool, string skillName)
    {
        Console.WriteLine($"Loading Skill. skillName:{skillName}");
        var sk = await loadSkillTool.InvokeAsync(new() { { "skillName", skillName } });
        var result = sk?.ToString() ?? "";
        Console.WriteLine($"Skill Loaded: '{result.Substring(0, Math.Min(200, result.Length))}...'");
        return result;
    }

    private static async Task<object?> ReadSkillResourceAsync(
        AIFunction readSkillResourceTool,
        string skillName,
        string resourceName,
        IServiceProvider? serviceProvider,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Loading SkillResource. skillName:{skillName} resourceName:{resourceName}  ");
        try
        {
            var sk = await readSkillResourceTool.InvokeAsync(
                new AIFunctionArguments
                {
                    ["skillName"] = skillName,
                    ["resourceName"] = resourceName,
                    Services = serviceProvider
                },
                cancellationToken);
            var result = sk?.ToString() ?? "";
            Console.WriteLine($"SkillResource Loaded: '{result.Substring(0, Math.Min(80, result.Length))}...'");
            return sk;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[ERROR] Loading SkillResource failed: {exception.Message}");

            return $"ERROR_FATAL: Loading SkillResource failed': {exception.Message}.";
        }
    }

    private static async Task<object?> RunSkillScriptAsync(
        AIFunction runSkill,
        string skillName,
        string scriptName,
        IDictionary<string, object?>? arguments = null,
        IServiceProvider? serviceProvider = null,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"running Skill script. skillName:{skillName} scriptName:{scriptName}  ");
        try
        {
            var sk = await runSkill.InvokeAsync(
                new AIFunctionArguments
                {
                    ["skillName"] = skillName,
                    ["scriptName"] = scriptName,
                    ["arguments"] = arguments,
                    Services = serviceProvider
                },
                cancellationToken);
            var result = sk?.ToString() ?? "";
            Console.WriteLine($"Skill script executed: '{result.Substring(0, Math.Min(80, result.Length))}...'");
            return sk;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[ERROR] running skill script failed: {exception.Message}");

            return $"ERROR_FATAL: running skill script failed': {exception.Message}.";
        }
    }

    private record SkillFrontMatter(string Name, string Description);
    private record Skills(IReadOnlyList<SkillFrontMatter> FrontMatters, string? ResourceInstructions, string? ScriptInstructions);


    private Skills ParseSkills(string xml)
    {
        var doc = XElement.Parse(xml);

        var frontMatters = doc.Elements("skill")
            .Select(s => new SkillFrontMatter(
                s.Element("name")?.Value ?? string.Empty,
                s.Element("description")?.Value ?? string.Empty
            ))
            .ToList();

        return new Skills(
            frontMatters.AsReadOnly(),
            doc.Element("resource_instructions")?.Value?.Trim(),
            doc.Element("script_instructions")?.Value?.Trim()
        );
    }

    private static string ToHtml(IEnumerable<SkillFrontMatter> skills)
    {
        var sb = new StringBuilder();

        foreach (var skill in skills)
        {
            sb.AppendLine("<skill>");
            sb.AppendLine($"<name>{SecurityElement.Escape(skill.Name)}</name>");
            sb.AppendLine($"<description>{SecurityElement.Escape(skill.Description)}</description>");
            sb.AppendLine("</skill>");
        }

        return sb.ToString();
    }

    private static string? BuildSkillsInstructionPrompt(AgentSkillsProviderOptions? options, Skills skills)
    {
        IEnumerable<SkillFrontMatter> agentSkills = skills.FrontMatters;
        string promptTemplate = options?.SkillsInstructionPrompt ?? DefaultSkillsInstructionPrompt;


        if (!agentSkills.Any())
        {
            return string.Empty;
        }

        var html = ToHtml(agentSkills);
        return new StringBuilder(promptTemplate)
            .Replace("{skills}", html)
            .Replace("{resource_instructions}", skills.ResourceInstructions ?? string.Empty)
            .Replace("{script_instructions}", skills.ScriptInstructions ?? string.Empty)
            .ToString();
    }
}
