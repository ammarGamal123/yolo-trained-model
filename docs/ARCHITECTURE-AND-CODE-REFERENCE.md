# modelDetector — Complete Architecture & Code Reference

> A production-grade .NET 8 Clean Architecture application for real-time YOLO object detection via ONNX Runtime, OpenCV, and GDI+.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Architecture Philosophy](#2-architecture-philosophy)
3. [Layer-by-Layer Breakdown](#3-layer-by-layer-breakdown)
4. [Every File Explained — Line by Line](#4-every-file-explained--line-by-line)
5. [Data Flow & Runtime Execution](#5-data-flow--runtime-execution)
6. [Detection Pipeline Deep Dive](#6-detection-pipeline-deep-dive)
7. [Key Design Patterns & Principles](#7-key-design-patterns--principles)
8. [Configuration Reference](#8-configuration-reference)
9. [Dependencies & Their Roles](#9-dependencies--their-roles)
10. [How to Build, Run & Deploy](#10-how-to-build-run--deploy)
11. [Extending the Project](#11-extending-the-project)
12. [Troubleshooting](#12-troubleshooting)

---

## 1. Project Overview

**What this project is:** A Windows console application that performs real-time object detection using YOLO models exported to ONNX format. It operates in two modes:

| Mode | Description |
|------|-------------|
| **Webcam** | Captures live video from a camera, runs detection on every frame, displays annotated video in a window |
| **Image File** | Loads a single image from disk, runs detection, saves annotated output, optionally opens it |

**What this project is NOT:** This is not a machine learning training framework. The ML model (`.onnx` file) is pre-trained elsewhere (e.g., via Ultralytics YOLO, PyTorch, etc.) and loaded at runtime for inference only.

**Key stats:**
- **Target Framework:** `net8.0-windows`
- **Assembly Name:** `modelDetector`
- **Default Model:** `soap_v7.onnx` (detects `soap`, `soap-cover`)
- **Model Input Size:** 640×640 pixels
- **Runtime Type:** Self-contained Windows x64 executable
- **Total Source Files:** 14 (excluding notebooks and test data)
- **Lines of Code:** ~1,300

---

## 2. Architecture Philosophy

This project follows **Clean Architecture** (also known as Onion Architecture or Hexagonal Architecture). The core idea is simple:

> **Dependencies point inward.** Outer layers depend on inner layers. Inner layers know nothing about outer layers.

### The Four Concentric Layers

```
┌─────────────────────────────────────────────────────────┐
│                   PRESENTATION LAYER                     │
│  (ConsoleUserInterface — what the user sees)             │
│  ┌───────────────────────────────────────────────────┐   │
│  │               APPLICATION LAYER                    │   │
│  │  (Use Cases + Interfaces — business logic)         │   │
│  │  ┌─────────────────────────────────────────────┐   │   │
│  │  │              INFRASTRUCTURE LAYER             │   │   │
│  │  │  (ONNX, OpenCV, GDI+, FileSystem — tech)     │   │   │
│  │  │  ┌───────────────────────────────────────┐   │   │   │
│  │  │  │            DOMAIN LAYER                │   │   │   │
│  │  │  │  (DetectionResult, InputSource — pure) │   │   │   │
│  │  │  └───────────────────────────────────────┘   │   │   │
│  │  └─────────────────────────────────────────────┘   │   │
│  └───────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### Dependency Rules

| Layer | Can Depend On | Cannot Depend On |
|-------|--------------|-----------------|
| **Domain** | Nothing | Everything |
| **Application** | Domain | Infrastructure, Presentation |
| **Infrastructure** | Application (interfaces), Domain | Presentation |
| **Presentation** | Application (interfaces), Domain | Infrastructure |

This means:
- `DetectionResult` (Domain) has **zero** `using` statements for any other project namespace
- `IObjectDetector` (Application) defines **what** detection does, not **how**
- `OnnxObjectDetector` (Infrastructure) **implements** the interface — it's a detail
- `ConsoleUserInterface` (Presentation) talks to **interfaces**, never concrete implementations

### Why This Matters

If you want to swap ONNX Runtime for TensorRT:
1. Create `TensorRtObjectDetector : IObjectDetector` in Infrastructure
2. Change **one line** in `Program.cs`: `new TensorRtObjectDetector(options)`
3. **Zero** changes to Application, Domain, or Presentation layers

---

## 3. Layer-by-Layer Breakdown

### Domain Layer (`Domain/`)

**Purpose:** Pure business objects. No frameworks, no libraries, no external dependencies. Just data and rules.

| File | What It Does |
|------|-------------|
| `DetectionResult.cs` | Represents a single detected object: class ID, confidence, bounding box coordinates (X1, Y1, X2, Y2), plus computed properties (Width, Height, Area) and a formatter |
| `InputSource.cs` | Simple enum with two values: `Webcam = 1`, `ExistingImage = 2` |

**Why it exists:** These are the fundamental data types that every other layer shares. They are the "language" of the application.

### Application Layer (`Application/`)

**Purpose:** Use cases (what the app does) and interfaces (contracts for how things are done). Zero external dependencies — only references Domain.

| File | What It Does |
|------|-------------|
| `IObjectDetector.cs` | Contract: "Give me a Bitmap, I'll return detections" |
| `IImageRenderer.cs` | Contract: "Give me an image and detections, I'll return an annotated image" |
| `IUserInterface.cs` | Contract: 15 methods for all user interactions (welcome, prompts, reports, browsing) |
| `IWebcamDetectionLoop.cs` | Contract: "Run the live camera detection loop" |
| `IProjectPathProvider.cs` | Contract: "Resolve paths and find image files" |
| `DetectionOptions.cs` | Configuration POCO: model path, thresholds, camera index, etc. |
| `RunDetectionApplication.cs` | Main orchestrator: shows welcome, loops between webcam/image modes, handles errors |
| `DetectImageFromFileUseCase.cs` | Single-image workflow: load → detect → render → save → report |
| `ImageDetectionReport.cs` | DTO: input path, output path, list of detections |

**Why it exists:** This layer defines the **behavior** of the application. It says "what" needs to happen without saying "how." The interfaces are contracts that Infrastructure fulfills.

### Infrastructure Layer (`Infrastructure/`)

**Purpose:** Technical implementations. This is where external libraries live (ONNX Runtime, OpenCV, GDI+, FileSystem).

| File | What It Does |
|------|-------------|
| `OnnxObjectDetector.cs` | Loads ONNX model, preprocesses images, runs inference, parses output, applies NMS |
| `ImagePreprocessor.cs` | Converts Bitmap → CHW float array (resize, separate channels, normalize 0-255 → 0-1) |
| `NmsProcessor.cs` | Non-Maximum Suppression: removes duplicate/overlapping bounding boxes |
| `WebcamDetectionLoop.cs` | OpenCV webcam capture → detect → render → display loop |
| `DetectionOverlayRenderer.cs` | GDI+ drawing: bounding boxes, labels, confidence scores |
| `ProjectPathProvider.cs` | Smart path resolution: detects dev vs. deployed environment |

**Why it exists:** This layer contains all the "messy" technical details. It's isolated so that changing a library (e.g., swapping GDI+ for SkiaSharp) only affects this layer.

### Presentation Layer (`Presentation/`)

**Purpose:** User interaction. Console UI with ASCII art, colored output, file dialogs.

| File | What It Does |
|------|-------------|
| `ConsoleUserInterface.cs` | 514-line full console UI: banner, menus, color-coded output, confidence bars, WinForms file dialogs |

**Why it exists:** Decoupled from business logic. You could replace it with a WPF UI, a web UI, or a REST API — the Application layer wouldn't change.

### Composition Root (`Program.cs`)

**Purpose:** Wires everything together. The only place where concrete implementations are instantiated and connected.

---

## 4. Every File Explained — Line by Line

### 4.1 `Program.cs` — The Composition Root

```
Lines 1-8:  using statements — import all layers
Line 10:    Namespace declaration
Line 12:    static class — no instances needed, just an entry point
Line 14:    [STAThread] — required for WinForms OpenFileDialog (used in ConsoleUserInterface)
Line 15:    Main() — the application entry point
Line 17:    Create DetectionOptions with default values (soap_v7.onnx, 640x640, etc.)
Line 18:    Create ConsoleUserInterface — cast to IUserInterface interface
Line 20-23: Ask user: custom model or default? If custom, run the setup flow
Line 25:    Create OnnxObjectDetector — loads the ONNX model into memory
Line 26:    Create ProjectPathProvider — resolves file paths
Line 27:    Create DetectionOverlayRenderer — configures how boxes/labels are drawn
Line 28:    Create WebcamDetectionLoop — wires detector + renderer + UI together
Line 30-34: Create DetectImageFromFileUseCase — wires detector + renderer + path provider
Line 36-41: Create RunDetectionApplication — the main orchestrator, wires everything
Line 43:    Start the application loop

Lines 46-69:  PromptLoadCustomModel() — asks user if they want a custom ONNX model
Lines 71-108: LoadCustomModelFlow() — browses for .onnx, infers class count, prompts for labels
Lines 110-130: PromptForModelClassCount() — asks user how many classes their model detects
```

**Key insight:** `Program.cs` is the **only** file that knows about all concrete types. Every other file only knows about interfaces. This is the Dependency Inversion Principle in action.

---

### 4.2 `DetectionOptions.cs` — Configuration Center

```
Line 3:   sealed class — cannot be inherited (performance + intent)
Line 5:   ModelPath — path to the .onnx file, default "soap_v7.onnx"
Line 6:   ClassLabels — array of class names, default ["soap", "soap-cover"]
Line 7-8: ModelWidth/Height — the input size the model expects (640x640 for YOLO)
Line 9:   ConfidenceThreshold — minimum confidence to keep a detection (0.20 = 20%)
Line 10:  IouThreshold — maximum overlap before NMS removes a box (0.45 = 45%)
Line 11:  CameraIndex — which webcam to use (0 = first camera)
Line 12:  WindowTitle — title of the OpenCV display window
Line 13:  DefaultImagePath — fallback image when user doesn't specify one
Line 14:  OutputFileName — name of the annotated output image
Line 15:  AutoOpenOutput — whether to auto-open the output image after detection
```

**Key insight:** This single object flows through the entire application. Every layer reads from it. It's the "configuration DNA" of the app.

---

### 4.3 `DetectionResult.cs` — The Core Entity

```
Line 7:   sealed class — immutable after construction (init-only properties)
Line 12:  ClassId — zero-based index into the ClassLabels array
Line 17:  Confidence — model's certainty (0.0 to 1.0)
Line 22-37: X1, Y1, X2, Y2 — bounding box in corner format (top-left, bottom-right)
Line 40:  Width => X2 - X1 — computed property, no storage needed
Line 43:  Height => Y2 - Y1 — computed property
Line 46:  Area => Width * Height — computed property, useful for filtering
Line 53-60: Format() — converts raw data to human-readable string
            Lines 55-57: Safe class label lookup (handles out-of-range ClassId)
            Line 59: String interpolation with format specifiers:
                     {Confidence:P1} = percentage with 1 decimal (e.g., "44.2%")
                     {X1:F0} = float with 0 decimals (e.g., "206")
```

**Key insight:** This class uses `required init` properties, meaning every property MUST be set during construction. This prevents partially-constructed objects. The computed properties (Width, Height, Area) are derived — they're never stored, always calculated on demand.

---

### 4.4 `InputSource.cs` — The Mode Enum

```
Line 6:   enum — fixed set of named constants
Line 11:  Webcam = 1 — live camera mode
Line 16:  ExistingImage = 2 — static image file mode
```

**Key insight:** Using an enum instead of a boolean (`isWebcam`) makes the code self-documenting. `InputSource.Webcam` is clearer than `true`.

---

### 4.5 `IObjectDetector.cs` — The Detection Contract

```
Line 11:  interface + IDisposable — implementations must clean up resources
Line 18:  Detect(Bitmap) → IReadOnlyList<DetectionResult>
          - Takes any size Bitmap
          - Returns read-only list (caller can't modify)
          - Empty list means "nothing found" (not null)
```

**Key insight:** The interface says nothing about ONNX, YOLO, or neural networks. It just says "detect objects in an image." This means you could implement it with a rule-based system, a different ML framework, or even a mock for testing.

---

### 4.6 `IImageRenderer.cs` — The Rendering Contract

```
Line 10:  interface — no IDisposable (caller disposes the returned Bitmap)
Line 18:  DrawDetections(Bitmap, detections) → Bitmap
          - Source image is NOT modified (immutability)
          - Returns a NEW Bitmap that the caller must dispose
```

**Key insight:** The "don't modify the source" rule is critical. In the webcam loop, the source frame is reused every iteration. If the renderer modified it, you'd get accumulating artifacts.

---

### 4.7 `IUserInterface.cs` — The UI Contract

```
Line 8-26:  15 methods covering every user interaction:
            ShowWelcome()       — startup banner with config summary
            PromptForInputSource() — webcam or image?
            PromptForImagePath()   — how to get the image path
            ShowImageDetectionReport() — show results after image detection
            ShowWebcamInstructions() — tell user how to use webcam mode
            ShowDetections()      — display detection list with confidence bars
            ShowInfo/Error/Success() — colored console messages
            ShowPrompt()          — input prompt with label
            PromptForContinue()   — run again or exit?
            ShowExitMessage()     — goodbye message
            BrowseForOnnxFile()   — WinForms file dialog for .onnx
            PromptForClassLabels() — enter class names for custom model
            BrowseForImageFile()  — WinForms file dialog for images
```

**Key insight:** This interface is large because the UI has many responsibilities. In a larger system, this might be split into `IConsoleOutput`, `IUserInput`, and `IFileDialogProvider`. But for this app's size, one interface is appropriate.

---

### 4.8 `IWebcamDetectionLoop.cs` — The Webcam Contract

```
Line 7:   interface — single method
Line 12:  Run() — blocks until user exits (ESC key)
```

**Key insight:** The simplicity of this interface is intentional. The implementation handles all the complexity (camera access, frame reading, detection, display, cleanup). The caller just says "run."

---

### 4.9 `IProjectPathProvider.cs` — The Path Contract

```
Line 9:   interface — four methods
Line 14:  GetProjectRoot() — absolute path to app root
Line 19:  GetProjectFilePath(relative) — combine root with relative path
Line 25:  GetAbsolutePath(path) — if already absolute, return as-is; else resolve
Line 30:  GetImageFiles() — enumerate .jpg/.png/etc. in app root
```

**Key insight:** This abstraction solves a real problem: paths work differently during development (project root is `DeepLearning/`) vs. after publishing (root is the publish folder). The implementation handles both transparently.

---

### 4.10 `RunDetectionApplication.cs` — The Main Orchestrator

```
Lines 9-13:  Five dependencies injected via constructor (no DI container needed)
Line 29:     Execute() — the main application loop
Line 31:     Show welcome banner with current configuration
Line 33:     while(true) — infinite loop until user chooses to exit
Line 37:     Ask user: webcam or image?
Lines 39-43:  If webcam: show instructions, run the live detection loop
Lines 45-51:  If image: resolve path, run detection use case, show report
Lines 53-64:  Catch specific exceptions (file not found, access denied, general)
Lines 66-71:  Ask user: continue or exit? If exit, show goodbye and return
```

**Key insight:** This is the "conductor" of the application. It doesn't know HOW detection works, HOW rendering works, or HOW the UI looks. It just coordinates the flow: "ask user → dispatch to handler → show result → repeat."

---

### 4.11 `DetectImageFromFileUseCase.cs` — Single Image Workflow

```
Lines 26-50:  Constructor injection of four dependencies
Line 62:      Execute(string) — main method, takes a path string
Line 64:      ResolveImagePath() — convert user input to absolute path
Lines 66-71:  Validate file exists, throw FileNotFoundException if not
Line 73:      Load Bitmap from disk (using statement ensures disposal)
Line 74:      Run detector — returns list of DetectionResult
Line 75:      Render overlays — returns new annotated Bitmap
Line 77-78:  Save annotated image to output path
Lines 80-84:  Optionally open the output image in default viewer
Lines 86-91:  Return report with input/output paths and detections
Lines 100-108: ResolveImagePath() — handles empty, absolute, and relative paths
```

**Key insight:** This use case is a pure pipeline: input → transform → output. Each step depends on the previous one's result. The `using` statements on lines 73 and 75 ensure that Bitmaps are disposed even if an exception occurs.

---

### 4.12 `ImageDetectionReport.cs` — The Result DTO

```
Line 13:  sealed class — data transfer object
Lines 18-28: Three required init-only properties:
             InputPath  — what was processed
             OutputPath — where the result was saved
             Detections — what was found
```

**Key insight:** `required` keyword means the compiler enforces that all three properties are set. You cannot create a partially-populated report. This is a compile-time safety net.

---

### 4.13 `OnnxObjectDetector.cs` — The Detection Engine

```
Lines 20-36: Constructor — loads ONNX model, stores input tensor name
             Line 34: InferenceSession — the ONNX Runtime engine
             Line 35: InputMetadata.Keys.First() — gets the name of the input tensor
                      (YOLO models typically have one input named "images")

Lines 39-53: Detect(Bitmap) — the main detection pipeline
             Line 41: Preprocess image → CHW float array
             Line 42: Wrap array in DenseTensor with shape [1, 3, 640, 640]
                      - 1 = batch size (single image)
                      - 3 = color channels (R, G, B)
                      - 640, 640 = height, width
             Lines 44-47: Create NamedOnnxValue — wraps tensor with input name
             Line 49: Run inference — blocks until model processes the tensor
             Line 50: Parse raw output into DetectionResult objects
             Line 52: Apply NMS — remove duplicate/overlapping boxes

Lines 63-92: InferClassCount() — auto-detect how many classes the model has
             Lines 65-68: Create a zero-filled input tensor (dummy data)
             Line 75: Run inference with dummy input
             Line 76: Find the detection head output tensor
             Lines 79-83: Calculate class count from tensor dimensions
                          YOLO output: [1, C, N] or [1, N, C]
                          C = 4 (box params) + classCount
                          So: classCount = channelCount - 4
             Lines 85-89: Validate class count is positive

Lines 101-163: ParseDetections() — convert raw tensor to DetectionResult list
               Line 106: Find the detection head (main output tensor)
               Line 107: Convert tensor to flat float array
               Lines 109-114: Determine layout (channels-first vs channels-last)
                              - YOLOv8: [1, 4+classes, 8400] (channels-first)
                              - YOLOv11: [1, 8400, 4+classes] (channels-last)
               Lines 116-119: ValueAt(channel, box) — helper to read tensor value
                              Handles both layouts with a single function
               Lines 121-122: Scale factors — convert from model coordinates (640x640)
                              back to original image coordinates
               Lines 125-160: Loop through all candidate boxes
                              Lines 127-130: Read box parameters (center X, center Y, width, height)
                              Lines 132-144: Find the best class for this box
                                             (highest confidence among all classes)
                              Lines 146-149: Skip if below confidence threshold
                              Lines 151-159: Create DetectionResult with scaled coordinates
                                             Convert from center-width-height to corner format:
                                             X1 = centerX - width/2
                                             Y1 = centerY - height/2
                                             X2 = centerX + width/2
                                             Y2 = centerY + height/2

Lines 170-192: FindDetectionHead() — find the right output tensor
               Lines 173-188: Look for a 3D tensor [1, A, B] where:
                              - min(A,B) >= 6 (at least 4 box params + 2 classes)
                              - max(A,B) >= 1000 (YOLO produces 8400 candidates)
               Lines 190-191: Fallback — if no match, use the first output tensor
```

**Key insight:** This is the most complex file in the project. The critical understanding is:
1. YOLO models output a tensor of shape `[1, C, N]` or `[1, N, C]`
2. Each of the N candidates has 4 box values + C class confidence values
3. The code must handle both layouts because different YOLO versions use different formats
4. Coordinates from the model are relative to the 640×640 input — they must be scaled back to the original image size

---

### 4.14 `ImagePreprocessor.cs` — Image to Tensor Converter

```
Lines 24-58: Static class — no state, no instances needed
Line 37:     ToChwArray(Bitmap, targetWidth, targetHeight) → float[]
Line 39:     Resize image to target dimensions (bilinear interpolation by default)
Line 41:     Allocate output array: 3 channels × height × width
Lines 44-55: Nested loops — iterate every pixel
             Line 48: GetPixel(x, y) — read RGB values (0-255)
             Line 49: Calculate flat index for this pixel position
             Line 51: Store R value at index (first plane)
             Line 52: Store G value at pixelCount + index (second plane)
             Line 53: Store B value at 2*pixelCount + index (third plane)
                      Each value is divided by 255 to normalize to 0.0-1.0
```

**Key insight:** The CHW (Channels-Height-Width) format is what neural networks expect. Images are normally stored as HWC (Height-Width-Channels, i.e., each pixel has R,G,B together). The preprocessor separates the channels into three contiguous planes. This is called "planar" vs "interleaved" format.

For a 640×640 image:
- Array length = 3 × 640 × 640 = 1,228,800 floats
- Indices 0 to 409,599: Red channel
- Indices 409,600 to 819,199: Green channel
- Indices 819,200 to 1,228,799: Blue channel

---

### 4.15 `NmsProcessor.cs` — Duplicate Box Remover

```
Lines 22-56: Static class — pure function, no state
Line 33:     Apply(detections, iouThreshold) → filtered list
Line 37:     Group detections by class ID (each class is processed independently)
Line 39-41:  Sort each group by confidence (highest first)
Lines 43-52: Greedy NMS algorithm:
             Line 45: Take the highest-confidence box — keep it
             Line 47: Remove it from candidates
             Lines 49-51: Remove all remaining boxes that overlap too much (IoU > threshold)
             Repeat until no candidates remain

Lines 62-73: CalculateIoU(first, second) → float (0.0 to 1.0)
             Lines 64-67: Find the overlapping rectangle
                          overlapX1 = max of left edges
                          overlapY1 = max of top edges
                          overlapX2 = min of right edges
                          overlapY2 = min of bottom edges
             Line 69: Calculate overlap area (clamp to 0 if no overlap)
             Line 70: Union area = area1 + area2 - overlap (inclusion-exclusion principle)
             Line 72: IoU = overlap / union (add tiny epsilon to avoid division by zero)
```

**Key insight:** NMS is essential because YOLO produces thousands of candidate boxes per image, many of which overlap the same object. Without NMS, you'd see 10-20 boxes around a single object. NMS keeps only the most confident one.

The IoU (Intersection over Union) calculation:
```
IoU = 0.0 → boxes don't overlap at all
IoU = 0.5 → boxes overlap by 50%
IoU = 1.0 → boxes are identical
```

With threshold 0.45: if two boxes overlap by more than 45%, the lower-confidence one is discarded.

---

### 4.16 `WebcamDetectionLoop.cs` — Live Camera Detection

```
Lines 24-48: Constructor — inject dependencies (options, detector, renderer, UI)
Line 51:     Run() — the main loop method
Line 53:     Open webcam using OpenCV VideoCapture
             - CameraIndex 0 = first camera
             - VideoCaptureAPIs.DSHOW = DirectShow backend (Windows-specific, more reliable)
Lines 55-61: Check if camera opened successfully
Line 63:     Create Mat (OpenCV's image container) — reused every frame
Line 65:     while(true) — infinite loop
Line 67:     Capture one frame from camera into Mat
Lines 69-72: Skip empty frames (can happen during camera initialization)
Line 74:     Convert Mat → Bitmap (OpenCvSharp.Extensions)
Line 75:     Run detection on the Bitmap
Line 76:     Render detection overlays onto a new Bitmap
Line 77:     Convert overlay Bitmap → Mat for display
Line 79:     Display the frame in a window
Line 81:     Wait 1ms for key press; if ESC (key code 27), break the loop
Lines 87-89: Cleanup: release camera, destroy windows, notify user
```

**Key insight:** The frame rate is determined by how fast detection runs. Each iteration:
1. Capture frame (~1ms)
2. Convert to Bitmap (~5ms)
3. Run ONNX inference (~20-100ms depending on model and CPU)
4. Render overlays (~5ms)
5. Display (~1ms)

Total: ~30-110ms per frame = ~9-33 FPS. The `WaitKey(1)` ensures the window stays responsive.

---

### 4.17 `DetectionOverlayRenderer.cs` — Bounding Box Drawer

```
Lines 12-23: Constructor — store class labels from options
Line 26:     DrawDetections(image, detections) → annotated Bitmap
Line 28:     Create a copy of the source image (don't modify original)
Line 30:     Create Graphics object for drawing
Line 31:     Create pen: DeepSkyBlue color, 2px width (for bounding boxes)
Line 32:     Create font: Segoe UI, 9pt, Bold (for labels)
Line 33:     Create brush: semi-transparent dark background (ARGB: 200, 15, 23, 42)
Line 34:     Create brush: white text
Lines 36-53: Loop through each detection:
             Lines 38-42: Create Rectangle from X1, Y1, X2, Y2 (cast to int)
             Line 44: Draw the bounding box rectangle
             Line 46: Format the label text (e.g., "soap 0.72")
             Line 47: Measure the text size to know how big the label background should be
             Line 48: Calculate label Y position (above the box, but not above the image)
             Line 49: Create label background rectangle (with 8px horizontal padding)
             Line 51: Fill the label background
             Line 52: Draw the label text (with 4px padding from edges)
Line 55:     Return the annotated image
Lines 58-65: FormatLabel() — create the label string
             Lines 60-62: Safe class label lookup with fallback
             Line 64: Format: "label confidence" (e.g., "soap 0.72")
```

**Key insight:** The `using` statements on lines 30-34 ensure all GDI+ objects (Graphics, Pen, Font, Brush) are disposed. GDI+ objects wrap unmanaged resources — failing to dispose them causes memory leaks.

---

### 4.18 `ProjectPathProvider.cs` — Smart Path Resolver

```
Lines 15-34: Constructor — determine the app root directory
Line 27:     Get the directory where the .exe is running
Line 29:     Calculate the dev root: go up 3 levels from base directory
             (base = bin/Debug/net8.0-windows, up 3 = project root)
Lines 31-33: Check if DeepLearning.csproj exists at dev root
             - If yes: we're in development mode → use dev root
             - If no: we're in deployed mode → use base directory

Line 37:     GetProjectRoot() — return the determined root
Line 40-41:  GetProjectFilePath(relative) — combine root with relative path
Lines 44-47: GetAbsolutePath(path) — if already absolute, normalize; else resolve
Lines 50-62: GetImageFiles() — enumerate image files in app root
             Line 56: Check file extension against known image extensions
             Line 59: Return just the filename (not full path)
             Line 60: Sort alphabetically
```

**Key insight:** The dev root detection is clever. During development:
```
AppContext.BaseDirectory = D:\Ammar\YOLO\ml.net\DeepLearning\bin\Debug\net8.0-windows\
Path.Combine(baseDir, "..", "..", "..") = D:\Ammar\YOLO\ml.net\DeepLearning\
```
After publishing:
```
AppContext.BaseDirectory = D:\publish\
Path.Combine(baseDir, "..", "..", "..") = D:\
(no .csproj at D:\, so use baseDir = D:\publish\)
```

---

### 4.19 `ConsoleUserInterface.cs` — The Full Console UI

```
Lines 13-34: Class setup with box-drawing characters and ASCII art banner
             Lines 15-20: Unicode box-drawing characters for clean console output
             Lines 22-34: ASCII art banner + title box

Lines 36-47: ShowWelcome() — clear screen, print banner, show config summary
Lines 49-78: PromptForInputSource() — numbered menu for webcam vs image
Lines 80-128: PromptForImagePath() — 4 options: browse, app folder, type path, default
Lines 130-138: ShowImageDetectionReport() — show input/output paths + detections
Lines 140-147: ShowWebcamInstructions() — tell user how to use webcam mode
Lines 149-186: ShowDetections() — grouped display with confidence bars
               Lines 159-162: Group by class name, sort by max confidence
               Lines 167-185: For each group: show count, top 3 detections, "+N more"
Lines 188-199: ShowInfo/Error/Success/Prompt — colored console output wrappers
Lines 200-220: PromptForContinue() — run again or exit?
Lines 222-232: ShowExitMessage() — goodbye banner
Lines 234-249: BrowseForOnnxFile() — WinForms OpenFileDialog for .onnx files
Lines 251-298: PromptForClassLabels() — enter class names, validate count matches
Lines 300-315: BrowseForImageFile() — WinForms OpenFileDialog for image files
Lines 317-375: BrowseForImage() — list available images in app folder, let user pick
Lines 377-408: PromptCustomPath() — let user type a path manually
Lines 410-418: PrintBanner() — print ASCII art in cyan
Lines 420-434: PrintBoxedContent() — draw a box around content lines
Lines 436-442: PrintHeader() — section header with decorative line
Lines 444-457: PrintOption() — numbered menu item with color coding
Lines 459-462: PrintLine() — blank line or custom text
Lines 464-469: PrintPrompt() — colored input prompt
Lines 471-497: PrintInfo/Success/Error/Warning() — colored message output
Lines 499-505: PrintDetectionItem() — confidence bar + coordinates
Lines 507-513: BuildConfidenceBar() — create visual bar like [████████░░] 64%
```

**Key insight:** This file is the largest because it handles all user interaction. The design principle is consistency: every output method follows the same pattern (color → write → reset). The confidence bar on lines 507-513 is a nice touch — it converts a float (0.0-1.0) into a visual representation using Unicode block characters.

---

### 4.20 `DeepLearning.csproj` — Project Configuration

```
Line 1:   SDK-style project file
Line 4:   OutputType Exe — this is a console application
Line 5:   TargetFramework net8.0-windows — .NET 8, Windows-specific
Line 6:   ImplicitUsings — automatically import common namespaces (System, System.IO, etc.)
Line 7:   Nullable — enable nullable reference types (compiler warnings for null)
Line 8:   CopyLocalLockFileAssemblies — copy all NuGet packages to output folder
Line 9:   SupportedOSPlatform — declare Windows-only
Line 10:  UseWindowsForms — enable WinForms (needed for OpenFileDialog)
Line 11:  StartupObject — explicit entry point class
Lines 12-18: Assembly metadata (name, version, description)
Lines 22-26: NuGet package references
Lines 30-35: File copy rules — copy sample.jpg and soap_v7.onnx to output folder
```

---

## 5. Data Flow & Runtime Execution

### Startup Sequence

```
1. Program.Main() starts
   ↓
2. DetectionOptions created with defaults
   ↓
3. ConsoleUserInterface created
   ↓
4. User asked: custom model or default?
   ↓ (if custom)
5. Browse for .onnx file
   ↓
6. Infer class count from model output shape
   ↓
7. User enters class labels
   ↓
8. OnnxObjectDetector loads the model
   ↓
9. All dependencies wired together
   ↓
10. RunDetectionApplication.Execute() starts the main loop
```

### Main Loop

```
┌─────────────────────────────────────┐
│  ShowWelcome()                      │
│  ↓                                  │
│  PromptForInputSource()             │
│  ↓                                  │
│  ┌──────────────┬──────────────┐    │
│  │   Webcam     │  Image File  │    │
│  │   ↓          │   ↓          │    │
│  │ ShowWebcam   │ PromptFor    │    │
│  │ Instructions │ ImagePath    │    │
│  │   ↓          │   ↓          │    │
│  │ WebcamLoop   │ DetectImage  │    │
│  │ .Run()       │ .Execute()   │    │
│  │   ↓          │   ↓          │    │
│  │ [blocks]     │ ShowReport   │    │
│  └──────────────┴──────────────┘    │
│  ↓                                  │
│  PromptForContinue()                │
│  ↓                                  │
│  ┌──────────────┬──────────────┐    │
│  │  Continue    │    Exit      │    │
│  │  (loop)      │  (return)    │    │
│  └──────────────┴──────────────┘    │
└─────────────────────────────────────┘
```

### Webcam Detection Loop

```
┌─────────────────────────────────────────────┐
│  Open VideoCapture(cameraIndex)             │
│  ↓                                          │
│  ┌──────────────────────────────────────┐   │
│  │  capture.Read(frame)                 │   │
│  │  ↓                                   │   │
│  │  Mat → Bitmap (converter)            │   │
│  │  ↓                                   │   │
│  │  detector.Detect(bitmap)             │   │
│  │    ↓                                 │   │
│  │    ImagePreprocessor.ToChwArray()    │   │
│  │    ↓                                 │   │
│  │    ONNX inference                    │   │
│  │    ↓                                 │   │
│  │    ParseDetections()                 │   │
│  │    ↓                                 │   │
│  │    NmsProcessor.Apply()              │   │
│  │    ↓                                 │   │
│  │  renderer.DrawDetections()           │   │
│  │    ↓                                 │   │
│  │  Bitmap → Mat (converter)            │   │
│  │    ↓                                 │   │
│  │  Cv2.ImShow(window, frame)           │   │
│  │    ↓                                 │   │
│  │  WaitKey(1) == 27?                   │   │
│  │    ├─ Yes → break                    │   │
│  │    └─ No  → continue loop            │   │
│  └──────────────────────────────────────┘   │
│  ↓                                          │
│  capture.Release()                          │
│  Cv2.DestroyAllWindows()                    │
└─────────────────────────────────────────────┘
```

---

## 6. Detection Pipeline Deep Dive

### The Complete Journey of a Single Image

```
Step 1: INPUT
  ─────────
  A Bitmap of any size enters the pipeline.
  Example: 1920×1080 webcam frame.

Step 2: PREPROCESSING (ImagePreprocessor)
  ────────────────────────────────────────
  a) Resize: 1920×1080 → 640×640 (bilinear interpolation)
  b) Channel separation: HWC → CHW format
     - Before: [R0,G0,B0, R1,G1,B1, ...] (interleaved)
     - After:  [R0,R1,..., G0,G1,..., B0,B1,...] (planar)
  c) Normalization: divide each value by 255
     - Before: 0-255 (byte range)
     - After:  0.0-1.0 (float range)
  d) Output: float[1,228,800] (3 × 640 × 640)

Step 3: TENSOR CREATION
  ─────────────────────
  Wrap the float array in a DenseTensor with shape [1, 3, 640, 640].
  The dimensions mean:
    - 1: batch size (one image at a time)
    - 3: RGB channels
    - 640: height
    - 640: width

Step 4: INFERENCE (ONNX Runtime)
  ──────────────────────────────
  The InferenceSession runs the model:
    - Input: DenseTensor<float> [1, 3, 640, 640]
    - Output: Tensor<float> [1, C, N] or [1, N, C]
      where C = 4 + classCount, N = 8400 (for YOLO)
  
  The model internally:
    1. Applies convolutional layers to extract features
    2. Runs the detection head to predict boxes and classes
    3. Returns raw predictions (no post-processing)

Step 5: PARSING (ParseDetections)
  ───────────────────────────────
  For each of the 8400 candidate boxes:
    a) Read box parameters: centerX, centerY, width, height
    b) Read class confidences: class0, class1, ..., classN
    c) Find the best class (highest confidence)
    d) If best confidence < threshold (0.20), skip this box
    e) Convert coordinates from 640×640 space to original image space:
       X1 = (centerX - width/2) × (originalWidth / 640)
       Y1 = (centerY - height/2) × (originalHeight / 640)
       X2 = (centerX + width/2) × (originalWidth / 640)
       Y2 = (centerY + height/2) × (originalHeight / 640)
    f) Create DetectionResult object

Step 6: NON-MAXIMUM SUPPRESSION (NmsProcessor)
  ────────────────────────────────────────────
  After parsing, you might have 50-200 detections (many overlapping).
  NMS reduces this to the final set:
    a) Group by class ID
    b) For each class:
       - Sort by confidence (highest first)
       - Keep the top box
       - Remove all boxes with IoU > 0.45 with the kept box
       - Repeat with remaining boxes
    c) Result: typically 1-10 clean, non-overlapping detections

Step 7: RENDERING (DetectionOverlayRenderer)
  ──────────────────────────────────────────
  For each final detection:
    a) Draw a DeepSkyBlue rectangle (2px) at the bounding box
    b) Create a label: "className confidence"
    c) Measure label text size
    d) Draw semi-transparent dark background above the box
    e) Draw white text on the background

