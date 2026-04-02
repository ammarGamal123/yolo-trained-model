# modelDetector — YOLO Object Detection Console Application

> Real-time object detection for bottles, soaps, and soap covers using YOLO + ONNX Runtime.

---

## Quick Start

```bash
# Build
cd backend
dotnet build DeepLearning.sln -c Release

# Run
dotnet run

# Publish (self-contained)
dotnet publish -c Release -r win-x64 --self-contained
```

---

## What It Does

Detects **3 object classes** in images or webcam video:
- `bottle` — plastic/glass bottles
- `soap` — soap bars
- `soap-cover` — soap wrappers/covers

**Modes:**
| Mode | Description |
|------|-------------|
| **Webcam** | Real-time detection from camera |
| **Image File** | Detect in a single image, save annotated output |
| **Batch/Folder** | Process all images in a folder |

---

## Current Model: detector_v4 (Enhanced)

| Property | Value |
|---|---|
| Model | YOLOv11n fine-tuned |
| Classes | bottle, soap, soap-cover |
| Input | 640×640 |
| Training | 300 epochs, AdamW, 12x augmentation |
| Default threshold | 45% confidence |

---

## Project Structure

```
yolo-trained-model/
├── backend/
│   ├── Program.cs                          # Composition root
│   ├── DeepLearning.csproj                 # .NET 8 project config
│   ├── publish/                            # Deployment scripts
│   │
│   ├── Domain/                             # Core entities
│   │   ├── Entities/DetectionResult.cs     # Detection data model
│   │   └── Enums/InputSource.cs            # Input mode enum
│   │
│   ├── Application/                        # Business logic
│   │   ├── Abstractions/                   # Interfaces
│   │   ├── Configuration/                  # DetectionOptions.cs
│   │   ├── Models/                         # DTOs
│   │   └── UseCases/                       # Workflows
│   │
│   ├── Infrastructure/                     # Implementations
│   │   ├── Detection/                      # ONNX inference + NMS
│   │   ├── Capture/                        # Webcam loop
│   │   ├── Rendering/                      # GDI+ box drawing
│   │   ├── ModelMetadata/                  # Model catalog
│   │   └── Pathing/                        # File path resolution
│   │
│   └── Presentation/                       # User interface
│       └── UI/ConsoleUserInterface.cs      # Console UI
│
├── ai-training/
│   ├── scripts/                            # Training pipeline
│   ├── dataset/                            # Training data
│   ├── raw-data/                           # Raw images
│   └── yolo-battles-values/                # Bottle labels
│
├── models/                                 # ONNX model files
└── docs/                                   # Documentation
```

---

## Configuration

Edit `backend/Application/Configuration/DetectionOptions.cs`:

| Setting | Default | Description |
|---|---|---|
| `ModelPath` | `../models/detector_v3.onnx` | ONNX model file |
| `ConfidenceThreshold` | `0.45` | Min confidence to keep detection |
| `IouThreshold` | `0.45` | Max overlap before NMS removes box |
| `ClassLabels` | `["bottle", "soap", "soap-cover"]` | Class names |

---

## Documentation

| Document | Description |
|---|---|
| [README.md](README.md) | This file — project overview |
| [YOLO-TRAINING-GUIDE.md](YOLO-TRAINING-GUIDE.md) | Complete training workflow |
| [ARCHITECTURE-AND-CODE-REFERENCE.md](ARCHITECTURE-AND-CODE-REFERENCE.md) | Architecture deep-dive |
| [DETECTION-PIPELINE.md](DETECTION-PIPELINE.md) | Backend pipeline reference |

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.ML.OnnxRuntime` | 1.24.3 | ONNX model inference |
| `OpenCvSharp4` | 4.13.0 | Webcam capture + display |
| `OpenCvSharp4.Extensions` | 4.13.0 | Bitmap ↔ Mat conversion |
| `OpenCvSharp4.runtime.win` | 4.13.0 | Native OpenCV binaries |
| `System.Drawing.Common` | 10.0.5 | GDI+ image rendering |

---

## Troubleshooting

| Problem | Solution |
|---|---|
| No detections | Lower `ConfidenceThreshold` to 0.25 |
| Too many false positives | Raise `ConfidenceThreshold` to 0.55 |
| Webcam won't open | Close other camera apps, change `CameraIndex` |
| Model file not found | Copy `.onnx` to models/ folder |
| Classes appear wrong | Check class label order matches training |

---

## Training Your Own Model

```bash
cd ai-training

# Run the full pipeline
python scripts/run_pipeline.py

# Or run individual steps
python scripts/0_validate_labels.py
python scripts/1_prepare_dataset.py
python scripts/2_augment_dataset.py
python scripts/3_create_config.py
python scripts/4_train.py
python scripts/5_export.py
```

See [YOLO-TRAINING-GUIDE.md](YOLO-TRAINING-GUIDE.md) for detailed instructions.

---

*v2.0.0 — April 2026*
