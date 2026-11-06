# RiverDesk Toolkit

RiverDesk Toolkit 是 RiverXILee 开发的 Windows 桌面维护工具，界面使用简体中文。

- 空间清理可先扫描，再确认清理项目。
- 软件卸载调用已登记的卸载器，并列出需要人工确认的残留。
- 强制删除支持选择指定文件或目录，确认后删除并查看结果。
- 软件更新可检查 GitHub 正式版、查看说明、下载和校验安装包。

从 [GitHub Releases](https://github.com/ShallowThoughts/riverdesk-toolkit/releases) 下载 RiverDeskToolkit-Setup.exe。安装后从开始菜单打开，也可在 Windows「已安装的应用」中卸载。

## 构建和发布

使用 Windows .NET Framework 自带的 C# 编译器和 NSIS 3。运行 build.ps1 时可传入 -NsisCompiler 指定 makensis.exe。

VersionInfo.cs 中的 VersionInfo.Number 是版本来源。启动程序与功能库使用同一版本。构建结果包括启动程序 dist/RiverDeskToolkit.exe、功能库 dist/RiverDesk.Toolkit.dll 和安装包 dist/RiverDeskToolkit-Setup.exe。安装器会把 EXE、DLL 和 LICENSE.txt 一起部署到安装目录。EXE 与 DLL 需要配套使用。

发布时运行 build.ps1 -Sign，创建 v主版本.次版本.修订号 标签，并附上 dist/RiverDeskToolkit-Setup.exe 和 dist/update.txt。在线更新下载完整安装包，同时更新启动程序与功能库。更新清单使用独立 RSA 签名，程序内置公钥并验证安装包的 SHA-256。安装器的 Windows Authenticode 签名与更新清单签名是两件事。未指定 -Sign 的本地构建会移除旧的 dist/update.txt，避免误用与新安装包不匹配的更新清单。

发布签名私钥由发布账户保存，生成更新清单时通过 -Sign 使用。换电脑或换账户前需要先安排密钥迁移。生成新密钥会让旧安装拒绝更新。

液态玻璃测试版使用 build.ps1 -LiquidGlassBeta -Sign 构建，版本为 1.3.4-beta1，发布为 GitHub prerelease。正式版与测试版都通过安装包部署。测试版内的更新检查查询正式版。

运行 test-ui.ps1 检查普通界面，运行 test-ui.ps1 -LiquidGlassBeta 检查液态玻璃界面的绘制、导航键盘操作和展示文字行为。

导航图标使用 Windows 自带的 [Segoe Fluent Icons](https://learn.microsoft.com/windows/apps/design/iconography/segoe-fluent-icons-font)，兼容 Segoe MDL2 Assets。

## 许可证

本项目源码采用 GNU General Public License v3.0（GPL-3.0-only）。完整条款见 [LICENSE](LICENSE)。
