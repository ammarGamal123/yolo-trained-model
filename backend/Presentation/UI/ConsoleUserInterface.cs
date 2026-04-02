// Full console UI: ASCII banner, colored output, menus, file dialogs, confidence bars.

using System;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using DeepLearning.Application.Abstractions;
using DeepLearning.Application.Configuration;
using DeepLearning.Application.Models;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.ModelMetadata;

namespace DeepLearning.Presentation.UI;

public sealed class ConsoleUserInterface : IUserInterface
{
    private const char BoxHoriz = '─';
    private const char BoxVert = '│';
    private const char BoxTopLeft = '┌';
    private const char BoxTopRight = '┐';
    private const char BoxBotLeft = '└';
    private const char BoxBotRight = '┘';

    private static readonly string[] BannerLines =
    [
        "  ██████╗ ██╗██████╗ ███████╗ ██████╗     ███████╗██╗ ██████╗ ███╗   ██╗ █████╗ ██╗     ",
        "  ██╔══██╗██║██╔══██╗██╔════╝██╔═══██╗    ██╔════╝██║██╔════╝ ████╗  ██║██╔══██╗██║     ",
        "  ██████╔╝██║██████╔╝█████╗  ██║   ██║    ███████╗██║██║  ███╗██╔██╗ ██║███████║██║     ",
        "  ██╔═══╝ ██║██╔══██╗██╔══╝  ██║   ██║    ╚════██║██║██║   ██║██║╚██╗██║██╔══██║██║     ",
        "  ██║     ██║██║  ██║███████╗╚██████╔╝    ███████║██║╚██████╔╝██║ ╚████║██║  ██║███████╗",
        "  ╚═╝     ╚═╝╚═╝  ╚═╝╚══════╝ ╚═════╝     ╚══════╝╚═╝ ╚═════╝ ╚═╝  ╚═══╝╚═╝  ╚═╝╚══════╝",
        "",
        "         ╔══════════════════════════════════════════════════════════════╗",
        "         ║        YOLO Object Detection Console  │  ONNX Runtime        ║",
        "         ╚══════════════════════════════════════════════════════════════╝"
    ];

    public void ShowWelcome(DetectionOptions options)
    {
        Console.Clear();
        PrintBanner();

        // Try to get model summary from catalog for richer display
        var summary = ModelCatalog.TryGetSummary(options.ModelPath);
        var infoLines = new List<string>
        {
            "✓ Application started successfully",
            $"  Model     : {Path.GetFileName(options.ModelPath)}{(summary != null ? $" ({summary.ModelName})" : "")}",
            $"  Classes   : {string.Join(", ", options.ClassLabels)}",
            $"  Threshold : {options.ConfidenceThreshold:P0}",
            $"  IoU       : {options.IouThreshold:P0}"
        };

        if (summary != null)
        {
            infoLines.Add($"  Input     : {summary.InputSize}");
            infoLines.Add($"  Metrics   : {summary.Metrics}");
        }

        PrintBoxedContent(infoLines.ToArray());
        PrintLine();
        PrintInfo("  Tip: Select [4] from the main menu to view detailed model information.");
    }

    public InputSource PromptForInputSource()
    {
        PrintHeader("SELECT INPUT SOURCE");
        PrintLine();
        PrintOption(1, "Webcam", "Use your camera for real-time detection");
        PrintOption(2, "Image File", "Detect objects in a static image");
        PrintOption(3, "Batch/Folder", "Process all images in a folder");
        PrintOption(4, "Model Info", "View details about the loaded model");
        PrintOption(5, "Settings", "Adjust confidence and IoU thresholds");
        PrintLine();

        while (true)
        {
            PrintPrompt("Choice");
            string? input = Console.ReadLine()?.Trim();
            PrintLine();

            if (input == "1")
            {
                PrintSuccess("▶ Webcam mode selected");
                return InputSource.Webcam;
            }

            if (input == "2")
            {
                PrintSuccess("▶ Image File mode selected");
                return InputSource.ExistingImage;
            }

            if (input == "3")
            {
                PrintSuccess("▶ Batch/Folder mode selected");
                return InputSource.BatchFolder;
            }

            if (input == "4")
            {
                PrintSuccess("▶ Model Info selected");
                return InputSource.ModelInfo;
            }

            if (input == "5")
            {
                PrintSuccess("▶ Settings selected");
                return InputSource.Settings;
            }

            PrintError("✗ Invalid choice. Please enter 1, 2, 3, 4, or 5.");
            PrintLine();
        }
    }

