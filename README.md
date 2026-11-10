# 基于 ESP32 与 Avalonia 的恒温箱 PID 控制仿真系统

ESP32 执行温度采样、PID、PWM 和故障保护；Wokwi 自定义热模型计算加热与散热，反馈温度；Avalonia 负责设置参数、显示曲线与记录实验。

## 运行与演示

需要 Windows WebView2 Runtime、网络；从源码运行还需要 .NET 10 SDK。

```powershell
./run.ps1
```

或直接运行已发布的 `artifacts/win-x64/ControlLab.Desktop.exe`。请保留同目录的 `Web` 文件夹。

1. 等待右上角“已连接”，点击“启动仿真”。
2. 目标温度默认 50℃，点击“开始控温”。
3. 在右上观察电路及热模型温度，在左侧控制区的曲线观察实际温度与目标温度。
4. 接近稳态后点击“开门 5 秒”，观察降温和关门后的恢复。
5. 调整目标温度、Kp/Ki/Kd，点击“应用参数”，比较响应。
6. 点击“停止控温”将加热输出立即置零。

三个 Dock 区域：左侧控制与温度曲线，右上仿真，右下日志；代码在仿真 WebView 内通过 main.py / pid.py 标签查看并高亮。右上角主题按钮同步切换 Semi、VS Code Dock、网页代码和 Wokwi 明暗色板。Windows WebView2 在子文档中注册主题消息监听，切换时不重载仿真。

## 控制与模型

```text
Avalonia 参数与曲线
       ↕ WebView / Wokwi 串口桥
ESP32 MicroPython 程序
       ├─ I²C 温度采样：GPIO22 / GPIO21，地址 0x48
       ├─ PID：100ms 周期
       ├─ PWM：GPIO23，500Hz
       └─ 开门：GPIO19
                  ↕
Wokwi thermal.chip.wasm 热过程模型
```

下位机代码：`firmware/main.py` 与 `firmware/pid.py`。
热过程代码：`firmware/thermal.chip.c`，编译为 Wokwi 自定义 WebAssembly 元件。

模型采用热平衡方程：

```text
C × dT/dt = Pmax × u − k × (T − Ta)
```

| 参数 | 数值 |
|---|---:|
| 环境温度 Ta | 25℃ |
| 热容 C | 80 J/K |
| 最大加热功率 Pmax | 250 W |
| 关门散热系数 k | 2.5 W/K |
| 开门散热系数 k | 20 W/K |

热模型每 100ms 按仿真时间积分，累计 PWM 高电平持续时间计算真实占空比，经 I²C 回传温度。
模型显示温度、橙色加热功率条和开门状态条。它将加热器与传感器抽象为过程模型，不包含功率级的 SPICE 分析。

PID 默认 Kp=8、Ki=0.7、Kd=4，输出范围 0–100%。采用条件积分抑制饱和累积，对测量值求导并滤波。
应用参数会重置积分与微分状态，便于从统一初始条件比较参数效果。

目标温度范围为 30–75℃；实际温度达到 80℃或传感器异常时，ESP32 在控制周期内关闭 PWM，并锁定停止状态。
故障消除后需手动点击“开始控温”恢复。停止控温不停止仿真，箱体继续自然散热。

温度遥测周期 500ms，曲线显示最近 180 秒，最多保存 7200 个采样点。

## 固件与构建

正常启动直接加载已打包的 MicroPython 固件、LittleFS 程序镜像及热模型，无须每次等待在线编译队列。
Wokwi 网页及仿真引擎仍依赖网络，当前不承诺离线运行。

```powershell
# main.py / pid.py 修改后，dotnet build 会自动重建程序镜像；也可手动执行：
./build-firmware.ps1

# 热模型 C 代码修改后重新编译：
./build-model.ps1

# Windows Native AOT 发布：
./publish.ps1
```

程序镜像构建使用 Python 与 littlefs-python 0.19.0，依赖安装到项目 `work/python-libs`。
模型构建脚本使用 Wokwi 编辑器的 clang-wasm 构建服务，需要联网；已提交预编译模型。

MicroPython v1.22.0 ESP32 固件从 0x1000 加载，LittleFS 镜像从 0x200000 加载。程序通过 UART2（GPIO17/16，115200）与宿主通信。
采用分段二进制固件启动，是为了让 Wokwi 嵌入 API 同时加载 MicroPython 程序和自定义模型。

## 验证

```powershell
python -m unittest discover -s tests -p 'test_*.py'
./tests/smoke-native.ps1
```

原生测试操作 AOT 程序的实际按钮，检查升温、开门、停止控温、参数应用以及主题/标签切换。

Wokwi 完整闭环测试：

```powershell
npm install --prefix work playwright
python -m http.server 8765 --bind 127.0.0.1 --directory artifacts/win-x64/Web
# 在另一个终端运行：
node tests/wokwi-live.cjs
```

实测默认参数下，约 20 秒温度达到 50.3℃，70 秒约为 50.0℃；稳态开门 5 秒降至约 41.3℃，关门后恢复。
完整测试还检查恢复后的持续稳定、停止切断加热、传感器故障保护和修复后的手动恢复。
Python 测试执行实际 PID 模块，并用模拟硬件运行实际下位机程序，验证超温、传感器断开及参数更新校验。

## 技术与限制

- Avalonia / 官方 NativeWebView 12.1.0，Crystal.Avalonia 3.1.1。
- Semi.Avalonia 12.1.0.1，GOZA.Dock 3.0.9，VS Code Dark Modern / Light Modern。
- 编译绑定、MVVM 命令、JSON 源码生成，Windows Native AOT。
- 界面 code-behind 仅有构造初始化，通信、控制状态和图表在独立类中。
- Wokwi 使用实验性的嵌入 API 和官方示例公开的演示 client_id，后续平台策略变化可能需要调整。
- Wokwi 内部画布主题由它自己的主题按钮管理。
- 当前验证 Windows；Linux/Wayland 需要相应 WebView 系统后端和独立验证。

第三方来源与许可见 `THIRD-PARTY-NOTICES.md`。
