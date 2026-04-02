# DeepLearning Project Walkthrough (Junior .NET Developer Guide)

> Goal: help you understand the full codebase, architecture, runtime flow, and how to safely modify/extend the project by yourself.

---

## 1) What this project does

This app is a YOLO + ONNX Runtime object detector with two modes:

- **Webcam mode**: real-time detection from camera frames
- **Image mode**: detect objects in one image file, save an annotated output image

It uses a clean architecture style so each area of code has a clear responsibility.

---

## 2) High-level architecture

```text
DeepLearning/
├─ Application/      # Use cases, interfaces, config, app-level models
├─ Domain/           # Core business entities and enums (pure logic)
├─ Infrastructure/   # ONNX runtime, OpenCV capture, rendering, file paths
├─ Presentation/     # Console UI
└─ Program.cs        # Composition root (wires everything together)
```

### Layer responsibilities

| Layer | Responsibility | Depends on |
|---|---|---|
| Domain | Business entities like `DetectionResult` | Nothing external |
| Application | Use-cases and interfaces (`IObjectDetector`, etc.) | Domain |
| Infrastructure | Technical implementations (ONNX/OpenCV/GDI+/filesystem) | Application + Domain + external libs |
| Presentation | User interaction (console prompts/output) | Application + Domain |
| Program.cs | Creates concrete objects and starts app flow | All layers |

---

## 3) Runtime flow (from app start to detection)

## 3.1 Startup flow

```text
Program.Main()
  ├─ create DetectionOptions
  ├─ create ConsoleUserInterface
  ├─ ask: custom model or default model?
  │    └─ if custom:
  │         ├─ browse for ONNX file
  │         ├─ infer class count from model output
  │         └─ prompt class labels (must match class count)
  ├─ create detector/renderer/path provider/use-cases
  └─ RunDetectionApplication.Execute()
```

## 3.2 Main app loop

```text
RunDetectionApplication.Execute()
  ├─ show welcome + active settings
  ├─ prompt input source (webcam or image file)
  ├─ if webcam:
  │    └─ WebcamDetectionLoop.Run()
  ├─ if image:
  │    └─ DetectImageFromFileUseCase.Execute(path)
  ├─ show detections / errors
  └─ ask user to continue or exit
```

---

## 4) Key files and what they do

| File | Purpose |
|---|---|
| `Program.cs` | Entry point + dependency wiring |
| `Application/Configuration/DetectionOptions.cs` | Central settings (model path, thresholds, labels, camera index, output name) |
| `Application/UseCases/RunDetectionApplication.cs` | Main application orchestration |
| `Application/UseCases/DetectImageFromFileUseCase.cs` | Single-image detection workflow |
| `Domain/Entities/DetectionResult.cs` | Detection entity (`ClassId`, confidence, box coords) |
| `Infrastructure/Detection/OnnxObjectDetector.cs` | Preprocess -> infer -> parse -> NMS |
| `Infrastructure/Detection/ImagePreprocessor.cs` | Bitmap -> CHW float tensor input |
| `Infrastructure/Detection/NmsProcessor.cs` | Removes overlapping duplicate boxes |
| `Infrastructure/Capture/WebcamDetectionLoop.cs` | OpenCV webcam loop with ESC to stop |
| `Infrastructure/Rendering/DetectionOverlayRenderer.cs` | Draws boxes + labels on image |
| `Infrastructure/Pathing/ProjectPathProvider.cs` | Resolves file paths consistently |
| `Presentation/UI/ConsoleUserInterface.cs` | Console UX: menus, prompts, reporting |

---

## 5) Detailed detection pipeline

```text
Input image (Bitmap)
    │
    ▼
ImagePreprocessor.ToChwArray(...)
    │     converts pixels to float[] in CHW layout
    ▼
OnnxObjectDetector.Detect(...)
    │
    ├─ build DenseTensor [1, 3, H, W]
    ├─ run ONNX Runtime session
    ├─ parse model output tensor:
    │    - supports [1, C, N] and [1, N, C]
    │    - for each candidate box, choose best class score
    │    - apply confidence threshold
    │    - scale bbox from model size to original image size
    └─ run NmsProcessor.Apply(...)
         to remove duplicate overlaps
    ▼
List<DetectionResult>
    │
    ├─ ConsoleUserInterface.ShowDetections(...)
    └─ DetectionOverlayRenderer.DrawDetections(...)
```

---

## 6) Why labels can show `class 2` and how this project fixes it

### Root cause

If a detection has `ClassId = 2` but your label array has only 2 entries (`[0]`, `[1]`), label lookup fails and renderer falls back to:

```text
class 2
```

### Current fix in code

1. The app **infers class count automatically** from ONNX output shape (`OnnxObjectDetector.InferClassCount()`).
2. The UI **enforces exact label count** in `PromptForClassLabels(expectedCount)`.
3. UI prints index mapping:

```text
[0] bottle, [1] bottles, [2] capped-bottle
```

