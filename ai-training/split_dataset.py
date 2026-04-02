import os
import shutil
import random

random.seed(42)

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

# ─── Paths ─────────────────────────────────────────────────────
IMAGE_DIR  = os.path.join(ROOT, "dataset", "images", "train")
LABEL_DIR  = os.path.join(ROOT, "yolo-battles-values")
OUTPUT_DIR = os.path.join(ROOT, "bottle-dataset")

# ─── Create folder structure ───────────────────────────────────
for split in ["train", "val"]:
    os.makedirs(os.path.join(OUTPUT_DIR, "images", split), exist_ok=True)
    os.makedirs(os.path.join(OUTPUT_DIR, "labels", split), exist_ok=True)

# ─── Find all matching image-label pairs ───────────────────────
label_files = [f for f in os.listdir(LABEL_DIR) if f.endswith(".txt")]
pairs = []

for label_file in label_files:
    base = label_file.replace(".txt", "")
    # Find matching image (try all common extensions)
    img_path = None
    for ext in [".jpeg", ".jpg", ".png", ".JPEG", ".JPG", ".PNG"]:
        candidate = os.path.join(IMAGE_DIR, base + ext)
        if os.path.exists(candidate):
            img_path = candidate
            break
    if img_path:
        pairs.append((img_path, os.path.join(LABEL_DIR, label_file)))
    else:
        print(f"SKIP (no image): {label_file}")

# ─── Shuffle and split 80/20 ──────────────────────────────────
random.shuffle(pairs)
split_idx = int(len(pairs) * 0.8)
train_pairs = pairs[:split_idx]
val_pairs   = pairs[split_idx:]

def copy_pairs(file_pairs, split):
    for img_path, label_path in file_pairs:
        shutil.copy(img_path,   os.path.join(OUTPUT_DIR, "images", split, os.path.basename(img_path)))
        shutil.copy(label_path, os.path.join(OUTPUT_DIR, "labels", split, os.path.basename(label_path)))

copy_pairs(train_pairs, "train")
copy_pairs(val_pairs,   "val")

# ─── Summary ───────────────────────────────────────────────────
print(f"\n{'='*50}")
print(f"Dataset organized successfully!")
print(f"{'='*50}")
print(f"  Train images: {len(train_pairs)}")
print(f"  Val images:   {len(val_pairs)}")
print(f"  Total:        {len(pairs)}")
print(f"\nOutput: {OUTPUT_DIR}")
print(f"  images/train/  → {len(train_pairs)} images")
print(f"  images/val/    → {len(val_pairs)} images")
print(f"  labels/train/  → {len(train_pairs)} labels")
print(f"  labels/val/    → {len(val_pairs)} labels")
