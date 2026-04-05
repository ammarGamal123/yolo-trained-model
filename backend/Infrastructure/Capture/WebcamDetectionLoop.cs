// Real-time webcam detection loop using OpenCV: capture → detect → render → display.
// Uses OpenCV resize for fast detection input and GDI+ for a clean stats overlay.

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using DeepLearning.Application.Abstractions;
using DeepLearning.Application.Configuration;
using DeepLearning.Domain.Entities;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Color = System.Drawing.Color;

namespace DeepLearning.Infrastructure.Capture;

/// <summary>
/// Manages webcam capture and real-time object detection display using OpenCV.
/// Displays at native camera resolution with a clean GDI+ stats overlay.
/// Detection uses fast OpenCV resize for minimal overhead.
/// </summary>
public sealed class WebcamDetectionLoop : IWebcamDetectionLoop
{
    private readonly DetectionOptions _options;
    private readonly IObjectDetector _detector;
    private readonly IImageRenderer _imageRenderer;
    private readonly IUserInterface _userInterface;

    // Panel
    private const int PanelWidth = 280;
    private const int PanelMargin = 14;
    private const int TitleBarHeight = 36;
    private const int RowHeight = 34;
    private const int PaddingX = 16;
    private const int PaddingY = 12;

    // Fonts
    private static readonly Font TitleFont = new("Segoe UI", 12, FontStyle.Bold);
    private static readonly Font LabelFont = new("Segoe UI", 11, FontStyle.Regular);
    private static readonly Font ValueFont = new("Segoe UI", 12, FontStyle.Bold);
    private static readonly StringFormat CenterAlign = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    private static readonly StringFormat LeftAlign = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
    private static readonly StringFormat RightAlign = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

    // Colors
    private static readonly Color PanelBg = Color.FromArgb(210, 12, 12, 12);
    private static readonly Color TitleBarBg = Color.FromArgb(230, 18, 42, 72);
    private static readonly Color BorderColor = Color.FromArgb(160, 60, 140, 200);
    private static readonly Color SeparatorColor = Color.FromArgb(50, 50, 50);

    // Label colors — soft, harmonious palette
    private static readonly Color FpsLabel = Color.FromArgb(80, 255, 80);
    private static readonly Color AvgFpsLabel = Color.FromArgb(80, 200, 255);
    private static readonly Color InferenceLabel = Color.FromArgb(255, 200, 60);
    private static readonly Color ObjectsLabel = Color.FromArgb(255, 120, 200);
    private static readonly Color FramesLabel = Color.FromArgb(180, 180, 255);
    private static readonly Color SessionLabel = Color.FromArgb(255, 180, 100);

    // Value colors
    private static readonly Color ValueGood = Color.FromArgb(80, 255, 80);
    private static readonly Color ValueWarm = Color.FromArgb(80, 200, 255);
    private static readonly Color ValueAlert = Color.FromArgb(255, 80, 80);
    private static readonly Color ValueWhite = Color.White;

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

