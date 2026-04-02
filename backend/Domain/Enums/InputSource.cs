// Input mode enum — Webcam (live camera), ExistingImage (static file), BatchFolder, ModelInfo, Settings.

namespace DeepLearning.Domain.Enums;

/// <summary>
/// Represents the available input modes and menu actions for the detection application.
/// </summary>
public enum InputSource
{
    /// <summary>
    /// Use the webcam as a live video source for real-time object detection.
    /// </summary>
    Webcam = 1,

    /// <summary>
    /// Use an existing image file stored on disk.
    /// </summary>
    ExistingImage = 2,

    /// <summary>
    /// Process all images in a folder through the detection pipeline.
    /// </summary>
    BatchFolder = 3,

    /// <summary>
    /// Display detailed information about the loaded model.
    /// </summary>
    ModelInfo = 4,

    /// <summary>
    /// Open the threshold tuning menu to adjust detection settings.
    /// </summary>
    Settings = 5
}
