# TypeU Win7 兼容性说明

## 结论

**TypeU 不支持 Windows 7。** 最低系统要求为 Windows 10 1809（17763）或更高版本。

## 不兼容原因

### 1. .NET 10 运行时要求

TypeU 基于 .NET 10 构建，.NET 10 官方支持的 Windows 最低版本为 **Windows 10 1809**。

参考：[.NET 10 支持的操作系统](https://learn.microsoft.com/dotnet/core/install/windows)

Win7 无法安装 .NET 10 运行时，也无法运行 .NET 10 编译的任何程序（包括 Native AOT 和 JIT 模式）。

### 2. Native AOT 编译要求

TypeU 使用 Native AOT 发布（`PublishAot=true`），AOT 编译产物依赖 Windows 10+ 的系统 API：

- **UCRT**（Universal C Runtime）：Win7 默认不包含，需额外安装 KB2999226。但即使安装 UCRT，AOT 产物的 API Set 依赖仍无法满足。
- **API Set**：AOT 产物引用的 `api-ms-win-*.dll` 在 Win7 上缺失。这些是 Windows 10+ 引入的虚拟 DLL 重定向机制，Win7 内核不支持。
- **TLS 1.3**：网络层默认使用 TLS 1.3，Win7 仅支持 TLS 1.2（需 KB4019276）。虽然 TypeU 不使用 TLS，但底层系统库可能依赖。

### 3. Avalonia UI 12 渲染后端

Avalonia 12 在 Windows 上使用 ANGLE（DirectX 11 via OpenGL ES），DXGI 1.2+ 在 Win7 上不可用。即使强行运行，渲染层将崩溃。

### 4. 技术栈完整依赖链

```
TypeU 可执行文件
  ├── .NET 10 Native AOT 产物 → 需要 Windows 10 1809+ API Set
  ├── Avalonia 12 → 需要 ANGLE/DXGI 1.2+
  ├── protobuf-net 3.2 → 依赖 .NET 10 运行时
  ├── Dapper 2.1 → 依赖 .NET 10 运行时
  └── SQLite (Microsoft.Data.Sqlite) → 依赖 .NET 10 运行时
```

以上每个组件都直接或间接依赖 Windows 10+，不存在"只关闭 AOT 就能在 Win7 上运行"的路径。

## 非 AOT 发布（用于其他兼容性场景）

虽然非 AOT 模式仍无法在 Win7 上运行（因 .NET 10 运行时本身不支持 Win7），但可用于其他兼容性场景（如某些精简版 Windows 10 或 Linux 变体）。

使用 `--no-aot` 参数发布非 AOT 版本：

```powershell
# Windows: 非 AOT 单文件发布（需要目标机器安装 .NET 10 运行时）
.\scripts\publish.ps1 -NoAot

# 或手动指定
dotnet publish src/TypeU.Teacher.GUI/TypeU.Teacher.GUI.csproj `
    -c Release -r win-x64 `
    -p:PublishSingleFile=true `
    -p:PublishAot=false `
    -p:IncludeNativeLibrariesForSelfExtract=true
```

## 若必须支持 Win7 的降级方案

> 以下方案需要重写全部代码，不推荐。

| 方案 | 改动量 | 说明 |
|------|--------|------|
| 降级到 .NET Framework 4.8 + WPF | 全部重写 | Win7 SP1 自带 .NET 4.8，但需重写所有项目文件、替换 Avalonia 为 WPF、替换 protobuf-net 为旧版、替换所有 .NET 10 专有 API |
| 降级到 .NET 8 + Avalonia 11 + 非 AOT | 大量修改 | .NET 8 同样不支持 Win7，无效 |
| 虚拟机/远程桌面方案 | 部署方案 | 在 Win7 上通过 RDP 连接到 Win10+ 虚拟机运行 TypeU |

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
- [ ] 防火墙放行 TCP 5700（教师端监听）/ UDP 5800（学生端发现广播）
- [ ] 子网内允许 UDP 广播（255.255.255.255 或子网定向广播）
- [ ] 教师端与学生端时钟同步（NTP，用于初始握手；考试期间以教师端时间为准）
