// Batch image detection use case — processes all images in a folder and returns aggregate results.

using System.Diagnostics;
using System.Drawing;
using DeepLearning.Application.Abstractions;
using DeepLearning.Application.Configuration;
using DeepLearning.Application.Models;

namespace DeepLearning.Application.UseCases;

/// <summary>
/// Encapsulates the complete workflow for detecting objects in all images within a folder.
///
/// Steps:
/// <list type="number">
///   <item>Resolve the folder path and enumerate image files.</item>
///   <item>For each image: load, detect, render, save annotated output.</item>
///   <item>Collect individual reports and aggregate statistics.</item>
///   <item>Return a batch report with totals and per-image results.</item>
/// </list>
///
/// Reuses the same detector and renderer as single-image detection,
/// ensuring consistent results across both modes.
/// </summary>
public sealed class DetectImagesInFolderUseCase
{
    private readonly DetectionOptions _options;
    private readonly IObjectDetector _detector;
    private readonly IImageRenderer _imageRenderer;
    private readonly IProjectPathProvider _pathProvider;

    /// <summary>Supported image file extensions for batch processing.</summary>
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];

    /// <summary>
    /// Creates a new instance configured with the given dependencies.
    /// </summary>
    /// <param name="options">Detection configuration such as thresholds and output settings.</param>
    /// <param name="detector">The object detection engine to use.</param>
    /// <param name="imageRenderer">The overlay renderer for drawing boxes and labels.</param>
    /// <param name="pathProvider">Resolves relative and absolute file paths.</param>
    public DetectImagesInFolderUseCase(
        DetectionOptions options,
        IObjectDetector detector,
        IImageRenderer imageRenderer,
        IProjectPathProvider pathProvider)
    {
        _options = options;
        _detector = detector;
        _imageRenderer = imageRenderer;
        _pathProvider = pathProvider;
    }

    /// <summary>
    /// Executes batch detection on all images in the specified folder.
    /// </summary>
    /// <param name="folderPath">
    /// An absolute path or a path relative to the project root.
    /// </param>
    /// <returns>A <see cref="BatchDetectionReport"/> with aggregate results.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the folder does not exist.</exception>
    public BatchDetectionReport Execute(string folderPath)
    {
        string resolvedPath = _pathProvider.GetAbsolutePath(folderPath.Trim());

        if (!Directory.Exists(resolvedPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {resolvedPath}");
        }

        // Enumerate all image files in the folder (non-recursive, top-level only)
        var imageFiles = Directory.EnumerateFiles(resolvedPath)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f)
            .ToList();

        var stopwatch = Stopwatch.StartNew();
        var reports = new List<ImageDetectionReport>();
        int failedCount = 0;

        // Create output subfolder for annotated images
        string outputFolder = Path.Combine(resolvedPath, "output");
        Directory.CreateDirectory(outputFolder);

        foreach (string imagePath in imageFiles)
        {
            try
            {
                using Bitmap sourceImage = new(imagePath);
                var detections = _detector.Detect(sourceImage);
                using Bitmap renderedImage = _imageRenderer.DrawDetections(sourceImage, detections);

                // Save annotated image to output folder
                string outputFileName = Path.GetFileNameWithoutExtension(imagePath) + "_detected" + Path.GetExtension(imagePath);
                string outputPath = Path.Combine(outputFolder, outputFileName);
                renderedImage.Save(outputPath);

                reports.Add(new ImageDetectionReport
                {
                    InputPath = imagePath,
                    OutputPath = outputPath,
                    Detections = detections
                });
            }
            catch
            {
                // Skip failed images but count them
                failedCount++;
            }
        }

        stopwatch.Stop();

        return new BatchDetectionReport
        {
            FolderPath = resolvedPath,
            TotalImages = imageFiles.Count,
            ProcessedImages = reports.Count,
            FailedImages = failedCount,
            Reports = reports,
            Elapsed = stopwatch.Elapsed
        };
    }
}
