# Project Unknown · 未知之境 V0.2

Godot/C# 原生 Windows 单人动作探索 RPG。地表 220×220 格，包含森林、草原、荒漠、湿地、雪地；保留原遗迹机关，新增林地墓室和熔炉两个地下城。

## 启动

下载 Windows 包后完整解压，运行 `Windows/ProjectUnknown.exe`，保留同目录的运行库和数据文件。玩家不需要安装 .NET SDK。

开发环境保持 **Godot 4.6.3 .NET + .NET 8 SDK**。在 Godot 中导入根目录 `project.godot`，等待资源导入，构建后按 **F5**。也可执行：

```sh
dotnet build ProjectUnknown.csproj
godot --path .
```

`godot` 必须指向 .NET 版，标准版不支持本项目的 C# 脚本。

## 操作

| 操作 | 按键 |
| --- | --- |
| 移动 / 奔跑 | WASD 或方向键 / Shift |
| 攻击方向 / 普通攻击 | 鼠标 / 鼠标左键，可按住连续攻击 |
| 闪避 | Space |
| 回旋斩 / 三连射 / 寒霜爆发 | 1 / 2 / 3，需先学习对应技能 |
| 互动、开箱、拾取、休息 | E |
| 背包 / 角色与技能 | I / C |
| 地图 / 探索日志 | M / J |
| 暂停 / 全屏 | Esc / F11 |

初始背包含剑、弓、法杖和药水。背包可装备、卸下、使用或丢弃物品；丢弃物品可在原地拾回。等级提升获得技能点，角色页可学习主动技能及三种被动强化。

完成中央遗迹并击败两个地下城 Boss 即完成主线，结算后可继续探索。营地恢复生命、设置复活点并解锁快速旅行；附近有敌人时无法休息或旅行。死亡保留装备、经验和已完成进度。击败的敌人在此存档中不会刷新。

## 存档

存档自动保存，也可在暂停菜单保存和导出 JSON 探索记录。V0.1 存档会迁移地图探索索引，保留原机关与遗迹进度，并补充初始角色、装备和技能数据。新游戏会先备份旧档；损坏存档不会被自动覆盖。

Windows：`%APPDATA%/ProjectUnknown/`；Linux：`~/.local/share/ProjectUnknown/`。装备、成长、地下城、检查点和地面掉落均保存；战斗冷却和存活敌人的当前生命在重新载入区域时重置。

## Windows 导出

安装匹配的 **4.6.3 .NET** 导出模板，在“项目 → 导出”选择已有的 **Windows Desktop** 预设，导出到 `Builds/Windows/ProjectUnknown.exe`，分发整个目录并附带字体授权。Linux/macOS 可运行：

```sh
GODOT_BIN=/path/to/godot-mono tools/export-windows.sh
```

需要 `dotnet` 在 PATH 中。脚本执行导入、Release 构建、Windows x64 自包含导出并复制字体授权。中文字体为 Noto Sans CJK，授权见 `Assets/Fonts/LICENSE-NotoSansCJK.txt`。

GitHub Actions 的 **Windows V0.2 release** 流程使用相同版本的 Godot 与 .NET SDK，运行集成检查、导出并上传到 `v0.2.0` 发布草稿，然后发布。首次由 `ci/windows-release-v02` 分支推送触发；合入默认分支后也可在 Actions 页面手动运行。需要已有的发布草稿，已公开版本不会被覆盖。

## 实际验证与限制

- Godot 导入、Debug/Release 构建和 Windows 导出成功，构建 0 错误、0 警告。
- 无头和图形模式各通过 **135 项集成检查**，覆盖旧机关、移动碰撞、战斗、三种武器、装备掉落、升级技能、死亡复活、地下城往返、两场 Boss 战、完整结局、存档恢复和 V0.1 迁移。Boss 由正常攻击与技能击败。测试定位缩短旅行，路线另做可达性检查，不等同于真人走完整条路线。
- 已实际检查 1280×720、1600×900、1920×1080 的菜单、背包、角色、地图、日志、设置及结算布局。Linux 可用 `GODOT_BIN=/path/to/godot-mono tools/verify.sh` 在隔离的临时存档目录重复检查。
- 云端没有音频设备，图形验证使用 Dummy 驱动；声音听感未验证。Xvfb 没有窗口管理器，F11 调用正常，真实全屏效果仍需 Windows PC 检验。
- Windows 包已导出并核验完整性，尚未在 Windows 10/11 原机验证显示、声音、输入和性能。构建未签名。