This guarantees class IDs map correctly to labels.

---

## 7) Mode-specific behavior

## 7.1 Webcam mode

```text
WebcamDetectionLoop.Run()
  ├─ open camera by CameraIndex
  ├─ for each frame:
  │    ├─ Mat -> Bitmap
  │    ├─ detector.Detect(bitmap)
  │    ├─ renderer.DrawDetections(...)
  │    ├─ Bitmap -> Mat
  │    └─ Cv2.ImShow(...)
  └─ stop on ESC
```

### Notes

- If camera cannot open, user gets a clear error.
- `ESC` is handled in the OpenCV window loop.

## 7.2 Image mode

```text
DetectImageFromFileUseCase.Execute(path)
  ├─ resolve path (absolute/relative/default)
  ├─ validate file exists
  ├─ load bitmap
  ├─ detect + draw
  ├─ save output image
  ├─ optionally auto-open output image
  └─ return ImageDetectionReport
```

---

## 8) Main configuration values you should know

File: `Application/Configuration/DetectionOptions.cs`

| Property | Meaning |
|---|---|
| `ModelPath` | ONNX model path |
| `ClassLabels` | Ordered class names by class index |
| `ModelWidth`, `ModelHeight` | Expected model input dimensions |
| `ConfidenceThreshold` | Minimum confidence to keep detection |
| `IouThreshold` | NMS overlap threshold |
| `CameraIndex` | Webcam index (`0` default) |
| `WindowTitle` | Webcam window title |
| `DefaultImagePath` | Fallback image path for image mode |
| `OutputFileName` | Annotated image output filename |
| `AutoOpenOutput` | Whether to open output after save |

---

## 9) How to safely modify this project

Use this checklist:

1. Keep each change in the correct layer.
2. Prefer interfaces in Application over direct concrete coupling.
3. Update config and UI messages together when behavior changes.
4. Build after edits:

```bash
dotnet build DeepLearning.sln -c Release
```

5. Test both modes when touching detection logic:
   - Webcam mode
   - Image mode

---

## 10) Common extension scenarios

## 10.1 Add a new mode: folder/batch image processing

1. Add `DetectImagesInFolderUseCase` in `Application/UseCases`.
2. Reuse `IObjectDetector` + `IImageRenderer`.
3. Add UI option in `ConsoleUserInterface`.
4. Route selection in `RunDetectionApplication`.

## 10.2 Replace Console UI with WinForms/WPF

1. Create a new class implementing `IUserInterface`.
2. Keep use-cases unchanged.
3. Swap the object created in `Program.cs`.

## 10.3 Swap detection backend

1. Implement `IObjectDetector`.
2. Keep return contract: `IReadOnlyList<DetectionResult>`.
3. Register new implementation in `Program.cs`.

---

## 11) Dependency overview

```text
Application depends on Abstractions
Infrastructure implements Abstractions
Presentation implements Abstractions
Program composes concrete implementations
Domain stays independent and reusable
```

This is why the project is maintainable and easy to evolve.

---

## 12) Troubleshooting quick map

| Symptom | Likely cause | Fix |
|---|---|---|
| Label shown as `class X` | Class labels count/order mismatch | Ensure labels count == inferred class count and order matches training |
| No detections | Threshold too high or wrong model/classes | Lower confidence threshold, verify model + labels |
| Wrong object names | Labels order mismatch | Re-enter labels in exact training order |
| Webcam fails | Camera busy/wrong index | Close other camera apps, try different `CameraIndex` |
| File not found | Invalid image/model path | Use full absolute path or valid relative path |

---

## 13) Suggested learning path for you

If you want to master this codebase quickly, read in this order:

1. `Program.cs`
2. `Application/UseCases/RunDetectionApplication.cs`
3. `Application/UseCases/DetectImageFromFileUseCase.cs`
4. `Infrastructure/Detection/OnnxObjectDetector.cs`
5. `Infrastructure/Rendering/DetectionOverlayRenderer.cs`
6. `Presentation/UI/ConsoleUserInterface.cs`

Then do one small enhancement yourself (for example: add a menu option or save detections as JSON).

---

## 14) Architecture visualization (single-page mental map)

```text
                         +-------------------+
                         |     Program.cs    |
                         |  (composition)    |
                         +---------+---------+
                                   |
       +---------------------------+---------------------------+
       |                           |                           |
       v                           v                           v
+--------------+         +-------------------+        +----------------------+
| Presentation |         |   Application     |        |    Infrastructure    |
| Console UI   |<------->| UseCases + IFaces |<------>| ONNX/OpenCV/Renderer |
+--------------+         +-------------------+        +----------------------+
                                    ^
                                    |
                               +----+----+
                               | Domain  |
                               | Entity  |
                               | Models  |
                               +---------+
```

---

## 15) Final notes

- This project is already organized in a professional way.
- Your latest class-label fix is aligned with clean architecture and robust UX.
- Keep following the same pattern: small focused classes + interface-driven design + explicit runtime validation.

