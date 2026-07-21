# TypeU

局域网打字考试系统。教师端统一管控考试流程，学生端完成打字测试与成绩上报，面向大学机房场景（约 30–100 台终端）。

**作者**：Larpx  
**许可**：[MIT License](LICENSE)（允许二次开发，须保留原作者版权与许可声明）

## 快速开始

```bash
# 需要 .NET 10 SDK
dotnet build TypeU.slnx
dotnet run --project src/TypeU.Teacher.GUI
dotnet run --project src/TypeU.Student.GUI
dotnet test src/TypeU.Tests
```

## 文档

| 文档 | 说明 |
| :--- | :--- |
| [项目介绍](docs/项目介绍.md) | 背景、功能、架构与技术选型 |
| [二次开发指南](docs/二次开发指南.md) | 环境、目录约定、扩展方式与发布 |
| [需求文档](需求文档.md) | 产品需求（PRD） |
| [技术方案 Spec](.trae/specs/design-typeu-solution/spec.md) | 详细规格与验收场景 |

## 技术栈（摘要）

.NET 10 · Avalonia UI · TCP + 自定义二进制协议 · SQLite · Native AOT
