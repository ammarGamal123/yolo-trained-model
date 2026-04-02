// Real-time webcam detection loop using OpenCV: capture → detect → render → display.

using System.Drawing;
using DeepLearning.Application.Abstractions;
using DeepLearning.Application.Configuration;
using DeepLearning.Domain.Entities;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace DeepLearning.Infrastructure.Capture;

/// <summary>
/// Manages webcam capture and real-time object detection display using OpenCV.
///
/// <para>
/// Each frame is captured from the camera, converted to a <see cref="Bitmap"/>,
/// passed to the detector, rendered with overlays, and displayed in a window.
/// The loop runs until the user presses the ESC key.</para>
///
/// <para>
/// This class depends on <see cref="IObjectDetector"/> and <see cref="IImageRenderer"/>
/// via constructor injection (SOLID Dependency Inversion Principle), so the detection
/// and rendering logic can be swapped without changing this class.
/// </para>
/// </summary>
public sealed class WebcamDetectionLoop : IWebcamDetectionLoop
{
    private readonly DetectionOptions _options;
    private readonly IObjectDetector _detector;
    private readonly IImageRenderer _imageRenderer;
    private readonly IUserInterface _userInterface;

    /// <summary>
    /// Creates a new webcam detection loop with all required dependencies.
    /// </summary>
    /// <param name="options">Must supply <see cref="DetectionOptions.CameraIndex"/> and <see cref="DetectionOptions.WindowTitle"/>.</param>
    /// <param name="detector">The object detection engine to run on each frame.</param>
    /// <param name="imageRenderer">Renders detection overlays on each frame.</param>
    /// <param name="userInterface">Used to print errors and status messages.</param>
    public WebcamDetectionLoop(
        DetectionOptions options,
        IObjectDetector detector,
        IImageRenderer imageRenderer,
        IUserInterface userInterface)
    {
        _options = options;
        _detector = detector;
        _imageRenderer = imageRenderer;
        _userInterface = userInterface;
    }

    /// <inheritdoc />
    public void Run()
    {
        using var capture = new VideoCapture(_options.CameraIndex, VideoCaptureAPIs.DSHOW);

        if (!capture.IsOpened())
        {
            _userInterface.ShowError(
                $"Unable to open webcam with camera index {_options.CameraIndex}. " +
                "Check that the camera is connected and not in use by another application.");
            return;
        }

        using var frame = new Mat();

        while (true)
        {
            capture.Read(frame);

            if (frame.Empty())
            {
                continue;
            }

            using Bitmap bitmapFrame = BitmapConverter.ToBitmap(frame);
            IReadOnlyList<DetectionResult> detections = _detector.Detect(bitmapFrame);
            using Bitmap overlay = _imageRenderer.DrawDetections(bitmapFrame, detections);
            using Mat displayFrame = BitmapConverter.ToMat(overlay);

            Cv2.ImShow(_options.WindowTitle, displayFrame);

            if (Cv2.WaitKey(1) == 27)
            {
                break;
            }
        }

        capture.Release();
        Cv2.DestroyAllWindows();
        _userInterface.ShowInfo("Webcam detection stopped.");
    }
}
