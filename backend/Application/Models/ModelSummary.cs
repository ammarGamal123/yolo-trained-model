// DTO carrying metadata about a detection model — name, classes, description, and training metrics.

namespace DeepLearning.Application.Models;

/// <summary>
/// Encapsulates human-readable metadata about an ONNX detection model.
/// Used to display model summaries in the console UI and to identify known models.
/// </summary>
public sealed class ModelSummary
{
    /// <summary>Human-readable model name (e.g., "Detector v3").</summary>
    public required string ModelName { get; init; }

    /// <summary>The ONNX filename (e.g., "detector_v3.onnx").</summary>
    public required string ModelFile { get; init; }

    /// <summary>Description of what the model detects and its capabilities.</summary>
    public required string Description { get; init; }

    /// <summary>Ordered class names the model was trained to detect.</summary>
    public required string[] Classes { get; init; }

    /// <summary>Model input dimensions (e.g., "640x640").</summary>
    public required string InputSize { get; init; }

    /// <summary>When the model was trained (e.g., "April 2026").</summary>
    public required string TrainingDate { get; init; }

    /// <summary>Training metrics summary (e.g., "mAP50: 70% | Precision: 85%").</summary>
    public required string Metrics { get; init; }
}
