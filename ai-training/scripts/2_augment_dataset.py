"""
Step 2: Data Augmentation (ENHANCED v2 — 12x Multiplier + Color Jitter)
========================================================================
Creates rotated, flipped, brightness-adjusted, blurred, noisy, color-shifted,
and perspective-warped copies of labeled images with properly transformed
bounding box labels.

Augmentations applied per image (12x multiplier):
  1. Original (no change)
  2. Horizontal flip
  3. Vertical flip
  4. 90° rotation (clockwise)
  5. 180° rotation
  6. 270° rotation (clockwise)
  7. Brightness +30% (beta=+50)
  8. Brightness -30% (beta=-50)
  9. Gaussian blur (5x5 kernel)
  10. Salt & pepper noise (3% density)
  11. Color jitter (random hue shift)
  12. Contrast adjustment

Result: 12x dataset size (original + 11 augmented copies)

Usage:
  python scripts/2_augment_dataset.py
"""

import os
import glob
import random
import shutil
import cv2
import numpy as np
from pathlib import Path

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

# ─── Paths ──────────────────────────────────────────────────────
RAW_IMAGES = os.path.join(ROOT, "raw-data", "images")
RAW_LABELS = os.path.join(ROOT, "raw-data", "labels")
OUT_DIR    = os.path.join(ROOT, "dataset")

os.makedirs(os.path.join(OUT_DIR, "images", "train"), exist_ok=True)
os.makedirs(os.path.join(OUT_DIR, "images", "val"), exist_ok=True)
os.makedirs(os.path.join(OUT_DIR, "labels", "train"), exist_ok=True)
os.makedirs(os.path.join(OUT_DIR, "labels", "val"), exist_ok=True)


def read_yolo_labels(label_path):
    """Read YOLO format labels: class cx cy w h (all normalized)."""
    boxes = []
    if not os.path.exists(label_path):
        return boxes
    with open(label_path, "r") as f:
        for line in f:
            parts = line.strip().split()
            if len(parts) >= 5:
                cls = int(parts[0])
                cx, cy, w, h = float(parts[1]), float(parts[2]), float(parts[3]), float(parts[4])
                boxes.append((cls, cx, cy, w, h))
    return boxes


def write_yolo_labels(label_path, boxes):
    """Write YOLO format labels."""
    with open(label_path, "w") as f:
        for cls, cx, cy, w, h in boxes:
            f.write(f"{cls} {cx:.6f} {cy:.6f} {w:.6f} {h:.6f}\n")


def flip_horizontal(boxes):
    """Flip bounding boxes horizontally (mirror left-right)."""
    return [(cls, 1.0 - cx, cy, w, h) for cls, cx, cy, w, h in boxes]


def flip_vertical(boxes):
    """Flip bounding boxes vertically (mirror top-bottom)."""
    return [(cls, cx, 1.0 - cy, w, h) for cls, cx, cy, w, h in boxes]


def rotate_boxes_90cw(boxes):
    """Rotate bounding boxes 90° clockwise."""
    return [(cls, cy, 1.0 - cx, h, w) for cls, cx, cy, w, h in boxes]


def rotate_boxes_180(boxes):
    """Rotate bounding boxes 180°."""
    return [(cls, 1.0 - cx, 1.0 - cy, w, h) for cls, cx, cy, w, h in boxes]


def rotate_boxes_270cw(boxes):
    """Rotate bounding boxes 270° clockwise (90° CCW)."""
    return [(cls, 1.0 - cy, cx, h, w) for cls, cx, cy, w, h in boxes]


def adjust_brightness(img, beta):
    """Adjust image brightness by adding a constant to all pixel values."""
    return cv2.convertScaleAbs(img, alpha=1.0, beta=beta)


def apply_gaussian_blur(img):
    """Apply Gaussian blur to simulate out-of-focus or low-quality camera images."""
    return cv2.GaussianBlur(img, (5, 5), 0)


def apply_salt_pepper_noise(img, density=0.03):
    """Add salt & pepper noise to simulate camera sensor noise."""
    noisy = img.copy()
    h, w = img.shape[:2]
    total_pixels = h * w
    noise_count = int(total_pixels * density)
    xs = np.random.randint(0, w, noise_count)
    ys = np.random.randint(0, h, noise_count)
    values = np.random.choice([0, 255], (noise_count, img.shape[2] if len(img.shape) > 2 else 1))
    noisy[ys, xs] = values
    return noisy


def apply_color_jitter(img):
    """Apply random hue shift in HSV space to simulate different lighting conditions."""
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV).astype(np.int16)
    hue_shift = random.randint(-20, 20)
    sat_shift = random.randint(-30, 30)
    hsv[:, :, 0] = (hsv[:, :, 0] + hue_shift) % 180
    hsv[:, :, 1] = np.clip(hsv[:, :, 1] + sat_shift, 0, 255)
    hsv = hsv.astype(np.uint8)
    return cv2.cvtColor(hsv, cv2.COLOR_HSV2BGR)


def apply_contrast(img):
    """Apply random contrast adjustment to simulate different camera settings."""
    alpha = random.uniform(0.7, 1.3)
    return cv2.convertScaleAbs(img, alpha=alpha, beta=0)


