# 第三方声明与致谢

本项目自行编写的代码采用根目录的 [MIT License](LICENSE)。第三方组件保留原作者的版权声明和许可证。

## 使用的组件

| 组件 | 用途 | 许可证文件 |
| --- | --- | --- |
| [MinHook v1.3.4](https://github.com/TsudaKageyu/minhook/tree/v1.3.4) | 原生函数挂钩，包含HDE指令解码器 | [MinHook-BSD.txt](licenses/MinHook-BSD.txt)、[源码许可](source/vendor/minhook/LICENSE.txt) |
| LLVM libc++、libc++abi、libunwind | Zig 0.13.0提供的C++运行库 | [libc++](licenses/LLVM-libcxx-LICENSE.txt)、[libc++abi](licenses/LLVM-libcxxabi-LICENSE.txt)、[libunwind](licenses/LLVM-libunwind-LICENSE.txt)：Apache-2.0附LLVM例外及文件内的其他通知 |
| MinGW-w64 | Windows目标运行库支持 | [mingw-w64-COPYING.txt](licenses/mingw-w64-COPYING.txt)，包括其中列明的例外 |
| Microsoft .NET Framework、Windows API | 系统界面与进程操作接口 | 使用Windows已安装的系统组件 |

## 开发参考

- [Logersnamed / FreecamMod](https://github.com/Logersnamed/FreecamMod)：相机数据布局、移动、输入与早期路径设计参考。Copyright (c) 2026 Logersnamed；[MIT许可证](licenses/FreecamMod-MIT.txt)。
- [borgCode / SilkySouls3](https://github.com/borgCode/SilkySouls3)：魂3单例地址、相机布局、暂停及时间倍率位置参考；[MIT许可证](licenses/SilkySouls3-MIT.txt)。
- [The Grand Archives / Dark Souls III Cheat Table](https://github.com/The-Grand-Archives/Dark-Souls-III-CT-TGA)：v3.4.0相机与HUD内存布局的交叉核对资料。
- [Mod Engine 2](https://github.com/soulsmods/ModEngine2)、[Mod Engine 3](https://github.com/garyttierney/me3)：模组加载与启动机制研究。
- Otis_Inf / Frans Bouma 的 [Elden Ring Camera Tools](https://opm.fransbouma.com/)：功能、配置、界面布局及EXE＋DLL注入流程参考；程序实现使用本项目源码。
- [FransBouma / InjectableGenericCameraSystem](https://github.com/FransBouma/InjectableGenericCameraSystem)：通用相机框架资料检索，该公开框架采用BSD-2-Clause许可证。

游戏相关地址及短指令签名还通过本地Dark Souls III 1.15.2程序核对。键盘说明图由KeyboardMap.cs绘制。
