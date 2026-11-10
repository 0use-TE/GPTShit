$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    # The same public clang-wasm builder used by the Wokwi custom-chip editor.
    $source = (Get-Content firmware/thermal.chip.c -Raw).Replace('"vendor/wokwi-api.h"', '"wokwi-api.h"')
    $body = @{
        sketch = ''; board = 'uno'; target = 'wasm'; builder = 'clang-wasm'
        projectId = 'controllab-thermal'; w = @{ b = 'interactive' }
        files = @(@{ name = 'app.c'; content = $source },
                  @{ name = 'wokwi-api.h'; content = (Get-Content firmware/vendor/wokwi-api.h -Raw) })
    } | ConvertTo-Json -Depth 8 -Compress
    $result = Invoke-RestMethod 'https://wokwi.com/build' -Method Post -ContentType 'application/json' `
        -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 180
    if (!$result.hex) { throw $result.stderr }
    $binary = [Convert]::FromBase64String($result.hex)
    if ($binary[0] -ne 0 -or $binary[1] -ne 97 -or $binary[2] -ne 115 -or $binary[3] -ne 109) { throw 'Invalid WebAssembly result' }
    [IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'firmware/thermal.chip.wasm'), $binary)
    Write-Output 'Thermal model compiled.'
} finally { Pop-Location }
