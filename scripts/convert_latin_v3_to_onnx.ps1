# Esegui: ./scripts/convert_latin_v3_to_onnx.ps1
$ErrorActionPreference = "Stop"
$PRIMARY_URL  = $env:PRIMARY_URL  ? $env:PRIMARY_URL  : "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/latin_PP-OCRv3_mobile_rec_infer.tar"
$FALLBACK_URL = $env:FALLBACK_URL ? $env:FALLBACK_URL : "https://paddleocr.bj.bcebos.com/PP-OCRv3/multilingual/latin_PP-OCRv3_rec_infer.tar"

$ROOT = (Get-Item "$PSScriptRoot/..").FullName
$SRC  = Join-Path $ROOT "models/src/latin_v3"
$OUT  = Join-Path $ROOT "models/rec"
$DOC  = Join-Path $ROOT "docs"
$MAN  = Join-Path $ROOT "models/sources.json"
New-Item -Force -ItemType Directory $SRC,$OUT,$DOC | Out-Null

python -m pip install --upgrade pip | Out-Null
python -m pip install paddlepaddle==2.6.0 paddle2onnx==1.3.1 onnx onnxruntime setuptools | Out-Null

function Download-Extract([string]$url) {
  $tar = Join-Path $SRC ([System.IO.Path]::GetFileName($url))
  Invoke-WebRequest -Uri $url -OutFile $tar
  (Get-FileHash $tar -Algorithm SHA256).Hash | Out-File ($tar + ".sha256")
  tar -xf $tar -C $SRC --strip-components=1
}

if (-not (Test-Path (Join-Path $SRC "inference.pdmodel"))) {
  try { Download-Extract $PRIMARY_URL } catch { Write-Host "[WARN] Primary failed" }
  if (-not (Test-Path (Join-Path $SRC "inference.pdmodel"))) {
    Write-Host "[WARN] Primary missing, using fallback"; Download-Extract $FALLBACK_URL
  }
}

$dst = Join-Path $OUT "latin_PP-OCRv3_mobile_rec_infer.onnx"
if (Test-Path $dst) {
  Write-Host "[SKIP] $dst esiste"
} else {
  paddle2onnx --model_dir $SRC --model_filename inference.pdmodel --params_filename inference.pdiparams `
              --save_file $dst --opset_version 11 --enable_onnx_checker True | Out-Null
  (Get-FileHash $dst -Algorithm SHA256).Hash | Out-File ($dst + ".sha256")
}

# Validazione ONNX
python - << 'PY'
import onnx, sys
p = sys.argv[1]
m = onnx.load(p); onnx.checker.check_model(m)
print("[CHECK] OK:", p)
PY
$dst

# Guida
@"# Conversione latin_PP-OCRv3_mobile_rec → ONNX
- Fonte primaria (BOS): $PRIMARY_URL
- Fallback storico: $FALLBACK_URL
- Converter: paddle2onnx --opset_version 11 --enable_onnx_checker True
- Output: models/rec/latin_PP-OCRv3_mobile_rec_infer.onnx + .sha256
- Verifica: onnx.checker.check_model
"@ | Out-File -Encoding UTF8 (Join-Path $DOC "model-conversion-latin-v3.md")

Write-Host "[DONE] -> $dst"