Step 8: OUTPUT
  ────────────
  The annotated Bitmap is:
    - Saved to disk as output.jpg
    - Optionally opened in the default image viewer
    - Reported to the user with detection details
```

---

## 7. Key Design Patterns & Principles

### SOLID Principles

| Principle | How It's Applied |
|-----------|-----------------|
| **S**ingle Responsibility | Each class does one thing: `OnnxObjectDetector` detects, `DetectionOverlayRenderer` renders, `ConsoleUserInterface` displays |
| **O**pen/Closed | Add new detection engines by implementing `IObjectDetector` — no existing code changes |
| **L**iskov Substitution | Any `IObjectDetector` implementation can replace `OnnxObjectDetector` without breaking the app |
| **I**nterface Segregation | Five small, focused interfaces instead of one large `IDetectionService` |
| **D**ependency Inversion | High-level modules (`RunDetectionApplication`) depend on abstractions (`IObjectDetector`), not concretions (`OnnxObjectDetector`) |

### Design Patterns Used

| Pattern | Where | Why |
|---------|-------|-----|
| **Composition Root** | `Program.cs` | Single place where all dependencies are wired |
| **Strategy** | `IObjectDetector`, `IImageRenderer`, `IUserInterface` | Swappable algorithms |
| **Use Case** | `DetectImageFromFileUseCase`, `RunDetectionApplication` | Each business operation is its own class |
| **DTO** | `ImageDetectionReport`, `DetectionResult` | Data carriers between layers |
| **Template Method** | `NmsProcessor.Apply()` | Fixed algorithm structure with configurable threshold |
| **Factory** | `ImagePreprocessor.ToChwArray()` | Static factory method for tensor data |

### Immutability

- `DetectionResult`: all properties are `init`-only — cannot be modified after creation
- `ImageDetectionReport`: all properties are `required init` — must be fully populated
- `DetectionOptions`: mutable by design (needed for custom model loading), but sealed
- `IReadOnlyList<T>`: used everywhere instead of `List<T>` — prevents accidental modification

### Resource Management

- `using` statements for all `IDisposable` objects: Bitmaps, Graphics, Pens, Fonts, InferenceSession, VideoCapture
- `IObjectDetector : IDisposable` — ensures the ONNX session is cleaned up
- `using var` syntax in `WebcamDetectionLoop` — modern C# disposal pattern

---

## 8. Configuration Reference

### DetectionOptions Properties (v2.0)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ModelPath` | `string` | `"detector_v4.onnx"` | Path to the ONNX model file |
| `ClassLabels` | `string[]` | `["bottle", "soap", "soap-cover"]` | Human-readable names for each class |
| `ModelWidth` | `int` | `640` | Model input width (must match model) |
| `ModelHeight` | `int` | `640` | Model input height (must match model) |
| `ConfidenceThreshold` | `float` | `0.45f` | Minimum confidence to keep a detection |
| `IouThreshold` | `float` | `0.45f` | Maximum IoU before NMS removes a box |
| `CameraIndex` | `int` | `0` | Which webcam to use (0 = first) |
| `WindowTitle` | `string` | `"Object Detection (ESC to exit)"` | OpenCV window title |
| `DefaultImagePath` | `string` | `"sample.jpg"` | Fallback image path |
| `OutputFileName` | `string` | `"output.jpg"` | Name of the annotated output |
| `AutoOpenOutput` | `bool` | `true` | Auto-open output image after detection |

