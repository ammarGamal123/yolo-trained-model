"""
Step 1: Prepare Dataset (FIXED - Merges Bottle + Soap Labels)
==============================================================
Consolidates raw images and labels into a unified raw-data folder.

IMPORTANT:
  - soaps-images/ contains DIFFERENT images from bottles-images/
  - soaps-images/ has 45 images from March 15 (WhatsApp-Image-2026-03-15-*)
  - bottles-images/ has 56 images from March 30 (WhatsApp Image 2026-03-30*)
  - soaps-extra-images/ has 12 additional images (unlabeled, for testing)

Class mapping in label files:
  yolo-battles-values/: class 0 = bottle
  yolo-soaps-values/:   class 0 = soap-cover, class 1 = soap

Target mapping (unified):
  class 0 = bottle
  class 1 = soap-cover
  class 2 = soap

Usage:
  python scripts/1_prepare_dataset.py
"""

import os
import shutil
import glob

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

# ─── Source paths ───────────────────────────────────────────────
BOTTLE_IMAGES = os.path.join(ROOT, "images", "bottles-to-train")
BOTTLE_LABELS = os.path.join(ROOT, "images-values", "yolo-battles-values")
SOAP_IMAGES   = os.path.join(ROOT, "images", "soaps-to-train")
SOAP_LABELS   = os.path.join(ROOT, "images-values", "yolo-soaps-values")
MIXED_IMAGES  = os.path.join(ROOT, "images", "mixed-object-images")
MIXED_LABELS  = os.path.join(ROOT, "images-values", "yolo-mixed-object-values")

# ─── Output paths ───────────────────────────────────────────────
RAW_IMAGES = os.path.join(ROOT, "raw-data", "images")
RAW_LABELS = os.path.join(ROOT, "raw-data", "labels")

# Clean stale data before writing
if os.path.exists(RAW_IMAGES):
    shutil.rmtree(RAW_IMAGES)
if os.path.exists(RAW_LABELS):
    shutil.rmtree(RAW_LABELS)

os.makedirs(RAW_IMAGES, exist_ok=True)
os.makedirs(RAW_LABELS, exist_ok=True)

# ─── Helper: find image with any extension ──────────────────────
IMAGE_EXTS = [".jpeg", ".jpg", ".png", ".JPEG", ".JPG", ".PNG", ".jfif"]

def find_image(base_name, search_dir):
    """Find an image file by base name, trying all known extensions."""
    for ext in IMAGE_EXTS:
        candidate = os.path.join(search_dir, base_name + ext)
        if os.path.exists(candidate):
            return candidate
    return None


# ─── Helper: read YOLO labels ───────────────────────────────────
def read_labels(path):
    """Read YOLO labels, return list of [cls, cx, cy, w, h]."""
    boxes = []
    if not os.path.exists(path):
        return boxes
    with open(path, "r") as f:
        for line in f:
            parts = line.strip().split()
            if len(parts) >= 5:
                boxes.append([int(parts[0]), float(parts[1]), float(parts[2]),
                              float(parts[3]), float(parts[4])])
    return boxes


# ─── Helper: remap soap label classes ───────────────────────────
def remap_soap(boxes):
    """
    Remap soap label classes to unified mapping.
    Original: 0 = soap-cover (wrapper), 1 = soap (bar)
    Target:   1 = soap-cover, 2 = soap
    """
    remapped = []
    for cls, cx, cy, w, h in boxes:
        if cls == 0:
            new_cls = 1  # soap-cover (wrapper)
        elif cls == 1:
            new_cls = 2  # soap (bar)
        else:
            continue  # skip unknown classes
        remapped.append([new_cls, cx, cy, w, h])
    return remapped


# ─── Helper: write YOLO labels ──────────────────────────────────
def write_labels(path, boxes):
    """Write YOLO labels to file."""
    with open(path, "w") as f:
        for cls, cx, cy, w, h in boxes:
            f.write(f"{cls} {cx:.6f} {cy:.6f} {w:.6f} {h:.6f}\n")


# ─── Process bottle images ──────────────────────────────────────
print("[1/3] Processing bottle images...")
bottle_count = 0
for label_path in glob.glob(os.path.join(BOTTLE_LABELS, "*.txt")):
    base = os.path.splitext(os.path.basename(label_path))[0]
    
    # Find matching image
    img_path = find_image(base, BOTTLE_IMAGES)
    if img_path is None:
        print(f"  WARNING: No image for label {os.path.basename(label_path)}")
        continue
    
    # Read bottle labels
    boxes = read_labels(label_path)
    
    # Filter only bottle class (0)
    bottle_boxes = [box for box in boxes if box[0] == 0]
    
    if not bottle_boxes:
        continue
    
    # Copy image
    dest_name = f"bottle_{base}.jpg"
    shutil.copy2(img_path, os.path.join(RAW_IMAGES, dest_name))
    
    # Write labels (class 0 = bottle, unchanged)
    write_labels(os.path.join(RAW_LABELS, dest_name.replace(".jpg", ".txt")), bottle_boxes)
    bottle_count += 1

