# inputor

> Windows 平台隐私安全的中英文输入统计工具

[English](README.en.md)

---

inputor 在后台静默统计你每天在各应用中输入的中文和英文字符数量，帮助你了解自己的输入习惯与工作模式。它**从不记录原始文本**，只保留字符计数和元数据。

## 功能特性

- **按应用统计** — 分别追踪每个应用的中英文输入量
- **趋势与热力图** — 直观展示输入量随时间的变化趋势
- **支持主流输入法** — 兼容搜狗拼音、微软拼音等 IME 的组合输入
- **系统托盘集成** — 最小化到托盘，不打扰日常使用
- **CSV 导出** — 将统计数据导出到 `%Documents%\inputor-exports`
- **本地存储** — 所有数据保存在本机 `%LocalAppData%\inputor`，不联网

## 隐私承诺

inputor 的核心设计原则是**零原始文本持久化**：

- 原始输入文本仅在内存中短暂用于快照差值计算
- 磁盘、日志、导出文件中**只存在字符计数、应用名称和日期分桶统计**
- 密码输入框自动排除在外
- 不收集任何遥测数据，不访问网络

## 已知限制

- 统计依赖目标控件通过 Windows UI Automation 暴露文本内容，部分自定义编辑器可能无法被识别
- 管理员权限窗口（UAC 弹窗等）无法被监控
- 密码输入框自动排除

## 安装

在 [Releases](../../releases) 页面下载最新版本：

| 文件 | 说明 |
|------|------|
| `inputor-x.x.x-setup-win-x64.exe` | 安装包（推荐） |
| `inputor-x.x.x-portable-win-x64.zip` | 便携版，解压即用 |

**系统要求**：Windows 10 1809 或更高版本，x64

> 安装包会自动部署 Windows App Runtime 依赖项。便携版同理。

## 从源码构建

**依赖**：.NET 8 SDK，Windows 10 SDK

```bash
git clone https://github.com/shiquda/inputor.git
cd inputor
dotnet restore inputor.sln
dotnet build inputor.sln
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj
```

构建发布包（需要安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)）：

```bash
just publish
```

输出位于 `artifacts/publish/`。

## CLI 探针

```bash
# 统计字符数
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --count-sample "Hello世界"

# 模拟 IME 组合输入序列
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-sequence "你|你好|你好世|你好世界"

# 模拟粘贴检测
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-paste "Hello" "Hello World" "World"

# 模拟批量加载过滤
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-bulk 12 "Hello world" "Edit" false
```

## 参与贡献

欢迎提交 Issue 和 Pull Request，请先阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 许可证

本项目基于 [GNU General Public License v3.0](LICENSE) 发布。
