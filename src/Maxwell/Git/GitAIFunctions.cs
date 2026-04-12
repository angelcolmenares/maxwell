using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Maxwell;

/// <summary>
/// Exposes git CLI operations as <see cref="AIFunction"/> instances, each
/// guarded by an <see cref="IFileSystemAccessValidator"/> and backed by a
/// <see cref="GitCliRunner"/> that shells out to the local git executable.
/// </summary>
/// <remarks>
/// PAT-authenticated push
/// ───────────────────────
/// Supply a GitHub Personal Access Token in the constructor.
/// The token is injected into the remote URL at push time using the format:
///   https://x-access-token:{PAT}@github.com/owner/repo.git
/// The token is never written to the repository configuration.
///
/// Read functions  : <see cref="GetReadFunctions"/>
/// Write functions : <see cref="GetWriteFunctions"/>
/// All functions   : <see cref="GetAllFunctions"/>
/// </remarks>
public sealed class GitAIFunctions
{
    private readonly IFileSystemAccessValidator _validator;
    private readonly GitCliRunner               _runner;
    private readonly string?                    _personalAccessToken;

    /// <param name="validator">Validates that repository paths are in the allow-list.</param>
    /// <param name="runner">Executes git commands. A default instance is created if null.</param>
    /// <param name="personalAccessToken">
    /// GitHub PAT used for authenticated push/pull over HTTPS.
    /// Leave null to rely on the system credential store instead.
    /// </param>
    public GitAIFunctions(
        IFileSystemAccessValidator validator,
        GitCliRunner?              runner               = null,
        string?                    personalAccessToken  = null)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator           = validator;
        _runner              = runner ?? new GitCliRunner();
        _personalAccessToken = personalAccessToken;
    }

    // =========================================================================
    // Read operations
    // =========================================================================

    /// <summary>Returns the working-tree status (staged, unstaged, untracked files).</summary>
    public AIFunction GetGitStatus() =>
        AIFunctionFactory.Create(
            async ([Description("Full path to the git repository root.")] string repositoryPath) =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                var result = await _runner.RunAsync(repositoryPath, "status");
                return result.ToAgentString();
            },
            name: "get_git_status",
            description: "Returns the current git status of the repository, showing staged, unstaged, and untracked files.");

    /// <summary>Returns the diff of uncommitted changes.</summary>
    public AIFunction GetDiff() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path to the git repository root.")] string repositoryPath,
                [Description("When true, shows diff of staged (indexed) changes. When false, shows unstaged changes.")] bool staged = false,
                [Description("Optional specific file or directory path to limit the diff to.")] string? path = null) =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                string args = staged ? "diff --staged" : "diff";
                if (!string.IsNullOrWhiteSpace(path))
                    args += $" -- \"{path}\"";

                var result = await _runner.RunAsync(repositoryPath, args);
                return string.IsNullOrWhiteSpace(result.Stdout)
                    ? "No differences found."
                    : result.ToAgentString();
            },
            name: "get_diff",
            description: "Returns the diff of changes in the repository. Use staged=true to see what is already in the index.");

    /// <summary>Returns the commit log.</summary>
    public AIFunction GetLog() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path to the git repository root.")] string repositoryPath,
                [Description("Maximum number of commits to return.")] int limit = 10,
                [Description("Optional branch or commit ref to start from. Defaults to HEAD.")] string? @ref = null) =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                string refPart = string.IsNullOrWhiteSpace(@ref) ? "HEAD" : @ref;
                string args    = $"log {refPart} --oneline --decorate -n {limit}";

                var result = await _runner.RunAsync(repositoryPath, args);
                return result.ToAgentString();
            },
            name: "get_log",
            description: "Returns the recent commit history in a compact one-line format.");

    /// <summary>Returns the name of the currently checked-out branch.</summary>
    public AIFunction GetCurrentBranch() =>
        AIFunctionFactory.Create(
            async ([Description("Full path to the git repository root.")] string repositoryPath) =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                var result = await _runner.RunAsync(repositoryPath, "branch --show-current");
                return result.ToAgentString();
            },
            name: "get_current_branch",
            description: "Returns the name of the currently active git branch.");

    /// <summary>Lists local and/or remote branches.</summary>
    public AIFunction GetBranches() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path to the git repository root.")] string repositoryPath,
                [Description("When true, also lists remote-tracking branches.")] bool includeRemotes = false) =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                string args = includeRemotes ? "branch -a" : "branch";
                var result  = await _runner.RunAsync(repositoryPath, args);
                return result.ToAgentString();
            },
            name: "get_branches",
            description: "Lists git branches. Set includeRemotes=true to also show remote-tracking branches.");

    /// <summary>Returns the configured remote URLs of the repository.</summary>
    public AIFunction GetRemotes() =>
        AIFunctionFactory.Create(
            async ([Description("Full path to the git repository root.")] string repositoryPath) =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                var result = await _runner.RunAsync(repositoryPath, "remote -v");
                return result.ToAgentString();
            },
            name: "get_remotes",
            description: "Returns the remote repositories (name + URL) configured for this repository.");

    // =========================================================================
    // Write operations
    // =========================================================================

    /// <summary>Stages files for the next commit (git add).</summary>
    public AIFunction StageFiles() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path to the git repository root.")] string repositoryPath,
                [Description("Space-separated list of file paths to stage, or '.' to stage everything.")] string paths = ".") =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                var result = await _runner.RunAsync(repositoryPath, $"add {paths}");
                return result.IsSuccess
                    ? $"Staged '{paths}' successfully."
                    : result.ToAgentString();
            },
            name: "stage_files",
            description: "Stages files for the next commit. Use paths='.' to stage all changes.");

    /// <summary>Unstages files (git restore --staged).</summary>
    public AIFunction UnstageFiles() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path to the git repository root.")] string repositoryPath,
                [Description("Space-separated list of file paths to unstage, or '.' for all staged files.")] string paths = ".") =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                var result = await _runner.RunAsync(repositoryPath, $"restore --staged {paths}");
                return result.IsSuccess
                    ? $"Unstaged '{paths}' successfully."
                    : result.ToAgentString();
            },
            name: "unstage_files",
            description: "Removes files from the staging area without discarding the working-tree changes.");

    /// <summary>Creates a commit with the staged changes.</summary>
    public AIFunction CreateCommit() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path to the git repository root.")] string repositoryPath,
                [Description("The commit message.")] string message,
                [Description("When true, automatically stage all tracked modified/deleted files before committing (git commit -a).")] bool stageAll = false) =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                // Escape double-quotes inside the message so the shell does not break
                string safeMessage = message.Replace("\"", "\\\"");
                string args        = stageAll
                    ? $"commit -a -m \"{safeMessage}\""
                    : $"commit -m \"{safeMessage}\"";

                var result = await _runner.RunAsync(repositoryPath, args);
                return result.ToAgentString();
            },
            name: "create_commit",
            description: "Creates a git commit with the currently staged changes and the supplied message. Set stageAll=true to automatically stage all tracked changes first.");

    /// <summary>Pushes commits to a remote repository.</summary>
    public AIFunction GitPush() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path to the git repository root.")] string repositoryPath,
                [Description("Name of the remote to push to, e.g. 'origin'.")] string remote = "origin",
                [Description("Branch to push. Defaults to the currently active branch.")] string? branch = null,
                [Description("When true, sets the upstream tracking reference (git push --set-upstream).")] bool setUpstream = false) =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                // Resolve the branch name if not supplied
                string targetBranch = branch ?? string.Empty;
                if (string.IsNullOrWhiteSpace(targetBranch))
                {
                    var branchResult = await _runner.RunAsync(repositoryPath, "branch --show-current");
                    if (!branchResult.IsSuccess)
                        return $"Error resolving current branch: {branchResult.ToAgentString()}";
                    targetBranch = branchResult.Stdout.Trim();
                }

                // Build the effective remote URL — inject PAT when available
                string effectiveRemote = await ResolveAuthenticatedRemoteAsync(
                    repositoryPath, remote);

                string upstreamFlag = setUpstream ? "--set-upstream " : string.Empty;
                string args         = $"push {upstreamFlag}\"{effectiveRemote}\" {targetBranch}";

                var result = await _runner.RunAsync(repositoryPath, args);
                return result.ToAgentString();
            },
            name: "git_push",
            description: "Pushes local commits to the specified remote and branch. Uses the PAT for HTTPS authentication when one has been configured.");

    /// <summary>Pulls and integrates changes from a remote repository.</summary>
    public AIFunction GitPull() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path to the git repository root.")] string repositoryPath,
                [Description("Name of the remote to pull from, e.g. 'origin'.")] string remote = "origin",
                [Description("Branch to pull. Defaults to the currently tracked branch.")] string? branch = null,
                [Description("When true, rebases local commits on top of the fetched commits instead of merging.")] bool rebase = false) =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                string effectiveRemote = await ResolveAuthenticatedRemoteAsync(
                    repositoryPath, remote);

                string rebaseFlag  = rebase ? "--rebase " : string.Empty;
                string branchPart  = string.IsNullOrWhiteSpace(branch) ? string.Empty : $" {branch}";
                string args        = $"pull {rebaseFlag}\"{effectiveRemote}\"{branchPart}";

                var result = await _runner.RunAsync(repositoryPath, args);
                return result.ToAgentString();
            },
            name: "git_pull",
            description: "Fetches and integrates changes from a remote repository. Set rebase=true to rebase instead of merge.");

    /// <summary>Checks out an existing branch or creates a new one.</summary>
    public AIFunction CheckoutBranch() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path to the git repository root.")] string repositoryPath,
                [Description("Name of the branch to check out or create.")] string branch,
                [Description("When true, creates the branch if it does not exist (git checkout -b).")] bool create = false) =>
            {
                if (!await _validator.ValidateAccessAsync(repositoryPath))
                    return AccessDenied(repositoryPath);

                string args = create
                    ? $"checkout -b \"{branch}\""
                    : $"checkout \"{branch}\"";

                var result = await _runner.RunAsync(repositoryPath, args);
                return result.ToAgentString();
            },
            name: "checkout_branch",
            description: "Switches to the specified branch. Set create=true to create the branch if it does not exist.");

    // =========================================================================
    // Function-set accessors
    // =========================================================================

    /// <summary>
    /// Returns the subset of functions that only read repository state.
    /// Safe to expose to agents that should not modify anything.
    /// </summary>
    public List<AIFunction> GetReadFunctions() =>
    [
        GetGitStatus(),
        GetDiff(),
        GetLog(),
        GetCurrentBranch(),
        GetBranches(),
        GetRemotes(),
    ];

    /// <summary>
    /// Returns the subset of functions that mutate repository state
    /// (staging, committing, pushing, pulling, branching).
    /// </summary>
    public List<AIFunction> GetWriteFunctions() =>
    [
        StageFiles(),
        UnstageFiles(),
        CreateCommit(),
        GitPush(),
        GitPull(),
        CheckoutBranch(),
    ];

    /// <summary>Returns all available git functions (read and write).</summary>
    public List<AIFunction> GetAllFunctions() =>
    [
        .. GetReadFunctions(),
        .. GetWriteFunctions(),
    ];

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static string AccessDenied(string path) =>
        $"Access denied: the path '{path}' is not within an allowed directory.";

    /// <summary>
    /// Resolves the URL of <paramref name="remote"/> and, if a PAT is configured,
    /// returns an authenticated HTTPS URL of the form:
    ///   https://x-access-token:{PAT}@github.com/owner/repo.git
    ///
    /// Falls back to the plain remote name when no PAT is set or the URL is
    /// not an HTTP/HTTPS GitHub URL (e.g. SSH remotes are left untouched).
    /// </summary>
    private async Task<string> ResolveAuthenticatedRemoteAsync(
        string repositoryPath,
        string remote)
    {
        if (string.IsNullOrWhiteSpace(_personalAccessToken))
            return remote;

        var urlResult = await _runner.RunAsync(
            repositoryPath, $"remote get-url \"{remote}\"");

        if (!urlResult.IsSuccess || string.IsNullOrWhiteSpace(urlResult.Stdout))
            return remote;

        string url = urlResult.Stdout.Trim();

        // Only inject the PAT into plain HTTPS URLs — leave SSH untouched
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return remote;

        // Strip any existing credentials embedded in the URL
        var uri = new Uri(url);
        string cleanUrl = $"https://{uri.Host}{uri.PathAndQuery}";

        return $"https://x-access-token:{_personalAccessToken}@{uri.Host}{uri.PathAndQuery}";
    }
}