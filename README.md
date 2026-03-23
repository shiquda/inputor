# inputor

[English](README.en.md)

---

inputor 是一款帮助你了解自己每日输入行为的 Windows 工具。打开 inputor 并挂在后台，它就会静默统计你每天在各应用打了多少字。利用 inputor 的统计功能，就可以多维度地观察这些数据的趋势和分布。

inputor **不记录原始文本**，只保留字符计数和元数据，保护您的隐私安全。

![应用分布统计截图](imgs/stats-page.png)

## 功能特性

- **按应用统计** — 分别追踪每个应用的输入量
- **趋势与热力图** — 直观展示输入量随时间的变化趋势
- **支持中英文统计** — 利用启发式算法，兼容微软拼音等中文输入法的字数统计
- **应用标签与快捷操作** — 可为应用打标签，并直接隐藏高噪声应用、编辑别名或调整分组
- **备份恢复与统计源管理** — 支持导出 ZIP 备份、恢复统计数据与设置，或切换到指定 JSON 统计文件
- **调试日志落盘** — 调试页支持将事件流追加到本地文本文件，便于排查统计异常
- **系统托盘集成** — 最小化到托盘，不打扰日常使用
- **保护隐私** — 所有数据保存在本机 `%LocalAppData%\inputor`，不联网

## 安装

### 方式一：下载预编译版本

> 适合想要体验 inputor 的普通用户。

在 [Releases](https://github.com/shiquda/inputor/releases) 页面下载最新版本：

| 文件 | 说明 |
|------|------|
| `inputor-x.x.x-setup-win-x64.exe` | 安装包（推荐） |
| `inputor-x.x.x-portable-win-x64.zip` | 便携版，解压即用 |

**系统要求**：Windows 10 1809 或更高版本，x64

> 安装包会自动部署 Windows App Runtime 依赖项。便携版同理。

### 方式二：从源码构建

> 适合开发者或需要定制功能的用户。

**依赖**：.NET 8 SDK，Windows 10 SDK

```bash
git clone https://github.com/shiquda/inputor.git
cd inputor
dotnet restore inputor.sln
dotnet build inputor.sln
just dev
```

若本机未安装 `just`，可退回：

```bash
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj
```

构建发布包（需要安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)）：

```bash
just publish
```

构建输出位于 `artifacts/publish/`。

## 使用指南

参见 [用户手册](./docs/user-guide.md)。

## 隐私承诺

inputor 的核心设计原则是**零原始文本持久化**：

- 原始输入文本仅在内存中短暂用于快照差值计算
- 磁盘、日志、导出文件中默认**只存在字符计数、应用名称和日期分桶统计**
- 密码输入框自动排除在外
- **不收集任何遥测数据，不访问网络**

> 例外：调试页支持用户主动启用“磁盘调试日志”，并可进一步选择是否包含原始输入文本；该选项默认关闭，仅用于本地排障，使用后建议手动删除日志文件。

## 已知限制

- 统计依赖目标控件通过 Windows UI Automation 暴露文本内容，部分自定义编辑器可能无法被识别
- 管理员权限窗口（UAC 弹窗等）无法被监控

## 参与贡献

本项目正处于活跃开发阶段，欢迎您以任何形式参与贡献！开发者非常期待各位的反馈，无论是 Bug 反馈还是功能请求。

您可以以多种方式参与项目的贡献，包括但不限于：

- 简单地给本项目点 Star；
- 把本项目分享给其他有需要的人；
- 提交 Issue；
- 提交 Pull Request。

若您需要贡献代码，请先阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 许可证

本项目基于 [GNU General Public License v3.0](LICENSE) 发布。

---

### 我为什么开发 inputor？

我只是好奇我每天给 AI 类应用打了多少字。
