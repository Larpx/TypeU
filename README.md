# TypeU

局域网打字考试系统。教师端统一管控考试流程，学生端完成打字测试与成绩上报，面向大学机房场景（约 30–100 台终端）。

**作者**：Larpx  
**许可**：[MIT License](LICENSE)（允许二次开发，须保留原作者版权与许可声明）

## 快速开始

```bash
# 需要 .NET 10 SDK（目标平台：Windows 10 1809+ / Linux x64，不支持 Win7）
dotnet build TypeU.slnx
dotnet run --project src/TypeU.Teacher.GUI
dotnet run --project src/TypeU.Student.GUI
dotnet test src/TypeU.Tests

# Native AOT 发布
./scripts/publish-aot.ps1 -Runtime win-x64
```

## 考试流程（摘要）

- 学生默认单机；配置教师 IP 后 Ping→TCP→Hello
- 仅教师「开始考试」后可登录；登录后禁止自行退出
- 重考 1–5 次，每次成绩提交；导出含各次成绩与最高成绩
- 结束考试后学生可自选登出；教师可单独「允许登出」
- 开考会话/登录名单/成绩持久化，教师端可恢复未结束考试

## 文档

| 文档 | 说明 |
| :--- | :--- |
| [项目介绍](docs/项目介绍.md) | 背景、功能、架构与技术选型 |
| [项目方案](docs/项目方案.md) | 总体技术方案规格与验收场景 |
| [需求文档](docs/需求文档.md) | 产品需求（PRD） |

## 技术栈（摘要）

.NET 10 · Avalonia UI 12 · TCP + 自定义二进制协议 · SQLite · Native AOT