print(f"  Bottles: {bottle_count} images processed")


# ─── Process soap images ────────────────────────────────────────
print("\n[2/3] Processing soap images...")
soap_count = 0
for label_path in glob.glob(os.path.join(SOAP_LABELS, "*.txt")):
    base = os.path.splitext(os.path.basename(label_path))[0]
    
    # Find matching image
    img_path = find_image(base, SOAP_IMAGES)
    if img_path is None:
        print(f"  WARNING: No image for label {os.path.basename(label_path)}")
        continue
    
    # Read soap labels
    boxes = read_labels(label_path)
    
    if not boxes:
        continue
    
    # Remap classes: 0->2 (soap-cover), 1->1 (soap)
    remapped = remap_soap(boxes)
    
    if not remapped:
        continue
    
    # Copy image
    dest_name = f"soap_{base}.jpg"
    shutil.copy2(img_path, os.path.join(RAW_IMAGES, dest_name))
    
    # Write remapped labels
    write_labels(os.path.join(RAW_LABELS, dest_name.replace(".jpg", ".txt")), remapped)
    soap_count += 1

print(f"  Soaps: {soap_count} images processed")


# ─── Process mixed images ───────────────────────────────────────
print("\n[3/3] Processing mixed images...")
mixed_count = 0
for label_path in glob.glob(os.path.join(MIXED_LABELS, "*.txt")):
    base = os.path.splitext(os.path.basename(label_path))[0]
    
    # Find matching image
    img_path = find_image(base, MIXED_IMAGES)
    if img_path is None:
        print(f"  WARNING: No image for label {os.path.basename(label_path)}")
        continue
    
    # Read mixed labels (already in unified format: 0=bottle, 1=soap-cover, 2=soap)
    boxes = read_labels(label_path)
    
    if not boxes:
        continue
    
    # Copy image
    dest_name = f"mixed_{base}.jpg"
    shutil.copy2(img_path, os.path.join(RAW_IMAGES, dest_name))
    
    # Write labels (already in correct format)
    write_labels(os.path.join(RAW_LABELS, dest_name.replace(".jpg", ".txt")), boxes)
    mixed_count += 1

print(f"  Mixed: {mixed_count} images processed")


# ─── Summary ────────────────────────────────────────────────────
total_images = len(glob.glob(os.path.join(RAW_IMAGES, "*.*")))
total_labels = len(glob.glob(os.path.join(RAW_LABELS, "*.txt")))

# Count classes
class_counts = {0: 0, 1: 0, 2: 0}
for label_path in glob.glob(os.path.join(RAW_LABELS, "*.txt")):
    for box in read_labels(label_path):
        class_counts[box[0]] = class_counts.get(box[0], 0) + 1

print(f"\n{'='*60}")
print(f"  RAW DATA PREPARED (MERGED BOTTLE + SOAP + MIXED)")
print(f"{'='*60}")
print(f"  Bottle images:   {bottle_count}")
print(f"  Soap images:     {soap_count}")
print(f"  Mixed images:    {mixed_count}")
print(f"  Total images:    {total_images}")
print(f"  Total labels:    {total_labels}")
print(f"")
print(f"  Class distribution:")
print(f"    bottle (0):      {class_counts.get(0, 0)} instances")
print(f"    soap-cover (1):  {class_counts.get(1, 0)} instances")
print(f"    soap (2):        {class_counts.get(2, 0)} instances")
print(f"  Output:        raw-data/")

# Validation: ensure all 3 classes have data
if class_counts.get(0, 0) == 0:
    print(f"\n  [!] WARNING: No bottle instances found!")
if class_counts.get(1, 0) == 0:
    print(f"\n  [!] WARNING: No soap-cover instances found!")
if class_counts.get(2, 0) == 0:
    print(f"\n  [!] WARNING: No soap instances found!")

if class_counts.get(0, 0) > 0 and class_counts.get(1, 0) > 0 and class_counts.get(2, 0) > 0:
    print(f"\n  [OK] All 3 classes have training data — ready for augmentation.")

print(f"{'='*60}")
