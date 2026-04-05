// Real-time webcam detection loop using OpenCV: capture → detect → render → display.
// Maximizes webcam quality (native 1080p, MJPEG codec) and renders a compact info panel.

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DeepLearning.Application.Abstractions;
using DeepLearning.Application.Configuration;
using DeepLearning.Domain.Entities;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Color = System.Drawing.Color;

namespace DeepLearning.Infrastructure.Capture;

/// <summary>
/// Manages webcam capture and real-time object detection display using OpenCV.
/// Captures at native 1080p with MJPEG codec for maximum quality.
/// Renders a compact, user-friendly stats overlay using GDI+.
/// </summary>
public sealed class WebcamDetectionLoop : IWebcamDetectionLoop
{
    private readonly DetectionOptions _options;
    private readonly IObjectDetector _detector;
    private readonly IImageRenderer _imageRenderer;
    private readonly IUserInterface _userInterface;

    // ── Compact panel dimensions ──
    private const int PanelWidth = 260;
    private const int PanelMargin = 12;
    private const int TitleBarHeight = 32;
    private const int RowHeight = 28;
    private const int PaddingX = 14;
    private const int PaddingY = 10;

    // ── Fonts (smaller for compact panel) ──
    private static readonly Font TitleFont = new("Segoe UI", 11, FontStyle.Bold);
    private static readonly Font LabelFont = new("Segoe UI", 9, FontStyle.Bold);
    private static readonly Font ValueFont = new("Segoe UI", 10, FontStyle.Bold);
    private static readonly StringFormat CenterAlign = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    private static readonly StringFormat LeftAlign = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
    private static readonly StringFormat RightAlign = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

    // ── Colors ──
    private static readonly Color PanelBg = Color.FromArgb(200, 10, 10, 10);
    private static readonly Color TitleBarBg = Color.FromArgb(220, 15, 35, 65);
    private static readonly Color BorderColor = Color.FromArgb(140, 60, 140, 200);
    private static readonly Color SeparatorColor = Color.FromArgb(40, 255, 255, 255);

    // Label colors — softer, harmonious palette
    private static readonly Color SpeedLabelColor = Color.FromArgb(80, 255, 80);
    private static readonly Color AvgSpeedLabelColor = Color.FromArgb(80, 200, 255);
    private static readonly Color AiResponseLabelColor = Color.FromArgb(255, 200, 60);
    private static readonly Color DetectedLabelColor = Color.FromArgb(255, 120, 200);
    private static readonly Color ScannedLabelColor = Color.FromArgb(180, 180, 255);
    private static readonly Color UptimeLabelColor = Color.FromArgb(255, 180, 100);

    // Value colors
    private static readonly Color ValueGood = Color.FromArgb(80, 255, 80);
    private static readonly Color ValueWarm = Color.FromArgb(80, 200, 255);
    private static readonly Color ValueAlert = Color.FromArgb(255, 80, 80);
    private static readonly Color ValueWhite = Color.White;

    /// <summary>
    /// Creates a new webcam detection loop with all required dependencies.
    /// </summary>
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

