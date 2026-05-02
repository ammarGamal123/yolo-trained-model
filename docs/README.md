# modelDetector — YOLO Object Detection Engine

> Real-time **YOLO + ONNX Runtime** object detection for bottles, soaps, and soap covers.
> Built with **Clean Architecture**, **Dependency Injection**, and **C# .NET 8**.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Features](#features)
- [Architecture](#architecture)
- [Data Flow](#data-flow)
- [Project Structure](#project-structure)
- [Configuration](#configuration)
- [Dependency Injection](#dependency-injection)
- [Dependencies](#dependencies)
- [Current Model](#current-model)
- [Training Pipeline](#training-pipeline)
- [Troubleshooting](#troubleshooting)

---

## Quick Start

```bash
# Build
cd backend
dotnet build DeepLearning.sln -c Release

# Run (interactive menu)
dotnet run

# Publish self-contained executable
dotnet publish -c Release -r win-x64 --self-contained
```

**Windows only** — requires `net8.0-windows` for OpenCV, GDI+, and WinForms dialogs.

---

## Features

- **3 input modes:** live webcam, single image, batch folder processing
- **Real-time webcam loop** with OpenCV display, FPS/stats overlay, and ESC-exit
- **Batch processing** — annotate every image in a folder with one command
- **Interactive model selection** — browse for custom ONNX models at startup
- **Dynamic threshold tuning** — adjust confidence and IoU thresholds at runtime
- **Model catalog** — recognized ONNX files display rich summaries (name, description, metrics)
- **Custom model registry** — unknown models can be saved with user-defined class labels
- **Soft-NMS** — Gaussian score decay for overlapping detections
- **Merge close detections** — merges near-identical boxes by center distance

---

## Architecture

The application follows **Clean Architecture** with four layers, each depending only on the layer
below it. All cross-layer dependencies point **inward** — Domain knows nothing, Application knows
only abstractions, Infrastructure implements those abstractions, Presentation drives the UI.

```
┌──────────────────────────────────────────────────────────┐
│                    Presentation                          │
│            ConsoleUserInterface (IUserInterface)         │
├──────────────────────────────────────────────────────────┤
│                    Application                           │
│   ┌─────────────┐  ┌──────────────┐  ┌───────────────┐  │
│   │ Abstractions│  │ Configuration│  │   UseCases    │  │
│   │ (5Interfaces)│  │ DetectionOpts│  │ (Workflows)  │  │
│   └─────────────┘  └──────────────┘  └───────────────┘  │
├──────────────────────────────────────────────────────────┤
│                    Infrastructure                        │
│  OnnxObjectDetector  │  WebcamDetectionLoop             │
│  DetectionOverlay    │  ImagePreprocessor               │
│  NmsProcessor        │  ProjectPathProvider             │
│  ModelCatalog        │  CustomModelRegistry             │
├──────────────────────────────────────────────────────────┤
│                    Domain                                │
│         DetectionResult  │  InputSource  │  Metrics     │
└──────────────────────────────────────────────────────────┘
```

### Layer Responsibilities

| Layer | Role | External Dependencies |
|---|---|---|
| **Domain** | Pure business objects (`DetectionResult`, `InputSource` enum, `DetectionMetrics`) | None |
| **Application** | Use case workflows, interface contracts (5 abstractions), configuration, report DTOs | None |
| **Infrastructure** | ONNX Runtime inference, OpenCV capture, GDI+ rendering, path resolution, model metadata | ONNX, OpenCV, System.Drawing |
| **Presentation** | Console UI — menus, banners, prompts, detection reports, file dialogs | WinForms (dialogs only) |

### SOLID Principles Applied

| Principle | Where |
|---|---|
| **Single Responsibility** | Each class has one job: detect, preprocess, draw NMS, prompt |
| **Open/Closed** | New detector backend = implement `IObjectDetector`, register in DI — zero changes elsewhere |
| **Liskov Substitution** | Any `IObjectDetector` works in all use cases (ONNX, TensorRT, OpenVINO) |
| **Interface Segregation** | 5 focused interfaces: `IObjectDetector`, `IUserInterface`, `IImageRenderer`, `IProjectPathProvider`, `IWebcamDetectionLoop` |
| **Dependency Inversion** | Application depends on abstractions (`IObjectDetector`), not on `OnnxObjectDetector` or OpenCV |

---

## Data Flow

```
User launches app
    │
    ▼
Program.Main()
    ├── Load appsettings.json → DetectionOptions
    ├── Interactive model selection (optional)
    ├── Resolve & validate model path
    ├── Build DI container (ServiceCollection)
    └── Resolve RunDetectionApplication.Execute()
            │
            ▼
    PromptForInputSource() ──► Webcam | Image | Batch | Info | Settings
                                  │
                                  ▼
                          ┌─ Webcam Mode ──────────────────────┐
                          │  WebcamDetectionLoop.Run()          │
                          │    Capture frame (OpenCV)           │
                          │    OnnxObjectDetector.Detect()      │
                          │      → ImagePreprocessor.ToChwArray │
                          │      → InferenceSession.Run()       │
                          │      → ParseDetections()            │
                          │      → NmsProcessor.Apply()         │
                          │    DetectionOverlayRenderer          │
                          │    Cv2.ImShow() live window          │
                          │    [ESC] → stop                     │
                          └────────────────────────────────────┘
                                  │
                          ┌─ Image Mode ────────────────────────┐
                          │  DetectImageFromFileUseCase.Execute  │
                          │    Load Bitmap from disk             │
                          │    → OnnxObjectDetector.Detect()     │
                          │    → DrawDetections()                │
                          │    → Save output.jpg                 │
                          │    → Return report                   │
                          └────────────────────────────────────┘
                                  │
                          ┌─ Batch Mode ────────────────────────┐
                          │  DetectImagesInFolderUseCase.Execute │
                          │    Enumerate images in folder        │
                          │    Loop: detect → draw → save        │
                          │    → Aggregate report                │
                          └────────────────────────────────────┘
```

### Inference Pipeline Detail

```
Bitmap
  │
  ▼ ImagePreprocessor.ToChwArray()
  │   Resize with letterbox (pad to 640×640)
  │   LockBits → CHW float array (R,G,B planes)
  │
  ▼ DenseTensor<float> [1, 3, 640, 640]
  │
  ▼ InferenceSession.Run()
  │   ONNX Runtime (ORT_PARALLEL, ALL_OPT)
  │
  ▼ Raw output tensor [1, C, N] or [1, N, C]
  │
  ▼ ParseDetections()
  │   Map center/wh → corner coordinates
  │   Undo letterbox scaling
  │   Clip to image bounds
  │
  ▼ NmsProcessor.Apply()
  │   Group by class → sort by confidence
  │   Soft-NMS (Gaussian decay) or standard NMS
  │
  ▼ MergeCloseDetections()
  │   Average boxes within center-distance threshold
  │
  ▼ FilterContainedBoxes()
  │   Suppress soap (class 0) inside soap-cover (class 1)
  │
  ▼ IReadOnlyList<DetectionResult>
```

---

## Project Structure

```
yolo-trained-model/
├── backend/
│   ├── appsettings.json                    # External configuration
│   ├── Program.cs                          # Composition root + DI setup
│   ├── DeepLearning.csproj                 # .NET 8 project file
│   │
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── DetectionResult.cs          # Bounding box with class + confidence
│   │   │   └── DetectionMetrics.cs         # FPS, inference timing
│   │   └── Enums/
│   │       └── InputSource.cs              # Webcam / Image / Batch / Info / Settings
│   │
│   ├── Application/
│   │   ├── Abstractions/
│   │   │   ├── IObjectDetector.cs          # Detect(Bitmap) → IReadOnlyList<DetectionResult>
│   │   │   ├── IUserInterface.cs           # Display + input + prompts
│   │   │   ├── IImageRenderer.cs           # DrawDetections(Bitmap) → Bitmap
│   │   │   ├── IProjectPathProvider.cs     # Resolve dev/deployed paths
│   │   │   └── IWebcamDetectionLoop.cs     # Run() blocking loop
│   │   ├── Configuration/
│   │   │   ├── DetectionOptions.cs          # All settings POCO
│   │   │   └── CustomModelRegistry.cs       # Persist custom model metadata
│   │   ├── Models/
│   │   │   ├── ImageDetectionReport.cs      # Single-image result
│   │   │   ├── BatchDetectionReport.cs      # Aggregate folder result
│   │   │   └── ModelSummary.cs              # Model metadata DTO
│   │   └── UseCases/
│   │       ├── RunDetectionApplication.cs   # Main orchestrator loop
│   │       ├── DetectImageFromFileUseCase.cs # Single-image workflow
│   │       └── DetectImagesInFolderUseCase.cs # Batch folder workflow
│   │
│   ├── Infrastructure/
│   │   ├── Detection/
│   │   │   ├── OnnxObjectDetector.cs       # YOLO inference engine
│   │   │   ├── ImagePreprocessor.cs        # Bitmap → CHW float array
│   │   │   └── NmsProcessor.cs             # Non-Maximum Suppression
│   │   ├── Capture/
│   │   │   └── WebcamDetectionLoop.cs      # OpenCV capture + display loop
│   │   ├── Rendering/
│   │   │   └── DetectionOverlayRenderer.cs # GDI+ box + label drawing
│   │   ├── ModelMetadata/
│   │   │   └── ModelCatalog.cs             # Known model registry
│   │   └── Pathing/
│   │       └── ProjectPathProvider.cs      # Dev vs deployed path resolution
│   │
│   └── Presentation/
│       └── UI/
│           └── ConsoleUserInterface.cs      # All console I/O (750+ lines)
│
├── ai-training/
│   ├── scripts/                            # Ordered YOLO training scripts
│   │   ├── 0_validate_labels.py
│   │   ├── 1_prepare_dataset.py
│   │   ├── 2_augment_dataset.py
│   │   ├── 3_create_config.py
│   │   ├── 4_train.py
│   │   ├── 5_export.py
│   │   └── run_pipeline.py                 # Orchestrates all steps
│   ├── dataset/                            # Training data config
│   └── models/                             # Trained weights (.pt)
│
├── models/                                 # ONNX model files (gitignored)
├── docs/                                   # Documentation
│   └── README.md                           # This file
│
├── AGENTS.md                               # OpenCode AI instructions
└── .gitignore
```

---

## Configuration

Settings are loaded from `backend/appsettings.json` at startup and bound to `DetectionOptions`.
You can also override any value via environment variables.

```json
{
  "Detection": {
    "ModelPath": "../models/detector_v4.onnx",
    "ClassLabels": ["soap", "soap-cover", "bottle"],
    "ConfidenceThreshold": 0.50,
    "IouThreshold": 0.50,
    "CameraIndex": 0,
    "UseSoftNms": true,
    "MergeCloseDetections": true
  }
}
```

| Key | Default | Description |
|---|---|---|
| `ModelPath` | `../models/detector_v4.onnx` | Path to ONNX model file |
| `ClassLabels` | `["soap", "soap-cover", "bottle"]` | Must match training order |
| `ModelWidth` | `640` | Model input width (px) |
| `ModelHeight` | `640` | Model input height (px) |
| `ConfidenceThreshold` | `0.50` | Minimum confidence to keep a detection |
| `IouThreshold` | `0.50` | IoU threshold for NMS |
| `CameraIndex` | `0` | Webcam device index |
| `WindowTitle` | `Object Detection (ESC to exit)` | OpenCV window title |
| `DefaultImagePath` | `../models/test/img.jpg` | Default test image |
| `OutputFileName` | `output.jpg` | Annotated output filename |
| `AutoOpenOutput` | `true` | Open result in default viewer |
| `DisplayWidth` | `960` | Webcam display width |
| `DisplayHeight` | `540` | Webcam display height |
| `UseSoftNms` | `true` | Enable Soft-NMS (Gaussian decay) |
| `SoftNmsSigma` | `0.5` | Sigma for Soft-NMS |
| `MergeCloseDetections` | `true` | Merge near-identical boxes |
| `MergeDistanceThreshold` | `30.0` | Center-distance threshold for merging |

> **Pro tip:** Environment variables override JSON values. Set `Detection__ConfidenceThreshold=0.3`
> to lower the threshold without editing the file.

---

## Dependency Injection

The DI container is built manually in `Program.cs:52-62` using `Microsoft.Extensions.DependencyInjection`.
All services are registered as singletons:

```csharp
var services = new ServiceCollection();
services.AddSingleton(options);                                    // DetectionOptions
services.AddSingleton<IUserInterface>(userInterface);              // ConsoleUserInterface
services.AddSingleton<IProjectPathProvider>(pathProvider);          // ProjectPathProvider
services.AddSingleton<CustomModelRegistry>(customModelRegistry);
services.AddSingleton<IImageRenderer, DetectionOverlayRenderer>();
services.AddSingleton<IObjectDetector, OnnxObjectDetector>();
services.AddSingleton<IWebcamDetectionLoop, WebcamDetectionLoop>();
services.AddSingleton<DetectImageFromFileUseCase>();
services.AddSingleton<DetectImagesInFolderUseCase>();
services.AddSingleton<RunDetectionApplication>();
```

The interactive model-selection flow runs *before* the container is built (it mutates
`DetectionOptions`), then the container wires the final state into all dependents.

To add a new service or swap an implementation:
1. Implement the relevant interface
2. Add the registration to the `ServiceCollection` block
3. Do **not** use `new` elsewhere in `Main()`

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.ML.OnnxRuntime` | 1.24.3 | YOLO model inference engine |
| `OpenCvSharp4` | 4.13.0 | Webcam capture + display window |
| `OpenCvSharp4.Extensions` | 4.13.0 | `BitmapConverter.ToBitmap` / `ToMat` |
| `OpenCvSharp4.runtime.win` | 4.13.0 | Native OpenCV binaries (Windows) |
| `System.Drawing.Common` | 10.0.5 | GDI+ image loading + rendering |
| `Microsoft.Extensions.Hosting` | 10.0.7 | DI container + Configuration + Logging |

---

## Current Model

**Detector v4 (Enhanced)** — YOLOv11n fine-tuned on bottle, soap, and soap-cover classes.

| Property | Value |
|---|---|
| Architecture | YOLOv11n |
| Input | 640×640 |
| Classes | bottle, soap, soap-cover (in training order) |
| Training | 300 epochs, AdamW, mosaic + mixup augmentation |
| Loss | Focal Loss (cls\_weight=2.0), label smoothing |
| Confidence threshold | 50% (adjustable) |
| Model file | `models/detector_v4.onnx` |

Additional pre-built models available in the catalog: `detector_v3.onnx`, `yolov8n.onnx`,
`yolo11n.onnx`, `bottle_v1.onnx`.

---

## Training Pipeline

The `ai-training/` directory contains a complete YOLO training pipeline in Python:

```bash
cd ai-training

# Run all steps in order
python scripts/run_pipeline.py

# Or run individual steps
python scripts/0_validate_labels.py   # Validate annotation format
python scripts/1_prepare_dataset.py   # Split train/val
python scripts/2_augment_dataset.py   # 12x augmentation
python scripts/3_create_config.py     # Generate YOLO config
python scripts/4_train.py             # Train the model
python scripts/5_export.py            # Export to ONNX
```

Export your trained model to `models/` and the app picks it up automatically.

---

## Troubleshooting

| Problem | Solution |
|---|---|
| **No detections** | Lower `ConfidenceThreshold` in Settings menu or `appsettings.json` |
| **Too many false positives** | Raise `ConfidenceThreshold` to 0.60+ |
| **Webcam won't open** | Close other camera apps, change `CameraIndex` to 1 or 2 |
| **Model file not found** | Copy `.onnx` to `models/` folder (files are gitignored) |
| **Classes appear wrong** | Class label order must match exactly with training order |
| **Output image not saved** | Check write permissions in the output directory |
| **Batch folder slow** | CPU-only inference — reduce image resolution or batch size |

---

## Repository

**[github.com/ammarGamal123/yolo-trained-model](https://github.com/ammarGamal123/yolo-trained-model)**

---

*Version 2.0.0 — May 2026*
