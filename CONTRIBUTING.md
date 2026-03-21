# 参与贡献

感谢你对 inputor 的兴趣！以下是参与贡献的基本指南。

## 提交 Issue

- **Bug 报告**：请描述复现步骤、预期行为、实际行为，以及操作系统版本和输入法信息
- **功能建议**：请说明使用场景和期望的行为
- **请勿**在 Issue 中粘贴任何包含个人输入内容的截图或日志

## 提交 Pull Request

1. Fork 本仓库并基于 `master` 创建分支
2. 遵循现有代码风格（参见 [AGENTS.md](AGENTS.md) 中的代码风格说明）
3. 保持改动最小化，避免在修复 Bug 时顺带重构
4. 提交前确保 `dotnet build inputor.sln` 通过
5. PR 描述中说明改动的原因和内容

## 代码风格要点

- 文件作用域命名空间，每文件一个顶级类
- 私有字段：`_camelCase`；属性/方法：`PascalCase`
- 启用了可空引用类型，使用 `is null` / `is not null` 检查
- OS、文件系统、UIA 调用需有防御性错误处理
- 不在监控线程直接更新 WinUI 控件，通过 `DispatcherQueue` 回到 UI 线程

## 隐私守则

所有贡献**必须**遵守以下原则：

- 不将原始输入文本写入磁盘、日志、导出或任何持久化存储
- 密码字段必须保持排除状态
- 不引入任何形式的网络请求或遥测

## 开发环境

- .NET 8 SDK
- Windows 10 SDK（WinUI 3 需要）
- Visual Studio 2022 或 JetBrains Rider（推荐）
- 可选：[Inno Setup 6](https://jrsoftware.org/isinfo.php)（用于构建安装包）
- 可选：[just](https://github.com/casey/just)（用于运行 `justfile` 中的快捷命令）