    public string PromptForImagePath(string defaultImagePath, IProjectPathProvider pathProvider)
    {
        PrintHeader("IMAGE FILE MODE");
        PrintLine();
        PrintOption(1, "Browse my device", "Open file dialog to select an image");
        PrintOption(2, "Browse app folder", "Select from images in the app folder");
        PrintOption(3, "Type path manually", "Enter a full path to an image file");
        PrintOption(4, "Use default", $"Press Enter to use: {defaultImagePath}");
        PrintLine();

        while (true)
        {
            PrintPrompt("Option");
            string? input = Console.ReadLine()?.Trim();
            PrintLine();

            if (input == "1")
            {
                string? browsed = BrowseForImageFile();
                if (!string.IsNullOrEmpty(browsed))
                {
                    PrintSuccess($"✓ Selected: {Path.GetFileName(browsed)}");
                    return browsed;
                }
                PrintInfo("No file selected. Try another option.");
                PrintLine();
                continue;
            }

            if (input == "2")
            {
                return BrowseForImage(pathProvider, defaultImagePath);
            }

            if (input == "3")
            {
                return PromptCustomPath();
            }

            if (string.IsNullOrWhiteSpace(input) || input == "4")
            {
                PrintSuccess($"✓ Using default: {defaultImagePath}");
                return defaultImagePath;
            }

            PrintError("✗ Invalid option. Enter 1, 2, 3, or 4.");
            PrintLine();
        }
    }

    public void ShowImageDetectionReport(ImageDetectionReport report, string[] classLabels)
    {
        PrintHeader("DETECTION COMPLETE");

        string timeDisplay = FormatElapsedTime(report.Elapsed);

        PrintBoxedContent([
            $"  Input  : {report.InputPath}",
            $"  Output : {report.OutputPath}",
            $"  Time   : {timeDisplay}"
        ]);
        ShowDetections(report.Detections, classLabels);
    }

