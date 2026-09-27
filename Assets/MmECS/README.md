# MmECS 当前模块边界

每个类及嵌套类型使用 XML summary 说明当前职责 分部类按所在文件说明实际负责的边界 接口与数据结构同样说明契约或数据含义 后续新增文件沿用此规范

## 目录与程序集

| 目录 | 程序集 | 职责 |
|---|---|---|
| Runtime/Core | MmECS.Core | 实体 组件 查询 结构缓冲 单例 随机源 世界快照与诊断 |
| Samples/Movement/Runtime | Game.Simulation | 移动及战斗样例 输入命令 固定步调度与组合快照 |
| Samples/Combat/Unity | Game.Combat.UnityAdapter | 战斗样例界面 角色血条 飘字与输入转换 |
| Editor | MmECS.Editor | 自动验收 基准和报告菜单 |

Core 命名空间保持 MmECS 样例保持 Game.Simulation

Core 与 Game.Simulation 均为纯 C# 不引用 UnityEngine 或第三方插件 Game.Simulation 只依赖 Core 独立 Unity 适配层引用引擎和 UGUI 验收程序集额外依赖 UnityEditor 与适配层

## 复用与接入

按 Assets 源码模块使用 通用部分位于 Runtime/Core 移动样例按需保留 无需额外第三方依赖

现有 Simulation 负责累计引擎传入的时间并执行固定 Tick 不新增 FixedTickDriver

Core 的 AssemblyInfo 通过 InternalsVisibleTo 授权 Game.Simulation 调用内部 Tick 入口 更换宿主程序集名称时需同步调整授权 该授权开放所有 internal 成员 宿主属于受信任调度代码

## 当前验收

GC 分配补测通过 AgentScripts/ProbeMmEcsAllocation.cs 执行 该脚本在 Pipeline 中临时编译并读取 Profiler 原始 GC.Alloc 大小元数据 以空操作和已知数组分配校准 预热后查询 移动及单移动系统 Tick 的五个样本均为零分配 结构变化及快照有实际分配 详见 Plan/UserPlan/MmECS设计架构/10-GC分配专项报告.md 原线程计数接口仍不可用

2026-09-26 G 移动访问优化后统一入口为 16 组且全部通过 E 场景再次通过 新增 ComponentLookup 绑定访问验收与同工作量移动基准 结果及测量限制见 Plan/UserPlan/MmECS设计架构/09-性能优化报告.md

2026-09-26 框架首版先完成 14 组验收 随后按用户要求完成 E 战斗样例 新增纯模拟战斗验收和真实 Unity 场景验收 统一入口现包含 15 组纯模拟验收 场景结果见 Plan/UserPlan/MmECS设计架构/08-阶段E验收报告.md 基准限制仍见 06 与 07 报告

Create 已允许初始化或内部结构提交阶段调用 运行期直接调用仍被拒绝 Spawn<TFirst,TSecond> 已支持两种不同且已注册的 unmanaged 初始组件 按 FIFO 延迟创建并保存请求结果 下一 Tick 系统可消费 未消费结果跨 Tick 保留 阶段 D 按约定的最小核心范围完成

创建验收菜单为 MmECS/验收阶段 D 创建 当前不支持任意数量初始组件 业务攻击 Command 与表现输出已在游戏模拟层实现 非法结构提交会抛错且不回滚已执行操作 不继续使用失败 World

## 当前快照接口

Simulation.Capture 返回 Simulation.Snapshot Simulation.Restore 接受同一实例保存的快照 快照可重复恢复 保存 World 核心状态以及未来输入 冻结 Tick 和累计时间 Core 的 World.CaptureState RestoreState 保持 internal 游戏输入仍只存在于样例程序集

仅在 Tick 或 Advance 成功结束且结构队列为空时操作快照 必须先完成至少一个 Tick 执行中与失败 Tick 不允许捕获恢复 恢复要求系统列表引用及顺序不变 系统内部的可变业务状态不自动保存 MovementSystem 查询列表会由下次查询重建

