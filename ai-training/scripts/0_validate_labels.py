"""
Step 0: Validate Labels Against Images
========================================
Verifies that label class IDs are correct by analyzing bounding box positions
and sizes in the raw data. Catches class-swap bugs before training.

Usage:
  python scripts/0_validate_labels.py
"""

import os
import glob
import cv2
import numpy as np

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

SOAP_LABELS = os.path.join(ROOT, "yolo-soaps-values")
SOAP_IMAGES = os.path.join(ROOT, "dataset", "images", "train")
BOTTLE_LABELS = os.path.join(ROOT, "yolo-battles-values")
BOTTLE_IMAGES = os.path.join(ROOT, "dataset", "images", "train")

IMAGE_EXTS = [".jpeg", ".jpg", ".png", ".JPEG", ".JPG", ".PNG"]


def find_image(base_name, search_dir):
    for ext in IMAGE_EXTS:
        candidate = os.path.join(search_dir, base_name + ext)
        if os.path.exists(candidate):
            return candidate
    return None


def analyze_label_file(label_path, image_path):
    """Analyze a label file and return statistics per class."""
    img = cv2.imread(image_path)
    if img is None:
        return None

    h, w = img.shape[:2]
    results = []

    with open(label_path, "r") as f:
        for line in f:
            parts = line.strip().split()
            if len(parts) < 5:
                continue
            cls = int(parts[0])
            cx, cy, bw, bh = float(parts[1]), float(parts[2]), float(parts[3]), float(parts[4])

            # Convert to pixel coordinates
            x1 = (cx - bw / 2) * w
            y1 = (cy - bh / 2) * h
            x2 = (cx + bw / 2) * w
            y2 = (cy + bh / 2) * h
            area = bw * bh

            results.append({
                "class": cls,
                "cx": cx, "cy": cy, "w": bw, "h": bh,
                "area": area,
                "y_center_px": cy * h,
                "x_center_px": cx * w,
                "width_px": bw * w,
                "height_px": bh * h,
            })

    return results


print("=" * 60)
print("  LABEL VALIDATION")
print("=" * 60)

# Analyze soap labels
print("\n[SOAP LABELS ANALYSIS]")
print("-" * 60)

soap_class_stats = {0: [], 1: []}
for label_path in sorted(glob.glob(os.path.join(SOAP_LABELS, "*.txt"))):
    base = os.path.splitext(os.path.basename(label_path))[0]
    img_path = find_image(base, SOAP_IMAGES)
    if img_path is None:
        continue

    boxes = analyze_label_file(label_path, img_path)
    if boxes is None:
        continue

    for box in boxes:
        soap_class_stats[box["class"]].append(box)

for cls_id in [0, 1]:
    stats = soap_class_stats[cls_id]
    if not stats:
        print(f"  Class {cls_id}: No boxes found")
        continue

    areas = [s["area"] for s in stats]
    cy_vals = [s["cy"] for s in stats]
    w_vals = [s["w"] for s in stats]
    h_vals = [s["h"] for s in stats]

    print(f"  Class {cls_id}: {len(stats)} boxes")
    print(f"    Area:   min={min(areas):.4f}, max={max(areas):.4f}, mean={np.mean(areas):.4f}")
    print(f"    Center Y: min={min(cy_vals):.4f}, max={max(cy_vals):.4f}, mean={np.mean(cy_vals):.4f}")
    print(f"    Width:  min={min(w_vals):.4f}, max={max(w_vals):.4f}, mean={np.mean(w_vals):.4f}")
    print(f"    Height: min={min(h_vals):.4f}, max={max(h_vals):.4f}, mean={np.mean(h_vals):.4f}")

# Classify which class is which based on size
class0_mean_area = np.mean([s["area"] for s in soap_class_stats[0]]) if soap_class_stats[0] else 0
class1_mean_area = np.mean([s["area"] for s in soap_class_stats[1]]) if soap_class_stats[1] else 0

print(f"\n  Class 0 mean area: {class0_mean_area:.4f}")
print(f"  Class 1 mean area: {class1_mean_area:.4f}")

if class0_mean_area > class1_mean_area:
    print(f"  -> Class 0 appears to be LARGER (likely soap bar)")
    print(f"  -> Class 1 appears to be SMALLER (likely soap-cover)")
    print(f"  Current mapping in code: class 0 -> soap(1), class 1 -> soap-cover(2)")
    print(f"  If class 0 = soap and class 1 = soap-cover, mapping is CORRECT")
    print(f"  If class 0 = soap-cover and class 1 = soap, mapping is WRONG")
else:
    print(f"  -> Class 0 appears to be SMALLER (likely soap-cover)")
    print(f"  -> Class 1 appears to be LARGER (likely soap bar)")
    print(f"  Current mapping in code: class 0 -> soap(1), class 1 -> soap-cover(2)")
    print(f"  If class 0 = soap-cover and class 1 = soap, mapping is CORRECT")
    print(f"  If class 0 = soap and class 1 = soap-cover, mapping is WRONG")

# Analyze bottle labels
print(f"\n[BOTTLE LABELS ANALYSIS]")
print("-" * 60)
bottle_class_stats = {}
for label_path in sorted(glob.glob(os.path.join(BOTTLE_LABELS, "*.txt"))):
    base = os.path.splitext(os.path.basename(label_path))[0]
    img_path = find_image(base, BOTTLE_IMAGES)
    if img_path is None:
        continue

    boxes = analyze_label_file(label_path, img_path)
    if boxes is None:
        continue

    for box in boxes:
        cls = box["class"]
        if cls not in bottle_class_stats:
            bottle_class_stats[cls] = []
        bottle_class_stats[cls].append(box)

for cls_id in sorted(bottle_class_stats.keys()):
    stats = bottle_class_stats[cls_id]
    areas = [s["area"] for s in stats]
    print(f"  Class {cls_id}: {len(stats)} boxes, mean_area={np.mean(areas):.4f}")

# Summary
print(f"\n{'=' * 60}")
print(f"  VALIDATION SUMMARY")
print(f"{'=' * 60}")
total_soap = sum(len(v) for v in soap_class_stats.values())
total_bottle = sum(len(v) for v in bottle_class_stats.values())
print(f"  Soap labels: {total_soap} boxes across 2 classes")
print(f"  Bottle labels: {total_bottle} boxes")
print(f"")
print(f"  Current unified mapping:")
print(f"    0: bottle")
print(f"    1: soap")
print(f"    2: soap-cover")
print(f"")
print(f"  NOTE: If classes appear swapped, edit scripts/1_prepare_dataset.py")
print(f"        remap_soap() function to flip the mapping.")
print(f"{'=' * 60}")
