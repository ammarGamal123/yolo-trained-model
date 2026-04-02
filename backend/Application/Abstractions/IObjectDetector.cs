// Contract for any object detection engine — decouples the app from a specific ML runtime.

using System.Drawing;
using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Abstractions;

/// <summary>
/// Contract for any object detection engine.
/// Implementations load a model file and return a list of detected objects for a given image.
/// This abstraction allows the application to stay decoupled from any specific ML runtime (ONNX, TensorRT, etc.).
/// </summary>
public interface IObjectDetector : IDisposable
{
    /// <summary>
    /// Runs object detection on a single image.
    /// </summary>
    /// <param name="image">The source image to analyze. Any size is supported; the detector resizes internally.</param>
    /// <returns>A read-only list of detected objects with positions and confidence scores. Returns an empty list if no objects meet the confidence threshold.</returns>
    IReadOnlyList<DetectionResult> Detect(Bitmap image);
}
