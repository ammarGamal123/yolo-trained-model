"""
Full Training Pipeline — One Command to Run All Steps
======================================================
Runs the complete training pipeline from data preparation to ONNX export.

Steps:
  0. Validate labels
  1. Prepare dataset (merge bottle + soap data)
  2. Augment dataset (12x multiplier)
  3. Create data.yaml config
  4. Train YOLO model (300 epochs)
  5. Export to ONNX

Usage:
  python scripts/run_pipeline.py
  python scripts/run_pipeline.py --skip-train  (skip training, only export)
  python scripts/run_pipeline.py --validate-only  (only run validation)
"""

import os
import sys
import subprocess
import argparse

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)


def run_step(step_name, script_path, *args):
    """Run a pipeline step and report success/failure."""
    print(f"\n{'='*60}")
    print(f"  RUNNING: {step_name}")
    print(f"{'='*60}")

    result = subprocess.run(
        [sys.executable, script_path, *args],
        cwd=ROOT
    )

    if result.returncode != 0:
        print(f"\n  FAILED: {step_name} (exit code {result.returncode})")
        return False

    print(f"\n  COMPLETED: {step_name}")
    return True


def main():
    parser = argparse.ArgumentParser(description="Full training pipeline")
    parser.add_argument("--skip-train", action="store_true", help="Skip training, only export")
    parser.add_argument("--validate-only", action="store_true", help="Only run label validation")
    args = parser.parse_args()

    print("=" * 60)
    print("  FULL TRAINING PIPELINE v2")
    print("=" * 60)

    steps = [
        ("Step 0: Validate Labels", os.path.join(SCRIPT_DIR, "0_validate_labels.py")),
        ("Step 1: Prepare Dataset", os.path.join(SCRIPT_DIR, "1_prepare_dataset.py")),
        ("Step 2: Augment Dataset", os.path.join(SCRIPT_DIR, "2_augment_dataset.py")),
        ("Step 3: Create Config", os.path.join(SCRIPT_DIR, "3_create_config.py")),
    ]

    if not args.validate_only:
        if not args.skip_train:
            steps.append(("Step 4: Train Model", os.path.join(SCRIPT_DIR, "4_train.py")))
        steps.append(("Step 5: Export to ONNX", os.path.join(SCRIPT_DIR, "5_export.py")))

    for step_name, script_path in steps:
        if not os.path.exists(script_path):
            print(f"\n  WARNING: {script_path} not found, skipping")
            continue

        success = run_step(step_name, script_path)
        if not success:
            print(f"\n  Pipeline stopped at: {step_name}")
            return 1

    print(f"\n{'='*60}")
    print(f"  PIPELINE COMPLETE")
    print(f"{'='*60}")
    print(f"  Next steps:")
    print(f"    1. Check runs/detect/train/results.png for training charts")
    print(f"    2. Check runs/detect/train/confusion_matrix.png for class accuracy")
    print(f"    3. Copy models/detector_v4.onnx to the .NET project root")
    print(f"    4. Update ModelCatalog.cs with actual training metrics")
    print(f"{'='*60}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
