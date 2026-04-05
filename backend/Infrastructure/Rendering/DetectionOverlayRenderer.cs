// GDI+ renderer that draws bounding boxes and class labels on detected images.
// Uses per-class colors, percentage confidence, and polished label badges.

using System.Drawing;
using System.Drawing.Drawing2D;
using DeepLearning.Application.Abstractions;
using DeepLearning.Application.Configuration;
using DeepLearning.Domain.Entities;

namespace DeepLearning.Infrastructure.Rendering;

/// <summary>
/// Draws bounding boxes and class labels on images for detected objects.
/// Uses GDI+ via <see cref="System.Drawing"/> to render overlays without modifying the source image.
/// Each class gets a distinct color for easy visual identification.
/// </summary>
public sealed class DetectionOverlayRenderer : IImageRenderer
{
    private readonly string[] _classLabels;

    /// <summary>
    /// Curated palette of distinct, vibrant colors assigned per class index.
    /// </summary>
    private static readonly Color[] ClassColors =
    [
        Color.FromArgb(0, 200, 255),   // cyan — class 0
        Color.FromArgb(255, 100, 200), // pink — class 1
        Color.FromArgb(100, 255, 100), // green — class 2
        Color.FromArgb(255, 180, 50),  // orange — class 3
        Color.FromArgb(180, 120, 255), // purple — class 4
        Color.FromArgb(255, 80, 80),   // red — class 5
        Color.FromArgb(80, 255, 220),  // teal — class 6
        Color.FromArgb(255, 255, 80),  // yellow — class 7
    ];

    /// <summary>
    /// Creates a new renderer configured with the given options.
    /// </summary>
    /// <param name="options">Must supply the <see cref="DetectionOptions.ClassLabels"/> array.</param>
    public DetectionOverlayRenderer(DetectionOptions options)
    {
        _classLabels = options.ClassLabels;
    }

    /// <inheritdoc />
    public Bitmap DrawDetections(Bitmap image, IReadOnlyCollection<DetectionResult> detections)
    {
        Bitmap canvas = new(image);

        using Graphics graphics = Graphics.FromImage(canvas);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using Font labelFont = new("Segoe UI", 11, FontStyle.Bold);
        using SolidBrush labelForeground = new(Color.White);

        foreach (DetectionResult detection in detections)
        {
            Color classColor = GetClassColor(detection.ClassId);

            Rectangle rectangle = Rectangle.FromLTRB(
                (int)detection.X1,
                (int)detection.Y1,
                (int)detection.X2,
                (int)detection.Y2);

            // Draw bounding box with class-specific color
            using Pen boxPen = new(classColor, 3f);
            graphics.DrawRectangle(boxPen, rectangle);

            // Draw corner accents for a modern look
            DrawCornerAccents(graphics, rectangle, classColor);

            // Draw label badge
            string label = FormatLabel(detection);
            SizeF textSize = graphics.MeasureString(label, labelFont);
            float labelY = Math.Max(0, detection.Y1 - textSize.Height - 6);
            float badgeWidth = textSize.Width + 12;
            float badgeHeight = textSize.Height + 6;
            RectangleF badgeRect = new(detection.X1, labelY, badgeWidth, badgeHeight);

            // Semi-transparent badge background matching class color
            using SolidBrush badgeBrush = new(Color.FromArgb(210, classColor.R / 4, classColor.G / 4, classColor.B / 4));
            using GraphicsPath badgePath = CreateRoundedRect(badgeRect, 4);
            graphics.FillPath(badgeBrush, badgePath);

            // Thin colored top border on badge
            using Pen badgeBorderPen = new(classColor, 2f);
            graphics.DrawLine(badgeBorderPen, badgeRect.X + 2, badgeRect.Y, badgeRect.Right - 2, badgeRect.Y);

            // Label text
            graphics.DrawString(label, labelFont, labelForeground, badgeRect.X + 6, badgeRect.Y + 3);
        }

        return canvas;
    }

    private string FormatLabel(DetectionResult detection)
    {
        string label = detection.ClassId >= 0 && detection.ClassId < _classLabels.Length
            ? _classLabels[detection.ClassId]
            : $"class {detection.ClassId}";

        int percent = (int)(detection.Confidence * 100);
        return $"{label} {percent}%";
    }

    private static Color GetClassColor(int classId)
    {
        return ClassColors[classId % ClassColors.Length];
    }

    /// <summary>
    /// Draws small corner accent lines on the bounding box for a modern detection look.
    /// </summary>
    private static void DrawCornerAccents(Graphics g, Rectangle rect, Color color)
    {
        int accentLen = Math.Min(20, Math.Min(rect.Width, rect.Height) / 4);
        if (accentLen < 4) return;

        using Pen accentPen = new(color, 4f);

        // Top-left
        g.DrawLine(accentPen, rect.Left, rect.Top, rect.Left + accentLen, rect.Top);
        g.DrawLine(accentPen, rect.Left, rect.Top, rect.Left, rect.Top + accentLen);

        // Top-right
        g.DrawLine(accentPen, rect.Right, rect.Top, rect.Right - accentLen, rect.Top);
        g.DrawLine(accentPen, rect.Right, rect.Top, rect.Right, rect.Top + accentLen);

        // Bottom-left
        g.DrawLine(accentPen, rect.Left, rect.Bottom, rect.Left + accentLen, rect.Bottom);
        g.DrawLine(accentPen, rect.Left, rect.Bottom, rect.Left, rect.Bottom - accentLen);

        // Bottom-right
        g.DrawLine(accentPen, rect.Right, rect.Bottom, rect.Right - accentLen, rect.Bottom);
        g.DrawLine(accentPen, rect.Right, rect.Bottom, rect.Right, rect.Bottom - accentLen);
    }

    /// <summary>
    /// Creates a rounded rectangle GraphicsPath for badge backgrounds.
    /// </summary>
    private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
