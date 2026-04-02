# Soap Detector — Professional Usage Guide

> **Last Updated:** March 2026  
> **Version:** 1.0.0  
> **Platform:** Windows x64  
> **Branch:** `feature/clean-architecture-yolo-detector` (SOAP version — see `feature/bottle-detector` for bottle detection)

---

## Table of Contents

1. [What This App Does](#1-what-this-app-does)
2. [How to Run](#2-how-to-run)
3. [Providing an Image Path](#3-providing-an-image-path)
4. [Understanding the Output](#4-understanding-the-output)
5. [Deployment to Other Machines](#5-deployment-to-other-machines)
6. [Configuration Reference](#6-configuration-reference)
7. [Troubleshooting](#7-troubleshooting)

---

## 1. What This App Does

The Soap Detector is a console application that uses a trained YOLO ONNX model to find **soap** and **soap-cover** objects in images or live webcam video.

**Two modes:**

| Mode | Description |
|---|---|
| **Webcam** | Opens your camera and shows a live video window with detection boxes drawn in real time. Press `ESC` inside the window to stop. |
| **Image File** | Loads a single image from disk, runs detection, prints results in the console, and saves an annotated copy. |

---

## 2. How to Run

### For Development (from source)

```bash
dotnet build
dotnet run
```

### For Deployment (from published folder)

Copy the entire `publish/` folder to the target machine. Double-click `modelDetector.exe` to run it.

---

## 3. Providing an Image Path

When you choose **Image File** mode, the app asks for an image path.

### Option A — Press Enter (use the default)

The default image is `sample.jpg` in the same folder as the exe. This is useful for quick testing.

### Option B — Type a full path

Type the full path to any image on your machine, then press Enter.

**Accepts both Windows backslashes and forward slashes:**

```
D:\Photos\production\soap_batch_01.jpg
D:/Photos/production/soap_batch_01.jpg
C:\Users\John\Desktop\test_image.jpg
```

### Option C — Type a relative path

If you put images in the same folder as the exe, just type the filename:

```
sample.jpg
output_from_camera.png
batch/2024_03_30_photo.jpg
```

### Supported image formats

| Format | Extension | Example |
|---|---|---|
| JPEG | `.jpg`, `.jpeg` | `photo.jpg` |
| PNG | `.png` | `scan.png` |
| Bitmap | `.bmp` | `image.bmp` |
| GIF | `.gif` | `frame.gif` |

---

## 4. Understanding the Output

### Console output

```
  Total detections: 3

  [DETECT]  soap  (2 found, top conf: 72%)
      ██████████████ 72%  at (120, 340, 480, 690)
      ███████████░░ 65%  at (550, 210, 820, 560)

  [DETECT]  soap-cover  (1 found, top conf: 88%)
      ███████████████░ 88%  at (50, 120, 700, 950)
```

**What each part means:**

| Part | Meaning |
|---|---|
| `soap` | The detected class name |
| `3 found` | Total number of that class found in the image |
| `top conf: 72%` | The confidence of the most confident detection of this class |
| `██████████████ 72%` | Visual confidence bar (more blocks = higher confidence) |
| `at (X1, Y1, X2, Y2)` | Bounding box coordinates in pixels: left, top, right, bottom |

### Confidence bar

```
████████████░░░░ 64%
```
Each block represents ~7% confidence. The bar above shows 64% (9 out of 14 blocks filled).

### Annotated output image

The app also saves `output.jpg` next to the exe with all bounding boxes and labels drawn on the image. This file auto-opens by default.

---

## 5. Deployment to Other Machines

### Published output

The `publish/` folder contains everything needed to run the app on any Windows x64 machine — no .NET installation required on the target machine.

```
publish/
├── modelDetector.exe          ← The main application (173 MB, self-contained)
├── modelDetector.pdb          ← Debug symbols (optional, not needed for users)
├── soap_v7.onnx             ← Your trained YOLO model
├── sample.jpg                ← Default sample image for quick testing
├── onnxruntime.lib          ← ONNX Runtime native library
└── onnxruntime_providers_shared.lib
```

### Steps to deploy

1. Copy the entire `publish/` folder to the target Windows machine (via USB, network share, or zip).
2. On the target machine, double-click `modelDetector.exe`.
3. That's it — no installation, no .NET runtime, no admin rights required.

### Things to know about deployment

| Topic | Detail |
|---|---|
| **File size** | The exe is ~173 MB because it bundles the entire .NET 8 runtime. This is normal for self-contained apps. |
| **Model file** | `soap_v7.onnx` must stay in the same folder as the exe. |
| **Sample image** | `sample.jpg` is optional but useful for testing. |
| **Output images** | Saved in the same folder as the exe (`output.jpg`). |
| **Camera** | Webcam mode requires a camera connected to the target machine. |
| **Admin rights** | Usually not required. The app writes output files to its own folder. |

---

## 6. Configuration Reference

All settings live in `Application/Configuration/DetectionOptions.cs` in the source code.

### Main settings to change

| Setting | Default | What it does |
|---|---|---|
| `ModelPath` | `"soap_v7.onnx"` | Change this to use a different ONNX model file. |
| `ClassLabels` | `["soap", "soap-cover"]` | Class names. Must match the order used during training. |
| `ConfidenceThreshold` | `0.15f` | Minimum confidence to show a detection. Range: 0.0–1.0. Lower = more detections (more noise). Higher = fewer detections (more precise). |
| `IouThreshold` | `0.45f` | How much box overlap triggers duplicate removal. Range: 0.0–1.0. |
| `CameraIndex` | `0` | Which camera to use. `0` = default webcam, `1` = second camera, etc. |
| `DefaultImagePath` | `"sample.jpg"` | Default image used when the user presses Enter. |
| `OutputFileName` | `"output.jpg"` | Filename for the annotated output image. |
| `AutoOpenOutput` | `true` | Set to `false` to disable auto-opening the result image. |
| `ModelWidth` / `ModelHeight` | `640` | Input size for the model. Must match the ONNX model. |

### How to change settings permanently

1. Open `Application/Configuration/DetectionOptions.cs` in the source code.
2. Change the default values of the properties.
3. Rebuild and republish.

---

## 7. Troubleshooting

| Symptom | Likely cause | Solution |
|---|---|---|
| "Image file was not found" | Wrong path to the image | Use a full path or put the image in the same folder as the exe. Forward slashes work too: `D:/folder/image.jpg`. |
| "No objects met the confidence threshold" | Image has no soap, or confidence threshold is too high | Lower `ConfidenceThreshold` in `DetectionOptions.cs`, or try a different image. |
| "Unable to open webcam" | No camera, or camera in use by another app | Check camera connection. Close other apps using the camera. Try changing `CameraIndex` to `1`. |
| "ONNX model not found" | Model file missing from the publish folder | Copy `soap_v7.onnx` into the same folder as `modelDetector.exe`. |
| Exe crashes on double-click | Running from a mapped network drive or restricted folder | Copy the `publish/` folder to the local machine (e.g., Desktop or Documents). |
| Output image not saved | `AutoOpenOutput` may have caused a silent error | Check that the exe folder is writable (not in Program Files). |
| Wrong labels on boxes | `ClassLabels` order does not match training | Update the labels in `DetectionOptions.cs` to match the exact training order. |

---

## Quick Reference Card

```
dotnet run                        ← Run from source
publish/modelDetector.exe          ← Run deployed app

Choice 1  → Webcam mode          ← Press ESC to stop
Choice 2  → Image mode            ← Press Enter for default, or type a path

Lower ConfidenceThreshold         ← More detections
Raise ConfidenceThreshold         ← Fewer, more confident detections
```
