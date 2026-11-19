# Third-party notices and provenance

This document records dependencies and development references. The project's own code is offered under the MIT License in the root LICENSE file. Third-party files retain their original copyright notices and license terms; the project's license does not relicense those files, any game, or any proprietary camera tool.

## Shipped dependencies

| Component | Role | License notice |
| --- | --- | --- |
| [MinHook v1.3.4](https://github.com/TsudaKageyu/minhook/tree/v1.3.4) | Native detour library, shipped as source under source/vendor/minhook and compiled into the DLL | licenses/MinHook-BSD.txt and source/vendor/minhook/LICENSE.txt; includes the HDE notices |
| LLVM libc++, libc++abi, libunwind from the Zig 0.13.0 toolchain | Statically linked C++ runtime support | licenses/LLVM-libcxx-LICENSE.txt, LLVM-libcxxabi-LICENSE.txt, LLVM-libunwind-LICENSE.txt; Apache-2.0 with LLVM exceptions and the included legacy notices |
| MinGW-w64 support supplied by Zig 0.13.0 | Windows target runtime support | licenses/mingw-w64-COPYING.txt; the supplied notice includes exceptions for separately marked parts |
| Microsoft .NET Framework and Windows APIs | Required system libraries, not bundled in this project | Installed on the user's Windows system; no Microsoft binaries or fonts are redistributed here |

## Open-source development references

The development history records reading the following public projects. Retain their notices conservatively when distributing this project, including when source has been rewritten or adapted rather than copied verbatim.

- [Logersnamed/FreecamMod](https://github.com/Logersnamed/FreecamMod): camera frame/layout, movement, input and early path design references. MIT notice: licenses/FreecamMod-MIT.txt, Copyright (c) 2026 Logersnamed.
- [borgCode/SilkySouls3](https://github.com/borgCode/SilkySouls3): public Dark Souls III singleton addresses, camera layout and pause/time-scale locations. MIT notice: licenses/SilkySouls3-MIT.txt, preserving its original copyright line verbatim.

The historical ZIP downloads used repository branches without retaining an immutable commit identifier. Local license copies and reference-file hashes are available in the private audit. Do not describe a later repository HEAD as the exact version originally consulted.

## Other research and functional references

- [The Grand Archives Dark Souls III Cheat Table](https://github.com/The-Grand-Archives/Dark-Souls-III-CT-TGA), locally downloaded as v3.4.0: consulted to cross-check camera/HUD memory layout. The current project does not bundle the table, its Lua/AutoAssembler scripts, forms, or assets. No general open-source license was identified for the table during this review; this acknowledgment does not grant permission to copy or redistribute it.
- [Mod Engine 2](https://github.com/soulsmods/ModEngine2) and [Mod Engine 3](https://github.com/garyttierney/me3): launcher/source research. Current release does not bundle or launch either engine. Historical releases bundled ME3 separately and require their own license review if redistributed.
- Otis_Inf / Frans Bouma's Elden Ring Camera Tools 1.0.17: the user supplied a commercial compiled package as a reference for features, configuration and the EXE + injected DLL workflow. Its readme/configuration and supplied UI screenshots informed requirements. No permission to redistribute that package or relicense its code is asserted. IGCSClient.exe, EldenRingCameraTools.dll, ModernWpf, ToastNotifications, original screenshots and their assets are not included in this release.
- [FransBouma/InjectableGenericCameraSystem](https://github.com/FransBouma/InjectableGenericCameraSystem) is a separate public BSD-2-Clause project that was identified during research. Its public license must not be assumed to cover the commercial Elden Ring 1.0.17 package. No copy of IGCS source is present in the shipped source tree.

Game-specific addresses and short instruction signatures were also verified against a local Dark Souls III 1.15.2 image. No game executable, memory image, savefile, texture or official logo is included. Game titles identify the supported game; no publisher or camera-tool author endorsement is claimed.

The keyboard guide is drawn by KeyboardMap.cs with standard keyboard labels, revised bindings and installed system fonts. It does not embed the original user-supplied keyboard picture or its watermark. The application uses its own WinForms view rather than the commercial tool's WPF binaries, XAML, icons or other assets.
