param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet publish src/ControlLab/ControlLab.Desktop/ControlLab.Desktop.csproj -c Release -r $Runtime -o "artifacts/$Runtime"
    if ($LASTEXITCODE -ne 0) { throw "Native AOT 发布失败：$LASTEXITCODE" }
} finally { Pop-Location }
