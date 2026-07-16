using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maxwell;
public delegate Task<List<AIFunction>> AiToolsDelegate(CancellationToken cancellationToken = default);
public delegate Task<AIAgent?> ToolSelectorDelegate(CancellationToken cancellationToken = default);
public delegate Task<AIAgent?> AssistantSelectorDelegate(CancellationToken cancellationToken = default);
public delegate Task<bool> ValidateAccessDelegate(string path, CancellationToken cancellationToken = default);
public delegate Task<Assistants> AssistantsDelegate(CancellationToken cancellationToken=default);