### Tuning Guide

| Goal | Change | Effect |
|------|--------|--------|
| Detect more objects | Lower `ConfidenceThreshold` to 0.10 | More detections, but more false positives |
| Fewer false positives | Raise `ConfidenceThreshold` to 0.50 | Fewer detections, but might miss objects |
| Remove overlapping boxes | Lower `IouThreshold` to 0.30 | More aggressive NMS |
| Keep overlapping boxes | Raise `IouThreshold` to 0.70 | Less aggressive NMS |
| Use a different camera | Change `CameraIndex` to 1, 2, etc. | Opens a different webcam |
| Faster detection | Use a smaller model (e.g., `yolo11n.onnx`) | Lower accuracy, higher FPS |
| Better accuracy | Use a larger model (e.g., `yolo11s.onnx`) | Higher accuracy, lower FPS |

---

## 9. Dependencies & Their Roles

### NuGet Packages

| Package | Version | What It Provides | Used By |
|---------|---------|-----------------|---------|
| `Microsoft.ML.OnnxRuntime` | 1.24.3 | ONNX model inference engine | `OnnxObjectDetector` |
| `OpenCvSharp4` | 4.13.0 | OpenCV bindings for .NET | `WebcamDetectionLoop` |
| `OpenCvSharp4.Extensions` | 4.13.0 | Bitmap ↔ Mat conversion | `WebcamDetectionLoop` |
| `OpenCvSharp4.runtime.win` | 4.13.0 | Native OpenCV binaries for Windows | `WebcamDetectionLoop` |
| `System.Drawing.Common` | 10.0.5 | GDI+ for image manipulation | `DetectionOverlayRenderer`, `ImagePreprocessor` |

