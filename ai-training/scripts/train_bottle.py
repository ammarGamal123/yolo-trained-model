import os
import sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

DATA_YAML = os.path.join(ROOT, "bottle-dataset", "bottle.yaml")
PRETRAINED = os.path.join(ROOT, "yolo11n.pt")
OUTPUT_DIR = os.path.join(ROOT, "runs", "detect_bottle")

def main():
    from ultralytics import YOLO

    print("=" * 60)
    print("  YOLO TRAINING: BOTTLE ONLY (MAX EPOCHS + AUGMENTED)")
    print("=" * 60)
    
    model = YOLO(PRETRAINED)

    results = model.train(
        data=DATA_YAML,
        epochs=1000,          # Maximum number of epochs
        imgsz=640,
        batch=16,
        workers=0,
        name="train_bottle_v2",
        project=OUTPUT_DIR,
        exist_ok=True,
        patience=50,          # Early stopping to prevent overfitting
        optimizer="AdamW",
        lr0=0.001,
        lrf=0.01,
        cos_lr=True,
        save=True,
        plots=True,
        verbose=True,
        # Reduced mosaic because we have negative images now
        mosaic=0.3,
        mixup=0.0,
        copy_paste=0.0,
        hsv_h=0.015,
        hsv_s=0.5,
        hsv_v=0.3,
        degrees=5.0,
        translate=0.1,
        scale=0.5,
        shear=0.0,
        perspective=0.0,
        # Flips are already done manually, but we can let YOLO do slightly more
        flipud=0.0,
        fliplr=0.5,
        # Loss weights
        cls=1.0,              # Only 1 class, so cls loss is less relevant
        box=7.5,
        dfl=1.5,
        label_smoothing=0.05,
        rect=False,
        close_mosaic=30,
        amp=False,            # Ensure stability
    )

    print("\n" + "=" * 60)
    print("  BOTTLE TRAINING COMPLETE")
    print("=" * 60)

if __name__ == "__main__":
    main()
