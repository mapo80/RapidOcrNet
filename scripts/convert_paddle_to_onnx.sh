#!/usr/bin/env bash
set -euo pipefail

python3 -m pip install --upgrade pip
python3 -m pip install paddlepaddle==2.6.0 paddle2onnx==1.3.1 onnx onnxruntime setuptools
command -v pyenv >/dev/null 2>&1 && pyenv rehash

python3 --version
paddle2onnx --version

REC_IT_URL="${REC_IT_URL:-https://paddleocr.bj.bcebos.com/dygraph_v2.0/multilingual/it_mobile_v2.0_rec_infer.tar}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/models/src"; OUT="$ROOT/models/rec"
mkdir -p "$SRC" "$OUT"
tarball="$SRC/$(basename "$REC_IT_URL")"
if [ ! -f "$tarball" ]; then
  curl -L --fail -o "$tarball" "$REC_IT_URL"
  sha256sum "$tarball" | awk '{print $1}' > "$tarball.sha256"
fi
work="$SRC/it_mobile_v2.0_rec_infer"
[ -d "$work" ] || mkdir -p "$work" && tar -xf "$tarball" -C "$work"

dst="$OUT/it_mobile_v2.0_rec_infer.onnx"
if [ ! -f "$dst" ]; then
  paddle2onnx \
    --model_dir "$work" \
    --model_filename inference.pdmodel \
    --params_filename inference.pdiparams \
    --save_file "$dst" \
    --opset_version 11 \
    --enable_onnx_checker True
  sha256sum "$dst" | awk '{print $1}' > "$dst.sha256"
else
  echo "[SKIP] $dst già presente"
fi

if [ ! -f "$dst" ]; then
  echo "[INFO] Uso fallback latin_PP-OCRv3"
  BASE="${REC_LATIN_DIR:-https://huggingface.co/PaddlePaddle/latin_PP-OCRv3_mobile_rec/resolve/main}"
  WF="$SRC/latin_ppocrv3"; mkdir -p "$WF"
  for f in inference.pdmodel inference.pdiparams inference.yml; do
    [ -f "$WF/$f" ] || curl -L --fail -o "$WF/$f" "$BASE/$f"
  done
  dst_fb="$OUT/latin_PP-OCRv3_mobile_rec_infer.onnx"
  paddle2onnx \
    --model_dir "$WF" \
    --model_filename inference.pdmodel \
    --params_filename inference.pdiparams \
    --save_file "$dst_fb" \
    --opset_version 11 \
    --enable_onnx_checker True
  sha256sum "$dst_fb" | awk '{print $1}' > "$dst_fb.sha256"
fi

python3 - <<'PY' "$ROOT"
import sys, os, json, subprocess
root = sys.argv[1]
json_path = os.path.join(root, 'models', 'sources.json')
data = {
  "recognition": {
    "primary": {
      "name": "it_mobile_v2.0_rec_infer",
      "url": "https://paddleocr.bj.bcebos.com/dygraph_v2.0/multilingual/it_mobile_v2.0_rec_infer.tar",
      "tar_sha256": "",
      "onnx": "models/rec/it_mobile_v2.0_rec_infer.onnx",
      "onnx_sha256": "",
      "opset": 11,
      "converter": ""
    },
    "fallback": {
      "name": "latin_PP-OCRv3_mobile_rec",
      "url_dir": "https://huggingface.co/PaddlePaddle/latin_PP-OCRv3_mobile_rec/resolve/main",
      "onnx": "models/rec/latin_PP-OCRv3_mobile_rec_infer.onnx",
      "onnx_sha256": "",
      "opset": 11
    }
  }
}

tarball = os.path.join(root, 'models', 'src', os.path.basename(data['recognition']['primary']['url']))
sha_path = tarball + '.sha256'
if os.path.exists(sha_path):
    data['recognition']['primary']['tar_sha256'] = open(sha_path).read().strip()

prim_sha_path = os.path.join(root, data['recognition']['primary']['onnx'] + '.sha256')
if os.path.exists(prim_sha_path):
    data['recognition']['primary']['onnx_sha256'] = open(prim_sha_path).read().strip()
    try:
        import onnx
        m = onnx.load(os.path.join(root, data['recognition']['primary']['onnx']))
        data['recognition']['primary']['opset'] = m.opset_import[0].version
    except Exception:
        pass

fb_sha_path = os.path.join(root, data['recognition']['fallback']['onnx'] + '.sha256')
if os.path.exists(fb_sha_path):
    data['recognition']['fallback']['onnx_sha256'] = open(fb_sha_path).read().strip()
    try:
        import onnx
        m = onnx.load(os.path.join(root, data['recognition']['fallback']['onnx']))
        data['recognition']['fallback']['opset'] = m.opset_import[0].version
    except Exception:
        pass

try:
    ver = subprocess.check_output(['paddle2onnx', '--version'], text=True).strip()
    data['recognition']['primary']['converter'] = ver
except Exception:
    pass

with open(json_path, 'w') as f:
    json.dump(data, f, indent=2)
PY

python3 - <<'PY' "$OUT"/*_infer.onnx
import sys, onnx
for p in sys.argv[1:]:
    print("[CHECK]", p)
    m = onnx.load(p); onnx.checker.check_model(m)
    print("OK")
PY
