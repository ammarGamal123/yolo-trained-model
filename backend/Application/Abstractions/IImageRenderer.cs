// Contract for rendering detection overlays onto an image without modifying the source.

using System.Drawing;
using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Abstractions;

/// <summary>
/// Contract for rendering detection results onto an image.
/// Implementations draw bounding boxes and labels on a copy of the source image without modifying the original.
/// </summary>
public interface IImageRenderer
{
    /// <summary>
    /// Draws bounding boxes and class labels on a copy of the provided image.
    /// </summary>
    /// <param name="image">The original image to annotate. It is not modified.</param>
    /// <param name="detections">The detection results to render.</param>
    /// <returns>A new <see cref="Bitmap"/> with the overlays drawn on it. The caller is responsible for disposing it.</returns>
    Bitmap DrawDetections(Bitmap image, IReadOnlyCollection<DetectionResult> detections);
}