        // ── Maximize webcam quality ──
        // Set MJPEG codec for higher quality than default YUY2
        capture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));

        // Request maximum resolution (1080p)
        capture.Set(VideoCaptureProperties.FrameWidth, 1920);
        capture.Set(VideoCaptureProperties.FrameHeight, 1080);

        // Request high framerate
        capture.Set(VideoCaptureProperties.Fps, 30);

        // Enable auto-focus and auto-exposure for best image quality
        capture.Set(VideoCaptureProperties.AutoFocus, 1);
        capture.Set(VideoCaptureProperties.AutoExposure, 1);

        // Read actual capture dimensions (camera may not support requested)
        int captureWidth = (int)capture.Get(VideoCaptureProperties.FrameWidth);
        int captureHeight = (int)capture.Get(VideoCaptureProperties.FrameHeight);

        using var frame = new Mat();
        int totalFrames = 0;
        var sessionStopwatch = Stopwatch.StartNew();
        DetectionMetrics? finalMetrics = null;

        Cv2.NamedWindow(_options.WindowTitle, WindowFlags.Normal);
        Cv2.ResizeWindow(_options.WindowTitle, captureWidth, captureHeight);

        while (true)
        {
            capture.Read(frame);

            if (frame.Empty())
            {
                continue;
            }

            // Use native resolution — no downscaling for maximum quality
            using Bitmap bitmapFrame = BitmapConverter.ToBitmap(frame);
            IReadOnlyList<DetectionResult> detections = _detector.Detect(bitmapFrame);
            using Bitmap overlay = _imageRenderer.DrawDetections(bitmapFrame, detections);

            totalFrames++;
            var metrics = _detector.GetMetrics();
            finalMetrics = metrics;

            DrawInfoPanelGdi(overlay, metrics, detections.Count, totalFrames, sessionStopwatch.Elapsed);

            using Mat displayFrame = BitmapConverter.ToMat(overlay);
            Cv2.ImShow(_options.WindowTitle, displayFrame);

            if (Cv2.WaitKey(1) == 27)
            {
                break;
            }
        }

        capture.Release();
        Cv2.DestroyAllWindows();

        sessionStopwatch.Stop();
        _userInterface.ShowWebcamSummary(finalMetrics!, totalFrames, sessionStopwatch.Elapsed);
    }

    /// <summary>
    /// Draws a compact, user-friendly stats panel using GDI+ with anti-aliased text.
    /// </summary>
    private void DrawInfoPanelGdi(Bitmap bitmap, DetectionMetrics metrics, int detectionCount, int totalFrames, TimeSpan totalTime)
    {
        int panelHeight = TitleBarHeight + (RowHeight * 6) + PaddingY * 2 + 4;
        int x = bitmap.Width - PanelWidth - PanelMargin;
        int y = PanelMargin;

        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // Panel background with rounded corners
        using GraphicsPath panelPath = CreateRoundedRect(x, y, PanelWidth, panelHeight, 8);
        using SolidBrush bgBrush = new(PanelBg);
        g.FillPath(bgBrush, panelPath);

        // Title bar background (top portion with rounded top corners)
        using GraphicsPath titlePath = CreateRoundedRectTop(x, y, PanelWidth, TitleBarHeight, 8);
        using SolidBrush titleBrush = new(TitleBarBg);
        g.FillPath(titleBrush, titlePath);

        // Border
        using Pen borderPen = new(BorderColor, 1.5f);
        g.DrawPath(borderPen, panelPath);

        // Separator line between title and content
        using Pen sepPen = new(BorderColor, 1.5f);
        g.DrawLine(sepPen, x + 4, y + TitleBarHeight, x + PanelWidth - 4, y + TitleBarHeight);

        // Title text — user-friendly name
        g.DrawString("\U0001f4ca Live Stats", TitleFont, Brushes.White,
            new RectangleF(x, y, PanelWidth, TitleBarHeight), CenterAlign);

        // Content rows
        int rowY = y + TitleBarHeight + PaddingY;

        // Row 1: Speed (was FPS)
        double currentFps = metrics.CurrentFPS;
        Color fpsValueColor = currentFps >= 10 ? ValueGood : currentFps >= 5 ? ValueWarm : ValueAlert;
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "Speed", $"{currentFps:F1} fps", SpeedLabelColor, fpsValueColor);

        // Row 2: Avg Speed (was AVG FPS)
        rowY += RowHeight;
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "Avg Speed", $"{metrics.AverageFPS:F1} fps", AvgSpeedLabelColor, ValueWhite);

        // Separator
        rowY += RowHeight;
        using Pen rowSepPen = new(SeparatorColor, 1);
        g.DrawLine(rowSepPen, x + PaddingX, rowY, x + PanelWidth - PaddingX, rowY);

        // Row 3: AI Response (was INFERENCE)
        rowY += RowHeight - 6;
        double lastMs = metrics.LastInferenceMs;
        Color timeValueColor = lastMs < 100 ? ValueGood : lastMs < 200 ? ValueWarm : ValueAlert;
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "AI Response", $"{lastMs:F0} ms", AiResponseLabelColor, timeValueColor);

        // Row 4: Detected (was OBJECTS)
        rowY += RowHeight;
        string objText = detectionCount > 0 ? $"{detectionCount} found" : "None";
        Color objValueColor = detectionCount > 0 ? ValueWarm : ValueWhite;
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "Detected", objText, DetectedLabelColor, objValueColor);

        // Row 5: Scanned (was FRAMES)
        rowY += RowHeight;
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "Scanned", $"{totalFrames:N0}", ScannedLabelColor, ValueWhite);

        // Row 6: Uptime (was SESSION)
        rowY += RowHeight;
        string timeStr = totalTime.TotalMinutes >= 1
            ? $"{(int)totalTime.TotalMinutes}m {totalTime.Seconds}s"
            : $"{totalTime.TotalSeconds:F0}s";
        DrawRow(g, x + PaddingX, rowY, PanelWidth - PaddingX * 2, "Uptime", timeStr, UptimeLabelColor, ValueWhite);
    }

    /// <summary>
    /// Draws a single row with colored label on left, colored value on right.
    /// </summary>
    private static void DrawRow(Graphics g, int x, int y, int width, string label, string value, Color labelColor, Color valueColor)
    {
        RectangleF labelRect = new(x, y, width / 2, RowHeight);
        RectangleF valueRect = new(x + width / 2, y, width / 2, RowHeight);

        using SolidBrush labelBrush = new(labelColor);
        using SolidBrush valBrush = new(valueColor);

        g.DrawString(label, LabelFont, labelBrush, labelRect, LeftAlign);
        g.DrawString(value, ValueFont, valBrush, valueRect, RightAlign);
    }

    /// <summary>
    /// Creates a rounded rectangle path.
    /// </summary>
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

    /// <summary>
    /// Creates a rounded rectangle path with only top corners rounded.
    /// </summary>
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
}
