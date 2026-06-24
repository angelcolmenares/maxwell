using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Maxwell;

/// <summary>
/// Exposes common file-system operations as <see cref="AIFunction"/> instances
/// that are individually guarded by an <see cref="IFileSystemAccessValidator"/>.
/// </summary>
/// <remarks>
/// Every function checks access before performing any I/O.
/// If the validator rejects a path the function returns an error string instead
/// of throwing, so the AI agent receives a structured refusal it can reason about.
///
/// Read functions  : <see cref="GetReadFunctions"/>
/// Write functions : <see cref="GetWriteFunctions"/>
/// All functions   : <see cref="GetAllFunctions"/>
/// </remarks>
public sealed class FileSystemAIFunctions
{
    private readonly IFileSystemAccessValidator _validator;

    public FileSystemAIFunctions(IFileSystemAccessValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
    }

    // =========================================================================
    // Read operations
    // =========================================================================

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that reads the text content of a file.
    /// </summary>
    public AIFunction GetFileContent() =>
        AIFunctionFactory.Create(
            async ([Description("Full path of the file to read.")] string path) =>
            {
                if (!await _validator.ValidateAccessAsync(path))
                    return AccessDenied(path);

                if (!File.Exists(path))
                    return $"Error: file not found at '{path}'.";

                return await File.ReadAllTextAsync(path);
            },
            name: "get_file_content",
            description: "Returns the full text content of a file at the specified path. Use ONLY for text files");

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that lists files inside a directory.
    /// </summary>
    public AIFunction GetFiles() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path of the directory to list files in.")] string directory,
                [Description("Optional search pattern, e.g. '*.txt'. Defaults to '*'.")] string pattern = "*",
                [Description("When true, searches all sub-directories recursively.")] bool recursive = false) =>
            {
                if (!await _validator.ValidateAccessAsync(directory))
                    return AccessDenied(directory);

                if (!Directory.Exists(directory))
                    return $"Error: directory not found at '{directory}'.";

                SearchOption option = recursive
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                string[] files = Directory.GetFiles(directory, pattern, option);
                return files.Length == 0
                    ? "No files found matching the criteria."
                    : string.Join(Environment.NewLine, files);
            },
            name: "get_files",
            description: "Lists files in a directory, with optional pattern filtering and recursive search.");

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that lists sub-directories inside a directory.
    /// </summary>
    public AIFunction GetFolders() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path of the directory to list sub-directories in.")] string directory,
                [Description("When true, searches all sub-directories recursively.")] bool recursive = false) =>
            {
                if (!await _validator.ValidateAccessAsync(directory))
                    return AccessDenied(directory);

                if (!Directory.Exists(directory))
                    return $"Error: directory not found at '{directory}'.";

                SearchOption option = recursive
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                string[] folders = Directory.GetDirectories(directory, "*", option);
                return folders.Length == 0
                    ? "No sub-directories found."
                    : string.Join(Environment.NewLine, folders);
            },
            name: "get_folders",
            description: "Lists sub-directories inside a directory, with optional recursive search.");

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that checks whether a file exists.
    /// </summary>
    public AIFunction FileExists() =>
        AIFunctionFactory.Create(
            async ([Description("Full path of the file to check.")] string path) =>
            {
                if (!await _validator.ValidateAccessAsync(path))
                    return AccessDenied(path);

                return File.Exists(path)
                    ? $"File exists: '{path}'."
                    : $"File does not exist: '{path}'.";
            },
            name: "file_exists",
            description: "Checks whether a file exists at the specified path.");

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that checks whether a directory exists.
    /// </summary>
    public AIFunction FolderExists() =>
        AIFunctionFactory.Create(
            async ([Description("Full path of the directory to check.")] string path) =>
            {
                if (!await _validator.ValidateAccessAsync(path))
                    return AccessDenied(path);

                return Directory.Exists(path)
                    ? $"Directory exists: '{path}'."
                    : $"Directory does not exist: '{path}'.";
            },
            name: "folder_exists",
            description: "Checks whether a directory exists at the specified path.");

    // =========================================================================
    // Write operations
    // =========================================================================

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that creates (or overwrites) a file.
    /// </summary>
    public AIFunction CreateFile() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path of the file to create.")] string path,
                [Description("Text content to write into the file.")] string content,
                [Description("When true, overwrites the file if it already exists.")] bool overwrite = false) =>
            {
                if (!await _validator.ValidateAccessAsync(path))
                    return AccessDenied(path);

                if (!overwrite && File.Exists(path))
                    return $"Error: file already exists at '{path}'. Set overwrite=true to replace it.";

                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                await File.WriteAllTextAsync(path, content);
                return $"File created successfully at '{path}'.";
            },
            name: "create_file",
            description: "Creates a new file (or optionally overwrites an existing one) with the supplied text content.");

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that creates a directory (including all intermediate directories).
    /// </summary>
    public AIFunction CreateFolder() =>
        AIFunctionFactory.Create(
            async ([Description("Full path of the directory to create.")] string path) =>
            {
                if (!await _validator.ValidateAccessAsync(path))
                    return AccessDenied(path);

                if (Directory.Exists(path))
                    return $"Directory already exists at '{path}'.";

                await Task.Run(() => Directory.CreateDirectory(path));
                return $"Directory created at '{path}'.";
            },
            name: "create_folder",
            description: "Creates a directory (and any required intermediate directories) at the specified path.");

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that moves a file to a new location.
    /// </summary>
    public AIFunction MoveFile() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path of the source file to move.")] string sourcePath,
                [Description("Full destination path including the new file name.")] string destinationPath,
                [Description("When true, overwrites the destination if it already exists.")] bool overwrite = false) =>
            {
                if (!await _validator.ValidateAccessAsync(sourcePath))
                    return AccessDenied(sourcePath);

                if (!await _validator.ValidateAccessAsync(destinationPath))
                    return AccessDenied(destinationPath);

                if (!File.Exists(sourcePath))
                    return $"Error: source file not found at '{sourcePath}'.";

                string? destDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                await Task.Run(() => File.Move(sourcePath, destinationPath, overwrite));
                return $"File moved from '{sourcePath}' to '{destinationPath}'.";
            },
            name: "move_file",
            description: "Moves a file from a source path to a destination path, with optional overwrite.");

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that deletes a file.
    /// </summary>
    public AIFunction DeleteFile() =>
        AIFunctionFactory.Create(
            async ([Description("Full path of the file to delete.")] string path) =>
            {
                if (!await _validator.ValidateAccessAsync(path))
                    return AccessDenied(path);

                if (!File.Exists(path))
                    return $"Error: file not found at '{path}'.";

                await Task.Run(() => File.Delete(path));
                return $"File deleted: '{path}'.";
            },
            name: "delete_file",
            description: "Permanently deletes the file at the specified path.");

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that deletes a directory and all its contents.
    /// </summary>
    public AIFunction DeleteFolder() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path of the directory to delete.")] string path,
                [Description("When true, deletes all contents recursively. Required if the directory is non-empty.")] bool recursive = false) =>
            {
                if (!await _validator.ValidateAccessAsync(path))
                    return AccessDenied(path);

                if (!Directory.Exists(path))
                    return $"Error: directory not found at '{path}'.";

                await Task.Run(() => Directory.Delete(path, recursive));
                return $"Directory deleted: '{path}'.";
            },
            name: "delete_folder",
            description: "Deletes a directory. Set recursive=true to also delete all contents.");

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that copies a file.
    /// </summary>
    public AIFunction CopyFile() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path of the source file to copy.")] string sourcePath,
                [Description("Full destination path including the file name.")] string destinationPath,
                [Description("When true, overwrites the destination file if it already exists.")] bool overwrite = false) =>
            {
                if (!await _validator.ValidateAccessAsync(sourcePath))
                    return AccessDenied(sourcePath);

                if (!await _validator.ValidateAccessAsync(destinationPath))
                    return AccessDenied(destinationPath);

                if (!File.Exists(sourcePath))
                    return $"Error: source file not found at '{sourcePath}'.";

                string? destDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                await Task.Run(() => File.Copy(sourcePath, destinationPath, overwrite));
                return $"File copied from '{sourcePath}' to '{destinationPath}'.";
            },
            name: "copy_file",
            description: "Copies a file to a destination path, with optional overwrite.");

    /// <summary>
    /// Returns an <see cref="AIFunction"/> that copies an entire directory tree.
    /// </summary>
    public AIFunction CopyFolder() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path of the source directory to copy.")] string sourcePath,
                [Description("Full path of the destination directory.")] string destinationPath,
                [Description("When true, overwrites existing files in the destination.")] bool overwrite = false) =>
            {
                if (!await _validator.ValidateAccessAsync(sourcePath))
                    return AccessDenied(sourcePath);

                if (!await _validator.ValidateAccessAsync(destinationPath))
                    return AccessDenied(destinationPath);

                if (!Directory.Exists(sourcePath))
                    return $"Error: source directory not found at '{sourcePath}'.";

                await Task.Run(() => CopyDirectoryRecursive(sourcePath, destinationPath, overwrite));
                return $"Directory copied from '{sourcePath}' to '{destinationPath}'.";
            },
            name: "copy_folder",
            description: "Copies a directory and all its contents to a destination path.");

    // =========================================================================
    // Function-set accessors
    // =========================================================================

    /// <summary>
    /// Returns the subset of functions that only read from the file system.
    /// </summary>
    public List<AIFunction> GetReadFunctions() =>
    [
        GetFileContent(),
        GetFiles(),
        GetFolders(),
        FileExists(),
        FolderExists(),
    ];

    /// <summary>
    /// Returns the subset of functions that mutate the file system.
    /// </summary>
    public List<AIFunction> GetWriteFunctions() =>
    [
        CreateFile(),
        CreateFolder(),
        MoveFile(),
        DeleteFile(),
        DeleteFolder(),
        CopyFile(),
        CopyFolder(),
    ];

    /// <summary>
    /// Returns all available file-system functions (read and write).
    /// </summary>
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

    private static void CopyDirectoryRecursive(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
        {
            string destFile = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite);
        }

        foreach (string subDir in Directory.GetDirectories(source))
        {
            string destSubDir = Path.Combine(destination, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSubDir, overwrite);
        }
    }
}