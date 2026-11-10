param([string]$AppPath = "$PSScriptRoot/../artifacts/win-x64/ControlLab.Desktop.exe")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Get-Elements {
    $queue = [System.Collections.Generic.Queue[System.Windows.Automation.AutomationElement]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new()
    $queue.Enqueue($script:appWindow)
    while ($queue.Count -gt 0 -and $seen.Count -lt 2000) {
        $element = $queue.Dequeue()
        $key = $element.GetRuntimeId() -join ':'
        if (!$seen.Add($key)) { continue }
        Write-Output $element
        foreach ($child in $element.FindAll([System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition)) { $queue.Enqueue($child) }
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
$appProcess = Start-Process -FilePath $executable -WorkingDirectory (Split-Path $executable) -WindowStyle Hidden -PassThru
try {
    Wait-Label '已连接'
    Select-Tab '日志'
    Wait-Label '已确认命令：0' 10
    $startupNames = Get-Elements | ForEach-Object { $_.Current.Name }
    if ($startupNames -match 'Unable to invoke script before any page was loaded') { throw '启动期间调用了未就绪的 WebView' }
    Invoke-Button '启动仿真'
    Wait-Label '运行中'
    Invoke-Button '点亮 LED'
    Wait-Label 'LED 已点亮' 15
    Wait-Label '已确认命令：1' 15
    Invoke-Button '熄灭 LED'
    Wait-Label 'LED 已熄灭' 15
    Wait-Label '已确认命令：2' 15
    Invoke-Button 'Ping 下位机'
    Wait-Label '已确认命令：3' 15
    Invoke-Button '浅色模式'
    Wait-Label '深色模式' 10
    Select-Tab '代码'
    Wait-Label '"""Runs inside the simulated ESP32' 10
    Invoke-Button '深色模式'
    Wait-Label '浅色模式' 10
    Select-Tab '日志'
    Invoke-Button 'Ping 下位机'
    Wait-Label '已确认命令：4' 15
    Write-Output 'PASS  Semi 明暗切换 / VS Code Dock / 标签页切换后通信'
    Write-Output 'PASS  Windows Native AOT → WebView → Wokwi → ESP32 → ACK'
} finally {
    if (!$appProcess.HasExited) { Stop-Process -Id $appProcess.Id }
}
