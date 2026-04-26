namespace Maxwell;

public static class AppSettings
{
    public const string HomeDirectoryName = ".maxwell";
    public const string LogsDirectoryName = "logs";
    public const string ChatsDirectoryName = "chats";
    public const string WikisDirectoryName = "wikis";
    public const string HomeEnvironmentVariable = "MAXWELL_HOME";
    public const string ConnectionsJsonFileName = "connections.json";
    public const string AgentsJsonFileName = "agents.json";
    public const string ChatsJsonFileName = "chats.json";
    public const string FileSystemAccessJson = "file-system-access.json";
    public const string SkillsDirectoryName = "skills";
    public const string InstructionsDirectoryName = "instructions";

    public const string DefaultWorkspaceId = "00000000-0000-0000-0000-000000000000";
    public const string DefaultChatId = "00000000-0000-0000-0000-000000000000";
    public const string DefaultWorkspaceName = "default";

    public const string WorkspacesDirectoryName = "ws";

    public static string BaseDirectory => AppContext.BaseDirectory;
    public static string Home =>
        Environment.GetEnvironmentVariable(HomeEnvironmentVariable)
        ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), HomeDirectoryName);

    public static string WorkspacesDirectory => Path.Combine(Home, WorkspacesDirectoryName);
    public static string DefaultWorkspaceDirectory => Path.Combine(WorkspacesDirectory, DefaultWorkspaceId);

    public static string ConnectionsJsonFile() => Path.Combine(WorkspacesDirectory, DefaultWorkspaceId, ConnectionsJsonFileName);


    public static string ConnectionsJsonFile(Guid workspaceId) =>
    Path.Combine(WorkspacesDirectory, workspaceId.ToString(), ConnectionsJsonFileName);

    public static string AgentsJsonFile() => Path.Combine(WorkspacesDirectory, DefaultWorkspaceId, AgentsJsonFileName);

    public static string AgentsJsonFile(Guid workspaceId) =>
    Path.Combine(WorkspacesDirectory, workspaceId.ToString(), AgentsJsonFileName);

    public static string ChatsJsonFile() => Path.Combine(WorkspacesDirectory, DefaultWorkspaceId, ChatsJsonFileName);

    public static string ChatsJsonFile(Guid workspaceId) =>
    Path.Combine(WorkspacesDirectory, workspaceId.ToString(), ChatsJsonFileName);

    public static string GetSkillDirectory(Guid workspaceId) =>
    Path.Combine(WorkspacesDirectory, workspaceId.ToString(), SkillsDirectoryName);

    public static string GetInstructionsDirectory(Guid workspaceId) =>
    Path.Combine(WorkspacesDirectory, workspaceId.ToString(), InstructionsDirectoryName);

    public static string GetFileSystemAccessJson(Guid workspaceId)
    {
        return Path.Combine(WorkspacesDirectory, workspaceId.ToString(), FileSystemAccessJson);
    }

    public static string GetLogsDirectory(Guid workspaceId)
    => Path.Combine(Home, LogsDirectoryName, workspaceId.ToString());

    public static string GetChatStoreJson(Guid workspaceId, Guid chatId)
    {
        return Path.Combine(WorkspacesDirectory, workspaceId.ToString(), ChatsDirectoryName, $"{chatId}.json");
    }

    // <summary>Path to index.md — one per workspace (shared across all chats).</summary>
    public static string GetIndexMd(Guid workspaceId) =>
        Path.Combine(GetWorkspaceDirectory(workspaceId),WikisDirectoryName, "index.md");


    /// <summary>Directory where per-exchange detail .md files are written.</summary>
    public static string GetDetailsDirectory(Guid workspaceId) =>
        Path.Combine(GetWorkspaceDirectory(workspaceId), WikisDirectoryName);

    private static string GetWorkspaceDirectory(Guid workspaceId)
    {
        return Path.Combine(WorkspacesDirectory, workspaceId.ToString());
    }
}