    private string FormatElapsedTime(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s";
        }
        if (elapsed.TotalMinutes >= 1)
        {
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
        }
        if (elapsed.TotalSeconds >= 1)
        {
            return $"{elapsed.TotalSeconds:F2}s";
        }
        return $"{elapsed.TotalMilliseconds:F0}ms";
    }

    public void ShowWebcamInstructions(DetectionOptions options)
    {
        PrintHeader("WEBCAM MODE");
        PrintBoxedContent([
            $"Opening webcam (index {options.CameraIndex})...",
            $"Press ESC in the window to stop"
        ]);
    }

    public void ShowDetections(IReadOnlyList<DetectionResult> detections, string[] classLabels)
    {
        if (detections.Count == 0)
        {
            PrintWarning("⚠ No objects detected.");
            PrintInfo("  Lower the confidence threshold or try a different image.");
            PrintLine();
            return;
        }

        var grouped = detections
            .GroupBy(d => classLabels.ElementAtOrDefault(d.ClassId) ?? $"unknown({d.ClassId})")
            .OrderByDescending(g => g.Max(d => d.Confidence))
            .ToList();

        PrintSuccess($"✓ Total: {detections.Count} detection(s)");
        PrintLine();

        foreach (var group in grouped)
        {
            string className = group.Key;
            int count = group.Count();
            float topConf = group.Max(d => d.Confidence);

            PrintLine($"  [{className}] ×{count}  (top: {topConf:P0})");

            foreach (DetectionResult detection in group.OrderByDescending(d => d.Confidence).Take(3))
            {
                PrintDetectionItem(detection);
            }

            if (group.Count() > 3)
            {
                PrintInfo($"  ... and {group.Count() - 3} more");
            }
            PrintLine();
        }
    }

    public void ShowInfo(string message) => PrintInfo(message);

    public void ShowError(string message)
    {
        PrintHeader("ERROR");
        PrintBoxedContent([$"✗ {message}"]);
    }

    public void ShowSuccess(string message) => PrintSuccess(message);

    public void ShowPrompt(string label) => PrintPrompt(label);

    public bool PromptForContinue()
    {
        PrintHeader("WHAT'S NEXT?");
        PrintLine();
        PrintOption(1, "Back to main menu", "Run another detection");
        PrintOption(2, "Exit", "Close the application");
        PrintLine();

        while (true)
        {
            PrintPrompt("Choice");
            string? input = Console.ReadLine()?.Trim();
            PrintLine();

            if (input == "1") return true;
            if (input == "2") return false;

            PrintError("✗ Invalid. Enter 1 or 2.");
            PrintLine();
        }
    }

    public void ShowExitMessage()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine("  ╔══════════════════════════════════════════╗");
        Console.WriteLine("  ║  Thanks for using Object Detector!      ║");
        Console.WriteLine("  ║  Goodbye! 👋                              ║");
        Console.WriteLine("  ╚══════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    public string? BrowseForOnnxFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select ONNX Model File",
            Filter = "ONNX Models (*.onnx)|*.onnx|All Files (*.*)|*.*",
            FilterIndex = 1,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            return dialog.FileName;
        }
        return null;
    }

    public string[] PromptForClassLabels(int expectedCount)
    {
        PrintHeader("CONFIGURE CLASS LABELS");
        PrintLine();
        PrintInfo($"The model has {expectedCount} class(es).");
        PrintInfo("Enter class names separated by commas:");
        PrintLine();
        PrintInfo("  Example: bottle, soap, soap-cover");
        PrintLine();

        while (true)
        {
            PrintPrompt("Classes");
            string? input = Console.ReadLine()?.Trim();
            PrintLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                PrintError("✗ No input. Please enter class names.");
                PrintLine();
                continue;
            }

            string[] labels = input.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();

            if (labels.Length == 0)
            {
                PrintError("✗ No valid class names entered.");
                PrintLine();
                continue;
            }

            if (labels.Length != expectedCount)
            {
                PrintError($"✗ Expected exactly {expectedCount} class name(s), but got {labels.Length}.");
                PrintInfo("Tip: if your model detects more objects than you think, add the missing class labels.");
                PrintLine();
                continue;
            }

            string mapping = string.Join(", ", labels.Select((label, index) => $"[{index}] {label}"));
            PrintSuccess($"✓ {labels.Length} class(es) configured: {mapping}");
            return labels;
        }
    }

    public string? BrowseForImageFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Image File",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files (*.*)|*.*",
            FilterIndex = 1,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            return dialog.FileName;
        }
        return null;
    }

    /// <summary>
    /// Displays detailed information about a known model from the catalog.
    /// </summary>
    public void ShowModelInfo(ModelSummary summary)
    {
        PrintHeader("MODEL INFORMATION");
        PrintLine();

        var lines = new List<string>
        {
            $"  Name        : {summary.ModelName}",
            $"  File        : {summary.ModelFile}",
            $"  Input Size  : {summary.InputSize}",
            $"  Classes     : {string.Join(", ", summary.Classes)}",
            $"  Trained     : {summary.TrainingDate}",
            $"  Metrics     : {summary.Metrics}",
            "",
            $"  Description :",
            $"    {summary.Description}"
        };

        PrintBoxedContent(lines.ToArray());
        PrintLine();
    }

    /// <summary>
    /// Interactive menu for adjusting detection thresholds in real-time.
    /// </summary>
    public void ShowThresholdMenu(DetectionOptions options)
    {
        while (true)
        {
            PrintHeader("ADJUST DETECTION SETTINGS");
            PrintLine();
            PrintOption(1, "Confidence Threshold", $"Current: {options.ConfidenceThreshold:P0}");
            PrintOption(2, "IoU Threshold", $"Current: {options.IouThreshold:P0}");
            PrintOption(3, "Reset to defaults", "Confidence: 25%, IoU: 45%");
            PrintOption(4, "Back to main menu", "Return without changes");
            PrintLine();

            PrintPrompt("Choice");
            string? input = Console.ReadLine()?.Trim();
            PrintLine();

            if (input == "1")
            {
                options.ConfidenceThreshold = PromptForThreshold("Confidence", options.ConfidenceThreshold);
                PrintSuccess($"✓ Confidence threshold updated to {options.ConfidenceThreshold:P0}");
                PrintLine();
                continue;
            }

            if (input == "2")
            {
                options.IouThreshold = PromptForThreshold("IoU", options.IouThreshold);
                PrintSuccess($"✓ IoU threshold updated to {options.IouThreshold:P0}");
                PrintLine();
                continue;
            }

            if (input == "3")
            {
                options.ConfidenceThreshold = 0.45f;
                options.IouThreshold = 0.45f;
                PrintSuccess("✓ Thresholds reset to defaults (Confidence: 45%, IoU: 45%)");
                PrintLine();
                continue;
            }

            if (input == "4" || string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            PrintError("✗ Invalid choice. Enter 1, 2, 3, or 4.");
            PrintLine();
        }
    }

    /// <summary>
    /// Displays a batch detection report with aggregate statistics.
    /// </summary>
    public void ShowBatchDetectionReport(BatchDetectionReport report, string[] classLabels)
    {
        PrintHeader("BATCH DETECTION COMPLETE");
        PrintLine();

        var lines = new List<string>
        {
            $"  Folder     : {report.FolderPath}",
            $"  Images     : {report.TotalImages} found",
            $"  Processed  : {report.ProcessedImages} successful",
            $"  Failed     : {report.FailedImages}",
            $"  Detections : {report.TotalDetections} total",
            $"  Time       : {FormatElapsedTime(report.Elapsed)}"
        };

        // Add per-class breakdown
        var classCounts = report.GetClassCounts(classLabels);
        if (classCounts.Count > 0)
        {
            lines.Add("");
            lines.Add("  Per-class breakdown:");
            foreach (var kvp in classCounts.OrderByDescending(k => k.Value))
            {
                lines.Add($"    {kvp.Key}: {kvp.Value}");
            }
        }

        PrintBoxedContent(lines.ToArray());
        PrintLine();

        if (report.FailedImages > 0)
        {
            PrintWarning($"⚠ {report.FailedImages} image(s) could not be processed.");
            PrintLine();
        }

        if (report.ProcessedImages > 0)
        {
            PrintInfo($"  Annotated images saved to: {Path.Combine(report.FolderPath, "output")}");
            PrintLine();
        }
    }

    private string BrowseForImage(IProjectPathProvider pathProvider, string defaultImagePath)
    {
        string[] images = pathProvider.GetImageFiles();

        if (images.Length == 0)
        {
            PrintWarning("No images in app folder.");
            return PromptCustomPath();
        }

        PrintHeader("AVAILABLE IMAGES");
        PrintLine();
        PrintOption(0, "Use default", defaultImagePath);
        for (int i = 0; i < images.Length; i++)
        {
            PrintOption(i + 1, images[i], "");
        }
        PrintLine();

        while (true)
        {
            PrintPrompt("Select");
            string? input = Console.ReadLine()?.Trim();
            PrintLine();

            if (!int.TryParse(input, out int selection))
            {
                PrintError("✗ Enter a number.");
                PrintLine();
                continue;
            }

            if (selection == 0)
            {
                PrintSuccess($"✓ Default: {defaultImagePath}");
                return defaultImagePath;
            }

            if (selection < 1 || selection > images.Length)
            {
                PrintError($"✗ Out of range. Enter 0-{images.Length}.");
                PrintLine();
                continue;
            }

            string chosen = images[selection - 1];
            string fullPath = pathProvider.GetProjectFilePath(chosen);

            if (!File.Exists(fullPath))
            {
                PrintError($"✗ File not found: {chosen}");
                PrintLine();
                continue;
            }

            PrintSuccess($"✓ Selected: {chosen}");
            return fullPath;
        }
    }

    private string PromptCustomPath()
    {
        while (true)
        {
            PrintHeader("TYPE IMAGE PATH");
            PrintLine();
            PrintInfo("Enter full path or [B] to go back");
            PrintLine();
            PrintPrompt("Path");
            string? input = Console.ReadLine();
            PrintLine();

            if (string.IsNullOrWhiteSpace(input)) continue;

            string trimmed = input.Trim();

            if (trimmed.Equals("b", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (!File.Exists(trimmed))
            {
                PrintError($"✗ File not found: {trimmed}");
                PrintLine();
                continue;
            }

            PrintSuccess($"✓ Using: {Path.GetFileName(trimmed)}");
            return trimmed;
        }
    }

    /// <summary>
    /// Prompts the user for a threshold value (0-100%) with validation.
    /// </summary>
    private float PromptForThreshold(string name, float currentValue)
    {
        PrintLine();
        PrintInfo($"  Current {name} threshold: {currentValue:P0}");
        PrintInfo("  Enter a value between 0 and 100 (percentage), or [B] to cancel.");
        PrintLine();

        while (true)
        {
            PrintPrompt($"{name} (%)");
            string? input = Console.ReadLine()?.Trim();
            PrintLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return currentValue; // Keep current value
            }

            if (input.Equals("b", StringComparison.OrdinalIgnoreCase))
            {
                return currentValue; // Cancel
            }

            if (int.TryParse(input, out int percent) && percent >= 0 && percent <= 100)
            {
                return percent / 100f;
            }

            PrintError("✗ Invalid. Enter a number between 0 and 100.");
            PrintLine();
        }
    }

    private void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        foreach (string line in BannerLines)
        {
            Console.WriteLine(line);
        }
        Console.ResetColor();
    }

    private void PrintBoxedContent(string[] lines)
    {
        int maxLen = lines.Max(l => l.Length) + 4;
        string top = BoxTopLeft + new string(BoxHoriz, maxLen) + BoxTopRight;
        string bot = BoxBotLeft + new string(BoxHoriz, maxLen) + BoxBotRight;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(top);
        foreach (string line in lines)
        {
            Console.WriteLine(BoxVert + " " + line.PadRight(maxLen - 1) + BoxVert);
        }
        Console.WriteLine(bot);
        Console.ResetColor();
    }

    private void PrintHeader(string text)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine();
        Console.WriteLine($"  ╭─ {text} ─");
        Console.ResetColor();
    }

    private void PrintOption(int num, string title, string desc)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  [{num}] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(title);
        if (!string.IsNullOrEmpty(desc))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" - {desc}");
        }
        Console.WriteLine();
        Console.ResetColor();
    }

    private void PrintLine(string? text = null)
    {
        Console.WriteLine(text ?? string.Empty);
    }

    private void PrintPrompt(string label)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"  {label}: ");
        Console.ResetColor();
    }

    private void PrintInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
    }

    private void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
    }

    private void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
    }

    private void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
    }

    private void PrintDetectionItem(DetectionResult detection)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"    {BuildConfidenceBar(detection.Confidence)}");
        Console.ResetColor();
        Console.WriteLine($"  @ ({detection.X1:F0}, {detection.Y1:F0})");
    }

    private string BuildConfidenceBar(float confidence)
    {
        const int barWidth = 8;
        int filled = (int)Math.Round(confidence * barWidth);
        string bar = new string('█', filled) + new string('░', barWidth - filled);
        return $"[{bar}] {confidence:P0}";
    }
}
