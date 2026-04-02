// Persists and retrieves custom model metadata across application sessions.

using System.Text.Json;
using DeepLearning.Application.Models;

namespace DeepLearning.Application.Configuration;

/// <summary>
/// Manages user-provided metadata for custom ONNX models.
/// When a user loads a model not in the built-in catalog, they can add a description
/// and it will be saved to a JSON file for future sessions.
/// </summary>
public sealed class CustomModelRegistry
{
    private readonly string _registryPath;
    private Dictionary<string, ModelSummary> _entries;

    /// <summary>
    /// Creates a new registry that persists to the specified JSON file.
    /// </summary>
    /// <param name="registryPath">Full path to the JSON registry file (default: custom-models.json in app root).</param>
    public CustomModelRegistry(string? registryPath = null)
    {
        _registryPath = registryPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom-models.json");
        _entries = LoadRegistry();
    }

    /// <summary>
    /// Registers a custom model with user-provided metadata.
    /// </summary>
    /// <param name="modelPath">Full path to the ONNX model file.</param>
    /// <param name="labels">Array of class label names the model detects.</param>
    /// <param name="description">Optional human-readable description of the model.</param>
    public void RegisterModel(string modelPath, string[] labels, string? description = null)
    {
        string fileName = Path.GetFileName(modelPath);
        string absolutePath = Path.GetFullPath(modelPath);

        _entries[absolutePath] = new ModelSummary
        {
            ModelName = description != null ? $"{fileName} — {description}" : fileName,
            ModelFile = fileName,
            Description = description ?? "Custom user-provided model.",
            Classes = labels,
            InputSize = "640x640",
            TrainingDate = DateTime.Now.ToString("MMMM yyyy"),
            Metrics = "Custom model — metrics not available"
        };

        SaveRegistry();
    }

    /// <summary>
    /// Retrieves metadata for a previously registered custom model.
    /// </summary>
    /// <param name="modelPath">Full or relative path to the ONNX model file.</param>
    /// <returns>The stored ModelSummary, or null if not found.</returns>
    public ModelSummary? GetCustomModel(string modelPath)
    {
        string absolutePath = Path.GetFullPath(modelPath);
        return _entries.GetValueOrDefault(absolutePath);
    }

    /// <summary>
    /// Loads the registry from the JSON file on disk.
    /// Returns an empty dictionary if the file doesn't exist or is invalid.
    /// </summary>
    private Dictionary<string, ModelSummary> LoadRegistry()
    {
        if (!File.Exists(_registryPath))
        {
            return new Dictionary<string, ModelSummary>();
        }

        try
        {
            string json = File.ReadAllText(_registryPath);
            var entries = JsonSerializer.Deserialize<Dictionary<string, ModelSummary>>(json);
            return entries ?? new Dictionary<string, ModelSummary>();
        }
        catch
        {
            // If the file is corrupt or unreadable, start fresh
            return new Dictionary<string, ModelSummary>();
        }
    }

    /// <summary>
    /// Saves the current registry entries to the JSON file on disk.
    /// </summary>
    private void SaveRegistry()
    {
        try
        {
            string json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_registryPath, json);
        }
        catch
        {
            // Silently fail — registry persistence is non-critical
        }
    }
}
