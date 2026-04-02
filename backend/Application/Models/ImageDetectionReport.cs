// DTO carrying detection results: input path, output path, and list of detections.

using System.Collections.Generic;
using DeepLearning.Application.Abstractions;
using DeepLearning.Application.Configuration;
using DeepLearning.Application.Models;
using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Models;

/// <summary>
/// Encapsulates the complete result of an image detection run.
/// Produced by <see cref="UseCases.DetectImageFromFileUseCase"/> and consumed by the user interface.
/// </summary>
public sealed class ImageDetectionReport
{
    /// <summary>
    /// The full path to the input image file that was processed.
    /// </summary>
    public required string InputPath { get; init; }

    /// <summary>
    /// The full path to the output image file where annotations were saved.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// The list of detected objects found in the image.
    /// </summary>
    public required IReadOnlyList<DetectionResult> Detections { get; init; }

    /// <summary>Time taken to process this image.</summary>
    public TimeSpan Elapsed { get; init; }
}