每个 World 由一个 Simulation 驱动 当前已支持 WorldId StateEpoch 外部引用隔离 世界单例与随机源均随快照恢复 F 的核心范围完成 E 的未来攻击也纳入组合快照 表现输出和 Unity 对象不保存 恢复时清理旧批次并重新读取当前角色 不承诺跨实例迁移 跨版本存档或跨平台确定性

## 首版查询与世界状态

World.GetLookup<T> 返回绑定本世界组件池的 ComponentLookup<T> 用于循环前执行一次类型查找 Lookup.Get 仍校验存活代际及组件存在 仅供可信模拟内部使用 不提供 Add Remove 或底层数组 宿主继续读取状态副本

Lookup 必须从 World.GetLookup 创建 不使用 default Lookup 可以跨同世界扩容及快照恢复复用 返回的组件 ref 不得跨结构提交或恢复保留 原始 Entity 仍是世界内部身份 不提供跨世界或 Epoch 校验 MovementSystem 每次 Tick 获取两个 Lookup 避免系统切换世界时持有旧池

World.Query 支持全部实体 单组件 双组件交集及双组件排除一个类型 CreateQuery 接受 AllList 与 NoneList 类型数组并复制去重条件 EntityQuery.Execute 复用组件池引用 每次按最小必需池筛选 数量相同时按注册顺序选择 空 All 按实体槽位扫描 冲突条件返回空集 未注册类型报错 结构提交或恢复后同一查询对象可继续执行

World.RegisterSingleton<T> 在初始化阶段登记 unmanaged 全局状态 GetSingleton<T> 返回当前世界独享值的引用 引用不跨 Tick 或恢复持有 不影响未来的缓存不要放入单例

World 构造可传 randomSeed 默认 DeterministicRandom.DefaultSeed 为 1 NextRandomUInt NextRandomInt NextRandomFloat 使用 SplitMix64 世界间不共享随机状态 有界整数用拒绝采样消除取模偏差 零种子有效 此随机源不用于密码学

## 系统生命周期

ISimulationSystem 保持最小 Tick 接口 需要初始化或释放的系统实现可选 ISimulationLifecycle Init 按注册顺序执行 Dispose 按逆序执行 Simulation.Init 可显式调用 否则首次 Tick 或 Advance 自动调用 初始化开始后不可增删系统且不可重复注册同一个系统

初始化失败会清理所有已经进入 Init 的系统 包括抛错系统 未开始初始化的系统不会收到 Dispose 释放错误聚合后抛出以确保其余系统仍得到清理 Simulation.Dispose 可重复调用并清空系统引用 输入队列和追踪回调 释放后拒绝继续推进或使用快照 World 作为调用方持有的纯托管状态不会被此操作清空

恢复快照不重复执行 Init 或 Dispose 系统配置保持不变 可变业务状态必须放入组件或 World 单例 固定步必须能转换为有限正 float 引擎经过时间必须为有限非负值

## 诊断与自动测试

Simulation.InspectState 与 World.InspectState 返回独立的字段路径和值字典 StateDiagnostics.Compare 定位新增删除及字段变化 浮点值同时记录位模式以识别负零和 NaN 差异 诊断投影不是持久化快照 待提交结构命令内容不包含在投影中 重演比较在完整 Tick 边界进行

Simulation.Trace 可选接收逐系统差异及新增结构请求数 默认关闭 开启时使用反射和装箱复制世界状态 仅用于诊断 回调只观察结果 不修改模拟且不抛错 LastInputCount 与 LastStructuralCount 表示最近逻辑步的计数 不作为快照状态

编辑器菜单 MmECS/运行首版全部验收与基准 会执行 15 组行为验收并生成报告 CLI 可调用 MmECS.Editor.EcsAcceptanceReport.RunAll EcsBenchmark.Run 可配置实体数量 样本数 预热次数 结构变化比例及查询次数 当前 Mono 线程分配计数未通过校准时报告不可用 不误报为零分配

## 阶段 E 战斗样例

