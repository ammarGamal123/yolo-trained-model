# AGENTS.md — yolo-trained-model

## Build / Run
- `cd backend && dotnet build DeepLearning.sln -c Release`
- `dotnet run` — interactive menu chooses model, then loops asking input mode
- `dotnet publish -c Release -r win-x64 --self-contained`
- No test project exists yet

## Config
- `backend/appsettings.json` — `Detection` section binds to `DetectionOptions.cs`
- `DetectionOptions.cs` still has default values as code fallback; appsettings.json overrides them
- `IConfiguration` / `ServiceCollection` built manually in `Program.cs:52-62`

## DI Container
- All abstractions registered as singletons in `Program.cs:52-62`
- `DetectionOptions` mutated by interactive custom-model flow *before* container build
- `IObjectDetector` (OnnxObjectDetector), `IWebcamDetectionLoop`, use cases all wired via container
- Add new services to the `ServiceCollection` block, NOT with `new` in `Main()`

## Model Files
- `*.onnx` gitignored — copy manually into `models/`
- Default: `models/detector_v4.onnx` (class order: soap, soap-cover, bottle — must match training)
- `ModelCatalog.cs` matches known `.onnx` filenames to display rich summaries

## Windows-Only
- Target: `net8.0-windows` — requires Windows
- `[STAThread]` on `Main()` — WinForms file dialogs
- `AllowUnsafeBlocks` — `ImagePreprocessor` uses `LockBits`

## Training Pipeline (`ai-training/`)
Ordered scripts in `ai-training/scripts/`:  
`0_validate_labels → 1_prepare_dataset → 2_augment_dataset → 3_create_config → 4_train → 5_export`  
Run: `python scripts/run_pipeline.py`

## Quirks
- `ProjectPathProvider` detects dev vs deployed by checking for `DeepLearning.csproj` presence
- `RunDetectionApplication` is the composition root executor — wires use cases internally
- `CustomModelRegistry` persists to `custom-models.json` in the base directory
- `ConsoleUserInterface.cs` ~760 lines — needs splitting (display vs dialogs vs formatting)
- Branch `feature/clean-architecture-yolo-detector` (soap model) and `feature/bottle-detector` — **do NOT merge**
