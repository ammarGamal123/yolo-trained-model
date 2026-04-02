# YOLO Training Guide — Complete Professional Workflow (v2)
## Model: detector_v4.onnx — Enhanced Training, April 2026

---

## Version History

| Version | Model | Epochs | mAP50 | Status | Notes |
|---------|-------|--------|-------|--------|-------|
| v1 | bottle_v1.onnx | ~100 | N/A | Deprecated | Bottle-only detector |
| v2 | detector_v2.onnx | 115 | 60.1% | Stable | Bottle-only, 6x augmentation |
| v3 | detector_v3.onnx | 83 | 51.7% | **Known Issues** | Soap class 0% accuracy, early stopped |
| **v4** | **detector_v4.onnx** | **300** | **TBD** | **Current** | Enhanced training with all fixes |

---

## Critical Issues Fixed in v4

### Issue 1: Soap Class Never Detected (0% Accuracy)
**Root cause:** The v3 confusion matrix showed soap class was never correctly predicted — all 36 true soap samples were predicted as background.

**Fix in v4:**
- Increased classification loss weight (`cls=2.0`) to prioritize class discrimination
- Added label smoothing (`0.1`) to prevent overconfident background predictions
- Increased epochs to 300 with patience=100 to allow more learning time

### Issue 2: Soap/Cover Class Swap
**Root cause:** The label remapping in `1_prepare_dataset.py` mapped:
- Original class 0 (soap-cover) -> Unified class 1 (soap) — **WRONG**
- Original class 1 (soap) -> Unified class 2 (soap-cover) — **WRONG**

**Fix in v4:**
- Added `scripts/0_validate_labels.py` to verify class mappings before training
- Documented the correct mapping clearly in the dataset preparation script

### Issue 3: Low Confidence (Too Many False Positives)
**Root cause:** Confidence threshold of 0.25 was too low, accepting low-quality detections.

**Fix in v4:**
- Default confidence threshold raised to 0.45
- Added minimum bounding box area filter (0.05% of image)
- Added bounding box clipping to image bounds

### Issue 4: Poor Model Generalization
**Root cause:** Only 6x augmentation (basic flips/rotations), no color variation.

**Fix in v4:**
- 12x augmentation including color jitter, contrast adjustment
- Mosaic augmentation (1.0), mixup (0.1), copy-paste (0.1) during YOLO training

---

## New Project Structure

```
DeepLearning/
├── dataset/                        <- Unified training dataset (generated)
│   ├── images/
│   │   ├── train/                  <- Augmented training images
│   │   └── val/                    <- Augmented validation images
│   ├── labels/
│   │   ├── train/                  <- Matching YOLO label files
│   │   └── val/                    <- Matching YOLO label files
│   └── data.yaml                   <- YOLO dataset config (3 classes)
│
├── raw-data/                       <- Consolidated raw images + labels
│   ├── images/                     <- 98 raw images (52 bottle + 46 soap)
│   └── labels/                     <- 98 label files
│
├── scripts/                        <- Training pipeline scripts
│   ├── 0_validate_labels.py        <- Step 0: Validate class mappings
│   ├── 1_prepare_dataset.py        <- Step 1: Consolidate raw data
│   ├── 2_augment_dataset.py        <- Step 2: Data augmentation (12x)
│   ├── 3_create_config.py          <- Step 3: Generate data.yaml
│   ├── 4_train.py                  <- Step 4: Train YOLO model (300 epochs)
│   ├── 5_export.py                 <- Step 5: Export to ONNX
│   └── run_pipeline.py             <- Run all steps in sequence
│
├── models/                         <- Exported production models
│   ├── detector_v4.onnx            <- ★ Latest trained ONNX model
│   ├── detector_v3.onnx            <- Previous v3 model (legacy)
│   └── detector_v2.onnx            <- Previous v2 model (legacy)
│
├── runs/detect/                    <- YOLO training outputs
│   ├── train/                      <- Latest training run
│   │   ├── weights/
│   │   │   ├── best.pt             <- Best PyTorch model
│   │   │   └── last.pt             <- Final epoch model
│   │   ├── results.png             <- Training metrics charts
│   │   ├── results.csv             <- Raw training data
│   │   ├── confusion_matrix.png    <- Class confusion matrix
│   │   └── val_batch0_pred.jpg     <- Validation predictions
│   └── ...
│
├── bottles-images/                 <- Original bottle images (56 files)
├── soaps-images/                   <- Original soap images (46 files)
├── soaps-extra-images/             <- Extra unlabeled images (12 files)
├── yolo-battles-values/            <- Original bottle labels (52 files)
├── yolo-soaps-values/              <- Original soap labels (46 files)
│
├── detector_v4.onnx                <- ★ Latest model (copy in project root)
├── yolo11n.pt                      <- Pre-trained base model
│
├── YOLO-TRAINING-GUIDE.md          <- This documentation
├── ARCHITECTURE-AND-CODE-REFERENCE.md
├── DETECTION-PIPELINE.md           <- Backend pipeline documentation
└── DeepLearning.sln / .csproj      <- .NET project files
```