### What Each Dependency Does

**ONNX Runtime:** The engine that runs the neural network. It loads the `.onnx` file (a serialized model) and executes it on CPU. The `InferenceSession` class is the main entry point — you give it input tensors, it returns output tensors.

**OpenCvSharp4:** A .NET wrapper around OpenCV (the most popular computer vision library). It provides:
- `VideoCapture` — webcam access
- `Mat` — efficient image container
- `Cv2.ImShow()` — image display window
- `Cv2.WaitKey()` — keyboard input

**System.Drawing.Common:** Microsoft's GDI+ wrapper. It provides:
- `Bitmap` — image container
- `Graphics` — drawing context
- `Pen`, `Font`, `Brush` — drawing tools

---

## 10. How to Build, Run & Deploy

### Prerequisites

- .NET 8 SDK (or later)
- Windows 10/11 (for OpenCV native runtime and GDI+)
- A webcam (for live mode)
- An ONNX model file (`.onnx`)

### Build

```bash
dotnet build
```

### Run (Development)

```bash
dotnet run
```

### Run (With Specific Model)

```bash
# The app will prompt you to browse for a model at startup
dotnet run
# Select [1] Browse my device for ONNX model
```

### Publish (Self-Contained)

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

Output: `bin/Release/net8.0-windows/win-x64/publish/`

