# GpuOverlay

轻量级 Windows 桌面工具，实时显示 GPU / CPU 温度，透明悬浮窗 + 系统托盘，不影响正常操作。

## 预览

```
┌─────────────────────────┐
│ GPU    45°  12%  2048MB │
│         │               │
│ CPU    52°              │
└─────────────────────────┘
```

- 悬浮窗始终置顶，无边框透明背景
- 可拖动定位 / 锁定后鼠标穿透
- 系统托盘常驻，右键菜单控制

## 功能特性

| 功能 | 说明 |
|------|------|
| GPU 温度监控 | 通过 NVIDIA NVML 读取实时温度、使用率、显存 |
| CPU 温度监控 | 通过 LibreHardwareMonitor 读取 CPU 核心温度 |
| 无边框悬浮窗 | 透明背景、始终置顶、`SizeToContent` 自适应 |
| 拖动定位 | 未锁定时左键拖动，位置自动保存 |
| 鼠标穿透 | 锁定后启用 `WS_EX_TRANSPARENT`，不影响下层窗口 |
| 系统托盘 | 最小化到托盘，右键菜单：解锁 / 锁定 / 退出 |
| 位置记忆 | 关闭时保存位置到 `config.json`，下次启动恢复 |
| 温度变色 | 白 → 黄 → 橙 → 红，直观显示温度状态 |

## 环境要求

- **系统**：Windows 10 / 11（64-bit）
- **运行时**：.NET 8.0 Desktop Runtime（[下载](https://dotnet.microsoft.com/download/dotnet/8.0)）
- **GPU**：NVIDIA 显卡 + 驱动已安装（`nvml.dll`）
- **权限**：**必须以管理员身份运行**（CPU 温度需要内核驱动访问）

## 快速开始

### 1. 直接下载

从 [Releases](../../releases) 下载最新版本：

```
GpuOverlay.exe
2r.ico
```

**右键 → 以管理员身份运行**。

### 2. 从源码构建

```bash
git clone https://github.com/gearlam/GpuOverlay.git
cd GpuOverlay
dotnet publish -c Release
```

发布输出：`bin\Release\net8.0-windows\win-x64\publish\`

## 使用说明

| 操作 | 效果 |
|------|------|
| 左键拖动 | 移动悬浮窗位置（未锁定时） |
| 右键点击悬浮窗 | 打开窗口菜单（锁定 / 退出） |
| 右键点击托盘图标 | 打开托盘菜单（解锁 / 锁定 / 退出） |
| 双击托盘图标 | 解锁并激活窗口 |

## 锁定模式

- **未锁定**：可拖动，鼠标正常交互
- **锁定后**：不可拖动，鼠标穿透（`WS_EX_TRANSPARENT`），悬浮窗变半透明

## 项目结构

```
GpuOverlay/
├── MainWindow.xaml          # 悬浮窗 UI
├── MainWindow.xaml.cs       # 拖动、锁定、托盘、刷新逻辑
├── Nvml.cs                  # NVIDIA NVML P/Invoke 封装
├── HardwareMonitor.cs       # CPU 温度读取（LibreHardwareMonitor）
├── AppConfig.cs             # 配置加载 / 保存
├── App.xaml                 # 应用入口
├── config.json              # 位置和锁定状态
├── 2r.ico                   # 托盘图标
├── .gitignore               # Git 忽略规则
├── LICENSE                  # MIT 许可证
└── README.md                # 项目说明
```

## 技术栈

- **C# + WPF**（.NET 8）
- **NVIDIA NVML**（P/Invoke）— GPU 温度 / 使用率 / 显存
- **LibreHardwareMonitor** — CPU 核心温度（通过内核驱动读取 MSR 寄存器）
- **WinForms NotifyIcon** — 系统托盘
- **Win32 API**（`SetWindowLongPtr`）— 鼠标穿透

## 配置文件

`config.json` 示例：

```json
{
  "Left": 100.0,
  "Top": 100.0,
  "IsLocked": false,
  "Opacity": 1.0,
  "FontSize": 36.0
}
```

## 常见问题

### CPU 温度显示 NA？

- **必须以管理员身份运行**（LibreHardwareMonitor 需要内核驱动访问硬件）
- 右键 exe → 以管理员身份运行

### GPU 温度显示 NA？

- 确认已安装 NVIDIA 显卡驱动
- 检查 `nvml.dll` 是否存在于系统路径

### 托盘图标不显示？

- 确认 `2r.ico` 与 exe 在同一目录

### 程序右键托盘后闪退？

- 通常是权限不足导致 LibreHardwareMonitor 无法初始化
- 以管理员身份运行即可

## License

MIT License

## 致谢

- [NVIDIA Management Library (NVML)](https://docs.nvidia.com/deploy/nvml-api/)
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