打开 Assets/MmECS/Scene/CombatDemo.unity 后进入 Play Mode 初始为两名生命 100 伤害 25 攻击间隔 2 Tick 的角色 规则可在 CombatDemo Inspector 调整 本样例手动传入模拟时间 按钮提供攻击 反击 推进 Tick 连续 3 Tick 移动 保存 恢复及状态面板开关 Update 只驱动飘字动画 沿用原有 Simulation.Advance

纯 C# CombatSample.Create 创建同一战斗配置并完成首个 Tick Simulation.EnableCombat 在初始化前注册 Health Lifecycle AttackStats CombatIdentity 和 CombatClock 以及攻击伤害死亡系统 默认顺序为移动 攻击 伤害 死亡 结构提交 输出批次 完成 Tick

SubmitAttack 入队前校验世界 Epoch 代际和目标 Tick CombatClock 在 World 内分配稳定序号 按 Tick 分桶并保持同 Tick 入队顺序 消费时检查来源目标存活 自身攻击 距离和冷却 DamageSystem 再检查来源及目标是否已经死亡 因此同 Tick 较早的致死结算会屏蔽较晚的反击 DamageRequest 只活在当前 Tick 且必须被明确消费

ReadCombatActors 只返回值副本和宿主句柄 用于位置 血条及重新打开的面板 TryPeekCombatFrame 返回队首只读事件批次 AcknowledgeCombatFrame 确认后释放 同一批次不能重复确认 无事件的 Tick 不产生空帧 宿主须持续消费以释放队列

未来 AcceptedAttack 队列与命令序号随快照恢复 当前 Tick 临时请求与表现批次不保存 恢复清空旧输出 CombatDemo 发现 WorldId 或 Epoch 改变后清除已生成飘字并重绑当前状态 事件包含命中位置和实际伤害 因此目标已销毁仍可播放伤害效果

场景和 CombatPopup.prefab 均通过 Pipeline 制作 持久化组件引用 作者脚本为 AgentScripts/BuildMmEcsCombat.cs UI 字体复用项目 Assets/PathfindingAlgorithm/Arts/Fonts/NotoSansSC-Regular.ttf 模块迁出此工程时需一并提供或替换该字体

纯模拟菜单 MmECS/验收阶段 E 战斗通信 Play Mode 场景自动验收入口 MmECS.Editor.StageEUnityAcceptance.Run 测试完成后暂停以供截图 宿主表现计数与旧输出不参与模拟一致性比较

## 宿主引用与恢复后重绑

Entity 与 SpawnRequestId 是受信任模拟内部的身份 宿主持有 EntityHandle 或 SpawnRequestHandle 同时保存 WorldId StateEpoch 和内部身份 WorldId 区分世界实例 StateEpoch 在每次成功恢复时递增 两者不进入快照也不参与模拟一致性比较 被契约检查拒绝的恢复不会改变 StateEpoch

可信初始化或当前查询结果可通过 World.GetEntityHandle 绑定句柄 MoveInput 构造参数使用 EntityHandle Simulation.SubmitInput 先验证世界 批次与存活状态 再将输入转换为仅保存内部 Entity 的 AcceptedMoveInput 因而恢复前尚未提交的旧输入被拒绝 快照内已经接纳的未来输入仍能重演

World.GetSpawnRequestHandle 将当前创建请求绑定为宿主句柄 TryConsumeSpawnResult 的句柄重载先验证世界与批次 再消费结果 旧句柄不会误领编号复用后的新结果 创建结果记录创建事实 即使实体已销毁仍可消费 返回的 EntityHandle 是否存活另由 TryResolve 判断

恢复后在成功完成 Tick 且结构队列为空的边界通过 ReadEntityHandles 重新读取当前实体句柄 通过 ReadSpawnRequestHandles 读取未消费结果的当前请求句柄 宿主据此重新绑定 禁止将缓存的旧 Entity 或 SpawnRequestId 重新传入 Get 方法盖上当前身份 原始身份接口本身不提供跨世界或恢复批次隔离

外部引用隔离验收菜单为 MmECS/验收阶段 F 外部引用隔离 表现事件批次及恢复后的清理和重绑另由阶段 E 的纯模拟及 Unity 场景验收覆盖

设计与后续步骤见项目 Plan/UserPlan/MmECS设计架构
