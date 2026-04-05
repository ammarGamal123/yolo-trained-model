// Non-Maximum Suppression: removes duplicate/overlapping bounding boxes by IoU.
// Supports both standard NMS and Soft-NMS for better handling of overlapping detections.

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
    /// <param name="useSoftNms">If true, uses Soft-NMS which reduces scores instead of removing boxes.</param>
    /// <param name="softNmsSigma">Sigma parameter for Soft-NMS Gaussian decay (higher = less aggressive).</param>
    /// <returns>A filtered list containing only the best detection per object.</returns>
    public static IReadOnlyList<DetectionResult> Apply(
        IEnumerable<DetectionResult> detections, 
        float iouThreshold,
        bool useSoftNms = false,
        float softNmsSigma = 0.5f)
    {
        List<DetectionResult> keptDetections = [];

        foreach (IGrouping<int, DetectionResult> classGroup in detections.GroupBy(detection => detection.ClassId))
        {
            List<ScoredDetection> candidates = classGroup
                .Select(d => new ScoredDetection(d, d.Confidence))
                .OrderByDescending(d => d.Score)
                .ToList();

            while (candidates.Count > 0)
            {
                ScoredDetection best = candidates[0];
                
                if (best.Score < 0.1f)
                {
                    candidates.RemoveAt(0);
                    continue;
                }

                keptDetections.Add(new DetectionResult
                {
                    ClassId = best.Detection.ClassId,
                    Confidence = best.Score,
                    X1 = best.Detection.X1,
                    Y1 = best.Detection.Y1,
                    X2 = best.Detection.X2,
                    Y2 = best.Detection.Y2
                });

                candidates.RemoveAt(0);

                if (useSoftNms)
                {
                    for (int i = candidates.Count - 1; i >= 0; i--)
                    {
                        float iou = CalculateIoU(best.Detection, candidates[i].Detection);
                        if (iou > 0f)
                        {
                            float decay = (float)Math.Exp(-(iou * iou) / softNmsSigma);
                            candidates[i] = candidates[i] with { Score = candidates[i].Score * decay };
                        }
                    }
                }
                else
                {
                    candidates = candidates
                        .Where(c => CalculateIoU(best.Detection, c.Detection) < iouThreshold)
                        .ToList();
                }

                candidates = candidates
                    .Where(c => c.Score >= 0.1f)
                    .OrderByDescending(c => c.Score)
                    .ToList();
            }
        }

        return keptDetections;
    }

    /// <summary>
    /// Calculates Intersection over Union (IoU) between two bounding boxes.
    /// IoU = Overlap Area / Union Area. Returns 0.0 (no overlap) to 1.0 (identical boxes).
    /// </summary>
    public static float CalculateIoU(DetectionResult first, DetectionResult second)
    {
        float overlapX1 = Math.Max(first.X1, second.X1);
        float overlapY1 = Math.Max(first.Y1, second.Y1);
        float overlapX2 = Math.Min(first.X2, second.X2);
        float overlapY2 = Math.Min(first.Y2, second.Y2);

        float overlapArea = Math.Max(0f, overlapX2 - overlapX1) * Math.Max(0f, overlapY2 - overlapY1);
        float unionArea = first.Area + second.Area - overlapArea;

        return overlapArea / (unionArea + 1e-6f);
    }

    /// <summary>
    /// Calculates center distance between two bounding boxes.
    /// </summary>
    public static float CalculateCenterDistance(DetectionResult first, DetectionResult second)
    {
        float cx1 = (first.X1 + first.X2) / 2f;
        float cy1 = (first.Y1 + first.Y2) / 2f;
        float cx2 = (second.X1 + second.X2) / 2f;
        float cy2 = (second.Y1 + second.Y2) / 2f;

        float dx = cx1 - cx2;
        float dy = cy1 - cy2;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private readonly record struct ScoredDetection(DetectionResult Detection, float Score);
}
