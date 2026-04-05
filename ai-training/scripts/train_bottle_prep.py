import os
import shutil
import cv2
import random

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)

SOURCE_IMAGES = os.path.join(ROOT, "images", "bottles-to-train")
SOURCE_LABELS = os.path.join(ROOT, "images-values", "yolo-battles-values")

# We will use the soap images as negative examples (background) to teach YOLO what is NOT a bottle
NEGATIVE_IMAGES = os.path.join(ROOT, "images", "soaps-to-train")

OUT_DIR = os.path.join(ROOT, "bottle-dataset")
OUT_IMG_TRAIN = os.path.join(OUT_DIR, "images", "train")
OUT_IMG_VAL = os.path.join(OUT_DIR, "images", "val")
OUT_LBL_TRAIN = os.path.join(OUT_DIR, "labels", "train")
OUT_LBL_VAL = os.path.join(OUT_DIR, "labels", "val")

def setup_dirs():
    # Recreate the directories to ensure a clean state
    if os.path.exists(OUT_DIR):
        shutil.rmtree(OUT_DIR)
    for d in [OUT_IMG_TRAIN, OUT_IMG_VAL, OUT_LBL_TRAIN, OUT_LBL_VAL]:
        os.makedirs(d, exist_ok=True)

def parse_label(filepath):
    lines = []
    if os.path.exists(filepath):
        with open(filepath, 'r') as f:
            for line in f:
                parts = line.strip().split()
                if len(parts) >= 5:
                    lines.append([int(parts[0])] + [float(x) for x in parts[1:]])
    return lines

def save_label(filepath, boxes):
    with open(filepath, 'w') as f:
        for box in boxes:
            f.write(f"0 {box[1]:.6f} {box[2]:.6f} {box[3]:.6f} {box[4]:.6f}\n")

def flip_h(img, boxes):
    img_f = cv2.flip(img, 1)
    new_boxes = []
    for b in boxes:
        new_boxes.append([b[0], 1.0 - b[1], b[2], b[3], b[4]])
    return img_f, new_boxes

def add_blur(img):
    return cv2.GaussianBlur(img, (5, 5), 0)

def main():
    setup_dirs()
    
    # --- 1. Process Positive Images (Bottles) ---
    files = [f for f in os.listdir(SOURCE_IMAGES) if f.lower().endswith(('.jpg', '.jpeg', '.png'))]
    random.shuffle(files)
    
    val_split = 0.2
    val_count = int(len(files) * val_split)
    val_files = set(files[:val_count])
    
    print(f"Total positive files: {len(files)}")
    
    train_count = 0
    val_count = 0
    
    for filename in files:
        base = os.path.splitext(filename)[0]
        img_path = os.path.join(SOURCE_IMAGES, filename)
        lbl_path = os.path.join(SOURCE_LABELS, f"{base}.txt")
        
        if not os.path.exists(lbl_path):
            continue
            
        img = cv2.imread(img_path)
        if img is None:
            continue
            
        boxes = parse_label(lbl_path)
        if not boxes:
            continue
            
        is_val = filename in val_files
        
        if is_val:
            cv2.imwrite(os.path.join(OUT_IMG_VAL, filename), img)
            save_label(os.path.join(OUT_LBL_VAL, f"{base}.txt"), boxes)
            val_count += 1
        else:
            # Original
            cv2.imwrite(os.path.join(OUT_IMG_TRAIN, filename), img)
            save_label(os.path.join(OUT_LBL_TRAIN, f"{base}.txt"), boxes)
            train_count += 1
            
            # Augment 1: Horizontal Flip (Vertical flip removed as it breaks gravity constraints for bottles)
            img_h, boxes_h = flip_h(img, boxes)
            cv2.imwrite(os.path.join(OUT_IMG_TRAIN, f"{base}_h.jpg"), img_h)
            save_label(os.path.join(OUT_LBL_TRAIN, f"{base}_h.txt"), boxes_h)
            train_count += 1
            
            # Augment 2: Blur 
            img_b = add_blur(img)
            cv2.imwrite(os.path.join(OUT_IMG_TRAIN, f"{base}_b.jpg"), img_b)
            save_label(os.path.join(OUT_LBL_TRAIN, f"{base}_b.txt"), boxes)
            train_count += 1
            
            # Augment 3: Blur + Horizontal Flip
            img_hb = add_blur(img_h)
            cv2.imwrite(os.path.join(OUT_IMG_TRAIN, f"{base}_hb.jpg"), img_hb)
            save_label(os.path.join(OUT_LBL_TRAIN, f"{base}_hb.txt"), boxes_h)
            train_count += 1

    # --- 2. Process Negative Images (Soaps & Backgrounds) ---
    # These images teach YOLO what a bottle is NOT. We add them without labels.
    if os.path.exists(NEGATIVE_IMAGES):
        neg_files = [f for f in os.listdir(NEGATIVE_IMAGES) if f.lower().endswith(('.jpg', '.jpeg', '.png'))]
        random.shuffle(neg_files)
        
        neg_val_count = int(len(neg_files) * 0.2)
        neg_val_files = set(neg_files[:neg_val_count])
        
        print(f"Total negative files: {len(neg_files)}")
        
        for filename in neg_files:
            img_path = os.path.join(NEGATIVE_IMAGES, filename)
            img = cv2.imread(img_path)
            if img is None:
                continue
                
            is_val = filename in neg_val_files
            
            # For negative images, we just copy the image and create an EMPTY label file
            base = os.path.splitext(filename)[0]
            if is_val:
                cv2.imwrite(os.path.join(OUT_IMG_VAL, f"neg_{filename}"), img)
                open(os.path.join(OUT_LBL_VAL, f"neg_{base}.txt"), 'w').close()
                val_count += 1
            else:
                cv2.imwrite(os.path.join(OUT_IMG_TRAIN, f"neg_{filename}"), img)
                open(os.path.join(OUT_LBL_TRAIN, f"neg_{base}.txt"), 'w').close()
                train_count += 1
                
                # Add horizontal flip for negatives too to balance the dataset
                img_h = cv2.flip(img, 1)
                cv2.imwrite(os.path.join(OUT_IMG_TRAIN, f"neg_{base}_h.jpg"), img_h)
                open(os.path.join(OUT_LBL_TRAIN, f"neg_{base}_h.txt"), 'w').close()
                train_count += 1

    print(f"Total training images (pos + neg + aug): {train_count}")
    print(f"Total validation images: {val_count}")
    
    # Create bottle.yaml
    yaml_content = f"""path: {OUT_DIR}
train: images/train
val: images/val

names:
  0: bottle
"""
    with open(os.path.join(OUT_DIR, "bottle.yaml"), "w") as f:
        f.write(yaml_content)
    print("Created bottle.yaml")

if __name__ == "__main__":
    main()
