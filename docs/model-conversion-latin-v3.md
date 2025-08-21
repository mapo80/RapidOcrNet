# Conversione latin_PP-OCRv3_mobile_rec → ONNX
- Fonte primaria (BOS): `https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/latin_PP-OCRv3_mobile_rec_infer.tar`
- Fallback storico: `https://paddleocr.bj.bcebos.com/PP-OCRv3/multilingual/latin_PP-OCRv3_rec_infer.tar`
- Converter: `paddle2onnx --opset_version 11 --enable_onnx_checker True`
- Output: `models/rec/latin_PP-OCRv3_mobile_rec_infer.onnx` + `.sha256`
- Verifica: `onnx.checker.check_model`
