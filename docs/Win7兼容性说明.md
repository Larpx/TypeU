# TypeU Win7 兼容性说明

## 结论

**TypeU 不支持 Windows 7。** 最低系统要求为 Windows 10 1809（17763）或更高版本。

## 不兼容原因

### 1. .NET 10 运行时要求

TypeU 基于 .NET 10 构建，.NET 10 官方支持的 Windows 最低版本为 Windows 10 1809。

参考：[.NET 10 支持的操作系统](https://learn.microsoft.com/dotnet/core/install/windows)

### 2. Native AOT 编译要求

TypeU 使用 Native AOT 发布（`PublishAot=true`），AOT 编译产物依赖 Windows 10+ 的系统 API：

- **UCRT**（Universal C Runtime）：Win7 默认不包含，需额外安装 KB2999226。
- **API Set**：AOT 产物引用的 `api-ms-win-*.dll` 在 Win7 上缺失。
- **TLS 1.3**：网络层默认使用 TLS 1.3，Win7 仅支持 TLS 1.2（需 KB4019276）。

### 3. Avalonia UI 12 渲染后端

Avalonia 12 在 Windows 上使用 ANGLE（DirectX 11 via OpenGL ES），DXGI 1.2+ 在 Win7 上不可用。

## 若必须支持 Win7 的降级方案

> 不推荐。以下方案会失去 AOT 性能优势与 .NET 10 新特性。

1. **降级到 .NET Framework 4.8**：Win7 SP1 自带 .NET 4.8，但需重写所有项目文件与部分 API。
2. **改用 WPF**：Avalonia → WPF，绑定方式与 XAML 语法需重写。
3. **关闭 AOT**：保留 .NET 10 但使用 ReadyToRun + 单文件发布，仍需安装 .NET 10 运行时（Win7 不支持）。

## Windows 10 部署清单

| 组件 | 最低版本 | 备注 |
|------|---------|------|
| Windows 10 | 1809 (17763) | LTSC 2019 起支持 |
| .NET 10 运行时 | 无需安装 | AOT 产物自包含 |
| VC++ 运行时 | 2015-2022 | AOT 产物依赖 VCRedist |
| DirectX | 11.0 | Avalonia ANGLE 渲染 |
| 网络协议 | TCP/UDP | 默认 Windows 网络栈 |

## 部署前检查清单

- [ ] 目标机器为 Windows 10 1809+
- [ ] 已安装 VC++ Redistributable 2015-2022
- [ ] 防火墙放行 TCP 5700（教师端）/ TCP 5800（学生端 UDP 监听）
- [ ] 子网内允许 UDP 广播（255.255.255.255 或子网定向广播）
- [ ] 教师端与学生端时钟同步（NTP，用于初始握手；考试期间以教师端时间为准）
