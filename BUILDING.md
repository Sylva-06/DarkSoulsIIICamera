# 构建与验证

## 工具

1. Windows x64和系统.NET Framework 4.x编译器：%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe。
2. **Zig 0.13.0** Windows x64版，从[官方下载](https://ziglang.org/download/)获取。

MinHook源码随仓库提供，构建时自动复制第三方许可文件。

## 完整构建

在仓库根目录用PowerShell执行，路径换为自己的Zig位置：

```powershell
& .\source\build.ps1 -ZigPath 'C:\Tools\zig-0.13.0\zig.exe'
```

也可以设置环境变量DS3_CAMERA_ZIG，或将正确版本的zig.exe加入PATH。脚本会检查版本。默认输出到build/；可用-OutputDirectory指定其他输出位置。

输出包括EXE、DLL、默认配置和许可文件；已有配置保留。更新DLL前请退出游戏。

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

测试使用专用进程，不连接游戏。`--self-test`会写入测试配置，请在独立构建目录执行；`--ui-self-test`使用临时配置。

六个功能页的静态渲染：

```powershell
Start-Process .\build\DS3CameraClient.exe -ArgumentList '--ui-check' -Wait
```

输出到 `build/preview/`。

## 项目目录

- `source/`：程序源码。
- `source/assets/`：透明图标的SVG、PNG和多尺寸ICO；构建时将ICO嵌入EXE。需要调整图形时，可安装Python和Pillow后运行 `python source/assets/generate-icon.py` 重新生成。
- `build/`：本地构建输出。
- `camera-settings.example.xml`：默认配置模板；运行配置为构建目录中的 `camera-settings.xml`。

## 开发注意

- EXE与DLL的IPC版本须一致；程序名保持DS3CameraClient.exe，专用测试宿主依赖该名称。
- 不提交个人配置、日志、存档和构建输出。
