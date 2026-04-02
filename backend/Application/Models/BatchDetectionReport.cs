// DTO carrying the result of batch image detection — folder path, per-image reports, and aggregate stats.

using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Models;

/// <summary>
/// Encapsulates the complete result of a batch (folder) detection run.
/// Produced by <see cref="UseCases.DetectImagesInFolderUseCase"/> and consumed by the user interface.
/// </summary>
public sealed class BatchDetectionReport
{
    /// <summary>The full path to the folder that was processed.</summary>
    public required string FolderPath { get; init; }

    /// <summary>Total number of image files found in the folder.</summary>
    public required int TotalImages { get; init; }

    /// <summary>Number of images successfully processed.</summary>
    public required int ProcessedImages { get; init; }

    /// <summary>Number of images that failed during processing.</summary>
    public required int FailedImages { get; init; }

    /// <summary>Individual detection reports for each successfully processed image.</summary>
    public required IReadOnlyList<ImageDetectionReport> Reports { get; init; }

    /// <summary>Total time taken to process the entire folder.</summary>
    public required TimeSpan Elapsed { get; init; }

    /// <summary>Aggregate count of all detections across all images.</summary>
    public int TotalDetections => Reports.Sum(r => r.Detections.Count);

    /// <summary>Per-class detection counts aggregated across all images.</summary>
    public Dictionary<string, int> GetClassCounts(string[] classLabels)
    {
        var counts = new Dictionary<string, int>();
        foreach (var report in Reports)
        {
            foreach (var detection in report.Detections)
            {
                string label = detection.ClassId >= 0 && detection.ClassId < classLabels.Length
                    ? classLabels[detection.ClassId]
                    : $"unknown({detection.ClassId})";

                counts[label] = counts.GetValueOrDefault(label, 0) + 1;
            }
        }
        return counts;
    }
}
