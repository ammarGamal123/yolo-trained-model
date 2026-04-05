import os

# Paths
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

DATA_YAML = os.path.join(ROOT, "dataset", "data.yaml")
PRETRAINED = os.path.join(ROOT, "yolo11s.pt")
OUTPUT_DIR = os.path.join(ROOT, "runs", "detect_v5")


def main():
    from ultralytics import YOLO

    print("=" * 60)
    print("  YOLO TRAINING STARTED (MAX PERFORMANCE MODE)")
    print("=" * 60)

    # Load model
    model = YOLO(PRETRAINED if os.path.exists(PRETRAINED) else "yolo11s.pt")

    results = model.train(
        # Core
        data=DATA_YAML,
        epochs=500,
        imgsz=640,                # stable, can increase later
        batch=24,                 # tuned for 8GB VRAM
        device=0,                 # FORCE GPU
        workers=8,                # use CPU cores (important!)
        amp=True,                 # mixed precision (BIG boost)

        # Performance
        cache=True,               # cache dataset in RAM
        cos_lr=True,
        optimizer="AdamW",
        patience=150,

        # Training behavior
        rect=True,                # faster loading, better pipeline
        multi_scale=False,        # keep stable for performance

        # Augmentation (your config preserved)
        mosaic=0.8,
        mixup=0.15,
        copy_paste=0.15,

        hsv_h=0.02,
        hsv_s=0.8,
        hsv_v=0.5,

        degrees=5.0,
        translate=0.1,
        scale=0.5,
        shear=0.0,
        perspective=0.1,

        flipud=0.0,
        fliplr=0.5,

        # Loss tuning
        cls=3.0,
        box=7.5,
        dfl=1.5,

        # Regularization
        label_smoothing=0.05,

        # Mosaic scheduling
        close_mosaic=50,

        # Logging / output
        project=OUTPUT_DIR,
        name="train_v5_optimized",
        exist_ok=True,
        verbose=True,
        plots=True,
        save=True,
    )

    print("\nTraining Complete")
    print(f"Best model: {OUTPUT_DIR}/train_v5_optimized/weights/best.pt")


if __name__ == "__main__":
    main()