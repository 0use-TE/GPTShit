param([string]$AppPath = "$PSScriptRoot/../artifacts/win-x64/ControlLab.Desktop.exe", [switch]$Packed)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Get-Elements {
    $queue = [System.Collections.Generic.Queue[System.Windows.Automation.AutomationElement]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new()
    $queue.Enqueue($script:appWindow)
    while ($queue.Count -gt 0 -and $seen.Count -lt 2000) {
        $element = $queue.Dequeue()
        try { $key = $element.GetRuntimeId() -join ':' } catch { continue }
        if (!$seen.Add($key)) { continue }
        Write-Output $element
        try {
            foreach ($child in $element.FindAll([System.Windows.Automation.TreeScope]::Children,
                [System.Windows.Automation.Condition]::TrueCondition)) { $queue.Enqueue($child) }
        } catch { continue } # Native WebView can replace its accessibility nodes between frames.
    }
}

function Wait-Label([string]$Text, [int]$TimeoutSeconds = 150) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if ($script:appProcess.HasExited) { throw "应用提前退出，退出码：$($script:appProcess.ExitCode)" }
        $script:appProcess.Refresh()
        if ($script:appProcess.MainWindowHandle -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 200; continue }
        $script:appWindow = [System.Windows.Automation.AutomationElement]::FromHandle($script:appProcess.MainWindowHandle)
        if ($script:appWindow) {
            $elements = Get-Elements
            $names = @($elements | ForEach-Object { $_.Current.Name })
            if ($names -contains $Text -or @($names | Where-Object { $_ -and $_.StartsWith($Text) }).Count -gt 0) { Write-Output "PASS  $Text"; return }
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    $summary = $names | Where-Object { $_ -and $_.Length -lt 180 } | Select-Object -Unique -First 35
    throw "等待超时：$Text`n$($summary -join [Environment]::NewLine)"
}

function Invoke-Button([string]$Name) {
    $button = Get-Elements | Where-Object { $_.Current.Name -eq $Name } | Select-Object -First 1
    if (!$button) { throw "按钮不存在：$Name" }
    $pattern = $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Select-Tab([string]$Name) {
    $candidates = Get-Elements | Where-Object { $_.Current.Name -eq $Name }
    foreach ($candidate in $candidates) {
        $element = $candidate
        for ($level = 0; $level -lt 4 -and $element; $level++) {
            $selection = $null
            if ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selection)) {
                $selection.Select()
                Write-Output "PASS  切换标签：$Name"
                return
            }
            $element = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($element)
        }
    }
    throw "无法选择标签：$Name"
}

$executable = (Resolve-Path -LiteralPath $AppPath).Path
$priorProcesses = @(Get-Process -Name ControlLab.Desktop -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$appProcess = Start-Process -FilePath $executable -WorkingDirectory (Split-Path $executable) -WindowStyle Hidden -PassThru
try {
    if ($Packed) {
        $unpackRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'ControlLab-PID')) + [IO.Path]::DirectorySeparatorChar
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            $child = Get-Process -Name ControlLab.Desktop -ErrorAction SilentlyContinue | Where-Object {
                $_.Id -notin $priorProcesses -and $_.Path -and $_.Path.StartsWith($unpackRoot, [StringComparison]::OrdinalIgnoreCase)
            } | Select-Object -First 1
            if ($child) { $appProcess = $child; Write-Output 'PASS  单文件解包并启动 AOT 应用'; break }
            Start-Sleep -Milliseconds 200
        } while ([DateTime]::UtcNow -lt $deadline)
        if (!$child) { throw '单文件未启动解包后的应用' }
    }
    Wait-Label '已连接'
    Select-Tab '日志'
    Wait-Label '已确认命令：0' 10
    $startupNames = Get-Elements | ForEach-Object { $_.Current.Name }
    if ($startupNames -match 'Unable to invoke script before any page was loaded') { throw '启动期间调用了未就绪的 WebView' }
    Invoke-Button '启动仿真'
    Wait-Label '运行中'
    Invoke-Button '开始控温'
    Wait-Label '停止控温' 15
    Wait-Label '已确认命令：2' 15
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $temperature = Get-Elements | Where-Object { $_.Current.AutomationId -eq 'Temperature' } | Select-Object -First 1
        $value = [double]::Parse(($temperature.Current.Name -replace '[^0-9.]',''), [Globalization.CultureInfo]::InvariantCulture)
        if ($value -gt 30) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($value -le 30) { throw '下位机未驱动热模型升温' }
    Write-Output "PASS  温度已升至 $value °C"
    Invoke-Button '开门 5 秒'
    Wait-Label '门已打开' 10
    Wait-Label '开门 5 秒' 15
    Invoke-Button '应用参数'
    Wait-Label '已确认命令：4' 15
    Invoke-Button '浅色模式'
    Wait-Label '深色模式' 10
    Select-Tab 'main.py'
    Wait-Label 'main.py 下位机代码' 10
    Select-Tab 'pid.py'
    Wait-Label 'pid.py 下位机代码' 10
    Select-Tab '电路'
    Invoke-Button '深色模式'
    Wait-Label '浅色模式' 10
    Wait-Label '温度曲线' 10
    $names = Get-Elements | ForEach-Object { $_.Current.Name }
    if ($names -contains '导出数据') { throw '界面仍保留已移除的导出功能' }
    Invoke-Button '停止控温'
    Wait-Label '开始控温' 15
    Wait-Label '加热 0%' 10
    Select-Tab '日志'
    Wait-Label '已确认命令：5' 15
    Write-Output 'PASS  Native AOT / PID 控温 / 温度反馈 / 开门扰动 / 停止加热 / 主题及标签切换'
} finally {
    if (!$appProcess.HasExited) { Stop-Process -Id $appProcess.Id }
}
