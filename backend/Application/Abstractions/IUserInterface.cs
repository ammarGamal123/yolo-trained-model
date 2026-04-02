// Contract for all user interactions — display, input, file browsing, and reports.

using DeepLearning.Application.Configuration;
using DeepLearning.Application.Models;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Abstractions;

public interface IUserInterface
{
    void ShowWelcome(DetectionOptions options);
    InputSource PromptForInputSource();
    string PromptForImagePath(string defaultImagePath, IProjectPathProvider pathProvider);
    void ShowImageDetectionReport(ImageDetectionReport report, string[] classLabels);
    void ShowWebcamInstructions(DetectionOptions options);
    void ShowDetections(IReadOnlyList<DetectionResult> detections, string[] classLabels);
    void ShowInfo(string message);
    void ShowError(string message);
    void ShowSuccess(string message);
    void ShowPrompt(string label);
    bool PromptForContinue();
    void ShowExitMessage();

    string? BrowseForOnnxFile();
    string[] PromptForClassLabels(int expectedCount);
    string? BrowseForImageFile();

    // ─── New: Model information display ─────────────────────────
    void ShowModelInfo(ModelSummary summary);

    // ─── New: Threshold tuning menu ─────────────────────────────
    void ShowThresholdMenu(DetectionOptions options);

    // ─── New: Batch detection report display ────────────────────
    void ShowBatchDetectionReport(BatchDetectionReport report, string[] classLabels);
}