The published folder contains:
- `modelDetector.exe` — the application
- `soap_v7.onnx` — the default model (copied by csproj rule)
- `sample.jpg` — the default image (copied by csproj rule)
- All native dependencies (OpenCV runtime, ONNX runtime)

### Run (Published)

```bash
cd bin/Release/net8.0-windows/win-x64/publish/
.\modelDetector.exe
```

---

## 11. Extending the Project

### Add a New Detection Backend (e.g., TensorRT)

1. Create `Infrastructure/Detection/TensorRtObjectDetector.cs`
2. Implement `IObjectDetector` interface
3. In `Program.cs`, change line 25:
   ```csharp
   using IObjectDetector detector = new TensorRtObjectDetector(options);
   ```
4. That's it. Zero changes to other layers.

### Add a New UI (e.g., WPF)

1. Create `Presentation/Wpf/WpfUserInterface.cs`
2. Implement `IUserInterface` interface
3. In `Program.cs`, change line 18:
   ```csharp
   IUserInterface userInterface = new WpfUserInterface();
   ```
4. Remove `[STAThread]` if not needed (WPF needs it too, so keep it).

### Add a New Use Case (e.g., Batch Image Processing)

1. Create `Application/UseCases/BatchImageDetectionUseCase.cs`
2. Inject `IObjectDetector`, `IImageRenderer`, `IProjectPathProvider`
3. Loop through images, run detection, collect reports
4. Wire it up in `Program.cs` and add a menu option in `ConsoleUserInterface`.

