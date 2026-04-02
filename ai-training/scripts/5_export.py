"""
Step 5: Export to ONNX
======================
Exports the trained PyTorch model to ONNX format for .NET integration.

Usage:
  python scripts/5_export.py
"""

import os
import shutil
from ultralytics import YOLO

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

BEST_PT  = os.path.join(ROOT, "runs", "detect", "train", "weights", "best.pt")
OUT_ONNX = os.path.join(ROOT, "models", "detector_v4.onnx")
ROOT_ONNX = os.path.join(ROOT, "detector_v4.onnx")

print("=" * 60)
print("  EXPORTING MODEL TO ONNX (detector_v4)")
print("=" * 60)

if not os.path.exists(BEST_PT):
    print(f"\n  ERROR: Trained model not found at {BEST_PT}")
    print("  Run training first: python scripts/4_train.py")
    exit(1)

# Load trained model
model = YOLO(BEST_PT)

# Export to ONNX with dynamic batch size
model.export(format="onnx", opset=17, imgsz=640, dynamic=True)

# The export creates best.onnx next to best.pt
exported = BEST_PT.replace(".pt", ".onnx")
if os.path.exists(exported):
    os.makedirs(os.path.dirname(OUT_ONNX), exist_ok=True)
    shutil.copy2(exported, OUT_ONNX)
    shutil.copy2(exported, ROOT_ONNX)
    size_mb = os.path.getsize(OUT_ONNX) / (1024 * 1024)
    print(f"\n  ONNX model saved to:")
    print(f"    models/detector_v4.onnx ({size_mb:.1f} MB)")
    print(f"    detector_v4.onnx (project root copy)")
else:
    print(f"\n  ERROR: Expected exported file not found at {exported}")

print("=" * 60)
