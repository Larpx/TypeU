# Tasks

## 阶段一：基础设施搭建

- [ ] Task 1: 创建解决方案与项目骨架
  - [ ] SubTask 1.1: 创建 `TypeU.sln` 与 8 个项目（Core/Models/Network/Data/Services/Teacher.GUI/Student.GUI/Tests）
  - [ ] SubTask 1.2: 配置 `Directory.Build.props` 统一 .NET 10、Nullable、TreatWarningsAsErrors、LangVersion latest；禁用顶级语句（`ImplicitUsings=disable` 并在所有项目强制 Main 入口）
  - [ ] SubTask 1.3: 配置统一命名空间规则：所有代码命名空间以 `Larpx.PersonalTools.TypeU.` 开头，命名空间需包裹所有代码
  - [ ] SubTask 1.4: 添加 NuGet 引用（Avalonia、protobuf-net、Dapper、SQLite、MiniExcel、Serilog、CommunityToolkit.Mvvm）
  - [ ] SubTask 1.5: 配置 AOT 发布属性（PublishAot=true、StripSymbols=true、不生成 PDB）
  - **verify**: `dotnet build` 通过，0 错误 0 警告；全代码库无顶级语句，命名空间均以 `Larpx.PersonalTools.TypeU.` 开头

- [ ] Task 2: 搭建 MVVM 与 DI 基础设施
  - [ ] SubTask 2.1: 在 Core 项目创建 `IServiceCollection` 扩展与 `AppBootstrapper`
  - [ ] SubTask 2.2: 配置 Serilog 日志（文件 + 控制台，按日滚动）
  - [ ] SubTask 2.3: 在 GUI 项目创建 Avalonia `App.axaml` 与 ViewModel 基类
  - **verify**: 教师端与学生端可启动空窗口并写入日志

## 阶段二：核心库实现

- [ ] Task 3: 实现数据模型层（TypeU.Models）
  - [ ] SubTask 3.1: 定义实体类（Student、Question、ExamSession、ExamRecord、NonceCache）
  - [ ] SubTask 3.2: 定义 DTO（LoginDto、QuestionDto、ExamResultDto、StatusReportDto）
  - [ ] SubTask 3.3: 使用 protobuf-net Source Generator 标注 ProtoContract
  - **verify**: 单元测试覆盖序列化/反序列化往返

- [ ] Task 4: 实现通信协议层（TypeU.Network）
  - [ ] SubTask 4.1: 定义 `Packet` 结构与 `PacketReader/PacketWriter`
  - [ ] SubTask 4.2: 实现 AES-256-CBC 加解密包装器
  - [ ] SubTask 4.3: 实现 HMAC-SHA256 签名与校验
  - [ ] SubTask 4.4: 实现 Nonce 缓存与时间戳校验（防重放）
  - [ ] SubTask 4.5: 实现异步 TCP 服务端 `TcpExamServer`（支持 100 并发）
  - [ ] SubTask 4.6: 实现异步 TCP 客户端 `TcpExamClient`（含断线重连）
  - [ ] SubTask 4.7: 定义 `TimeSyncMessage` 消息类型，实现教师端定时（建议 10 秒）下发服务器时间，学生端按教师端时间计算考试剩余时长，防止本地改时间作弊
  - [ ] SubTask 4.8: 实现 UDP 广播自动发现机制：教师端定时（建议 5 秒）广播自身 IP+TCP 端口；学生端启动时监听 UDP 广播，收到后自动连接教师端；广播包同样走加密+签名流程
  - **verify**: 单元测试覆盖协议编解码、签名校验、防重放拦截；集成测试客户端↔服务端连通；时间同步消息可在 100 节点下稳定广播；学生端启动后可在 5 秒内自动发现并连接教师端

- [ ] Task 5: 实现数据访问层（TypeU.Data）
  - [ ] SubTask 5.1: 配置 SQLite 连接与 Dapper 仓储基类
  - [ ] SubTask 5.2: 实现建表脚本与迁移机制
  - [ ] SubTask 5.3: 实现 StudentRepository / QuestionRepository / ExamRepository / NonceRepository
  - **verify**: 单元测试覆盖 CRUD 与设备绑定过期逻辑