### Change the Rendering Style

1. Create `Infrastructure/Rendering/ColoredOverlayRenderer.cs`
2. Implement `IImageRenderer` with different colors/styles
3. In `Program.cs`, change line 27:
   ```csharp
   IImageRenderer imageRenderer = new ColoredOverlayRenderer(options);
   ```

### Add Confidence-Based Filtering

In `DetectImageFromFileUseCase.cs`, after line 74:
```csharp
detections = detections.Where(d => d.Confidence > 0.50).ToList();
```

---

## 12. Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| "Unable to open webcam" | Camera in use by another app | Close other apps using the camera, or change `CameraIndex` |
| "Model file not found" | `.onnx` file not in output directory | Copy the model to the project root or use the browse option |
| "Class count mismatch" | Wrong number of class labels for the model | Ensure `ClassLabels.Length` matches the model's output classes |
| Slow detection | Large model or slow CPU | Use a smaller model (nano variant), or lower the input resolution |
| No detections | Confidence threshold too high | Lower `ConfidenceThreshold` to 0.10 or 0.05 |
| Too many duplicate boxes | IoU threshold too high | Lower `IouThreshold` to 0.30 |
| Output image not opening | `AutoOpenOutput` issue or no default viewer | Set `AutoOpenOutput = false`, or check file associations |
| Build errors with OpenCV | Native runtime not restored | Run `dotnet restore`, ensure `OpenCvSharp4.runtime.win` is referenced |
| `System.Drawing` errors on Linux | GDI+ is Windows-only | This project is Windows-only by design (`net8.0-windows`) |

