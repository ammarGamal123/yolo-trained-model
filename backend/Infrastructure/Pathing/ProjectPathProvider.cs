// Smart path resolver that auto-detects dev vs. deployed environment.

using System;
using System.IO;
using System.Linq;
using DeepLearning.Application.Abstractions;

namespace DeepLearning.Infrastructure.Pathing;

/// <summary>
/// Resolves paths relative to the application base directory (where the .exe lives).
/// For development builds, this resolves to the project root so that relative paths
/// like "sample.jpg" and "soap_v7.onnx" are found correctly.
/// For published (deployed) builds, this resolves to the publish folder itself,
/// allowing the exe to be distributed as-is with its model and sample files.
/// </summary>
public sealed class ProjectPathProvider : IProjectPathProvider
{
    private readonly string _appRoot;

    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];

    /// <summary>
    /// Initializes a new <see cref="ProjectPathProvider"/> by determining
    /// the appropriate application root directory.
    /// </summary>
    public ProjectPathProvider()
    {
        string baseDir = AppContext.BaseDirectory;

        string devRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));

        _appRoot = File.Exists(Path.Combine(devRoot, "DeepLearning.csproj"))
            ? devRoot
            : baseDir;
    }

    /// <inheritdoc />
    public string GetProjectRoot() => _appRoot;

    /// <inheritdoc />
    public string GetProjectFilePath(string relativePath)
        => Path.GetFullPath(Path.Combine(_appRoot, relativePath));

    /// <inheritdoc />
    public string GetAbsolutePath(string path)
        => Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : GetProjectFilePath(path);

    /// <inheritdoc />
    public string[] GetImageFiles()
    {
        if (!Directory.Exists(_appRoot))
            return [];

        return Directory.GetFiles(_appRoot)
            .Where(f => ImageExtensions.Contains(
                Path.GetExtension(f).TrimStart('.').ToLowerInvariant(),
                StringComparer.OrdinalIgnoreCase))
            .Select(f => Path.GetFileName(f))
            .OrderBy(f => f)
            .ToArray();
    }
}
