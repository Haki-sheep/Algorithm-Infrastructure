# MmECS 首版 Review 与验收审查

本轮完成约定的可复用框架首版 继续保留此前跳过 E 的决定 A 到 D 基础能力 F 核心快照和 G 核心诊断基准已交付 不是原始 A 到 G 加全部游戏业务的完成声明

历史范围说明 本报告记录的是 E 实现前的首版审查 用户随后要求完成 E 现已补齐战斗命令 Unity 展示和表现恢复 见 08-阶段E验收报告 本报告的性能观察和目标设备验收限制仍有效

## Review 顺序

| 顺序 | 文件 | 重点 |
|---|---|---|
| 1 | Assets/MmECS/Runtime/Core/W/World/World.Resources.cs | 世界单例归属 初始化注册限制 值复制 |
| 2 | Assets/MmECS/Runtime/Core/W/DeterministicRandom.cs | 显式状态 固定算法常量 拒绝采样 |
| 3 | Assets/MmECS/Runtime/Core/W/World/World.Snapshot.cs | 单例与随机状态接入既有世界快照 |
| 4 | Assets/MmECS/Runtime/Core/W/EntityQuery.cs 与 World/World.Query.cs | 条件复制去重 最小候选池 平局注册顺序 空 All 与冲突条件 |
| 5 | Assets/MmECS/Samples/Movement/Runtime/Simulation/Simulation.Lifecycle.cs | 一次初始化 系统列表固定 失败逆序释放 聚合异常 |
| 6 | Assets/MmECS/Samples/Movement/Runtime/Simulation/Simulation.Runner.cs 与 Simulation.Tick.cs | 沿用原调度器 TickCore 第一行 BeginSimulation 有限时间契约 |
| 7 | Assets/MmECS/Runtime/Core/W/World/World.Diagnostics.cs 与 W/StateDiagnostics.cs | 诊断投影 字段差异 浮点位模式 排除宿主 Epoch |
| 8 | Assets/MmECS/Samples/Movement/Runtime/Simulation/Simulation.Diagnostics.cs | 默认禁用的系统追踪 输入与结构计数 |
| 9 | Assets/MmECS/Editor/EcsAcceptanceReport.cs 与 EcsBenchmark.cs | 统一验收入口 真实结果落盘 基准方法和计数器校准 |

新类型及分部类均说明各自职责 旧代码与未提交迁移保留 不清理工作区中的无关变更

## Findings

### P2 结构缓冲与快照仍有明确分配成本

- 位置 Assets/MmECS/Runtime/Core/W/Structural/StructuralBuffer.Command.cs 的 Add Remove Destroy 和 StructuralBuffer.Spawn.cs 的 Spawn
- 证据 每次请求创建独立命令对象 快照 Clone 和 Restore 复制数组及字典值 未引入池化或增量快照
- 影响 高频结构变化和大量快照可能增加 GC 压力 工作负载规模影响成本
- 当前处理 保留可读的 FIFO 首版实现 使用已提供的变化比例和快照基准衡量 目标项目有预算后再决定命令池或分块复制 不凭编辑器耗时保证设备达标

### P2 当前运行时分配计数和目标平台验证有缺口

- 位置 Assets/MmECS/Editor/EcsBenchmark.cs 的 HasAllocationCounter 与 Measure
- 证据 Unity 2022.3.62f3 Editor Mono 中创建 4096 字节数组后 GC.GetAllocatedBytesForCurrentThread 增量仍为零 校准失败后报告标为不可用
- 影响 无法依据本轮结果声称零 GC 或给出精确分配预算 托管堆采样含整个编辑器进程且不是精确峰值
- 当前处理 保留可重跑的能力检测 未支持的指标明确缺失 设备预算和 IL2CPP 结果需在实际运行目标上测量

未发现阻塞首版功能交付的 P0 或 P1 问题 诊断反射和装箱只在显式读取或开启 Trace 时执行 不进入默认系统 Tick 路径

## Assumptions

- 单线程 同程序版本 同配置 同数学实现的内存快照 每个 World 由一个 Simulation 驱动
- 系统配置在初始化后保持不变 影响未来的业务变量存入组件或世界单例 不自动克隆系统私有对象
- unmanaged 组件和单例不包含裸指针或外部地址 ref 不跨结构提交 Tick 或恢复保存
- 结构提交失败不事务回滚 失败 World 不继续模拟 Snapshot 不能修复半完成 Tick
- Spawn 保持阶段 D 已约定的两种不同且已注册初始组件接口 通用查询支持任意数量包含和排除条件
- Trace 回调只观察且不抛错 状态诊断投影不代替 Snapshot 待提交结构命令内容不属于该投影
- E 的业务 Command 战斗 UI View 输出及恢复清理仍未实现 联网和跨版本存档不属于本轮

## Verification

统一入口 `MmECS.Editor.EcsAcceptanceReport.RunAll()` 菜单 `MmECS/运行首版全部验收与基准`

```powershell
unity command set_autotick --enable true --project-path 'E:\AAAA学习资料\Demo\顶点数' --format json
unity command recompile --project-path 'E:\AAAA学习资料\Demo\顶点数' --format json
unity command recompile_status --project-path 'E:\AAAA学习资料\Demo\顶点数' --format json
unity command eval --code 'return MmECS.Editor.EcsAcceptanceReport.RunAll();' --project-path 'E:\AAAA学习资料\Demo\顶点数' --format json
```

编译必须等待 completed 且 failed 为 false 再调用测试 最终执行结果和环境见 [06-首版自动测试报告](06-首版自动测试报告.md)

行为覆盖 3000 步存储模型对照 实体代际和槽位复用 包含排除查询 系统执行顺序 固定 Tick FIFO 结构提交与保护 输入冻结 延迟创建 池复制 世界和组合快照 外部句柄隔离 单例及随机重演 生命周期成功失败清理 状态诊断和追踪 共 14 组验收

基准对 1000 与 10000 实体分别测量 8 种工作负载 每项预热 5 次 正式 30 次 默认结构变化比例 10% 查询次数 1 创建项包含扩容成本 其余为构造后的稳态测量 本轮未测编辑器进程冷启动

静态检查包括模块范围 git diff --check 类型职责 summary 超过 200 行脚本的 region Runtime 引擎引用和 asmdef 依赖 未修改 Luban 生成代码 Unity 序列化资源或项目生成文件

最终 Pipeline 编译状态 completed 且 failed 为 false 自动入口返回 ECS acceptance 14/14 passed 实际程序集反射结果 MmECS.Core 仅引用 netstandard Game.Simulation 仅引用 netstandard 与 MmECS.Core 模块范围 diff 检查通过 类型职责和长文件 region 检查未发现遗漏

时间边界修复记录

- 问题 无穷经过时间无法通过减去有限步长退出循环 NaN 会污染累计余时
- 修复了什么 拒绝非有限和负经过时间 固定步长必须能表示为有限正 float
- 如何测试修复是否完成 LifecycleAcceptance 验证 NaN 无穷 负时间 零及不可表示步长被拒绝 随后正常时间仍可推进

## Verdict

条件通过

首版功能通过自动验收 剩余条件是分配指标和目标设备性能预算 不将缺失的 E 业务切片计入完成比例 完整测试和基准输出以 06 报告为准