---

## Appendix A: File Tree

```
DeepLearning/
├── Program.cs                          # Composition root
├── DeepLearning.csproj                 # Project configuration
├── DeepLearning.sln                    # Visual Studio solution
├── .gitignore                          # Git ignore rules
├── README.md                           # Main documentation
│
├── Domain/
│   ├── Entities/
│   │   └── DetectionResult.cs          # Core detection entity
│   └── Enums/
│       └── InputSource.cs              # Input mode enum
│
├── Application/
│   ├── Abstractions/
│   │   ├── IObjectDetector.cs          # Detection contract
│   │   ├── IImageRenderer.cs           # Rendering contract
│   │   ├── IUserInterface.cs           # UI contract
│   │   ├── IWebcamDetectionLoop.cs     # Webcam loop contract
│   │   └── IProjectPathProvider.cs     # Path resolution contract
│   ├── Configuration/
│   │   └── DetectionOptions.cs         # Configuration POCO
│   ├── UseCases/
│   │   ├── RunDetectionApplication.cs  # Main orchestrator
│   │   └── DetectImageFromFileUseCase.cs # Single image workflow
│   └── Models/
│       └── ImageDetectionReport.cs     # Result DTO
│
├── Infrastructure/
│   ├── Detection/
│   │   ├── OnnxObjectDetector.cs       # ONNX detection engine
│   │   ├── ImagePreprocessor.cs        # Image → tensor converter
│   │   └── NmsProcessor.cs             # Non-Maximum Suppression
│   ├── Capture/
│   │   └── WebcamDetectionLoop.cs      # OpenCV webcam loop
│   ├── Rendering/
│   │   └── DetectionOverlayRenderer.cs # GDI+ overlay drawer
│   └── Pathing/
│       └── ProjectPathProvider.cs      # Smart path resolver
│
├── Presentation/
│   └── UI/
│       └── ConsoleUserInterface.cs     # Full console UI
│
├── .vscode/
│   ├── launch.json                     # Debug configuration
│   └── tasks.json                      # Build tasks
│
└── project-structure/
    ├── README.md                       # Architecture reference
    ├── PROFESSIONAL-GUIDE.md           # End-user guide
    ├── JUNIOR-DEVELOPER-CODE-WALKTHROUGH.md # Developer onboarding
    └── PROJECT-TREE.txt                # Quick folder tree
```

