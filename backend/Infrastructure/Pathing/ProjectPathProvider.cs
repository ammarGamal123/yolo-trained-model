// Smart path resolver that auto-detects dev vs. deployed environment.

using System;
using System.IO;
using System.Linq;
using DeepLearning.Application.Abstractions;

namespace DeepLearning.Infrastructure.Pathing;

/// <summary>
/// Resolves paths relative to the application base directory (where the .exe lives).
/// For development builds, this resolves to the backend/ folder so that relative paths
/// like "../models/yolo11n.onnx" and "../ai-training/..." are found correctly.
/// For published (deployed) builds, this resolves to the publish folder itself,
/// allowing the exe to be distributed as-is with its model and sample files.
/// </summary>
public sealed class ProjectPathProvider : IProjectPathProvider
{
    private readonly string _appRoot;
    private readonly bool _isDeployed;

    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];

    /// <summary>
    /// Initializes a new <see cref="ProjectPathProvider"/> by determining
    /// the appropriate application root directory.
    /// </summary>
    public ProjectPathProvider()
    {
        string baseDir = AppContext.BaseDirectory;

        string devRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));

        _isDeployed = !File.Exists(Path.Combine(devRoot, "DeepLearning.csproj"));
        _appRoot = _isDeployed ? baseDir : devRoot;
    }

    /// <summary>
    /// Returns true if this is a deployed (published) build.
    /// </summary>
    public bool IsDeployed => _isDeployed;

    /// <inheritdoc />
    public string GetProjectRoot() => _appRoot;

    /// <inheritdoc />
    public string GetProjectFilePath(string relativePath)
    {
        if (_isDeployed)
        {
            // In deployed mode, paths like "../models/x.onnx" should resolve to "models/x.onnx"
            // because the models folder is inside the publish folder.
            string cleaned = relativePath.StartsWith("..")
                ? relativePath.Substring(relativePath.IndexOf('/', relativePath.LastIndexOf("..") + 2) + 1)
                : relativePath;
            return Path.GetFullPath(Path.Combine(_appRoot, cleaned));
        }

        return Path.GetFullPath(Path.Combine(_appRoot, relativePath));
    }

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
