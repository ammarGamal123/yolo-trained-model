// modelDetector — Composition root: wires all dependencies and starts the application.
// See ARCHITECTURE-AND-CODE-REFERENCE.md for full documentation.

using DeepLearning.Application.Abstractions;
using DeepLearning.Application.Configuration;
using DeepLearning.Application.UseCases;
using DeepLearning.Infrastructure.Capture;
using DeepLearning.Infrastructure.Detection;
using DeepLearning.Infrastructure.ModelMetadata;
using DeepLearning.Infrastructure.Pathing;
using DeepLearning.Infrastructure.Rendering;
using DeepLearning.Presentation.UI;

namespace DeepLearning;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var options = new DetectionOptions();
        IUserInterface userInterface = new ConsoleUserInterface();
        var customModelRegistry = new CustomModelRegistry();
        IProjectPathProvider pathProvider = new ProjectPathProvider();

        if (PromptLoadCustomModel(userInterface, options, customModelRegistry))
        {
            LoadCustomModelFlow(userInterface, options, customModelRegistry);
        }

        string resolvedModelPath = pathProvider.GetAbsolutePath(options.ModelPath);
        if (!File.Exists(resolvedModelPath))
        {
            userInterface.ShowError($"Model file not found: {resolvedModelPath}");
            userInterface.ShowInfo("");
            userInterface.ShowInfo("  Attempted to load: " + options.ModelPath);
            userInterface.ShowInfo("  Resolved to: " + resolvedModelPath);
            userInterface.ShowInfo("");
            userInterface.ShowInfo("  Press any key to exit...");
            Console.ReadKey();
            return;
        }

        options.ModelPath = resolvedModelPath;

        using IObjectDetector detector = new OnnxObjectDetector(options);
        IImageRenderer imageRenderer = new DetectionOverlayRenderer(options);
        IWebcamDetectionLoop webcamDetectionLoop = new WebcamDetectionLoop(options, detector, imageRenderer, userInterface);

        var detectImageFromFileUseCase = new DetectImageFromFileUseCase(
            options,
            detector,
            imageRenderer,
            pathProvider);

        var detectImagesInFolderUseCase = new DetectImagesInFolderUseCase(
            options,
            detector,
            imageRenderer,
            pathProvider);

        var runDetectionApplication = new RunDetectionApplication(
            options,
            userInterface,
            webcamDetectionLoop,
            detectImageFromFileUseCase,
            detectImagesInFolderUseCase,
            pathProvider);

        runDetectionApplication.Execute();
    }

    private static bool PromptLoadCustomModel(IUserInterface userInterface, DetectionOptions options, CustomModelRegistry registry)
    {
        // Check if the default model is known and show its name
        var summary = ModelCatalog.TryGetSummary(options.ModelPath);
        string modelDisplayName = summary != null
            ? $"{Path.GetFileName(options.ModelPath)} ({summary.ModelName})"
            : Path.GetFileName(options.ModelPath);

        userInterface.ShowInfo("");
        userInterface.ShowInfo("  ═══ MODEL SELECTION ═══");
        userInterface.ShowInfo("");
        userInterface.ShowInfo($"  Default model: {modelDisplayName}");
        userInterface.ShowInfo("");
        userInterface.ShowInfo("  [1] Browse my device for ONNX model");
        userInterface.ShowInfo("  [2] Use default model");
        userInterface.ShowInfo("");

        while (true)
        {
            userInterface.ShowPrompt("Choice");
            string? input = Console.ReadLine()?.Trim();
            userInterface.ShowInfo("");

            if (input == "1") return true;
            if (input == "2") return false;

            userInterface.ShowError("Invalid. Enter 1 or 2.");
            userInterface.ShowInfo("");
        }
    }

    private static void LoadCustomModelFlow(IUserInterface userInterface, DetectionOptions options, CustomModelRegistry registry)
    {
        userInterface.ShowInfo("");
        userInterface.ShowInfo("  ═══ LOAD CUSTOM MODEL ═══");
        userInterface.ShowInfo("");

        string? onnxPath = userInterface.BrowseForOnnxFile();
        if (string.IsNullOrEmpty(onnxPath))
        {
            userInterface.ShowInfo("No model selected. Using default.");
            return;
        }

        userInterface.ShowInfo($"  Model selected: {Path.GetFileName(onnxPath)}");
        options.ModelPath = onnxPath;

        // Check if this model is already in the catalog
        var catalogSummary = ModelCatalog.TryGetSummary(onnxPath);
        if (catalogSummary != null)
        {
            userInterface.ShowInfo($"  Known model detected: {catalogSummary.ModelName}");
            userInterface.ShowInfo($"  Description: {catalogSummary.Description}");
            options.ClassLabels = catalogSummary.Classes;
            userInterface.ShowInfo("");
            userInterface.ShowSuccess($"  Model loaded successfully: {catalogSummary.ModelName}");
            userInterface.ShowInfo("");
            return;
        }

        // Check if this model was previously registered by the user
        var customSummary = registry.GetCustomModel(onnxPath);
        if (customSummary != null)
        {
            userInterface.ShowInfo($"  Previously registered model found.");
            options.ClassLabels = customSummary.Classes;
            userInterface.ShowInfo($"  Classes: {string.Join(", ", customSummary.Classes)}");
            userInterface.ShowInfo("");
            userInterface.ShowSuccess($"  Model loaded successfully: {customSummary.ModelName}");
            userInterface.ShowInfo("");
            return;
        }

        // Unknown model — infer class count and prompt for labels
        int inferredClassCount;
        try
        {
            using OnnxObjectDetector probeDetector = new(options);
            inferredClassCount = probeDetector.InferClassCount();
            userInterface.ShowInfo($"  Model inference: detected {inferredClassCount} class(es).");
        }
        catch (Exception ex)
        {
            userInterface.ShowError($"  Could not infer class count from the ONNX output: {ex.Message}");
            userInterface.ShowInfo("");
            userInterface.ShowInfo("  Falling back to manual class count entry.");
            inferredClassCount = PromptForModelClassCount(userInterface);
        }

        string[] classLabels = userInterface.PromptForClassLabels(inferredClassCount);
        options.ClassLabels = classLabels;

        // Ask if user wants to register this model for future sessions
        userInterface.ShowInfo("");
        userInterface.ShowInfo("  Would you like to save this model's info for future sessions?");
        userInterface.ShowInfo("  [1] Yes — save model metadata");
        userInterface.ShowInfo("  [2] No — just use it this session");
        userInterface.ShowInfo("");
        userInterface.ShowPrompt("Choice");
        string? saveChoice = Console.ReadLine()?.Trim();
        userInterface.ShowInfo("");

        if (saveChoice == "1")
        {
            userInterface.ShowInfo("  Enter a short description for this model (optional):");
            userInterface.ShowInfo("  (Press Enter to skip)");
            userInterface.ShowInfo("");
            userInterface.ShowPrompt("Description");
            string? description = Console.ReadLine()?.Trim();
            userInterface.ShowInfo("");

            registry.RegisterModel(onnxPath, classLabels, description);
            userInterface.ShowInfo("  Model metadata saved for future sessions.");
        }

        userInterface.ShowSuccess($"  Custom model loaded successfully!");
        userInterface.ShowInfo("");
    }

    private static int PromptForModelClassCount(IUserInterface userInterface)
    {
        while (true)
        {
            userInterface.ShowInfo("  How many classes does your model detect?");
            userInterface.ShowInfo("  (Enter a number, e.g., 2 for binary classification)");
            userInterface.ShowInfo("");

            userInterface.ShowPrompt("Class count");
            string? input = Console.ReadLine()?.Trim();
            userInterface.ShowInfo("");

            if (int.TryParse(input, out int count) && count > 0)
            {
                return count;
            }

            userInterface.ShowError("Invalid. Enter a positive number.");
            userInterface.ShowInfo("");
        }
    }
}
