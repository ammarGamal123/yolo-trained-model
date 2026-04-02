# Deployment Guide

> How to build, publish, and distribute the modelDetector application.

---

## Quick Deploy

```bash
cd backend
dotnet publish -c Release -r win-x64 --self-contained
```

Output folder: `backend/bin/Release/net8.0-windows/win-x64/publish/`

---

## What's in the Publish Folder

| File | Purpose |
|------|---------|
| `modelDetector.exe` | Application executable |
| `detector_v3.onnx` | Trained detection model |
| `modelDetector.dll` | Application assembly |
| `*.dll` | .NET runtime + dependencies |
| `*.dll` (OpenCV) | Native camera and display libraries |
| `onnxruntime.dll` | ONNX model inference engine |

**Total size:** ~350 MB (self-contained, no .NET SDK required on target machine)

---

## Running the Published App

### Option 1: Double-click
```
publish/modelDetector.bat
```

### Option 2: Command line
```cmd
cd publish
modelDetector.exe
```

---

## Publishing Options

### Self-Contained (Recommended)
Includes .NET runtime — user doesn't need anything installed.
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### Framework-Dependent
Smaller output (~20 MB) but requires .NET 8 Runtime on target.
```bash
dotnet publish -c Release -r win-x64
```

### Single File
Everything bundled into one executable.
```bash
dotnet publish -c Release -r win-x64 --self-contained /p:PublishSingleFile=true
```

---

## Distribution Checklist

- [ ] Build with `dotnet publish -c Release -r win-x64 --self-contained`
- [ ] Verify `detector_v3.onnx` is in the publish folder
- [ ] Test `modelDetector.exe` on a clean machine (no .NET SDK)
- [ ] Test webcam mode (camera access)
- [ ] Test image file mode (file permissions)
- [ ] Test batch/folder mode (folder access)
- [ ] Zip the publish folder for distribution

---

## Updating the Model

When a new model is trained:

1. Copy the new `.onnx` file to the `models/` folder
2. Update `DetectionOptions.cs`:
   ```csharp
   public string ModelPath { get; set; } = "../models/detector_v4.onnx";
   ```
3. Update `DeepLearning.csproj` to copy the new model:
   ```xml
   <None Include="..\models\detector_v4.onnx" Link="models\detector_v4.onnx">
     <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
   </None>
   ```
4. Re-publish: `dotnet publish -c Release -r win-x64 --self-contained`

---

## System Requirements

| Requirement | Minimum |
|---|---|
| OS | Windows 10 (64-bit) or later |
| CPU | x64 (Intel/AMD) |
| RAM | 2 GB |
| Disk Space | 400 MB |
| GPU | Not required (CPU inference) |
| .NET Runtime | Not required (self-contained) |
| Camera | Optional (only for webcam mode) |

---

*v2.0.0 — April 2026*