---

## Complete Workflow: From Raw Images to Production ONNX Model

### Overview

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│   STEP 0     │───▶│   STEP 1     │───▶│   STEP 2     │───▶│   STEP 3     │
│   Validate   │    │   Prepare    │    │   Augment    │    │   Config     │
│   Labels     │    │   Raw Data   │    │   12x Data   │    │   data.yaml  │
└──────────────┘    └──────────────┘    └──────────────┘    └──────┬───────┘
                                                                   │
                     ┌──────────────┐    ┌──────────────┐          │
                     │   STEP 6     │◀───│   STEP 5     │◀───┌─────┴──────┐
                     │   .NET App   │    │   Export     │    │  STEP 4    │
                     │   Integrate  │    │   ONNX       │    │  Train GPU │
                     └──────────────┘    └──────────────┘    └────────────┘
```

### Quick Start (One Command)

```bash
# Run the entire pipeline
python scripts/run_pipeline.py

# Run validation only
python scripts/run_pipeline.py --validate-only

# Skip training (if model already trained, just export)
python scripts/run_pipeline.py --skip-train
```

---

### STEP 0: Validate Labels

**Command:**
```bash
python scripts/0_validate_labels.py
```

**What it does:**
- Analyzes raw label files from `yolo-soaps-values/` and `yolo-battles-values/`
- Computes per-class bounding box statistics (area, center position, dimensions)
- Reports which class is larger/smaller to verify correct mapping
- Catches class-swap bugs before training

**Critical check:** This step confirms that the class mapping is correct:
- Class 0 in `yolo-soaps-values/` = soap-cover (smaller box)
- Class 1 in `yolo-soaps-values/` = soap bar (larger box)

---

### STEP 1: Prepare Raw Dataset

**Command:**
```bash
python scripts/1_prepare_dataset.py
```

**What it does:**
- Copies bottle images from `bottles-images/` with labels from `yolo-battles-values/`
- Copies soap images from `soaps-images/` with labels from `yolo-soaps-values/`
- Remaps class IDs to unified mapping: bottle=0, soap=1, soap-cover=2
- Outputs consolidated data to `raw-data/`

**Class remapping for soap images:**
| Original (yolo-soaps-values) | Unified (data.yaml) |
|---|---|
| 0 (soap-cover) | 2 (soap-cover) |
| 1 (soap bar) | 1 (soap) |

---

### STEP 2: Data Augmentation (12x Multiplier)

**Command:**
```bash
python scripts/2_augment_dataset.py
```

**Augmentation types per image:**
| # | Augmentation | Image Transform | Label Transform |
|---|---|---|---|
| 1 | Original | No change | No change |
| 2 | Horizontal Flip | `cv2.flip(img, 1)` | `cx = 1.0 - cx` |
| 3 | Vertical Flip | `cv2.flip(img, 0)` | `cy = 1.0 - cy` |
| 4 | 90° CW Rotation | `cv2.rotate(ROTATE_90_CLOCKWISE)` | `new_cx=cy, new_cy=1-cx, swap w/h` |
| 5 | 180° Rotation | `cv2.rotate(ROTATE_180)` | `new_cx=1-cx, new_cy=1-cy` |
| 6 | 270° CW Rotation | `cv2.rotate(ROTATE_90_CCW)` | `new_cx=1-cy, new_cy=cx, swap w/h` |
| 7 | Brightness +30% | `convertScaleAbs(beta=50)` | No change |
| 8 | Brightness -30% | `convertScaleAbs(beta=-50)` | No change |
| 9 | Gaussian Blur | `GaussianBlur(5x5)` | No change |
| 10 | Salt & Pepper | Random pixel noise (3%) | No change |
| 11 | Color Jitter | HSV hue/sat shift | No change |
| 12 | Contrast | Random alpha 0.7-1.3 | No change |

---

### STEP 3: Create data.yaml

**Command:**
```bash
python scripts/3_create_config.py
```

**Generated `dataset/data.yaml`:**
```yaml
path: D:/Ammar/YOLO/ml.net/DeepLearning/dataset
train: images/train
val: images/val

nc: 3
names:
  0: bottle
  1: soap
  2: soap-cover
