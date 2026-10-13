# Project Unknown · 未知之境 V0.1

Windows PC 单人俯视角探索游戏。在陌生大陆寻找遗迹、观察环境和机关反馈，将不同地区的线索联系起来。

## 开发环境

- Godot **4.6.3 .NET** 稳定版（标准版不能运行 C# 脚本）
- .NET **8 SDK**
- Windows 10/11，或支持 Godot .NET 的 Linux 开发环境

核心逻辑使用 C#，不需要第三方游戏库。中文字体为 SIL Open Font License 的 Noto Sans CJK。

## 打开与启动

在 Godot 项目管理器中导入根目录 `project.godot`，第一次打开等待资源导入，点击右上角“构建”后按 **F6/F5** 运行当前场景/项目。

也可以在项目目录运行：

```sh
dotnet build ProjectUnknown.csproj
godot --path .
```

其中 `godot` 必须指向 .NET 版可执行文件。Windows 下载版可以直接使用完整可执行路径。

## 操作

| 操作 | 按键 |
| --- | --- |
| 移动 | WASD / 方向键 |
| 奔跑 | Shift |
| 互动 | E |
| 地图 | M |
| 探索日志 | J |
| 暂停 / 返回 | Esc |
| 全屏切换 | F11 |

主菜单可以新游戏或继续存档。暂停菜单提供设置、存档和探索记录导出。存档与导出记录保存在 Godot `user://` 目录；本项目的 Windows 路径是 `%APPDATA%/ProjectUnknown/`，Linux 路径是 `~/.local/share/ProjectUnknown/`。

## Windows 导出

1. 在 Godot 的“编辑器 → 管理导出模板”中安装 **4.6.3 .NET** 导出模板。
2. 打开“项目 → 导出”，选择已有的 **Windows Desktop** 预设。
3. 点击“导出项目”，关闭调试导出，保存到 `Builds/Windows/ProjectUnknown.exe`。
4. 将 `Assets/Fonts/LICENSE-NotoSansCJK.txt` 一并复制到输出目录；分发整个 `Builds/Windows` 文件夹，包括 .NET 运行库与程序集目录。脚本导出会自动复制字体授权。

该导出包含 .NET 运行库，玩家无需安装 SDK。命令行导出：

```sh
dotnet build ProjectUnknown.csproj -c Release
godot --headless --path . --editor --import
godot --headless --path . --export-release "Windows Desktop" Builds/Windows/ProjectUnknown.exe
```

Linux/macOS 可使用 `tools/export-windows.sh`；通过 `GODOT_BIN` 指定 .NET 版 Godot 路径。

## 验证

已实际完成 Godot 资源导入、C# 构建（0 错误 / 0 警告）和 Windows x64 自包含版本导出。

Godot 场景集成检查覆盖固定种子、关键地点可达、移动/奔跑/水域碰撞、踩踏开桥、三类机关、存档恢复、结局和 JSON 导出，共 33 项。通关检查会定位到机关附近；世界路径另外以可达性检查验证，不等同于人工走完整条路线。

Linux 可重复运行 `GODOT_BIN=/path/to/godot-mono tools/verify.sh`，需要 `dotnet` 在 PATH 中。测试用临时 XDG 目录隔离存档；无头模式不创建音频流。无头和图形模式均通过 33 项检查，退出无对象泄漏警告。已在虚拟图形环境检查 1280×720、1600×900、1920×1080 布局以及菜单、地图、日志、设置与结局。

## 已知限制

- V0.1 使用固定范围、固定种子的世界；没有无限地图、多人或联网服务。
- 画面由项目内原创程序绘制，未使用商业素材。
- Windows 导出未签名，首次启动可能需要通过系统的应用信誉提示。
- 云端缺少音频设备，图形运行使用 Dummy 音频驱动；已检查音频资源播放/释放流程，但声音听感未验证。Xvfb 没有窗口管理器，F11 调用正常，真实全屏视觉仍需检验。
- Windows 构建已导出并检查完整性，尚未在 Windows 10/11 原机验证显示、声音、性能与输入兼容性。
