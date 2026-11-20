# Dark Souls III Camera · 黑暗之魂Ⅲ摄影工具

魂3离线摄影相机。使用独立Windows设置界面，将相机控制DLL加载到已运行的游戏进程，支持中文界面和游戏内快捷键。

作者：[Sylva-06](https://github.com/Sylva-06) · [MIT License](LICENSE) · [第三方声明与致谢](THIRD-PARTY-NOTICES.md)

## 功能

- 自由移动、鼠标／方向键转向、独立倾斜、FOV、相机锁定。
- F5回到当前人物附近，保持自由相机开启并恢复横屏。
- 正反90°竖屏、FOV构图补偿，按当前画面方向控制；升降沿世界竖直方向。
- 普通HUD与互动菜单隐藏、暂停、可配置战斗减速。
- 可修改按键、移动／转向速度、鼠标灵敏度、平滑响应和加速／减速倍率。
- 客户端断开或失去心跳后恢复本工具接管的状态。

## 下载与使用

从 [Releases](https://github.com/Sylva-06/DarkSoulsIIICamera/releases/latest) 下载 `DarkSoulsIIICamera-v1.0.0-win-x64.zip`，完整解压到可写目录后运行 `DS3CameraClient.exe`。

## 环境与启动

- Windows x64，.NET Framework 4.x。
- DarkSoulsIII.exe版本 **1.15.2.0**，仅离线使用。
- 先通过你的游戏／模组启动配置进入可操作场景，再打开DS3CameraClient.exe，点击“注入相机”。
- 本工具不负责启动游戏，也不会自动切换独立存档；存档由游戏启动配置决定。
- 已通过Mod Engine 2实测。与其他镜头、HUD或时间控制模组一起使用时，请检查功能是否冲突。

## 默认按键

![默认按键示意图](docs/images/default-keybindings.png)

按键可在程序的“按键设置”中修改。

<details>
<summary>查看文字按键表</summary>

| 按键 | 功能 |
| --- | --- |
| Insert / Home | 相机开关 / 锁定 |
| F5 / F6 | 回到人物附近 / 横屏与正反90°竖屏 |
| Delete / 小键盘0 | 隐藏或显示UI / 暂停或继续 |
| F9 | 减速与正常时间切换，默认0.25倍 |
| 小键盘8、5、4、6 | 前、后、左、右 |
| 小键盘7、9 | 世界竖直上升、下降 |
| 小键盘1、3、2 | 左右倾斜、倾斜复位 |
| 小键盘−、+、* | 拉近、拉远、视野复位 |
| 按住小键盘 . | 仅相机平移加速，默认5倍 |
| 鼠标／方向键／滚轮 | 转向／转向／调整视野 |
| F12 | 保留给Steam截图 |

</details>

## 开发

构建方法见[BUILDING.md](BUILDING.md)。
