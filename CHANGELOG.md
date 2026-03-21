# Changelog

本文件记录 inputor 的版本变更历史，格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)。

---

## [Unreleased]

## [0.1.0] - 2026-03-21

首个公开版本，完整的 WinUI 3 实现。

### 新增

- **监控核心**：通过 Windows UI Automation (FlaUI UIA3) 监控前台控件文本变化
- **字符计数**：基于可见文本正增量的中英文字符统计，支持主流 IME 组合输入
- **IME 兼容**：`CompositionAwareDeltaTracker` 修复退格过计数问题，兼容 TSF 协议
- **粘贴检测**：识别并过滤大段粘贴操作，避免计数虚高
- **批量加载过滤**：过滤应用启动时文本加载导致的计数膨胀
- **应用聚合**：支持按标签将多个进程归并为同一应用统计
- **系统托盘**：托盘图标集成，含仪表盘快捷入口、暂停、退出操作
- **主界面**：NavigationView 导航框架，含概览、统计、应用、调试、设置页
- **统计页**：趋势折线图、日历热力图、字符分布图表
- **设置页**：自动保存设置、版本与渠道信息展示、统计来源切换
- **CSV 导出**：导出统计数据到 `%Documents%\inputor-exports`
- **本地持久化**：JSON 文件存储至 `%LocalAppData%\inputor`
- **主题支持**：跟随系统深色/浅色模式，一致的窗口配色
- **国际化**：中英文界面本地化
- **开机自启**：可选的开机自动启动支持
- **发布流水线**：GitHub Actions 自动构建 + Inno Setup 安装包 + 便携 ZIP
- **CLI 探针**：`--count-sample`、`--simulate-sequence`、`--simulate-paste`、`--simulate-bulk` 测试命令

### 已知限制

- 依赖目标控件通过 UIA 暴露文本，部分自定义编辑器不支持
- 管理员权限窗口无法被监控（UAC 限制）
- 密码输入框自动排除

[Unreleased]: https://github.com/shiquda/inputor/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/shiquda/inputor/releases/tag/v0.1.0
