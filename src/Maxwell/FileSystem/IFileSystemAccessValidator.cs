namespace Maxwell;

/// <summary>
/// Validates whether a given file or directory path is within
/// the set of directories the AI agent is permitted to access.
/// </summary>
public interface IFileSystemAccessValidator
{
    /// <summary>
    /// Determines whether the supplied path falls inside at least one
    /// of the configured allowed directories.
    /// </summary>
    /// <param name="path">
    /// The fully-qualified file or directory path to validate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the path is permitted;
    /// <see langword="false"/> otherwise.
    /// </returns>
    Task<bool> ValidateAccessAsync(string path, CancellationToken cancellationToken =default);
    Task Reload(CancellationToken cancellationToken = default);
}