$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet run --project src/ControlLab/ControlLab.Desktop/ControlLab.Desktop.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "启动失败：$LASTEXITCODE" }
} finally { Pop-Location }
