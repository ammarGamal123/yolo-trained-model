// Contract for resolving file paths that work in both development and deployed environments.

using DeepLearning.Application.Abstractions;

namespace DeepLearning.Application.Abstractions;

/// <summary>
/// Contract for resolving project-relative and absolute file paths,
/// and for enumerating available image files.
/// </summary>
public interface IProjectPathProvider
{
    /// <summary>
    /// Returns the absolute path to the application root directory.
    /// </summary>
    string GetProjectRoot();

    /// <summary>
    /// Combines the project root with a relative path and returns the full absolute path.
    /// </summary>
    string GetProjectFilePath(string relativePath);

    /// <summary>
    /// Converts any path to an absolute path.
    /// If rooted, returns as-is; otherwise resolves relative to the project root.
    /// </summary>
    string GetAbsolutePath(string path);

    /// <summary>
    /// Returns all image files found in the application root directory.
    /// </summary>
    string[] GetImageFiles();
}
