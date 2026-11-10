$ErrorActionPreference = 'Stop'
python (Join-Path $PSScriptRoot 'tools/build_firmware.py')
if ($LASTEXITCODE -ne 0) { throw 'Firmware filesystem build failed.' }