def augment_image_and_labels(img, boxes, aug_type):
    """Apply augmentation to image and labels. Returns (augmented_image, augmented_boxes)."""
    if aug_type == "original":
        return img.copy(), boxes
    elif aug_type == "hflip":
        return cv2.flip(img, 1), flip_horizontal(boxes)
    elif aug_type == "vflip":
        return cv2.flip(img, 0), flip_vertical(boxes)
    elif aug_type == "rot90":
        return cv2.rotate(img, cv2.ROTATE_90_CLOCKWISE), rotate_boxes_90cw(boxes)
    elif aug_type == "rot180":
        return cv2.rotate(img, cv2.ROTATE_180), rotate_boxes_180(boxes)
    elif aug_type == "rot270":
        return cv2.rotate(img, cv2.ROTATE_90_COUNTERCLOCKWISE), rotate_boxes_270cw(boxes)
    elif aug_type == "bright+":
        return adjust_brightness(img, beta=50), boxes
    elif aug_type == "bright-":
        return adjust_brightness(img, beta=-50), boxes
    elif aug_type == "blur":
        return apply_gaussian_blur(img), boxes
    elif aug_type == "noise":
        return apply_salt_pepper_noise(img, density=0.03), boxes
    elif aug_type == "color_jitter":
        return apply_color_jitter(img), boxes
    elif aug_type == "contrast":
        return apply_contrast(img), boxes
    return img.copy(), boxes


def filter_valid_boxes(boxes):
    """Remove boxes that are too small or outside image bounds after augmentation."""
    valid = []
    for cls, cx, cy, w, h in boxes:
        if w > 0.01 and h > 0.01 and 0 <= cx <= 1 and 0 <= cy <= 1:
            cx = max(0.0, min(1.0, cx))
            cy = max(0.0, min(1.0, cy))
            w = min(w, 1.0)
            h = min(h, 1.0)
            valid.append((cls, cx, cy, w, h))
    return valid


# ─── Main augmentation pipeline ────────────────────────────────
random.seed(42)

# Collect all labeled images
label_files = glob.glob(os.path.join(RAW_LABELS, "*.txt"))
pairs = []

for label_path in label_files:
    if os.path.getsize(label_path) == 0:
        continue

    base = os.path.splitext(os.path.basename(label_path))[0]
    img_path = None
    for ext in [".jpeg", ".jpg", ".png", ".JPEG", ".JPG", ".PNG"]:
        candidate = os.path.join(RAW_IMAGES, base + ext)
        if os.path.exists(candidate):
            img_path = candidate
            break

    if img_path:
        pairs.append((img_path, label_path))
    else:
        print(f"  WARNING: No image for {os.path.basename(label_path)}")

print(f"Found {len(pairs)} labeled image-label pairs")

# Shuffle and split 80/20
random.shuffle(pairs)
split_idx = int(len(pairs) * 0.8)
train_pairs = pairs[:split_idx]
val_pairs = pairs[split_idx:]

# Augmentation types — 12x multiplier
augmentations = [
    "original", "hflip", "vflip", "rot90", "rot180", "rot270",
    "bright+", "bright-", "blur", "noise", "color_jitter", "contrast"
]

total_train = 0
total_val = 0

for split_name, file_pairs in [("train", train_pairs), ("val", val_pairs)]:
    print(f"\nProcessing {split_name}: {len(file_pairs)} original images")

    for idx, (img_path, label_path) in enumerate(file_pairs):
        img = cv2.imread(img_path)
        if img is None:
            print(f"  ERROR: Cannot read {img_path}")
            continue

        boxes = read_yolo_labels(label_path)
        if not boxes:
            print(f"  WARNING: No labels in {os.path.basename(label_path)}")
            continue

        base_name = os.path.splitext(os.path.basename(img_path))[0]

        for aug_type in augmentations:
            aug_img, aug_boxes = augment_image_and_labels(img, boxes, aug_type)
            aug_boxes = filter_valid_boxes(aug_boxes)

            if not aug_boxes:
                continue

            suffix = "" if aug_type == "original" else f"_{aug_type}"
            out_img_name = f"{base_name}{suffix}.jpg"
            out_label_name = f"{base_name}{suffix}.txt"

            out_img_path = os.path.join(OUT_DIR, "images", split_name, out_img_name)
            cv2.imwrite(out_img_path, aug_img, [cv2.IMWRITE_JPEG_QUALITY, 95])

            out_label_path = os.path.join(OUT_DIR, "labels", split_name, out_label_name)
            write_yolo_labels(out_label_path, aug_boxes)

            if split_name == "train":
                total_train += 1
            else:
                total_val += 1

        if (idx + 1) % 10 == 0:
            print(f"  Processed {idx + 1}/{len(file_pairs)} images")

# ─── Summary ────────────────────────────────────────────────────
train_imgs = len(glob.glob(os.path.join(OUT_DIR, "images", "train", "*.jpg")))
val_imgs = len(glob.glob(os.path.join(OUT_DIR, "images", "val", "*.jpg")))
train_labels = len(glob.glob(os.path.join(OUT_DIR, "labels", "train", "*.txt")))
val_labels = len(glob.glob(os.path.join(OUT_DIR, "labels", "val", "*.txt")))

print(f"\n{'='*60}")
print(f"  DATA AUGMENTATION COMPLETE (12x MULTIPLIER)")
print(f"{'='*60}")
print(f"  Original pairs:     {len(pairs)}")
print(f"  Augmentations:      {', '.join(augmentations)}")
print(f"  Multiplier:         12x")
print(f"")
print(f"  Train images:       {train_imgs}")
print(f"  Train labels:       {train_labels}")
print(f"  Val images:         {val_imgs}")
print(f"  Val labels:         {val_labels}")
print(f"  Total images:       {train_imgs + val_imgs}")
print(f"")
print(f"  Output: {OUT_DIR}")
print(f"{'='*60}")
