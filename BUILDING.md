# 构建与验证

## 工具

1. Windows x64和系统.NET Framework 4.x编译器：%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe。
2. **Zig 0.13.0**的Windows x64版本，从[Zig官方版本索引](https://ziglang.org/download/)获取并校验提供的SHA-256。解压后记下zig.exe路径。本仓库不携带工具链。

使用Zig提供的LLVM C++、MinGW和Windows系统库构建，MinHook v1.3.4源码已随仓库提供。第三方许可在licenses/，构建时一并复制到输出目录。

## 完整构建

在仓库根目录用PowerShell执行，路径换为自己的Zig位置：

```powershell
& .\source\build.ps1 -ZigPath 'C:\Tools\zig-0.13.0\zig.exe'
```

也可以设置环境变量DS3_CAMERA_ZIG，或将正确版本的zig.exe加入PATH。脚本会检查版本。默认输出到build/；可用-OutputDirectory指定其他输出位置。

主要输出：DS3CameraClient.exe、DS3CameraCore.dll、首次生成的camera-settings.xml、LICENSE、licenses/和THIRD-PARTY-NOTICES.md。重建不会覆盖输出目录已有的个人配置。更新DLL前请先退出加载它的游戏。

## 仅构建前端／外观预览

已有匹配版本DLL时，仅构建前端：

```powershell
& .\source\build-client.ps1
```

仅生成不连接游戏的外观预览：

```powershell
& .\source\build-preview.ps1
& .\build\DS3CameraPreview.exe
```

## 内部检查

完整构建后执行：

```powershell
Start-Process .\build\DS3CameraClient.exe -ArgumentList '--self-test' -Wait
Get-Content .\build\data\self-test.txt
Start-Process .\build\DS3CameraClient.exe -ArgumentList '--ui-self-test' -Wait
Get-Content .\build\data\ui-self-test.txt
```

测试仅向本工具自己的专用进程加载DLL，检查相机数学、输入归一化、IPC、恢复、暂停、减速及界面配置传递，不会自动启动或注入真实游戏。--self-test会在build/中读写测试配置，请不要指向个人正在使用的安装目录；--ui-self-test使用临时设置文件。

六个功能页的静态渲染：

```powershell
Start-Process .\build\DS3CameraClient.exe -ArgumentList '--ui-check' -Wait
```

输出到build/preview/。游戏内测试请自行通过原来的模组启动方式离线进入场景，再点击注入。

## 项目目录

- `source/`：程序源码。
- `build/`：本地构建输出。
- `camera-settings.example.xml`：默认配置模板；运行配置为构建目录中的 `camera-settings.xml`。

## 维护约定

- 小改动验证受影响范围；相机算法、DLL或通信协议变更需要相关回归检查。
- EXE与DLL的IPC版本须一致；程序名保持DS3CameraClient.exe，专用测试宿主依赖该名称。
- 不提交个人配置、日志、存档、商业参考包、游戏映像、下载包和构建缓存。
- 修改既有按键或速度参数只需保存XML配置，不需要重编译。
