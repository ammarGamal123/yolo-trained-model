// Static catalog of known ONNX models with their human-readable summaries.

using DeepLearning.Application.Models;

namespace DeepLearning.Infrastructure.ModelMetadata;

/// <summary>
/// Central registry of known detection models and their metadata.
/// When a user loads a model that matches a known entry, the app displays
/// a rich summary instead of just the filename.
/// </summary>
public static class ModelCatalog
{
    /// <summary>
    /// Detector v4 — Enhanced multi-object model trained with improved parameters.
    /// Trained with 300 epochs, AdamW optimizer, mosaic/mixup augmentation, focal loss.
    /// Addresses previous class-swap and low-confidence issues from v3.
    /// </summary>
    public static ModelSummary DetectorV4 => new()
    {
        ModelName = "Detector v4 (Enhanced)",
        ModelFile = "detector_v4.onnx",
        Description = "Enhanced multi-object detector for bottles, soaps, and soap covers. "
            + "Trained with 300 epochs, AdamW optimizer, mosaic/mixup augmentation, "
            + "increased classification loss weight (cls=2.0), and label smoothing. "
            + "Fixes class-swap bug from v3 and improves soap/soap-cover discrimination.",
        Classes = ["bottle", "soap", "soap-cover"],
        InputSize = "640x640",
        TrainingDate = "April 2026",
        Metrics = "Training in progress — metrics will be updated after completion"
    };

    /// <summary>
    /// Detector v3 — Multi-object model (had class-swap and low-confidence issues).
    /// Trained with 83 epochs (early stopped), 10x augmented dataset.
    /// Actual metrics from training: mAP50=51.7%, Precision=52.8%, Recall=45.0%.
    /// </summary>
    public static ModelSummary DetectorV3 => new()
    {
        ModelName = "Detector v3 (Legacy)",
        ModelFile = "detector_v3.onnx",
        Description = "Multi-object detector trained on bottles, soaps, and soap covers. "
            + "Known issues: soap class has 0% detection accuracy (confusion with background), "
            + "soap-cover class has 13.5% accuracy. Superseded by v4.",
        Classes = ["bottle", "soap-cover", "soap"],
        InputSize = "640x640",
        TrainingDate = "April 2026",
        Metrics = "mAP50: 51.7% | Precision: 52.8% | Recall: 45.0% | (83 epochs, early stopped)"
    };

    /// <summary>
    /// Detector v2 — Previous model trained on bottle-only dataset (52 images, 6x augmentation).
    /// Retained for backward compatibility.
    /// </summary>
    public static ModelSummary DetectorV2 => new()
    {
        ModelName = "Detector v2",
        ModelFile = "detector_v2.onnx",
        Description = "Object detector trained primarily on bottles. "
            + "Earlier version with limited class coverage. "
            + "Upgraded to v3 for full bottle + soap + soap-cover detection.",
        Classes = ["bottle", "soap-cover", "soap"],
        InputSize = "640x640",
        TrainingDate = "April 2026",
        Metrics = "mAP50: 60.1% | Precision: 80.8% | Recall: 63.9%"
    };

    /// <summary>
    /// YOLOv8n base model — Pre-trained on COCO dataset.
    /// Generic 80-class detector, not fine-tuned for our use case.
    /// </summary>
    public static ModelSummary YoloV8n => new()
    {
        ModelName = "YOLOv8n (COCO)",
        ModelFile = "yolov8n.onnx",
        Description = "Pre-trained YOLOv8 nano model on COCO 80-class dataset. "
            + "Detects common objects (person, car, dog, etc.). "
            + "Not fine-tuned for bottle/soap detection.",
        Classes = ["person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light", "... and 70 more"],
        InputSize = "640x640",
        TrainingDate = "COCO 2017",
        Metrics = "COCO mAP50-95: 37.3%"
    };

    /// <summary>
    /// YOLOv11n base model — Pre-trained on COCO dataset.
    /// Generic 80-class detector, not fine-tuned for our use case.
    /// </summary>
    public static ModelSummary YoloV11n => new()
    {
        ModelName = "YOLOv11n (COCO)",
        ModelFile = "yolo11n.onnx",
        Description = "Pre-trained YOLOv11 nano model on COCO 80-class dataset. "
            + "Latest YOLO architecture with improved accuracy. "
            + "Not fine-tuned for bottle/soap detection.",
        Classes = ["person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light", "... and 70 more"],
        InputSize = "640x640",
        TrainingDate = "COCO 2017",
        Metrics = "COCO mAP50-95: 39.5%"
    };

    /// <summary>
    /// Bottle v1 — Legacy single-class bottle detector.
    /// Superseded by v2 and v3.
    /// </summary>
    public static ModelSummary BottleV1 => new()
    {
        ModelName = "Bottle Detector v1",
        ModelFile = "bottle_v1.onnx",
        Description = "Legacy single-class bottle detector. "
            + "Only detects bottles. Superseded by detector_v3.onnx and detector_v4.onnx "
            + "which also detect soap and soap-cover.",
        Classes = ["bottle"],
        InputSize = "640x640",
        TrainingDate = "March 2026",
        Metrics = "Bottle-only detection"
    };

    /// <summary>
    /// Tries to find a known model summary by filename.
    /// </summary>
    /// <param name="modelPath">Full or relative path to the ONNX model file.</param>
    /// <returns>The matching ModelSummary, or null if the model is not in the catalog.</returns>
    public static ModelSummary? TryGetSummary(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath))
            return null;

        string fileName = Path.GetFileName(modelPath).ToLowerInvariant();

        return fileName switch
        {
            "detector_v4.onnx" => DetectorV4,
            "detector_v3.onnx" => DetectorV3,
            "detector_v2.onnx" => DetectorV2,
            "yolov8n.onnx" => YoloV8n,
            "yolo11n.onnx" => YoloV11n,
            "bottle_v1.onnx" => BottleV1,
            _ => null
        };
    }
}
