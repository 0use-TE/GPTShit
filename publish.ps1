param([string]$Runtime = 'win-x64', [switch]$SingleFile)
$ErrorActionPreference = 'Stop'
$stageRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'work/publish'))
$stage = Join-Path $stageRoot ([Guid]::NewGuid().ToString('N'))
Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    dotnet publish src/ControlLab/ControlLab.Desktop/ControlLab.Desktop.csproj -c Release -r $Runtime -o $stage -p:DebugSymbols=false -p:DebugType=None
    if ($LASTEXITCODE -ne 0) { throw "Native AOT 发布失败：$LASTEXITCODE" }
    # Only pack a fresh publish, never existing browser profiles or symbols.
    Get-ChildItem -LiteralPath $stage -Recurse -File -Filter '*.pdb' | Remove-Item -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'tools/licenses/wrappe-LICENSE.txt') -Destination (Join-Path $stage 'Licenses/wrappe.txt')
    $directoryOutput = Join-Path $PSScriptRoot "artifacts/$Runtime"
    New-Item -ItemType Directory -Force -Path $directoryOutput | Out-Null
    Get-ChildItem -LiteralPath $stage | Copy-Item -Destination $directoryOutput -Recurse -Force
    Write-Output "AOT output: $directoryOutput"
    if ($SingleFile) {
        if ($Runtime -ne 'win-x64') { throw '当前单文件脚本支持 win-x64。' }
        $tool = Join-Path $PSScriptRoot 'work/tools/wrappe-1.0.6.exe'
        $expectedHash = '3FDF78A356414C6E010EF1E02B027E1AA6A066641A0251972BE812686CA4CB5E'
        if (!(Test-Path -LiteralPath $tool)) {
            New-Item -ItemType Directory -Force -Path (Split-Path $tool) | Out-Null
            Invoke-WebRequest -Uri 'https://github.com/Systemcluster/wrappe/releases/download/v1.0.6/wrappe.exe' -OutFile $tool
        }
        if ((Get-FileHash -LiteralPath $tool -Algorithm SHA256).Hash -ne $expectedHash) { throw 'wrappe 下载校验失败。' }
        $manifest = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName | ForEach-Object {
            [IO.Path]::GetRelativePath($stage, $_.FullName) + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }) -join "`n"
        $version = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($manifest))).Substring(0,16).ToLowerInvariant()
        $singleOutput = Join-Path $PSScriptRoot 'artifacts/single-file/ControlLab.exe'
        New-Item -ItemType Directory -Force -Path (Split-Path $singleOutput) | Out-Null
        & $tool --compression 16 --unpack-target local --unpack-directory ControlLab-PID --versioning sidebyside --version-string $version --verification checksum --console never --show-information none --current-dir unpack $stage (Join-Path $stage 'ControlLab.Desktop.exe') $singleOutput
        if ($LASTEXITCODE -ne 0) { throw "wrappe 打包失败：$LASTEXITCODE" }
        $hash = (Get-FileHash -LiteralPath $singleOutput -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  ControlLab.exe" | Set-Content -LiteralPath "$singleOutput.sha256" -Encoding ascii
        Write-Output "Single-file output: $singleOutput"
        Write-Output "Size: $([Math]::Round((Get-Item -LiteralPath $singleOutput).Length / 1MB, 2)) MiB"
    }
} finally {
    Pop-Location
    if (Test-Path -LiteralPath $stage) {
        $resolvedStage = (Resolve-Path -LiteralPath $stage).Path
        if (!$resolvedStage.StartsWith($stageRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '暂存目录路径越界，取消清理。' }
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