- [ ] Task 6: 实现核心服务层（TypeU.Core）
  - [ ] SubTask 6.1: 实现 `DeviceFingerprintProvider`（CPU ID + MAC + 硬盘序列号哈希）
  - [ ] SubTask 6.2: 实现 `AntiCheatMonitor`（输入时间差检测 < 30ms、批量上屏处理）
  - [ ] SubTask 6.3: 实现速度异常滑动窗口监控
  - [ ] SubTask 6.4: 实现剪贴板与右键菜单拦截器
  - **verify**: 单元测试覆盖批量上屏场景、设备指纹稳定性

## 阶段三：业务服务实现

- [ ] Task 7: 实现教师端业务服务（TypeU.Services）
  - [ ] SubTask 7.1: `TeacherExamService`：开始/暂停/停止/重新考试，加密下发试题；重新考试需重置学生端状态与本地草稿
  - [ ] SubTask 7.2: `QuestionService`：题库 CRUD + TXT 导入
  - [ ] SubTask 7.3: `MonitoringService`：实时状态聚合与异常预警分发
  - [ ] SubTask 7.4: `GradeService`：收卷解密验证 + Excel 导出（MiniExcel）
  - [ ] SubTask 7.5: `DeviceBindingService`：查看绑定/强制解绑
  - [ ] SubTask 7.6: `LanDiscoveryService`：局域网一键扫描，发现所有在线电脑（IP/MAC/计算机名/是否已安装学生端），区分"已发现学生端"与"未安装学生端的设备"
  - [ ] SubTask 7.7: `TimeSyncService`：定时（10 秒）向所有在线学生端广播教师端时间，作为考试倒计时的唯一时间基准
  - [ ] SubTask 7.8: `TeacherDiscoveryService`：定时（5 秒）通过 UDP 广播教师端 IP+TCP 端口，供学生端自动发现；启动监听即开始广播，停止监听即停止广播
  - **verify**: 单元测试覆盖考试流程（含重新考试）、TXT 导入、Excel 导出格式、局域网扫描结果聚合、UDP 广播定时触发

- [ ] Task 8: 实现学生端业务服务（TypeU.Services）
  - [ ] SubTask 8.1: `StudentAuthService`：签到登录 + 设备绑定校验
  - [ ] SubTask 8.2: `TypingTestService`：原文比对、实时高亮、速度/正确率计算
  - [ ] SubTask 8.3: `StatusReportService`：1-2 秒定时上报 + 断线缓存补传
  - [ ] SubTask 8.4: `ResultSubmitService`：考试结束打包加密成绩回传
  - [ ] SubTask 8.5: `ClientTimeSyncService`：接收教师端时间广播，按教师端时间计算倒计时与考试起止，忽略本地系统时间修改
  - [ ] SubTask 8.6: `StudentDiscoveryService`：启动时监听 UDP 广播自动发现教师端并连接；超时未发现时触发手动输入教师端 IP 的兜底流程
  - **verify**: 单元测试覆盖原文比对、速度计算、断线补传、时间同步覆盖本地时间被修改场景、自动发现超时兜底

## 阶段四：UI 实现

- [ ] Task 9: 实现教师端 UI（TypeU.Teacher.GUI）
  - [ ] SubTask 9.1: 主窗口布局（左侧导航 + 顶部工具栏 + 中间看板 + 右侧异常浮窗）
  - [ ] SubTask 9.2: 学生列表与监控看板（状态色块：灰/绿/蓝/红）
  - [ ] SubTask 9.3: 题库管理界面（列表/编辑/TXT 导入）
  - [ ] SubTask 9.4: 考试控制面板（模式选择/时长设置/开始/暂停/停止/重新考试）
  - [ ] SubTask 9.5: 成绩统计与 Excel 导出界面
  - [ ] SubTask 9.6: 设备绑定管理界面（绑定时间/剩余时长/强制解绑）
  - [ ] SubTask 9.7: 局域网扫描界面（一键扫描按钮、设备列表展示 IP/MAC/计算机名/学生端状态、可手动添加未识别设备 IP）
  - [ ] SubTask 9.8: 明暗主题切换
  - **verify**: 手动验证各界面交互流程；30 节点并发监控无卡顿；局域网扫描结果可正确分类展示

