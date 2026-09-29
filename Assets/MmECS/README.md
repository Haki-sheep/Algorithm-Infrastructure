# MmECS 当前模块边界

每个类及嵌套类型使用 XML summary 说明当前职责 分部类按所在文件说明实际负责的边界 接口与数据结构同样说明契约或数据含义 后续新增文件沿用此规范

## 目录与程序集

| 目录 | 程序集 | 职责 |
|---|---|---|
| Runtime/Core | MmECS.Core | 实体身份与代际 组件 Sparse Set 世界查询 结构缓冲 通信 快照 系统与 Simulation 调度 |
| Samples/Basic | MmECS.Samples | 核心 API 用法示例 独立世界演示 无场景与 Unity 依赖 |

Core 命名空间为 MmECS 样例命名空间为 MmECS.Samples

两个程序集均为纯 C# 不引用 UnityEngine 或第三方插件 Core 以 noEngineReferences 关闭引擎引用 MmECS.Samples 只引用 MmECS.Core 当前不存在独立的 Unity 适配层与验收程序集

场景只有 Scene/SampleScene.unity 不含演示逻辑 用法示例为 Samples/Basic/ApiSample.cs 调用 ApiSample.Run 返回实体遍历 信号 表现事件 快照重演与延迟销毁的执行轨迹文本 无需场景与 Unity 组件即可运行

## 复用与接入

按 Assets 源码模块使用 通用部分位于 Runtime/Core 用法示例位于 Samples/Basic 无需额外第三方依赖 迁出此工程时两个程序集目录可整体复制

现有 Simulation 负责累计引擎传入的时间并执行固定 Tick 不新增 FixedTickDriver 引擎侧只需在 Update 中调用 Advance

Core 不对外授权 internal 成员 不存在 InternalsVisibleTo 声明 需要扩展调度语义或宿主业务状态时派生 Simulation 并重写 CreateSnapshot 与 DisposeState

## 对外 API 边界

World.Register<T> 登记 unmanaged 组件类型 Create 分配实体并返回世界内部身份 Entity 携带 Index 与 Generation Add Get Has Remove 按实体读写组件 IsAlive 校验编号与代际

World.ForEach<TFirst,TSecond,TAction>(ref TAction) 遍历同时拥有两种组件的实体 处理类型实现 IComponentAction<TFirst,TSecond>.Execute

World.Structural 延迟结构缓冲 入队 Add Remove Destroy 与 Spawn<TFirst,TSecond> 请求 按 FIFO 在系统执行结束后统一提交 PendingCount 与 LastCommittedCount 暴露队列长度与最近一次提交数量 实体直接创建与销毁为立即语义 只在初始化或内部提交阶段允许调用

World.Signals 同步信号 Emit 在发出点立即回调已绑定系统 系统实现 ISignalSystem 绑定 实现 ISignalHandler<T> 处理 初始化完成后信号注册关闭

World.Events 表现事件 Emit 累积为按 Tick 分批的输出 宿主通过 TryPeek Acknowledge Dispatch 消费 未确认的批次不会释放 恢复时清空旧批次

Simulation 绑定一个 World 与固定步长 Tick 执行单个逻辑步 Advance 累计引擎时间并执行零到多个逻辑步 Capture 与 Restore 操作快照 DispatchEvents 派发表现事件 CurrentTick WorldId StateEpoch 暴露当前批次

外部句柄 EntityHandle 与 SpawnRequestHandle 绑定 WorldId 与 StateEpoch 配 TryResolve 隔离世界实例与恢复批次

## 首版查询与世界状态

World.GetLookup<T> 返回绑定本世界组件池的 ComponentLookup<T> 用于循环前执行一次类型查找 Lookup.Get 仍校验存活代际及组件存在 仅供可信模拟内部使用 不提供 Add Remove 或底层数组 宿主继续读取状态副本

Lookup 必须从 World.GetLookup 创建 不使用 default Lookup 可以跨同世界扩容及快照恢复复用 返回的组件 ref 不得跨结构提交或恢复保留 原始 Entity 仍是世界内部身份 不提供跨世界或 Epoch 校验 系统每次 Tick 获取 Lookup 避免持有旧池

World.Query 支持全部实体 单组件 双组件交集及双组件排除一个类型 CreateQuery 接受 AllList 与 NoneList 类型数组并复制去重条件 EntityQuery.Execute 复用组件池引用 每次按最小必需池筛选 数量相同时按注册顺序选择 空 All 按实体槽位扫描 冲突条件返回空集 未注册类型报错 结构提交或恢复后同一查询对象可继续执行

World.RegisterSingleton<T> 在初始化阶段登记 unmanaged 全局状态 GetSingleton<T> 返回当前世界独享值的引用 引用不跨 Tick 或恢复持有 不影响未来的缓存不要放入单例

World 构造可传 randomSeed 默认 DeterministicRandom.DefaultSeed 为 1 NextRandomUInt NextRandomInt NextRandomFloat 使用 SplitMix64 世界间不共享随机状态 有界整数用拒绝采样消除取模偏差 零种子有效 此随机源不用于密码学

