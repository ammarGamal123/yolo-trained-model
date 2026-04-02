# Soap Detector — Project Structure

> **Last Updated:** March 2026  
> **Version:** 1.0.0  
> **Architecture:** Clean Architecture (Domain / Application / Infrastructure / Presentation)  
> **Branch:** `feature/clean-architecture-yolo-detector` — SOAP version

---

## BRANCH STRUCTURE — IMPORTANT

This repo has **two separate project variants** on different branches. **They must NOT be merged into each other.**

| Branch | Model | Classes | Purpose |
|---|---|---|---|
| `feature/clean-architecture-yolo-detector` | `soap_v7.onnx` | soap, soap-cover | Soap detection |
| `feature/bottle-detector` | `best_bottle.onnx` | bottle, bottles, capped-bottle | Bottle detection |

When switching branches, you must also replace the model file in the project root:
- Switching **to soap branch**: copy `soap_v7.onnx` into the project root
- Switching **to bottle branch**: copy `best_bottle.onnx` into the project root

Model files are **NOT committed to git** (excluded by `*.onnx` in `.gitignore`).

---

## 1. What the App Does

The Soap Detector loads a YOLO ONNX model (`soap_v7.onnx`) and lets the user detect objects in:

1. **Webcam** — live real-time detection using the camera.
2. **Image File** — load any image, detect objects, print results, save an annotated copy.

**Detected classes:** `soap`, `soap-cover`

---

## 2. Architecture Layers

```
DeepLearning/
|- Application/              ← Use cases, interfaces, configuration
|- Domain/                   ← Core business objects (no external deps)
|- Infrastructure/           ← ONNX Runtime, OpenCV, rendering, paths
|- Presentation/             ← Console UI (banners, prompts, output)
|- Program.cs                ← Composition root — wires everything together
|- publish/                  ← Deployable Windows executable
|- project-structure/         ← This folder: documentation
|- sample.jpg                ← Default test image
|- soap_v7.onnx              ← YOUR trained model (local only, not in git)
```

### Layer Responsibilities

| Layer | Responsibility | External dependencies? |
|---|---|---|
| **Domain** | Pure business objects (`DetectionResult`, `InputSource` enum) | None |
| **Application** | Use cases, interfaces, configuration, report models | None |
| **Infrastructure** | ONNX Runtime, OpenCV, GDI+ drawing, path resolution | Yes |
| **Presentation** | Console input/output, ASCII banner, confidence bars, formatted output | None |

### SOLID Principles Used

| Principle | Where it is applied |
|---|---|
| **Single Responsibility** | Each class has one job: detect, preprocess, draw, ask questions |
| **Open/Closed** | Add a new detector by implementing `IObjectDetector` — no changes elsewhere |
| **Liskov Substitution** | Any `IObjectDetector` implementation works in all use cases |
| **Interface Segregation** | Small, focused interfaces (`IObjectDetector`, `IImageRenderer`, `IUserInterface`) |
| **Dependency Inversion** | Application layer depends on abstractions, not OpenCV or ONNX directly |

---

## 3. Key Files Reference

| File | What it does |
|---|---|
| `Program.cs` | Composition root. Creates all dependencies and starts the app. |
| `DetectionOptions.cs` | All settings in one place: model path, thresholds, camera index, output settings. |
| `RunDetectionApplication.cs` | Main workflow: welcome → asks for input source → runs chosen mode → handles errors. |
| `DetectImageFromFileUseCase.cs` | Full image workflow: load → detect → draw → save → return report. |
| `OnnxObjectDetector.cs` | Runs the ONNX model. Handles preprocessing, inference, parsing, and NMS. |
| `ImagePreprocessor.cs` | Converts `Bitmap` to CHW float array for YOLO input. |
| `NmsProcessor.cs` | Removes duplicate overlapping bounding boxes using Intersection over Union (IoU). |
| `WebcamDetectionLoop.cs` | Opens camera, runs detection on each frame, displays live OpenCV window. |
| `DetectionOverlayRenderer.cs` | Draws bounding boxes and class labels on images. |
| `ConsoleUserInterface.cs` | Console UI: ASCII banner, prompts, confidence bars, formatted output. |
| `ProjectPathProvider.cs` | Resolves paths for both development and deployed environments. |

