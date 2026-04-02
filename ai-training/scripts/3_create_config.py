"""
Step 3: Create data.yaml
========================
Generates the YOLO dataset configuration file.

Usage:
  python scripts/3_create_config.py
"""

import os

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)
DATASET_DIR = os.path.join(ROOT, "dataset")

yaml_content = f"""path: {DATASET_DIR.replace(os.sep, '/')}
train: images/train
val: images/val

nc: 3
names:
  0: bottle
  1: soap-cover
  2: soap
"""

yaml_path = os.path.join(DATASET_DIR, "data.yaml")
with open(yaml_path, "w") as f:
    f.write(yaml_content)

print(f"data.yaml created at: {yaml_path}")
print(f"\nContents:")
print(yaml_content)