## 系统生命周期

ISimulationSystem 保持最小 Tick 接口 需要初始化或释放的系统实现可选 ISimulationLifecycle Init 按注册顺序执行 Dispose 按逆序执行 Simulation.Init 可显式调用 否则首次 Tick 或 Advance 自动调用 初始化开始后不可增删系统且不可重复注册同一个系统

初始化失败会清理所有已经进入 Init 的系统 包括抛错系统 未开始初始化的系统不会收到 Dispose 释放错误聚合后抛出以确保其余系统仍得到清理 Simulation.Dispose 可重复调用并清空系统引用 宿主业务状态和追踪回调 释放后拒绝继续推进或使用快照 World 作为调用方持有的纯托管状态不会被此操作清空

恢复快照不重复执行 Init 或 Dispose 系统配置保持不变 可变业务状态必须放入组件或 World 单例 固定步必须能转换为有限正 float 引擎经过时间必须为有限非负值

## 当前快照接口

Simulation.Capture 返回 Simulation.Snapshot Simulation.Restore 接受同一实例保存的快照 快照可重复恢复 保存 World 核心状态与累计时间以及捕获时的系统引用列表 世界核心状态包含实体分配器 组件池 世界单例 随机源 未消费的创建请求结果与 Tick Core 的 World.CaptureState RestoreState 保持 internal

仅在 Tick 或 Advance 成功结束且结构队列为空时操作快照 必须先完成至少一个 Tick 执行中与失败 Tick 不允许捕获恢复 恢复要求系统列表引用及顺序不变 系统内部的可变业务状态不自动保存 查询列表会由下次查询重建

每个 World 由一个 Simulation 驱动 当前已支持 WorldId StateEpoch 外部引用隔离 世界单例与随机源均随快照恢复 表现输出和 Unity 对象不保存 恢复时清空旧事件批次 不承诺跨实例迁移 跨版本存档或跨平台确定性

## 诊断与自动测试

Simulation.InspectState 与 World.InspectState 返回独立的字段路径和值字典 StateDiagnostics.Compare 定位新增删除及字段变化 浮点值同时记录位模式以识别负零和 NaN 差异 诊断投影不是持久化快照 待提交结构命令内容不包含在投影中 重演比较在完整 Tick 边界进行

Simulation.Trace 可选接收逐系统差异及新增结构请求数 默认关闭 开启时使用反射和装箱复制世界状态 仅用于诊断 回调只观察结果 不修改模拟且不抛错 LastStructuralCount 表示最近逻辑步已提交的结构请求数 不作为快照状态

当前工作区不含自动化验收入口 行为与结构检查由 Samples/Basic/ApiSample.Run 与手写场景验证覆盖

## 宿主引用与恢复后重绑

Entity 与 SpawnRequestId 是受信任模拟内部的身份 宿主持有 EntityHandle 或 SpawnRequestHandle 同时保存 WorldId StateEpoch 和内部身份 WorldId 区分世界实例 StateEpoch 在每次成功恢复时递增 两者不进入快照也不参与模拟一致性比较 被契约检查拒绝的恢复不会改变 StateEpoch

可信初始化或当前查询结果可通过 World.GetEntityHandle 绑定句柄 宿主的外部输入不进入 Core 快照 恢复后需要宿主重新提交 因此恢复前尚未提交的旧输入由宿主自行丢弃或重放

World.GetSpawnRequestHandle 将当前创建请求绑定为宿主句柄 TryConsumeSpawnResult 的句柄重载先验证世界与批次 再消费结果 旧句柄不会误领编号复用后的新结果 创建结果记录创建事实 即使实体已销毁仍可消费 返回的 EntityHandle 是否存活另由 TryResolve 判断

恢复后在成功完成 Tick 且结构队列为空的边界通过 ReadEntityHandles 重新读取当前实体句柄 通过 ReadSpawnRequestHandles 读取未消费结果的当前请求句柄 宿主据此重新绑定 禁止将缓存的旧 Entity 或 SpawnRequestId 重新传入 Get 方法盖上当前身份 原始身份接口本身不提供跨世界或恢复批次隔离

## 历史验收与报告

原有 MmECS.Editor 验收与基准程序集 移动与战斗样例程序集 Game.Simulation 与 Game.Combat.UnityAdapter CombatDemo 场景 CombatPopup.prefab 以及 AgentScripts 制作与探测脚本均已不在 Assets 树 编辑器菜单 MmECS/验收阶段 D 创建 MmECS/验收阶段 E 战斗通信 MmECS/验收阶段 F 外部引用隔离 MmECS/运行首版全部验收与基准 当前均无对应实现 相关结论无法从当前工作区重跑

历史验收与基准数据以 Plan/UserPlan/MmECS设计架构 报告为准 包含 05-实施路线与验收 06-首版自动测试报告 07-首版Review与验收审查 08-阶段E验收报告 09-性能优化报告 10-GC分配专项报告 11-三轮性能数据报告-2026-09-27

设计与后续步骤见项目 Plan/UserPlan/MmECS设计架构
