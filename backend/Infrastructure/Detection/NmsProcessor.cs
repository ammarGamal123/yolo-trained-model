// Non-Maximum Suppression: removes duplicate/overlapping bounding boxes by IoU.

using DeepLearning.Domain.Entities;

namespace DeepLearning.Infrastructure.Detection;

/// <summary>
/// Applies Non-Maximum Suppression (NMS) to a list of raw detection results.
///
/// <para>
/// YOLO models output thousands of candidate bounding boxes per image.
/// Many of these boxes overlap the same object. NMS removes duplicate boxes
/// so that only the most confident detection per object remains.
///
/// <list type="number">
///   <item>Group detections by class (each class is filtered independently).</item>
///   <item>Sort each group by confidence score, highest first.</item>
///   <item>Keep the top-scoring box, then discard every remaining box whose IoU
///        with it exceeds the threshold (too much overlap = same object).</item>
///   <item>Repeat until no candidates remain.</item>
/// </list>
/// </para>
/// </summary>
public static class NmsProcessor
{
    /// <summary>
    /// Applies Non-Maximum Suppression to the given detections.
    /// </summary>
    /// <param name="detections">Raw detections from the model (may contain duplicates).</param>
    /// <param name="iouThreshold">
    /// Maximum allowed IoU between two boxes before the lower-confidence one is discarded.
    /// Range: 0.0 (keep everything) to 1.0 (discard all but one per location).
    /// </param>
    /// <returns>A filtered list containing only the best detection per object.</returns>
    public static IReadOnlyList<DetectionResult> Apply(IEnumerable<DetectionResult> detections, float iouThreshold)
    {
        List<DetectionResult> keptDetections = [];

        foreach (IGrouping<int, DetectionResult> classGroup in detections.GroupBy(detection => detection.ClassId))
        {
            List<DetectionResult> candidates = classGroup
                .OrderByDescending(detection => detection.Confidence)
                .ToList();

            while (candidates.Count > 0)
            {
                DetectionResult bestDetection = candidates[0];
                keptDetections.Add(bestDetection);
                candidates.RemoveAt(0);

                candidates = candidates
                    .Where(candidate => CalculateIoU(bestDetection, candidate) < iouThreshold)
                    .ToList();
            }
        }

        return keptDetections;
    }

    /// <summary>
    /// Calculates Intersection over Union (IoU) between two bounding boxes.
    /// IoU = Overlap Area / Union Area. Returns 0.0 (no overlap) to 1.0 (identical boxes).
    /// </summary>
    private static float CalculateIoU(DetectionResult first, DetectionResult second)
    {
        float overlapX1 = Math.Max(first.X1, second.X1);
        float overlapY1 = Math.Max(first.Y1, second.Y1);
        float overlapX2 = Math.Min(first.X2, second.X2);
        float overlapY2 = Math.Min(first.Y2, second.Y2);

        float overlapArea = Math.Max(0f, overlapX2 - overlapX1) * Math.Max(0f, overlapY2 - overlapY1);
        float unionArea = first.Area + second.Area - overlapArea;

        return overlapArea / (unionArea + 1e-6f);
    }
}
