// Main application orchestrator — loops between webcam, image, batch, settings, and model info modes.

using DeepLearning.Application.Abstractions;
using DeepLearning.Application.Configuration;
using DeepLearning.Application.Models;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.ModelMetadata;

namespace DeepLearning.Application.UseCases;

/// <summary>
/// Main application orchestrator that coordinates user input, dispatches to the appropriate
/// use case (webcam, single image, batch folder, model info, settings), and handles errors.
///
/// This is the "conductor" of the application — it knows WHAT to do but not HOW.
/// All technical details are delegated to injected dependencies.
/// </summary>
public sealed class RunDetectionApplication
{
    private readonly DetectionOptions _options;
    private readonly IUserInterface _userInterface;
    private readonly IWebcamDetectionLoop _webcamDetectionLoop;
    private readonly DetectImageFromFileUseCase _detectImageFromFileUseCase;
    private readonly DetectImagesInFolderUseCase _detectImagesInFolderUseCase;
    private readonly IProjectPathProvider _pathProvider;

    public RunDetectionApplication(
        DetectionOptions options,
        IUserInterface userInterface,
        IWebcamDetectionLoop webcamDetectionLoop,
        DetectImageFromFileUseCase detectImageFromFileUseCase,
        DetectImagesInFolderUseCase detectImagesInFolderUseCase,
        IProjectPathProvider pathProvider)
    {
        _options = options;
        _userInterface = userInterface;
        _webcamDetectionLoop = webcamDetectionLoop;
        _detectImageFromFileUseCase = detectImageFromFileUseCase;
        _detectImagesInFolderUseCase = detectImagesInFolderUseCase;
        _pathProvider = pathProvider;
    }

    public void Execute()
    {
        _userInterface.ShowWelcome(_options);

        while (true)
        {
            try
            {
                InputSource inputSource = _userInterface.PromptForInputSource();

                switch (inputSource)
                {
                    case InputSource.Webcam:
                        _userInterface.ShowWebcamInstructions(_options);
                        _webcamDetectionLoop.Run();
                        break;

                    case InputSource.ExistingImage:
                        RunSingleImageDetection();
                        break;

                    case InputSource.BatchFolder:
                        RunBatchFolderDetection();
                        break;

                    case InputSource.ModelInfo:
                        ShowModelInformation();
                        break;

                    case InputSource.Settings:
                        _userInterface.ShowThresholdMenu(_options);
                        break;
                }
            }
            catch (FileNotFoundException ex)
            {
                _userInterface.ShowError($"Image file not found: \"{ex.FileName}\"");
            }
            catch (UnauthorizedAccessException ex)
            {
                _userInterface.ShowError($"Access denied: {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                _userInterface.ShowError($"Folder not found: {ex.Message}");
            }
            catch (Exception ex)
            {
                _userInterface.ShowError($"An unexpected error occurred: {ex.Message}");
            }

            bool shouldContinue = _userInterface.PromptForContinue();
            if (!shouldContinue)
            {
                _userInterface.ShowExitMessage();
                return;
            }
        }
    }

    /// <summary>
    /// Prompts the user for an image path and runs single-image detection.
    /// </summary>
    private void RunSingleImageDetection()
    {
        string defaultImagePath = _pathProvider.GetProjectFilePath(_options.DefaultImagePath);
        string selectedImagePath = _userInterface.PromptForImagePath(defaultImagePath, _pathProvider);

        var report = _detectImageFromFileUseCase.Execute(selectedImagePath);
        _userInterface.ShowImageDetectionReport(report, _options.ClassLabels);
    }

    /// <summary>
    /// Prompts the user for a folder path and runs batch detection on all images.
    /// </summary>
    private void RunBatchFolderDetection()
    {
        _userInterface.ShowInfo("");
        _userInterface.ShowInfo("  ═══ BATCH/FOLDER PROCESSING ═══");
        _userInterface.ShowInfo("");
        _userInterface.ShowInfo("  Select a folder containing images to process.");
        _userInterface.ShowInfo("  Annotated images will be saved in an 'output/' subfolder.");
        _userInterface.ShowInfo("");

        // Use a folder browser dialog via WinForms
        using var folderDialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder containing images to process",
            ShowNewFolderButton = false
        };

        if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _userInterface.ShowInfo($"  Processing folder: {folderDialog.SelectedPath}");
            _userInterface.ShowInfo("  Please wait...");
            _userInterface.ShowInfo("");

            var report = _detectImagesInFolderUseCase.Execute(folderDialog.SelectedPath);
            _userInterface.ShowBatchDetectionReport(report, _options.ClassLabels);
        }
        else
        {
            _userInterface.ShowInfo("  No folder selected. Returning to main menu.");
        }
    }

    /// <summary>
    /// Displays model information from the catalog, or a generic message for unknown models.
    /// </summary>
    private void ShowModelInformation()
    {
        var summary = ModelCatalog.TryGetSummary(_options.ModelPath);

        if (summary != null)
        {
            _userInterface.ShowModelInfo(summary);
        }
        else
        {
            // For custom or unknown models, show basic info
            var lines = new List<string>
            {
                $"  File     : {Path.GetFileName(_options.ModelPath)}",
                $"  Classes  : {string.Join(", ", _options.ClassLabels)}",
                $"  Input    : {_options.ModelWidth}x{_options.ModelHeight}",
                "",
                "  This is a custom model not in the built-in catalog.",
                "  To add a description, load it again from the main menu."
            };

            _userInterface.ShowInfo("");
            _userInterface.ShowInfo("  ═══ CUSTOM MODEL INFO ═══");
            _userInterface.ShowInfo("");
            PrintBoxedContent(lines.ToArray());
            _userInterface.ShowInfo("");
        }
    }

    /// <summary>
    /// Helper to print boxed content (duplicated from ConsoleUserInterface for simplicity).
    /// </summary>
    private void PrintBoxedContent(string[] lines)
    {
        const char boxHoriz = '─';
        const char boxVert = '│';
        const char boxTopLeft = '┌';
        const char boxTopRight = '┐';
        const char boxBotLeft = '└';
        const char boxBotRight = '┘';

        int maxLen = lines.Max(l => l.Length) + 4;
        string top = boxTopLeft + new string(boxHoriz, maxLen) + boxTopRight;
        string bot = boxBotLeft + new string(boxHoriz, maxLen) + boxBotRight;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(top);
        foreach (string line in lines)
        {
            Console.WriteLine(boxVert + " " + line.PadRight(maxLen - 1) + boxVert);
        }
        Console.WriteLine(bot);
        Console.ResetColor();
    }
}
