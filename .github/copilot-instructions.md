# Copilot instructions for Maxwell repository

This file contains repository-specific guidance for GitHub Copilot sessions. It is intended to help automated assistants and future Copilot CLI sessions work effectively in this repo.

---

Build, test, and lint commands

- Build solution (recommended):
  - From repository root (src folder contains the solution):
    - dotnet build "src\Maxwell.slnx" --configuration Debug
  - Restore and build (single command):
    - dotnet restore "src\Maxwell.slnx" && dotnet build "src\Maxwell.slnx"

- Run console demo:
  - dotnet run --project "src\Maxwell.ConsoleDemo\Maxwell.ConsoleDemo.csproj" --configuration Debug

- Tests:
  - There are no test projects checked in under src right now (only test packages are referenced in Directory.Packages.props). If/when tests are added:
    - Run all tests: dotnet test "src\Maxwell.slnx"
    - Run a single test (example): dotnet test "path\to\Tests.csproj" --filter "FullyQualifiedName=Namespace.ClassName.TestMethod"

- Linting / analyzers:
  - Repository enables .NET analyzers in Directory.Build.props (EnableNETAnalyzers=true). Running dotnet build will surface analyzer warnings/errors.
  - Use your IDE (Visual Studio / VS Code) or run dotnet build to see analyzer results.

---

High-level architecture (big picture)

- Purpose: Maxwell is an experimental multi-agent orchestration playground built on top of AgentFrameworkToolkit and Microsoft agent/AI components. The ConsoleDemo shows the typical runtime flow.

- Core runtime pieces (src\Maxwell):
  - Workspace (Maxwell.Workspaces.Workspace): orchestrates agents, chat stores, connection definitions, instructions, skills, and tool/assistant selection.
  - AgentFactory / WorkspaceAgentFactory: constructs AIAgents from agent definitions and connection settings.
  - Chat stores & history providers: JSON-based chat stores and optional wiki-backed history (index.md + per-exchange details).
  - Tooling integration: MCP client (Model Context Protocol) can enumerate external tools; the demo includes a Docker-based MCP launcher (docker mcp gateway run).
  - Console demo (src\Maxwell.ConsoleDemo\Program.cs): shows the interaction loop: user -> leader agent -> assistants/tools -> wiki updater.

- Runtime configuration: workspaces live in the user's home directory under a Maxwell-specific structure (see conventions below). Templates for connections/agents/chats are under src\Maxwell\templates.

---

Key repository conventions

- Workspace layout (on disk):
  - By default a workspace is stored under: %USERPROFILE%\.maxwell\ws\[WORKSPACE_GUID]\
  - Expected files/dirs inside a workspace GUID directory:
    - connections.json  (LLM/provider connection configs)
    - agents.json       (agent definitions: name, model, connection, role)
    - chats.json        (chat registry)
    - instructions/     (markdown files with agent system instructions, one file per agent name)
    - skills/           (agents-skills/ and shared-skills/ with SKILL.md files)
    - chats/            (per-chat json message stores)
    - wikis/            (index.md and per-exchange details used by the wiki updater)
  - Environment variable override: set MAXWELL_HOME to change the base home directory; otherwise AppSettings uses the user profile + ".maxwell".

- Defaults and templates:
  - Default workspace and chat GUIDs are defined in src\Maxwell\AppSettings (DefaultWorkspaceId/default chat id = 00000000-0000-...)
  - Templates: src\Maxwell\templates contains example agents.json, connections.json, chats.json.

- How agents are configured and loaded:
  - agents.json contains agent frontmatter with fields: name, model, connection, role, description, options.
  - Agent instructions are Markdown files in the instructions/ folder inside a workspace.
  - Connections (openai-local, cloud endpoints, or local endpoints) are specified in connections.json.

- Tooling and MCP servers:
  - The ConsoleDemo uses an McpClient and a helper that runs "docker mcp gateway run" to start a local MCP gateway. If you plan to use MCP tools, be prepared to run the MCP gateway (docker + mcp CLI) in your environment.
  - CreateAiFunctionsFactory in the demo wires together local file-system tools, git/md/image helpers, plus MCP-discovered functions.

- Logging and diagnostics:
  - Logs are written per-workspace under the Maxwell logs directory (AppSettings.GetLogsDirectory(workspaceId)).
  - Analyzers are enabled; TreatWarningsAsErrors=true in Directory.Build.props so builds can fail on analyzer issues.

- Project layout notes:
  - Solution (src\Maxwell.slnx) contains the main library (Maxwell) and the ConsoleDemo project.
  - There are no test projects in the repository root; test SDK references exist in Directory.Packages.props if tests are later added.

---

Where to look next (useful files)

- README.md at repository root: high-level overview and workspace structure.
- src\Maxwell\workspaces.md: concise description of workspace/JSON layout and required files.
- src\Maxwell\templates: example connections.json, agents.json, chats.json.
- src\Maxwell\AppSettings.cs: helper methods and path conventions (MAXWELL_HOME, default GUIDs, file locations).
- src\Maxwell\ConsoleDemo\Program.cs: shows runtime flow, MCP client usage, wiki updater, and how agents are invoked.

---

Notes for Copilot sessions

- When asked to make changes affecting runtime configuration, prefer editing or adding workspace JSON under src\Maxwell\templates and mention how to copy them to %MAXWELL_HOME%/ws/[GUID]/.
- For modifications that touch analyzer rules, remember Directory.Build.props enables analyzers and treats warnings as errors; run dotnet build locally to validate.
- If adding tools that rely on MCP, add clear instructions for starting/connecting to the MCP gateway (example in Program.cs uses "docker mcp gateway run").

---

If existing assistant/AI helper files are present (CLAUDE.md, AGENTS.md, .cursorrules, CONVENTIONS.md, etc.) include their relevant content into future Copilot notes; none were found when this file was generated.

---

Last updated: auto-generated for this session.
