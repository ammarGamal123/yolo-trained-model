# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased] — 2026-04-05

### UI — Detection Status Panel Redesign
- **Compact panel**: Shrunk panel width from 380px → 260px, reduced all font sizes and row heights for a sleek, non-intrusive overlay
- **User-friendly labels**: Replaced technical jargon with intuitive names:
  - `FPS` → `Speed` | `AVG FPS` → `Avg Speed` | `INFERENCE` → `AI Response`
  - `OBJECTS` → `Detected` | `FRAMES` → `Scanned` | `SESSION` → `Uptime`
- **Title**: Changed `DETECTION STATUS` → `📊 Live Stats`
- **Glassmorphism**: Increased panel transparency (alpha 230→200) for subtle see-through effect
- **Removed text shadows**: Cleaner single-pass text rendering

### UI — Bounding Box & Label Polish
- **Per-class colors**: Each detected class gets a distinct vibrant color (cyan, pink, green, orange, etc.) instead of uniform DeepSkyBlue
- **Corner accents**: Modern corner brackets on bounding boxes
- **Rounded label badges**: Labels now have rounded backgrounds with a colored top-border accent
- **Percentage confidence**: Confidence shown as `bottle 85%` instead of `bottle 0.85`
- **Anti-aliased rendering**: SmoothingMode and ClearType enabled on bounding box renderer

### Detection — Model Parameters
- **ConfidenceThreshold**: 0.25 → 0.50 — eliminates false positives, shows only high-confidence detections
- **IouThreshold**: 0.45 → 0.40 — tighter NMS removes more duplicate overlapping boxes
- **MinBoxAreaFraction**: 0.0005 → 0.001 — filters out more tiny noise boxes

### Webcam — Quality Maximization
- **Native resolution**: Removed forced 1280×720 downscale; frames displayed at full captured resolution (up to 1920×1080)
- **MJPEG codec**: Set FourCC to MJPEG for higher quality capture than default YUY2
- **Auto-focus & auto-exposure**: Enabled for sharpest possible image
- **30 FPS target**: Explicitly requested from camera
- **Dynamic window size**: Display window now adapts to actual camera resolution

### Files Changed
| File | Change |
|------|--------|
| `Application/Configuration/DetectionOptions.cs` | Confidence 0.50, IOU 0.40 |
| `Infrastructure/Detection/OnnxObjectDetector.cs` | MinBoxAreaFraction 0.001 |
| `Infrastructure/Rendering/DetectionOverlayRenderer.cs` | Per-class colors, corner accents, percentage labels |
| `Infrastructure/Capture/WebcamDetectionLoop.cs` | Compact panel, user-friendly labels, native resolution, MJPEG |
