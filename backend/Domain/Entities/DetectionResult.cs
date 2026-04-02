// Core domain entity representing a single detected object with class, confidence, and bounding box.

namespace DeepLearning.Domain.Entities;

/// <summary>
/// Represents a single detected object returned by the model.
/// Contains the class index, confidence score, and bounding box in corner-format coordinates.
/// </summary>
public sealed class DetectionResult
{
    /// <summary>
    /// Zero-based index of the detected class. Maps to the label at the same index in the class label array.
    /// </summary>
    public required int ClassId { get; init; }

    /// <summary>
    /// Model confidence for this detection, between 0.0 (not confident) and 1.0 (very confident).
    /// </summary>
    public required float Confidence { get; init; }

    /// <summary>
    /// Left edge of the bounding box, in original image pixels.
    /// </summary>
    public required float X1 { get; init; }

    /// <summary>
    /// Top edge of the bounding box, in original image pixels.
    /// </summary>
    public required float Y1 { get; init; }

    /// <summary>
    /// Right edge of the bounding box, in original image pixels.
    /// </summary>
    public required float X2 { get; init; }

    /// <summary>
    /// Bottom edge of the bounding box, in original image pixels.
    /// </summary>
    public required float Y2 { get; init; }

    /// <summary>Width of the bounding box in pixels (X2 minus X1).</summary>
    public float Width => X2 - X1;

    /// <summary>Height of the bounding box in pixels (Y2 minus Y1).</summary>
    public float Height => Y2 - Y1;

    /// <summary>Area of the bounding box in square pixels.</summary>
    public float Area => Width * Height;

    /// <summary>
    /// Builds a human-readable string describing this detection.
    /// </summary>
    /// <param name="classLabels">Array of class label strings, indexed by class ID.</param>
    /// <returns>A string like "soap 44.2% at (206, 190, 727, 953)".</returns>
    public string Format(string[] classLabels)
    {
        string label = ClassId >= 0 && ClassId < classLabels.Length
            ? classLabels[ClassId]
            : $"unknown({ClassId})";

        return $"{label} {Confidence:P1} at ({X1:F0}, {Y1:F0}, {X2:F0}, {Y2:F0})";
    }
}
