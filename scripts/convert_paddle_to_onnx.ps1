$ErrorActionPreference = 'Stop'

python -m pip install --upgrade pip
python -m pip install paddlepaddle==2.6.0 paddle2onnx==1.3.1 onnx onnxruntime setuptools
if (Get-Command pyenv -ErrorAction SilentlyContinue) { pyenv rehash }

python --version
paddle2onnx --version

$REC_IT_URL = $env:REC_IT_URL
if (-not $REC_IT_URL) { $REC_IT_URL = 'https://paddleocr.bj.bcebos.com/dygraph_v2.0/multilingual/it_mobile_v2.0_rec_infer.tar' }
$ROOT = Resolve-Path "$PSScriptRoot/.."
$SRC = Join-Path $ROOT 'models/src'
$OUT = Join-Path $ROOT 'models/rec'
New-Item -ItemType Directory -Force -Path $SRC, $OUT | Out-Null
$tarball = Join-Path $SRC (Split-Path $REC_IT_URL -Leaf)
if (-not (Test-Path $tarball)) {
  Invoke-WebRequest -Uri $REC_IT_URL -OutFile $tarball
  (Get-FileHash $tarball -Algorithm SHA256).Hash | Out-File "$tarball.sha256"
}
$work = Join-Path $SRC 'it_mobile_v2.0_rec_infer'
if (-not (Test-Path $work)) { New-Item -ItemType Directory -Force -Path $work | Out-Null; tar -xf $tarball -C $work }

$dst = Join-Path $OUT 'it_mobile_v2.0_rec_infer.onnx'
if (-not (Test-Path $dst)) {
  paddle2onnx --model_dir $work --model_filename inference.pdmodel --params_filename inference.pdiparams --save_file $dst --opset_version 11 --enable_onnx_checker True
  (Get-FileHash $dst -Algorithm SHA256).Hash | Out-File "$dst.sha256"
} else {
  Write-Host "[SKIP] $dst già presente"
}

if (-not (Test-Path $dst)) {
  Write-Host "[INFO] Uso fallback latin_PP-OCRv3"
  $BASE = $env:REC_LATIN_DIR
  if (-not $BASE) { $BASE = 'https://huggingface.co/PaddlePaddle/latin_PP-OCRv3_mobile_rec/resolve/main' }
  $WF = Join-Path $SRC 'latin_ppocrv3'
  New-Item -ItemType Directory -Force -Path $WF | Out-Null
  foreach ($f in 'inference.pdmodel','inference.pdiparams','inference.yml') {
    $target = Join-Path $WF $f
    if (-not (Test-Path $target)) {
      Invoke-WebRequest -Uri "$BASE/$f" -OutFile $target
    }
  }
  $dst_fb = Join-Path $OUT 'latin_PP-OCRv3_mobile_rec_infer.onnx'
  paddle2onnx --model_dir $WF --model_filename inference.pdmodel --params_filename inference.pdiparams --save_file $dst_fb --opset_version 11 --enable_onnx_checker True
  (Get-FileHash $dst_fb -Algorithm SHA256).Hash | Out-File "$dst_fb.sha256"
}

python - <<'PY' "$ROOT"
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

$onnxFiles = Get-ChildItem $OUT -Filter '*_infer.onnx' | ForEach-Object { $_.FullName }
python - <<'PY' @onnxFiles
import sys, onnx
for p in sys.argv[1:]:
    print('[CHECK]', p)
    m = onnx.load(p); onnx.checker.check_model(m)
    print('OK')
PY
