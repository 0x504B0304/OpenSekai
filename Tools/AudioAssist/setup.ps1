param([string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$runtime = Join-Path $project '.audio-assist-venv'
& $Python -m venv $runtime
if ($LASTEXITCODE -ne 0) { throw 'Could not create the developer Python environment.' }
$assistPython = Join-Path $runtime 'Scripts/python.exe'
& $assistPython -m pip install torch==2.8.0 torchaudio==2.8.0 --index-url https://download.pytorch.org/whl/cpu
if ($LASTEXITCODE -ne 0) { throw 'PyTorch installation failed.' }
& $assistPython -m pip install -r (Join-Path $project 'Assets/StreamingAssets/AudioAssist/requirements.txt')
if ($LASTEXITCODE -ne 0) { throw 'Developer dependency installation failed.' }
& $assistPython (Join-Path $PSScriptRoot 'package_runtime.py')
if ($LASTEXITCODE -ne 0) { throw 'Portable engine assembly failed.' }
Write-Host 'Offline engine ready. Windows builds include and verify it automatically.'
