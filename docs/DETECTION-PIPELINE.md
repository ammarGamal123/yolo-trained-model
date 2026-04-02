# Detection Pipeline — Backend Documentation

> Complete reference for the YOLO object detection backend pipeline, from raw image input to annotated output.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Pipeline Stages](#2-pipeline-stages)
3. [ONNX Model Loading](#3-onnx-model-loading)
4. [Image Preprocessing](#4-image-preprocessing)
5. [Model Inference](#5-model-inference)
6. [Output Parsing](#6-output-parsing)
7. [Non-Maximum Suppression](#7-non-maximum-suppression)
8. [Post-Processing Filters](#8-post-processing-filters)
9. [Rendering](#9-rendering)
10. [Configuration Reference](#10-configuration-reference)
11. [Model Versions & History](#11-model-versions--history)
12. [Performance Tuning](#12-performance-tuning)

---

## 1. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                    DETECTION PIPELINE                                │
│                                                                     │
│  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐        │
│  │  Input    │──▶│  Pre-    │──▶│  ONNX    │──▶│  Parse   │        │
│  │  Image    │   │  process │   │  Infer   │   │  Output  │        │
│  └──────────┘   └──────────┘   └──────────┘   └────┬─────┘        │
│                                                     │               │
│                                                     ▼               │
│  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐        │
│  │  Output   │◀──│  Render  │◀──│  Post-   │◀──│   NMS    │        │
│  │  Image    │   │  Boxes   │   │  Filter  │   │  Remove  │        │
│  └──────────┘   └──────────┘   └──────────┘   │  Dups    │        │
│                                                └──────────┘        │
└─────────────────────────────────────────────────────────────────────┘
```

### File Responsibility Map

| Stage | File | Class/Method |
|---|---|---|
| Configuration | `Application/Configuration/DetectionOptions.cs` | `DetectionOptions` |
| Preprocessing | `Infrastructure/Detection/ImagePreprocessor.cs` | `ToChwArray()` |
| Inference | `Infrastructure/Detection/OnnxObjectDetector.cs` | `Detect()` |
| Output Parsing | `Infrastructure/Detection/OnnxObjectDetector.cs` | `ParseDetections()` |
| NMS | `Infrastructure/Detection/NmsProcessor.cs` | `Apply()` |
| Rendering | `Infrastructure/Rendering/DetectionOverlayRenderer.cs` | `DrawDetections()` |
| Orchestration | `Application/UseCases/DetectImageFromFileUseCase.cs` | `Execute()` |

---

## 2. Pipeline Stages

### Stage 1: Input Image

The pipeline accepts any `System.Drawing.Bitmap` — webcam frames, file-loaded images, or generated images. No size restrictions.

### Stage 2: Preprocessing

Converts the bitmap to a CHW-normalized float tensor:
- Resize to 640×640 (bilinear interpolation)
- Separate R, G, B channels into three planes (CHW format)
- Normalize pixel values from 0-255 to 0.0-1.0

### Stage 3: ONNX Inference

Runs the YOLO model via ONNX Runtime `InferenceSession`:
- Input: `[1, 3, 640, 640]` float tensor
- Output: `[1, C, N]` or `[1, N, C]` float tensor
  - C = 4 + num_classes (box params + class scores)
  - N = 8400 (candidate boxes for YOLOv11)

### Stage 4: Output Parsing

Iterates through all 8400 candidate boxes:
1. Extract center-x, center-y, width, height
2. Extract class confidence scores
3. Select class with highest confidence
4. Filter by confidence threshold
5. Scale coordinates from model space (640×640) to original image space
6. Convert from center-format to corner-format (X1, Y1, X2, Y2)

### Stage 5: Non-Maximum Suppression

Removes overlapping duplicate boxes:
1. Group detections by class
2. Sort each group by confidence (descending)
3. Keep highest-confidence box
4. Remove boxes with IoU > threshold with the kept box
5. Repeat until no candidates remain

### Stage 6: Post-Processing

Applies additional filters:
- Minimum box area filter (removes tiny noise boxes)
- Coordinate clipping (ensures boxes stay within image bounds)
- Degenerate box removal (zero or negative area)

### Stage 7: Rendering

Draws bounding boxes and labels on a copy of the input image:
- DeepSkyBlue rectangle (2px width)
- Label with class name and confidence score
- Semi-transparent dark background for label readability

---

## 3. ONNX Model Loading

### InferenceSession Creation

```csharp
_session = new InferenceSession(_options.ModelPath);
_inputName = _session.InputMetadata.Keys.First();
```

The `InferenceSession` loads the entire model into memory. The input name is extracted from the model metadata (typically `"images"` for YOLO models).

### Model File Requirements

- Format: ONNX (opset 17+)
- Input shape: `[1, 3, 640, 640]` (batch, channels, height, width)
- Output shape: `[1, C, N]` or `[1, N, C]` where C = 4 + classes
- Supported models: YOLOv8, YOLOv11, custom-trained variants

---

## 4. Image Preprocessing

### CHW Conversion

The `ImagePreprocessor.ToChwArray()` method converts a Bitmap to a flat float array:

```csharp
// For a 640×640 image:
// Array length = 3 × 640 × 640 = 1,228,800 floats
// [0..409599]   = Red channel plane
// [409600..819199] = Green channel plane
// [819200..1228799] = Blue channel plane
```

### Why CHW Format?

Neural networks expect data in planar (CHW) format, not interleaved (HWC) format:
- **HWC** (normal images): `[R0,G0,B0, R1,G1,B1, ...]` — pixels are contiguous
- **CHW** (tensor input): `[R0,R1,..., G0,G1,..., B0,B1,...]` — channels are contiguous

### Normalization

Each pixel value is divided by 255 to scale from [0, 255] to [0.0, 1.0]. This is required because YOLO models are trained with normalized inputs.

---

## 5. Model Inference

### Tensor Construction

```csharp
var inputTensor = new DenseTensor<float>(chwData, [1, 3, _options.ModelHeight, _options.ModelWidth]);
```

- Batch size: 1 (one image at a time)
- Channels: 3 (RGB)
- Height: 640
- Width: 640

### Running Inference

```csharp
using var results = _session.Run(inputs);
```

ONNX Runtime executes the model graph and returns output tensors. The inference time depends on:
- Model size (nano < small < medium < large)
- CPU/GPU availability
- Input resolution

Typical times: 15-80ms on CPU, 3-10ms on GPU.

---

## 6. Output Parsing

### Output Tensor Layout

YOLO models output a tensor with shape:
- **YOLOv8**: `[1, 4+classes, 8400]` (channels-first)
- **YOLOv11**: `[1, 8400, 4+classes]` (channels-last)

The code auto-detects the layout by comparing dimension sizes:

```csharp
bool isChannelsFirst = dimA < dimB;  // dimA < dimB means [1, C, N]
```

### Per-Box Processing

For each of the 8400 candidate boxes:

```
Box data: [centerX, centerY, width, height, class0_conf, class1_conf, class2_conf]

1. Read box parameters (indices 0-3)
2. Read class confidences (indices 4, 5, 6)
3. Find class with maximum confidence
4. If max confidence < threshold, skip this box
5. Scale coordinates to original image size:
   X1 = (centerX - width/2) × (originalWidth / 640)
   Y1 = (centerY - height/2) × (originalHeight / 640)
   X2 = (centerX + width/2) × (originalWidth / 640)
   Y2 = (centerY + height/2) × (originalHeight / 640)
6. Create DetectionResult with class ID, confidence, and coordinates
```

### Confidence Threshold

The `ConfidenceThreshold` property controls which detections are kept:
- **0.45** (default): Only keeps detections with >45% confidence
- **0.25**: More detections, more false positives
- **0.60**: Fewer detections, fewer false positives

---

## 7. Non-Maximum Suppression

### Why NMS is Needed

YOLO outputs thousands of candidate boxes, many overlapping the same object. Without NMS, a single bottle might have 10-20 overlapping boxes.

### IoU Calculation

```
IoU = Intersection Area / Union Area

  ┌──────────┐
  │  A       │
  │    ┌─────┼─────┐
  │    │█████│     │
  └────┼─────┘     │
       │     B     │
       └───────────┘

  Intersection = ████ area
  Union = A + B - Intersection
  IoU = Intersection / Union
```

### NMS Algorithm

```csharp
foreach (classGroup in detections.GroupBy(d => d.ClassId))
{
    candidates = classGroup.OrderByDescending(d => d.Confidence).ToList();
    
    while (candidates.Count > 0)
    {
        best = candidates[0];           // Keep highest confidence
        keptDetections.Add(best);
        candidates.RemoveAt(0);
        
        // Remove overlapping boxes
        candidates = candidates
            .Where(c => IoU(best, c) < threshold)
            .ToList();
    }
}
```

### IoU Threshold

- **0.45** (default): Removes boxes with >45% overlap
- **0.30**: More aggressive NMS (fewer boxes kept)
- **0.70**: Less aggressive NMS (more boxes kept)

---

## 8. Post-Processing Filters

### Minimum Area Filter

Boxes smaller than 0.05% of the image area are removed:

```csharp
float minArea = image.Width * image.Height * 0.0005f;
filtered = nmsResult.Where(d => d.Area >= minArea).ToList();
```

This prevents tiny noise detections from appearing in the output.

### Coordinate Clipping

All box coordinates are clipped to image bounds:

```csharp
x1 = Math.Max(0, x1);
y1 = Math.Max(0, y1);
x2 = Math.Min(originalWidth, x2);
y2 = Math.Min(originalHeight, y2);
```

### Degenerate Box Removal

Boxes with zero or negative area (x2 <= x1 or y2 <= y1) are discarded.

---

## 9. Rendering

### Drawing Process

1. Create a copy of the source image (don't modify original)
2. For each detection:
   - Draw DeepSkyBlue rectangle (2px) at bounding box
   - Format label: `"{className} {confidence:0.00}"`
   - Measure label text size
   - Draw semi-transparent dark background above box
   - Draw white text on the background

### Color Scheme

| Element | Color | Code |
|---|---|---|
| Bounding box | DeepSkyBlue | `rgb(0, 191, 255)` |
| Label background | Dark semi-transparent | `rgba(15, 23, 42, 200/255)` |
| Label text | White | `rgb(255, 255, 255)` |

---

## 10. Configuration Reference

### DetectionOptions

| Property | Type | Default | Description |
|---|---|---|---|
| `ModelPath` | `string` | `"detector_v4.onnx"` | Path to ONNX model file |
| `ClassLabels` | `string[]` | `["bottle", "soap", "soap-cover"]` | Class names by index |
| `ModelWidth` | `int` | `640` | Model input width |
| `ModelHeight` | `int` | `640` | Model input height |
| `ConfidenceThreshold` | `float` | `0.45f` | Min confidence to keep detection |
| `IouThreshold` | `float` | `0.45f` | Max IoU before NMS removes box |
| `CameraIndex` | `int` | `0` | Webcam index |
| `WindowTitle` | `string` | `"Object Detection (ESC to exit)"` | OpenCV window title |
| `DefaultImagePath` | `string` | `"sample.jpg"` | Fallback image path |
| `OutputFileName` | `string` | `"output.jpg"` | Annotated output filename |
| `AutoOpenOutput` | `bool` | `true` | Auto-open output after detection |

### Tuning Guide

| Goal | Setting | Value |
|---|---|---|
| More detections | Lower `ConfidenceThreshold` | 0.25 |
| Fewer false positives | Raise `ConfidenceThreshold` | 0.55 |
| Aggressive NMS | Lower `IouThreshold` | 0.30 |
| Less aggressive NMS | Raise `IouThreshold` | 0.60 |
| Filter small objects | Increase `MinBoxAreaFraction` | 0.001 |
| Keep small objects | Decrease `MinBoxAreaFraction` | 0.0002 |

---

## 11. Model Versions & History

### detector_v4.onnx (Current — April 2026)

- **Training**: 300 epochs, AdamW, mosaic/mixup augmentation
- **Improvements**: Fixed soap class 0% accuracy, raised confidence threshold
- **Augmentation**: 12x (flips, rotations, brightness, blur, noise, color jitter, contrast)
- **Key parameters**: cls=2.0, label_smoothing=0.1, patience=100

### detector_v3.onnx (Legacy — April 2026)

- **Training**: 83 epochs (early stopped), AdamW, cosine LR
- **Known issues**: Soap class 0% accuracy, high false positive rate
- **Metrics**: mAP50=51.7%, Precision=52.8%, Recall=45.0%
- **Confusion**: 74% of true soap objects predicted as background

### detector_v2.onnx (Legacy — April 2026)

- **Training**: 115 epochs, 6x augmentation, 52 bottle images
- **Scope**: Bottle-only detection
- **Metrics**: mAP50=60.1%, Precision=80.8%, Recall=63.9%

### bottle_v1.onnx (Deprecated — March 2026)

- **Scope**: Single-class bottle detector
- **Status**: Superseded by v2/v3/v4

---

## 12. Performance Tuning

### Inference Speed

| Factor | Impact | Optimization |
|---|---|---|
| Model size | High | Use nano variant (yolo11n) for speed |
| Input resolution | High | Reduce from 640 to 320 for 4x speedup |
| CPU vs GPU | Very High | Use CUDA-enabled ONNX Runtime for GPU |
| Batch size | Medium | Batch multiple images for throughput |

### Memory Usage

| Component | Memory |
|---|---|
| ONNX model (nano) | ~10 MB |
| Input tensor (640×640) | ~5 MB |
| Output tensor (8400×7) | ~0.2 MB |
| Image buffer | ~3-15 MB (depends on resolution) |

### Accuracy vs Speed Trade-offs

| Configuration | Speed | Accuracy | Use Case |
|---|---|---|---|
| yolo11n, 640px, conf=0.45 | ~20ms | Good | Production default |
| yolo11n, 320px, conf=0.30 | ~5ms | Fair | Real-time webcam |
| yolo11s, 640px, conf=0.50 | ~50ms | Better | Batch processing |

---

*Backend Documentation — April 2026*