        capture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));
        capture.Set(VideoCaptureProperties.FrameWidth, 1920);
        capture.Set(VideoCaptureProperties.FrameHeight, 1080);
        capture.Set(VideoCaptureProperties.Fps, 30);
        capture.Set(VideoCaptureProperties.AutoFocus, 1);
        capture.Set(VideoCaptureProperties.AutoExposure, 1);

        int captureWidth = (int)capture.Get(VideoCaptureProperties.FrameWidth);
        int captureHeight = (int)capture.Get(VideoCaptureProperties.FrameHeight);

        int detectWidth = _options.ModelWidth;
        int detectHeight = _options.ModelHeight;
        using var detectFrame = new Mat();

        using var frame = new Mat();
        int totalFrames = 0;
        var sessionStopwatch = Stopwatch.StartNew();
        DetectionMetrics? finalMetrics = null;

        Cv2.NamedWindow(_options.WindowTitle, WindowFlags.Normal);
        Cv2.ResizeWindow(_options.WindowTitle, _options.DisplayWidth, _options.DisplayHeight);

        while (true)
        {
            capture.Read(frame);
            if (frame.Empty()) continue;

            using Bitmap detectBitmap = BitmapConverter.ToBitmap(frame);
            IReadOnlyList<DetectionResult> detections = _detector.Detect(detectBitmap);

            // Scale detections to display size for overlay
            double scaleX = (double)_options.DisplayWidth / frame.Width;
            double scaleY = (double)_options.DisplayHeight / frame.Height;
            var scaledDetections = ScaleDetections(detections, scaleX, scaleY);

            // Resize frame for display
            using var displayFrameMat = new Mat();
            Cv2.Resize(frame, displayFrameMat, new OpenCvSharp.Size(_options.DisplayWidth, _options.DisplayHeight));
            using Bitmap displayBitmap = BitmapConverter.ToBitmap(displayFrameMat);
            using Bitmap overlay = _imageRenderer.DrawDetections(displayBitmap, scaledDetections);

            totalFrames++;
            var metrics = _detector.GetMetrics();
            finalMetrics = metrics;

            DrawInfoPanel(overlay, metrics, detections.Count, totalFrames, sessionStopwatch.Elapsed);

            using Mat displayFrame = BitmapConverter.ToMat(overlay);
            Cv2.ImShow(_options.WindowTitle, displayFrame);

            if (Cv2.WaitKey(1) == 27) break;
        }

        capture.Release();
        Cv2.DestroyAllWindows();

        sessionStopwatch.Stop();
        _userInterface.ShowWebcamSummary(finalMetrics!, totalFrames, sessionStopwatch.Elapsed);
    }

    /// <summary>
    /// Draws a clean, eye-friendly stats panel using GDI+ with anti-aliased text.
    /// </summary>
    private void DrawInfoPanel(Bitmap bitmap, DetectionMetrics metrics, int detectionCount, int totalFrames, TimeSpan totalTime)
    {
        int panelHeight = TitleBarHeight + (RowHeight * 6) + PaddingY * 2 + 4;
        int x = bitmap.Width - PanelWidth - PanelMargin;
        int y = PanelMargin;

        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.CompositingQuality = CompositingQuality.HighQuality;

        // Panel background
        using GraphicsPath panelPath = CreateRoundedRect(x, y, PanelWidth, panelHeight, 10);
        using SolidBrush bgBrush = new(PanelBg);
        g.FillPath(bgBrush, panelPath);

        // Title bar
        using GraphicsPath titlePath = CreateRoundedRectTop(x, y, PanelWidth, TitleBarHeight, 10);
        using SolidBrush titleBrush = new(TitleBarBg);
        g.FillPath(titleBrush, titlePath);

        // Border
        using Pen borderPen = new(BorderColor, 2);
        g.DrawPath(borderPen, panelPath);

        // Separator
        g.DrawLine(new Pen(BorderColor, 2), x + 4, y + TitleBarHeight, x + PanelWidth - 4, y + TitleBarHeight);

        // Title
        g.DrawString("LIVE STATS", TitleFont, Brushes.White,
            new RectangleF(x, y, PanelWidth, TitleBarHeight), CenterAlign);

        // Rows
        int rowY = y + TitleBarHeight + PaddingY;

        double currentFps = metrics.CurrentFPS;
        Color fpsColor = currentFps >= 10 ? ValueGood : currentFps >= 5 ? ValueWarm : ValueAlert;
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "FPS", $"{currentFps:F1}", FpsLabel, fpsColor);

        rowY += RowHeight;
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "Avg FPS", $"{metrics.AverageFPS:F1}", AvgFpsLabel, ValueWhite);

        rowY += RowHeight;
        g.DrawLine(new Pen(SeparatorColor, 1), x + PaddingX, rowY, x + PanelWidth - PaddingX, rowY);

        rowY += RowHeight - 8;
        double lastMs = metrics.LastInferenceMs;
        Color timeColor = lastMs < 100 ? ValueGood : lastMs < 200 ? ValueWarm : ValueAlert;
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "Inference", $"{lastMs:F0} ms", InferenceLabel, timeColor);

        rowY += RowHeight;
        string objText = detectionCount > 0 ? $"{detectionCount} found" : "None";
        Color objColor = detectionCount > 0 ? ValueWarm : ValueWhite;
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "Objects", objText, ObjectsLabel, objColor);

        rowY += RowHeight;
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "Frames", $"{totalFrames:N0}", FramesLabel, ValueWhite);

        rowY += RowHeight;
        string timeStr = totalTime.TotalMinutes >= 1
            ? $"{(int)totalTime.TotalMinutes}m {totalTime.Seconds}s"
            : $"{totalTime.TotalSeconds:F0}s";
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "Session", timeStr, SessionLabel, ValueWhite);
    }

    private static void DrawRow(Graphics g, int x, int y, int width, string label, string value, Color labelColor, Color valueColor)
    {
        RectangleF labelRect = new(x, y, width / 2, RowHeight);
        RectangleF valueRect = new(x + width / 2, y, width / 2, RowHeight);

        using SolidBrush labelBrush = new(labelColor);
        using SolidBrush valBrush = new(valueColor);

        g.DrawString(label, LabelFont, labelBrush, labelRect, LeftAlign);
        g.DrawString(value, ValueFont, valBrush, valueRect, RightAlign);
    }

    private static GraphicsPath CreateRoundedRect(int x, int y, int width, int height, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(x, y, radius * 2, radius * 2, 180, 90);
        path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
        path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath CreateRoundedRectTop(int x, int y, int width, int height, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(x, y, radius * 2, radius * 2, 180, 90);
        path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
        path.AddLine(x + width, y + radius, x + width, y + height);
        path.AddLine(x + width, y + height, x, y + height);
        path.AddLine(x, y + height, x, y + radius);
        path.CloseFigure();
        return path;
    }

    private static List<DetectionResult> ScaleDetections(IReadOnlyList<DetectionResult> detections, double scaleX, double scaleY)
    {
        var scaled = new List<DetectionResult>(detections.Count);
        foreach (var d in detections)
        {
            scaled.Add(new DetectionResult
            {
                ClassId = d.ClassId,
                Confidence = d.Confidence,
                X1 = (float)(d.X1 * scaleX),
                Y1 = (float)(d.Y1 * scaleY),
                X2 = (float)(d.X2 * scaleX),
                Y2 = (float)(d.Y2 * scaleY)
            });
        }
        return scaled;
    }
}