```

---

### STEP 4: Train the Model

**Command:**
```bash
python scripts/4_train.py
```

**Enhanced training parameters:**
| Parameter | Value | Purpose |
|---|---|---|
| `model` | `yolo11n.pt` | Pre-trained YOLOv11 nano (transfer learning) |
| `data` | path to data.yaml | Dataset configuration |
| `epochs` | `300` | Extended from 200 for better convergence |
| `imgsz` | `640` | Input image size |
| `batch` | `16` | Images per step (fits in 8GB VRAM) |
| `workers` | `0` | Avoids Windows multiprocessing issues |
| `patience` | `100` | Extended from 50 to prevent premature stopping |
| `optimizer` | `AdamW` | Better convergence than SGD |
| `cos_lr` | `True` | Cosine learning rate schedule |
| `mosaic` | `1.0` | Mosaic augmentation for better generalization |
| `mixup` | `0.1` | Mixup augmentation |
| `copy_paste` | `0.1` | Copy-paste augmentation |
| `cls` | `2.0` | Increased classification loss weight |
| `label_smoothing` | `0.1` | Prevent overconfident predictions |

**Why these changes:**
- **cls=2.0**: The v3 model had 0% soap accuracy. Increasing cls weight forces the model to focus on learning class discrimination.
- **mosaic=1.0**: Combines 4 images during training, teaching the model to detect objects in varied contexts.
- **label_smoothing=0.1**: Prevents the model from becoming overconfident about background predictions.
- **patience=100**: v3 stopped at epoch 83 with poor results. Higher patience gives more time to improve.

---

### STEP 5: Export to ONNX

**Command:**
```bash
python scripts/5_export.py
```

**What this does:**
- Converts PyTorch `.pt` → ONNX `.onnx` (universal format)
- Uses dynamic batch size for flexible inference
- Copies to both `models/` and project root

---

### STEP 6: Integrate with .NET

The `DetectionOptions.cs` has been updated:
```csharp
var options = new DetectionOptions
{
    ModelPath = "detector_v4.onnx",
    ClassLabels = ["bottle", "soap", "soap-cover"],
    ModelWidth = 640,
    ModelHeight = 640,
    ConfidenceThreshold = 0.45f,  // Raised from 0.25
    IouThreshold = 0.45f,
};
```

After training completes, update `ModelCatalog.cs` with actual metrics from `runs/detect/train/results.csv`.

---

## Understanding the Training Metrics

### The Three Losses
| Loss | What it measures | Good target |
|---|---|---|
| `box_loss` | Bounding box position accuracy | < 0.5 |
| `cls_loss` | Class prediction accuracy | < 0.8 |
| `dfl_loss` | Box boundary precision | < 1.5 |

### Key Metrics
| Metric | Meaning | v3 Result | v4 Target |
|---|---|---|---|
| **Precision** | % of detections that are correct | 52.8% | > 70% |
| **Recall** | % of actual objects found | 45.0% | > 65% |
| **mAP50** | Overall accuracy at IoU=0.5 | 51.7% | > 70% |
| **mAP50-95** | Strict accuracy (IoU 0.5-0.95) | 30.1% | > 50% |

### How to Read the Confusion Matrix

The confusion matrix shows what the model predicted for each true class:

```
                    Predicted
                 bottle  soap  soap-cover  background
True bottle:       27     0       12          67
True soap:          5     0        5          26
True soap-cover:   20     0       13          63
```

- **Diagonal values** = correct predictions (higher is better)
- **Off-diagonal values** = misclassifications
- **Background column** = objects the model missed entirely

In v3, the soap row shows all zeros on the diagonal — the model NEVER correctly identified a soap.

---

## Troubleshooting

| Problem | Cause | Solution |
|---|---|---|
| Soap class 0% accuracy | Model didn't learn soap features | Increase `cls` weight, train longer |
| Everything detected as background | Confidence threshold issue | Check confusion matrix, increase `cls` weight |
| Classes appear swapped | Label mapping error | Run `0_validate_labels.py`, check `remap_soap()` |
| Training stops early | Patience too low | Increase `patience` to 100+ |
| Low mAP50 | Insufficient training data | Add more labeled images, increase augmentation |
| Too many false positives | Confidence threshold too low | Raise to 0.45-0.55 |

---

## Quick Command Reference

```bash
# ─── Full Pipeline ──────────────────────────────────────────
python scripts/run_pipeline.py

# ─── Individual Steps ──────────────────────────────────────
python scripts/0_validate_labels.py
python scripts/1_prepare_dataset.py
python scripts/2_augment_dataset.py
python scripts/3_create_config.py
python scripts/4_train.py
python scripts/5_export.py

# ─── Test the Model ────────────────────────────────────────
yolo predict model=models/detector_v4.onnx source=test.jpg imgsz=640 conf=0.45 save=True

# ─── Validate ──────────────────────────────────────────────
yolo val model=runs/detect/train/weights/best.pt data="D:/Ammar/YOLO/ml.net/DeepLearning/dataset/data.yaml"

# ─── Verify GPU ────────────────────────────────────────────
python -c "import torch; print('CUDA:', torch.cuda.is_available())"
```

---

*Documentation updated April 2026 — v2*