---

## 4. Clean Architecture Data Flow

```
User runs app
    │
    ▼
RunDetectionApplication.Execute()
    │
    ├─[1] PromptForInputSource()
    │        └── ConsoleUserInterface → User types 1 or 2
    │
    ├─[2] Webcam mode
    │        └── WebcamDetectionLoop.Run()
    │              Capture frame → OnnxObjectDetector.Detect()
    │                         → DetectionOverlayRenderer.DrawDetections()
    │                         → OpenCV ImShow() live window
    │              [ESC key] → stops and cleans up
    │
    └─[2] Image mode
             └── DetectImageFromFileUseCase.Execute(path)
                   ├─ Load image from disk
                   ├─ OnnxObjectDetector.Detect()
                   │     ImagePreprocessor.ToChwArray()
                   │     ONNX session.Run()
                   │     Parse output + NmsProcessor.Apply()
                   ├─ DetectionOverlayRenderer.DrawDetections()
                   ├─ Save output.jpg (auto-opens by default)
                   └─ ConsoleUserInterface → Detection report + confidence bars
```

---

## 5. How to Run

### From Source (development)

```bash
dotnet build
dotnet run
```

### From Published Executable (deployment)

Copy the `publish/` folder to any Windows x64 machine and run `modelDetector.exe`.

**No .NET installation required on the target machine.**

See `project-structure/PROFESSIONAL-GUIDE.md` for the full end-user guide.

---

## 6. How to Configure

Open `Application/Configuration/DetectionOptions.cs` and change the default property values.

### Most Common Changes

| What you want | Change this in DetectionOptions.cs |
|---|---|
| Use a different model | `ModelPath = "my_model.onnx"` |
| Update class names | `ClassLabels = ["class1", "class2"]` |
| Show more detections | `ConfidenceThreshold = 0.05f` |
| Show fewer detections | `ConfidenceThreshold = 0.50f` |
| Use a second camera | `CameraIndex = 1` |
| Change default image | `DefaultImagePath = "my_image.jpg"` |
| Disable auto-open result | `AutoOpenOutput = false` |

---

## 7. Important Rule When Changing the Model

If you replace `soap_v7.onnx` with a new model, make sure these three things match:

1. **Input size** — `ModelWidth` and `ModelHeight` must match the model's expected input.
2. **Class label order** — `ClassLabels` must be in the exact same order used during training.
3. **Class count** — The number of labels must match the number of classes in the model.

If the label order is wrong, detections will still run, but the names shown on the boxes will be incorrect.

---

## 8. What is Easy to Extend

Because the project is layered, these additions are low-risk:

| Extension | How |
|---|---|
| New detector backend | Implement `IObjectDetector` (e.g. TensorRT, OpenVINO) and register in `Program.cs` |
| New UI | Implement `IUserInterface` (e.g. WinForms, WPF) |
| Batch processing | Add a new use case in `Application/UseCases/` that loops over files in a folder |
| Logging | Inject an `ILogger` into the use cases |
| Config file | Replace `DetectionOptions` construction in `Program.cs` with values from `appsettings.json` |
| Unit tests | Add a test project that mocks `IObjectDetector` and `IImageRenderer` |

---

## 9. Documentation Files

| File | What it contains |
|---|---|
| `README.md` (this file) | Architecture overview, file reference, configuration guide, branch structure |
| `PROFESSIONAL-GUIDE.md` | End-user guide: how to run, image path examples, deployment, troubleshooting |
| `PROJECT-TREE.txt` | Quick folder tree map |
| `diagrams/` | Visual diagrams (pipeline, CHW format, NMS, tensor shapes) |

---

## 10. Verification

```bash
dotnet build                                              # Passed — 0 warnings, 0 errors
printf '2\n\n' | dotnet run --no-build                   # Passed — default image
printf '2\n/path/to/image.jpg' | ./publish/modelDetector.exe  # Passed — custom path
```

Detection results are printed to console with confidence bars and bounding box coordinates.
