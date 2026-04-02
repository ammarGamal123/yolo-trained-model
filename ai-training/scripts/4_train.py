"""
Step 4: Train YOLO Model (ENHANCED v2 — 300 epochs, AdamW, mosaic, focal loss)
================================================================================
Trains a YOLOv11n model on the augmented dataset.
Uses if __name__ == '__main__' guard for Windows multiprocessing.

Enhanced parameters for multi-class training:
  - epochs: 300 (extended for better convergence)
  - patience: 100 (prevent premature stopping)
  - optimizer: AdamW (better convergence)
  - cos_lr: True (cosine learning rate schedule)
  - mosaic: 1.0 (mosaic augmentation for better generalization)
  - mixup: 0.1 (mixup augmentation)
  - copy_paste: 0.1 (copy-paste augmentation)
  - cls: 2.0 (increased classification loss weight)
  - label_smoothing: 0.1 (prevent overconfident predictions)

Usage:
  python scripts/4_train.py
"""

import os
import sys

# Add project root to path
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

DATA_YAML = os.path.join(ROOT, "dataset", "data.yaml")
PRETRAINED = os.path.join(ROOT, "yolo11n.pt")
OUTPUT_DIR = os.path.join(ROOT, "runs", "detect")


def main():
    from ultralytics import YOLO

    print("=" * 60)
    print("  YOLO TRAINING STARTED (ENHANCED v2)")
    print("=" * 60)
    print(f"  Dataset:   {DATA_YAML}")
    print(f"  Model:     yolo11n.pt (pre-trained)")
    print(f"  Epochs:    300")
    print(f"  Image:     640x640")
    print(f"  Batch:     16")
    print(f"  Optimizer: AdamW")
    print(f"  LR Sched:  Cosine")
    print(f"  Patience:  100")
    print(f"  Mosaic:    1.0")
    print(f"  Mixup:     0.1")
    print(f"  Cls weight: 2.0")
    print(f"  Label smooth: 0.1")
    print(f"  Output:    {OUTPUT_DIR}")
    print("=" * 60)

    # Load pre-trained model
    model = YOLO(PRETRAINED)

    # Train with enhanced parameters
    results = model.train(
        data=DATA_YAML,
        epochs=300,
        imgsz=640,
        batch=16,
        workers=0,
        name="train",
        project=OUTPUT_DIR,
        exist_ok=True,
        patience=100,
        optimizer="AdamW",
        cos_lr=True,
        save=True,
        plots=True,
        verbose=True,
        # Augmentation
        mosaic=1.0,
        mixup=0.1,
        copy_paste=0.1,
        # Loss weights
        cls=2.0,
        # Regularization
        label_smoothing=0.1,
        # Multi-scale training
        rect=False,
    )

    print("\n" + "=" * 60)
    print("  TRAINING COMPLETE")
    print("=" * 60)
    print(f"  Best model: {OUTPUT_DIR}/train/weights/best.pt")
    print(f"  Last model: {OUTPUT_DIR}/train/weights/last.pt")
    print("=" * 60)


if __name__ == "__main__":
    main()