- [ ] Task 10: 实现学生端 UI（TypeU.Student.GUI）
  - [ ] SubTask 10.1: 登录签到界面（学号 + 姓名）；含自动发现状态提示与手动输入教师端 IP 的兜底入口
  - [ ] SubTask 10.2: 沉浸式考试界面（全屏置顶、禁止移动/最小化）
  - [ ] SubTask 10.3: 原文区 + 输入区（行号、光标高亮、字号 18-20px、实时绿/红高亮）
  - [ ] SubTask 10.4: 右上角悬浮圆形进度仪表盘（剩余时间/实时速度）
  - [ ] SubTask 10.5: 剪贴板/右键/拖拽拦截接入
  - **verify**: 手动验证沉浸模式、高亮对比、防粘贴；输入法批量上屏告警；自动发现失败时手动输入 IP 可正常连接

## 阶段五：联调与发布

- [ ] Task 11: 端到端联调
  - [ ] SubTask 11.1: 教师端 ↔ 多学生端签到绑定流程
  - [ ] SubTask 11.2: 三种考试模式全流程（下发/答题/上报/收卷/导出）
  - [ ] SubTask 11.3: 重新考试流程验证（重置学生端状态、清空草稿、重新下发试题）
  - [ ] SubTask 11.4: 局域网扫描验证（一键扫描发现所有设备、区分已装/未装学生端、手动添加 IP）
  - [ ] SubTask 11.5: 时间同步验证（教师端广播后学生端倒计时一致；学生端修改本地时间不影响考试计时）
  - [ ] SubTask 11.6: 双端自动发现验证（学生端启动后 5 秒内自动发现并连接教师端；自动发现失败时手动输入 IP 兜底可用）
  - [ ] SubTask 11.7: 断线重连与数据补传验证
  - [ ] SubTask 11.8: 防作弊场景验证（复制粘贴拦截、批量上屏、速度异常）
  - [ ] SubTask 11.9: 防重放/防篡改攻击验证
  - **verify**: 联调清单全部通过；100 节点压力测试稳定；时间同步误差 < 1 秒；自动发现成功率 > 95%

- [ ] Task 12: 跨平台发布配置
  - [ ] SubTask 12.1: Windows AOT 发布配置（Teacher.exe / Student.exe）
  - [ ] SubTask 12.2: Linux AOT 发布配置（原生 ELF）
  - [ ] SubTask 12.3: 发布脚本与产物校验（无 PDB、符号剥离）
  - [ ] SubTask 12.4: Win7 兼容性说明文档（补丁清单）
  - **verify**: 双平台产物启动正常；ILSpy 反编译无源码可见

- [ ] Task 13: 文档同步
  - [ ] SubTask 13.1: 将本方案 spec.md 同步至 `docs/项目方案.md` 作为正式版
  - [ ] SubTask 13.2: 补充 README 与各项目说明文档
  - **verify**: docs 目录文档齐全且与实现一致

# Task Dependencies

- Task 2 依赖 Task 1
- Task 3 依赖 Task 1（可并行于 Task 2）
- Task 4 依赖 Task 1、Task 3
- Task 5 依赖 Task 1、Task 3
- Task 6 依赖 Task 1、Task 3
- Task 7、Task 8 依赖 Task 4、Task 5、Task 6
- Task 9、Task 10 依赖 Task 7、Task 8
- Task 11 依赖 Task 9、Task 10
- Task 12 依赖 Task 11
- Task 13 依赖 Task 12

# 并行机会

- 阶段二中 Task 3 →（Task 4 / Task 5 / Task 6）可三路并行
- 阶段三中 Task 7 与 Task 8 可并行
- 阶段四中 Task 9 与 Task 10 可并行
