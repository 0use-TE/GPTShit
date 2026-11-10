# ControlLab · 控制系统综合设计原型

Avalonia 内嵌完整 Wokwi 电路仿真，通过双向消息桥与 ESP32 中运行的下位机程序通信。
界面使用 Semi.Avalonia，工作区使用 GOZA.Dock 的 VS Code Dark Modern / Light Modern 主题。

## 运行

需要 .NET 10 SDK、Windows WebView2 Runtime 和互联网连接。

```powershell
./run.ps1
```

也可以打开 `ControlLab.slnx`，将 `ControlLab.Desktop` 设为启动项目。

1. 等待右上角显示“已连接”。
2. 点击“启动仿真”，首次需要下载仿真环境，可能耗时几十秒。
3. 等待“运行中”，在左侧点击“点亮”“熄灭”或“Ping”。
4. 检查电路中的 LED 和左侧回传状态；右下角切换“日志”查看收发记录。
5. 出现连接错误时，点击“重新连接”重新加载电路及程序。

界面分为三个 Dock 区域：左侧控制，右上仿真，右下代码/日志。
右上角主题按钮同步切换 Semi 控件和 Dock 色板。
区域支持标签移动和分隔条调整。右下角“代码”标签提供带行号的只读下位机源码查看。
界面只保留操作及状态，接线说明和技术信息放在本文档中。
Wokwi 远端页面内的画布及工具栏使用其自身的主题按钮；宿主页面的主题切换独立于该远端设置。

原生 AOT 发布：

```powershell
./publish.ps1
./artifacts/win-x64/ControlLab.Desktop.exe
```

发布目录的 `Web` 文件夹须与 exe 一起保留。发布后的程序不需要安装 .NET，但需要 WebView2 和联网。

## 程序和电路

```text
Avalonia MVVM / Crystal.Avalonia
    ↕ NativeWebView.InvokeScript / invokeCSharpAction
本地 HTTP 页面 bridge.js
    ↕ Wokwi MessagePort 嵌入 API
Wokwi ESP32 + 电路 + MicroPython 运行时
    ↕ UART2：TX=GPIO17 / RX=GPIO16
firmware/main.py：解析命令、控制 GPIO2、回传 ACK 与心跳
    ↓
GPIO2 → 330Ω 电阻 → 绿色 LED → GND
```

`firmware/main.py` 在 Wokwi 仿真的 ESP32 内执行，控制逻辑未放到 C# 或网页中。
使用 MicroPython 独立下位机程序；当前没有 C/C++ 固件编译步骤。
UART2 避免与 MicroPython UART0 的 REPL 控制台冲突。上位机无须安装虚拟串口。

协议使用 UTF-8 JSON，每条以换行结尾：

```json
{"command":"led","id":"1","value":true}
{"type":"ack","id":"1","led":true,"uptime":1234}
```

支持 `led` 和 `ping`，下位机每秒发送 `heartbeat`。LED 状态由设备消息确认，发送命令本身不会修改状态。
命令等待确认 5 秒，网页 API 等待通常为 20 秒，启动等待为 120 秒。通信日志最多保留 200 条。

## 结构与约束

- Avalonia、Avalonia.Desktop、官方 Avalonia.Controls.WebView：**12.1.0**。
- Semi.Avalonia **12.1.0.1**：替换 Fluent 主题，支持明暗切换。
- GOZA.Dock **3.0.9**：VS Code 色板、标签页、停靠区域与分隔条。
- Crystal.Avalonia **3.1.1**：应用启动、服务注册、View/ViewModel 配对。
- CommunityToolkit.Mvvm：属性与命令源码生成；XAML 编译绑定。
- WebView 事件在 `Behaviors/SimulationBridge.cs` 中显式订阅，未通过反射绑定 CLR 事件。
- `Services/SimulationSession.cs` 管理通信状态，JSON 使用源码生成上下文，支持裁剪与 Native AOT。
- 界面 code-behind 仅保留模板生成的 `InitializeComponent()` 构造初始化，无界面交互或业务逻辑。
- `LocalWebServer` 仅监听 localhost 随机端口，提供静态页面与固件文件；页面验证 Wokwi 消息来源和 iframe 窗口。
- 当前交付验证 Windows；Linux/Wayland 还需对应系统 WebView 后端及独立实机验证。

## Wokwi 嵌入说明

依据 [Wokwi 自有账号下的嵌入示例](https://github.com/wokwi/wokwi-embed-example)实现。
该接口仍标注为实验原型、尚未正式支持；不能视为稳定公开 SDK，也不能据此承诺离线运行。

`Assets/Web/config.json` 使用官方示例公开的演示 client_id；本次实测可用。后续若 Wokwi 改变访问策略，需要更新 client_id 或与平台确认集成方式。
直接打开 `wokwi.com` 首页或普通项目 URL 不会自动获得该桥接协议。

电路以 `firmware/diagram.json` 定义，可复制到 Wokwi 网站编辑后再保存回本地；内嵌页面主要用于运行与观察。
修改源文件后重新构建，固件和网页会自动复制到运行/发布目录。

## 验证

Windows 自动化冒烟测试会启动发布后的 AOT 程序，通过 UI Automation 操作原生按钮，检查下位机回传：

```powershell
./tests/smoke-native.ps1
```

已实测网页端以及 Windows Native AOT 程序的连接、启动、开灯、关灯和 Ping 确认链路。
冒烟测试还覆盖 Semi 明暗切换、VS Code Dock 色板切换，以及程序标签页切换后继续接收下位机确认。
