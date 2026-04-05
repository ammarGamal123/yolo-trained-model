"""
Step 5: Export to ONNX (best.onnx + detector_v4.onnx)
=====================================================
Exports the trained PyTorch model to ONNX format for deployment.
Creates best.onnx in the training directory AND copies to other locations.

Usage:
  python scripts/5_export.py
"""

import os
import shutil
from ultralytics import YOLO

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

BEST_PT  = os.path.join(ROOT, "runs", "detect", "train", "weights", "best.pt")
BEST_ONNX = os.path.join(ROOT, "runs", "detect", "train", "weights", "best.onnx")
OUT_ONNX = os.path.join(ROOT, "models", "detector_v4.onnx")
ROOT_ONNX = os.path.join(ROOT, "detector_v4.onnx")

print("=" * 60)
print("  EXPORTING MODEL TO ONNX")
print("=" * 60)

if not os.path.exists(BEST_PT):
    print(f"\n  ERROR: Trained model not found at {BEST_PT}")
    print("  Run training first: python scripts/4_train.py")
    exit(1)

# Load trained model
model = YOLO(BEST_PT)

# Export to ONNX with dynamic batch size
print("\n  Exporting... (this may take a minute)")
model.export(format="onnx", opset=17, imgsz=640, dynamic=True)

# The export creates best.onnx next to best.pt
if os.path.exists(BEST_ONNX):
    size_mb = os.path.getsize(BEST_ONNX) / (1024 * 1024)
    print(f"\n  [OK] best.onnx created ({size_mb:.1f} MB)")
    print(f"    Location: runs/detect/train/weights/best.onnx")

    # Copy to other locations
    os.makedirs(os.path.dirname(OUT_ONNX), exist_ok=True)
    shutil.copy2(BEST_ONNX, OUT_ONNX)
    shutil.copy2(BEST_ONNX, ROOT_ONNX)
    print(f"\n  Copies saved to:")
    print(f"    models/detector_v4.onnx")
    print(f"    detector_v4.onnx (project root)")
else:
    print(f"\n  ERROR: Expected exported file not found at {BEST_ONNX}")

print("=" * 60)
