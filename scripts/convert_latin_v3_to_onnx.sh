#!/usr/bin/env bash
set -euo pipefail

PRIMARY_URL="${PRIMARY_URL:-https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/latin_PP-OCRv3_mobile_rec_infer.tar}"
FALLBACK_URL="${FALLBACK_URL:-https://paddleocr.bj.bcebos.com/PP-OCRv3/multilingual/latin_PP-OCRv3_rec_infer.tar}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/models/src/latin_v3"
OUT="$ROOT/models/rec"
DOC="$ROOT/docs"
MAN="$ROOT/models/sources.json"

mkdir -p "$SRC" "$OUT" "$DOC" "$ROOT/scripts"
python3 -m pip install --upgrade pip >/dev/null
python3 -m pip install paddlepaddle==2.6.0 paddle2onnx==1.3.1 onnx onnxruntime setuptools >/dev/null
command -v pyenv >/dev/null 2>&1 && pyenv rehash

download_and_extract() {
  local url="$1"
  local tarpath="$SRC/$(basename "$url")"
  echo "[DL] $url"
  curl -L --fail -o "$tarpath" "$url"
  sha256sum "$tarpath" | awk '{print $1}' > "$tarpath.sha256"
  echo "[EXTRACT] $tarpath -> $SRC"
  tar -xf "$tarpath" -C "$SRC" --strip-components=1
}

# Scarica ed estrai (primario → fallback)
if [ ! -f "$SRC/inference.pdmodel" ]; then
  download_and_extract "$PRIMARY_URL" || true
  if [ ! -f "$SRC/inference.pdmodel" ]; then
    echo "[WARN] Primary failed, using fallback..."
    download_and_extract "$FALLBACK_URL"
  fi
fi

DST="$OUT/latin_PP-OCRv3_mobile_rec_infer.onnx"
if [ -f "$DST" ]; then
  echo "[SKIP] ONNX già presente: $DST"
else
  echo "[CONVERT] paddle2onnx -> $DST"
  paddle2onnx \
    --model_dir "$SRC" \
    --model_filename inference.pdmodel \
    --params_filename inference.pdiparams \
    --save_file "$DST" \
    --opset_version 11 \
    --enable_onnx_checker True
  sha256sum "$DST" | awk '{print $1}' > "$DST.sha256"
fi

# Validazione ONNX
python3 - "$DST" <<'PY'
import onnx, sys
p = sys.argv[1]
m = onnx.load(p); onnx.checker.check_model(m)
print("[CHECK] OK:", p)
PY

# Aggiorna manifest JSON (crea se non esiste)
python3 - "$ROOT" <<'PY'
import json, os, subprocess, sys
root = sys.argv[1]
man  = os.path.join(root, 'models', 'sources.json')
outp = os.path.join(root, 'models', 'rec', 'latin_PP-OCRv3_mobile_rec_infer.onnx')
def sha(p):
    import hashlib; h=hashlib.sha256()
    with open(p,'rb') as f:
        for b in iter(lambda:f.read(1<<20), b''): h.update(b)
    return h.hexdigest()
entry = {
  "recognition": {
    "latin_v3": {
      "name": "latin_PP-OCRv3_mobile_rec",
      "primary_url": "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/latin_PP-OCRv3_mobile_rec_infer.tar",
      "fallback_url": "https://paddleocr.bj.bcebos.com/PP-OCRv3/multilingual/latin_PP-OCRv3_rec_infer.tar",
      "onnx": "models/rec/latin_PP-OCRv3_mobile_rec_infer.onnx",
      "onnx_sha256": sha(outp),
      "opset": 11,
      "converter": subprocess.check_output(["paddle2onnx","--version"], text=True).strip()
    }
  }
}
if os.path.exists(man):
    with open(man) as f: data=json.load(f)
else:
    data={}
data.setdefault("recognition",{}).update(entry["recognition"])
with open(man,"w") as f: json.dump(data,f,indent=2)
print("[MANIFEST] updated", man)
PY

# Guida sintetica
cat > "$DOC/model-conversion-latin-v3.md" <<'MD'
# Conversione latin_PP-OCRv3_mobile_rec → ONNX
- Fonte primaria (BOS): `https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/latin_PP-OCRv3_mobile_rec_infer.tar`
- Fallback storico: `https://paddleocr.bj.bcebos.com/PP-OCRv3/multilingual/latin_PP-OCRv3_rec_infer.tar`
- Converter: `paddle2onnx --opset_version 11 --enable_onnx_checker True`
- Output: `models/rec/latin_PP-OCRv3_mobile_rec_infer.onnx` + `.sha256`
- Verifica: `onnx.checker.check_model`
MD

echo "[DONE] -> $DST"
