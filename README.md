# RiverDesk Toolkit

RiverDesk Toolkit 是 RiverXILee 开发的 Windows 桌面维护工具，界面使用简体中文。

- 空间清理可先扫描，再确认清理项目。
- 软件卸载调用已登记的卸载器，并列出需要人工确认的残留。
- 强制删除支持选择指定文件或目录，确认后删除并查看结果。
- 软件更新可检查 GitHub 正式版、查看说明、下载和校验安装包。

从 [GitHub Releases](https://github.com/ShallowThoughts/riverdesk-toolkit/releases) 下载 RiverDeskToolkit-Setup.exe。安装后从开始菜单打开，也可在 Windows「已安装的应用」中卸载。

## 构建和发布

使用 Windows .NET Framework 自带的 C# 编译器和 NSIS 3。运行 build.ps1 时可传入 -NsisCompiler 指定 makensis.exe。

UpdateForm.cs 中的 VersionInfo.Number 是版本来源。发布时创建 v主版本.次版本.修订号 标签，并附上 dist/RiverDeskToolkit-Setup.exe 和 dist/update.txt。更新清单使用独立 RSA 签名，程序内置公钥并验证下载文件的 SHA-256。安装器的 Windows Authenticode 签名与更新清单签名是两件事。

发布签名私钥由发布账户保存，生成更新清单时通过 -Sign 使用。换电脑或换账户前需要先安排密钥迁移。生成新密钥会让旧安装拒绝更新。

导航图标使用 Windows 自带的 [Segoe Fluent Icons](https://learn.microsoft.com/windows/apps/design/iconography/segoe-fluent-icons-font)，兼容 Segoe MDL2 Assets。

## 许可证

本项目源码采用 GNU General Public License v3.0（GPL-3.0-only）。完整条款见 [LICENSE](LICENSE)。
