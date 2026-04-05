"""
Step 4c: Retrain YOLO Model (FIXED v6 — Conservative, Proven Settings)
======================================================================
v5 FAILED because:
  - Learning rate too high (0.01) for tiny dataset
  - Classification weight too high (3.0) caused divergence
  - Larger model (9.4M params) with only 124 images = overfitting
  - Model peaked at epoch 19 then got worse for 150 epochs
  - Final mAP50: 0.46% (v4 was 88.0%)

v6 Fixes:
  - Learning rate: 0.01 → 0.001 (10x lower for small dataset)
  - Classification weight: 3.0 → 1.5 (balanced, not aggressive)
  - Augmentation: reduced to gentle levels
  - Patience: 0 (no early stopping, let it train fully)
  - Batch size: 16 (more stable gradients)
  - Model: YOLOv11s (larger, but with conservative settings)

Usage:
  python scripts/4c_retrain_v6.py
"""

import os
import sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

DATA_YAML = os.path.join(ROOT, "dataset", "data.yaml")
PRETRAINED = os.path.join(ROOT, "yolo11s.pt")
OUTPUT_DIR = os.path.join(ROOT, "runs", "detect_v6")


def main():
    from ultralytics import YOLO

    print("=" * 60)
    print("  YOLO TRAINING v6 (CONSERVATIVE — PROVEN SETTINGS)")
    print("=" * 60)
    print(f"  Dataset:   {DATA_YAML}")
    print(f"  Model:     yolo11s.pt (9.4M params)")
    print(f"  Epochs:    300")
    print(f"  Patience:  0 (NO early stopping)")
    print(f"  Image:     640x640")
    print(f"  Batch:     16")
    print(f"  Optimizer: AdamW")
    print(f"  LR:        0.001 (10x LOWER than default)")
    print(f"  LR Sched:  Cosine")
    print(f"  Mosaic:    0.5 (gentle)")
    print(f"  Mixup:     0.05 (minimal)")
    print(f"  Copy-paste: 0.05 (minimal)")
    print(f"  Cls weight: 1.5 (balanced)")
    print(f"  Box weight: 7.5 (default)")
    print(f"  DFL weight: 1.5 (default)")
    print(f"  Label smooth: 0.05")
    print(f"  HSV:       h=0.015, s=0.5, v=0.3 (gentle)")
    print(f"  Degrees:   3.0 (gentle)")
    print(f"  Perspective: 0.05 (gentle)")
    print(f"  Close mosaic: 30")
    print(f"  Output:    {OUTPUT_DIR}")
    print("=" * 60)
    print("")
    print("  WHY THESE SETTINGS:")
    print("  - Low LR prevents divergence on small datasets")
    print("  - No early stopping ensures full training cycle")
    print("  - Gentle augmentation prevents noisy training data")
    print("  - Balanced cls weight prevents over-focusing on classes")
    print("")
    print("  EXPECTED: mAP50 should reach 80%+ (v4 was 88%)")
    print("=" * 60)

    if not os.path.exists(PRETRAINED):
        print(f"\n  Downloading yolo11s.pt...")
        model = YOLO("yolo11s.pt")
    else:
        print(f"\n  Using existing yolo11s.pt")
        model = YOLO(PRETRAINED)

    results = model.train(
        data=DATA_YAML,
        epochs=300,
        imgsz=640,
        batch=16,
        workers=0,
        name="train_v6",
        project=OUTPUT_DIR,
        exist_ok=True,
        patience=0,
        optimizer="AdamW",
        lr0=0.001,
        lrf=0.01,
        cos_lr=True,
        save=True,
        plots=True,
        verbose=True,
        mosaic=0.5,
        mixup=0.05,
        copy_paste=0.05,
        hsv_h=0.015,
        hsv_s=0.5,
        hsv_v=0.3,
        degrees=3.0,
        translate=0.1,
        scale=0.5,
        shear=0.0,
        perspective=0.05,
        flipud=0.0,
        fliplr=0.5,
        cls=1.5,
        box=7.5,
        dfl=1.5,
        label_smoothing=0.05,
        rect=False,
        close_mosaic=30,
        amp=False,
    )

    print("\n" + "=" * 60)
    print("  TRAINING COMPLETE (v6)")
    print("=" * 60)
    print(f"  Best model: {OUTPUT_DIR}/train_v6/weights/best.pt")
    print(f"  Last model: {OUTPUT_DIR}/train_v6/weights/last.pt")
    print("=" * 60)


if __name__ == "__main__":
    main()
