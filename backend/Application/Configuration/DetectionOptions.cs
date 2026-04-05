// Central configuration POCO — model path, thresholds, camera index, and output settings.

namespace DeepLearning.Application.Configuration;

public sealed class DetectionOptions
{
    public string ModelPath { get; set; } = "../models/detector_v4.onnx";
    public string[] ClassLabels { get; set; } = ["bottle", "soap-cover", "soap"];
    public int ModelWidth { get; set; } = 640;
    public int ModelHeight { get; set; } = 640;
    public float ConfidenceThreshold { get; set; } = 0.25f;
    public float IouThreshold { get; set; } = 0.50f;
    public int CameraIndex { get; set; } = 0;
    public string WindowTitle { get; set; } = "Object Detection (ESC to exit)";
    public string DefaultImagePath { get; set; } = "../models/test/img.jpg";
    public string OutputFileName { get; set; } = "output.jpg";
    public bool AutoOpenOutput { get; set; } = true;

    public int DisplayWidth { get; set; } = 960;
    public int DisplayHeight { get; set; } = 540;

    public bool UseSoftNms { get; set; } = true;
    public float SoftNmsSigma { get; set; } = 0.5f;
    public bool MergeCloseDetections { get; set; } = true;
    public float MergeDistanceThreshold { get; set; } = 30f;
}
