import os
import shutil
from ultralytics import YOLO

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

BEST_PT = os.path.join(ROOT, "runs", "detect_bottle", "train_bottle_v2", "weights", "best.pt")
BEST_ONNX = os.path.join(ROOT, "runs", "detect_bottle", "train_bottle_v2", "weights", "best.onnx")
OUT_ONNX = os.path.join(ROOT, "models", "best_bottle.onnx")

def main():
    print("=" * 60)
    print("  EXPORTING BOTTLE MODEL TO ONNX")
    print("=" * 60)

    if not os.path.exists(BEST_PT):
        print(f"\n  ERROR: Trained model not found at {BEST_PT}")
        exit(1)

    model = YOLO(BEST_PT)
    
    print("\n  Exporting... (this may take a minute)")
    model.export(format="onnx", opset=17, imgsz=640, dynamic=True)

    if os.path.exists(BEST_ONNX):
        size_mb = os.path.getsize(BEST_ONNX) / (1024 * 1024)
        print(f"\n  [OK] best.onnx created ({size_mb:.1f} MB)")

        os.makedirs(os.path.dirname(OUT_ONNX), exist_ok=True)
        shutil.copy2(BEST_ONNX, OUT_ONNX)
        
        print(f"\n  Copies saved to:")
        print(f"    {OUT_ONNX}")
    else:
        print(f"\n  ERROR: Expected exported file not found at {BEST_ONNX}")

    print("=" * 60)

if __name__ == "__main__":
    main()