---

## Appendix B: YOLO Output Tensor Explained

YOLO models output a tensor that contains all candidate bounding boxes. Understanding this tensor is key to understanding the detection pipeline.

### Tensor Shape

```
YOLOv8:  [1, 4 + classes, 8400]   (channels-first)
YOLOv11: [1, 8400, 4 + classes]   (channels-last)
```

For a 2-class model (soap, soap-cover):
```
[1, 6, 8400] or [1, 8400, 6]
```

### What Each Dimension Means

- **1**: Batch size (always 1 for single-image inference)
- **6**: Per-box data (4 box params + 2 class confidences)
- **8400**: Number of candidate boxes (anchors)

### Per-Box Data Layout

For each of the 8400 candidates:
```
[0] = center X (relative to 640×640 input)
[1] = center Y (relative to 640×640 input)
[2] = width (relative to 640×640 input)
[3] = height (relative to 640×640 input)
[4] = confidence for class 0 (soap)
[5] = confidence for class 1 (soap-cover)
```

### Why 8400 Boxes?

YOLO divides the image into a grid and predicts boxes at multiple scales:
- 80×80 grid × 1 anchor = 6,400 boxes
- 40×40 grid × 1 anchor = 1,600 boxes
- 20×20 grid × 1 anchor = 400 boxes
- **Total: 8,400 boxes**

Most of these 8,400 boxes have near-zero confidence. After thresholding (0.20), you typically get 50-200 candidates. After NMS, you get 1-10 final detections.

---

## Appendix C: Coordinate System

### Model Space vs Image Space

```
Model Space (what the model outputs):
  - All coordinates are relative to 640×640
  - If the model says centerX=320, it means the center is at pixel 320 of the 640px input

Image Space (what we need for drawing):
  - Coordinates must match the original image size
  - If the original image is 1920×1080:
    scaleX = 1920 / 640 = 3.0
    scaleY = 1080 / 640 = 1.6875
    pixelX = modelX × scaleX
```

### Center Format vs Corner Format

```
Model outputs boxes in center format:
  (centerX, centerY, width, height)

Drawing requires corner format:
  (X1, Y1, X2, Y2)

Conversion:
  X1 = centerX - width/2
  Y1 = centerY - height/2
  X2 = centerX + width/2
  Y2 = centerY + height/2
```

---

*This documentation covers every aspect of the modelDetector project. For questions about specific code sections, refer to Section 4 (Every File Explained — Line by Line) which breaks down each file's purpose, structure, and behavior.*
