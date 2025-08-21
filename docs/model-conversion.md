# Model Conversion Guide

This guide describes how to convert the Italian OCR recognition model from PaddlePaddle to ONNX format.

## Prerequisites
- Python and pip
- Paddle2ONNX (`paddle2onnx`)
- PaddlePaddle (CPU version)

The versions used when generating the committed models:

```
Python 3.12.10
paddle2onnx-1.3.1 with python>=3.8, paddlepaddle>=2.0.0
```

## Source Models
- Italian recognition (primary):
  - URL: `https://paddleocr.bj.bcebos.com/dygraph_v2.0/multilingual/it_mobile_v2.0_rec_infer.tar`
  - SHA256: `8db373d73c3fbf6bb0b4db70f927e6c42d6dfd26c4f651204ab9bf7b2d42a143`
- Latin fallback (PP-OCRv3):
  - URL directory: `https://huggingface.co/PaddlePaddle/latin_PP-OCRv3_mobile_rec/resolve/main`

## Conversion Steps

### Linux
```bash
bash scripts/convert_paddle_to_onnx.sh
ls -lh models/rec/*_infer.onnx
```
The script downloads the Paddle model, converts it to ONNX with `--opset_version 11`, and validates the output. Due to `hard_swish`, the converter promotes the model to opset 14 automatically.

### Windows (PowerShell)
```powershell
./scripts/convert_paddle_to_onnx.ps1
Get-ChildItem models/rec/*_infer.onnx
```

### Hash Verification
After conversion, verify file integrity:
```bash
sha256sum models/rec/it_mobile_v2.0_rec_infer.onnx
```
Expected hash: `6a37a6ce533203c1fac840a4da161f9734f90c38f1c65d68e14aed6614310bd5`.
On Windows:
```powershell
(Get-FileHash models/rec/it_mobile_v2.0_rec_infer.onnx -Algorithm SHA256).Hash
```

### Re-running Conversion
Delete the existing ONNX file and rerun the script. The scripts are idempotent; if the ONNX exists they print `SKIP`.

## Notes
- Conversion runs on CPU only.
- Existing detector models are not touched.
- Opset 11 is requested, but the converter may require a higher opset (14 in this case).
