# MmECS 核心设计

本文给出可以按顺序实现的首版设计 决策属于 MmECS 自身 并不代表两份参考项目采用同一实现

## 1 目标与范围

MmECS 是独立于游戏引擎的 C# 模拟核心 负责实体 组件 查询 系统及模拟状态

首版选择单线程 固定逻辑 Tick 每种组件一个紧密存储的 Sparse Set 显式系统顺序 Tick 末统一提交结构变更

学习目标是理解数据如何存储 如何定位 如何遍历 如何安全变化 然后用真实业务检验架构

首版可以承担移动 战斗 Buff 背包等规则 但 ECS 不会自动解决业务依赖 性能瓶颈或跨平台确定性

## 2 分清三层职责

| 层 | 内容 | 依赖方向 |
|---|---|---|
| MmECS.Core | Entity World Storage Query 结构缓冲 状态容器 | 只依赖所选 .NET 基础库 |
| Game.Simulation | 移动 伤害 技能等组件与系统 游戏配置访问 Tick 调度 | 依赖 Core |
| Game.UnityAdapter / Game.UnrealAdapter | 输入转换 表现对象映射 插值 动画 音效 UI | 依赖 Simulation 暴露的读写边界 |

System 的基础生命周期接口可以属于 Core 具体先运行移动还是伤害由 Game.Simulation 配置

Simulation 持有固定步长并接受经过的引擎时间 推进逻辑 Tick Adapter 负责采集和传入引擎帧时间 阶段 D 沿用现有 Simulation 不新增 FixedTickDriver

一帧渲染可以对应零个 一个或多个逻辑 Tick 不把 Unity.Update 当作模拟时间本身

## 3 Entity 是身份 不是对象集合

模拟内部的 Entity 由 int Index 与 uint Generation 组成 其意义限定在所属 World 内 代际从 1 开始 0 保留为无效值

World 用 AliveList 判断槽位是否存活 用 GenerationList 判断这个槽位当前属于第几次生命周期

示例 槽位 7 的第 3 个实体销毁后被复用 新实体为 7/4 旧句柄 7/3 必须失效

创建与销毁规则如下

1. 创建时优先从 FreeIndexList 取回收编号 没有则分配新编号
2. 标记 AliveList 为存活 返回当前 Index 和 Generation
3. 销毁时删除该实体所有组件 标记不存活 更新 Generation 并回收 Index
4. 获取组件前的有效性检查由 World 的公共访问边界负责 内部已验证的热路径不重复做同一检查

Generation 达到可表示的上限时不允许静默回绕 视为该 World 的生命周期上限并报告错误 不能宣称有限位宽句柄永远不会重复

允许空实体存在 删除最后一个组件不隐式销毁实体 只有显式 Destroy 才结束生命周期

这与 LeoECS Lite 的默认行为不同 好处是组件变化与实体生命周期分别可控

模拟内部避免传入另一个 World 的 Entity 外部句柄额外带 WorldId 与 StateEpoch 由入口验证所属世界和恢复批次

WorldId 与 StateEpoch 是宿主隔离信息 不能未经处理写入要求跨实例一致的模拟校验

## 4 Component 只定义数据

首版为模拟组件选择 `where T : unmanaged` 限制 同时项目规则禁止其中存放裸指针和外部地址

这样减少引用共享与浅拷贝问题 这是 MmECS 为状态管理作出的限制 ECS 概念本身允许托管组件

组件保存数值 枚举 自有数学结构 Entity 和配置编号 不保存 GameObject Transform ScriptableObject 或回调

配置对象在外部加载成不可变数据 模拟通过稳定配置编号访问 表现通过另一个映射取得 Prefab 图标和音效

背包条目和 Buff 实例首版可以各自作为实体 通过 Owner 组件引用拥有者 数量 字段 类型由对应组件保存

后续发现大量小实体成本不合适时 再加入 World 拥有的 Buffer 存储及稳定句柄 不直接在组件中放 List<T>

允许纯数据辅助方法 例如数学计算 不让组件方法调度其他系统 修改 UI 或启动异步操作

## 5 Sparse Set 如何存储组件

每个 `ComponentStorage<T>` 保存四项数据

| 名称 | 含义 |
|---|---|
| SparseList | Entity.Index 对应的组件行号加一 0 表示不存在 |
| DenseEntityList | 每个有效行属于哪个实体编号 |
| DenseValueList | 与实体行一一对应的组件值 |
| Count | 有效行数量 有效区间为 0 到 Count 减一 |

下面是存储示例 这些数字只用于展示映射关系

| 行号 | DenseEntityList | DenseValueList |
|---|---|---|
| 0 | 4 | 实体 4 的 Position |
| 1 | 9 | 实体 9 的 Position |
| 2 | 12 | 实体 12 的 Position |

此时 SparseList[4] 为 1 SparseList[9] 为 2 SparseList[12] 为 3

访问实体 9 的 Position 时 先得到行号 SparseList[9] 减一 再读取 DenseValueList[1]

稀疏索引是按实体编号寻址 组件数据则集中在有效区间 二者解决不同的问题

### 5.1 添加

前提是实体存活且当前不存在该组件

1. 确保实体索引与紧密数组容量足够
2. 将实体编号与组件值写入 Count 所指的末尾行
3. 将 SparseList[Entity.Index] 写成 Count 加一
4. 增加 Count 并登记该实体拥有的组件类型

容量足够时添加为常数级操作 扩容时涉及数组分配与复制 不能把每次添加都描述成没有代价

### 5.2 删除及末行填洞

删除实体 9 的 Position 时 不能留下空行再让所有查询都扫描空行

1. 找到待删除行 1 与最后有效行 2
2. 将最后一行的实体 12 和 Position 搬到行 1
3. 将 SparseList[12] 改为 2
4. 将 SparseList[9] 改为 0 清理旧末行并减少 Count
5. 从实体拥有的组件类型记录中移除此类型

删除后实体 12 的句柄没有变化 只有它的组件存储位置变化

必须回写被移动实体的稀疏索引 否则之后 Get 会读到旧位置

这种 swap-back 删除不保持插入顺序 业务不能把遍历顺序误当成固定优先级

### 5.3 与 LeoECS Lite 的差别

LeoECS Lite 的组件池使用回收槽位 组件删除后不把末尾值搬入空位 它的 Filter 实体列表才使用 swap-back

MmECS 首版选择紧密组件数组 是为了能直接用一个组件池驱动查询 这里借鉴了类型池与索引思想 没有原样复制其存储契约

### 5.4 ref 的生命周期

`ref T` 指向当前数组内的一个位置 它不代表实体身份

扩容会替换数组 删除会搬动末行 快照恢复会替换状态 即使旧引用在 C# 中仍可访问 也可能不再代表当前世界中的正确组件

首版只允许在当前系统执行和当前查询迭代范围内使用组件 ref 不保存到字段 不跨结构提交 不跨 Tick 不交给 View

## 6 World 如何统一管理不同组件类型

World 拥有 Type 到类型编号的 ComponentTypeDict 类型编号到组件池的 StorageList

`ComponentStorage<T>` 负责类型安全的数据访问 内部非泛型池接口只承担销毁实体时的 Remove 快照调度和容量等统一管理

热路径使用已经取得的泛型池 不用 object 接口读取每个组件 避免装箱和逐实体动态类型查找

每个实体维护已附加的组件类型列表 销毁实体时只删除实际拥有的组件 不扫描整个类型注册表

首版在 World.Init 阶段按显式顺序注册所需组件类型 运行中不按首次访问随机分配序列化类型身份

类型编号只是当前实现的索引 跨版本存档使用稳定类型标识与版本 同版本内存快照记录配置与类型注册的一致性信息

World 拥有模拟状态与可重建缓存 但应在代码职责上区分两者

模拟状态包括实体分配器 组件 单例 Tick 随机源和未来请求 缓存包括类型访问快捷路径和查询描述

## 7 Query 首版如何实现

支持 All 必须包含的类型 与 None 必须排除的类型 先实现常用的一个或两个必需组件查询

构造 Query 时保存类型与池的引用 执行时选择 Count 最小的必需组件池作为候选实体来源 数量相同时按已注册类型编号确定候选池

逐个候选实体检查其他必需池和排除池 命中后读取对应组件 行号不必在不同组件池之间相同

如果没有必需组件 例如查询全部实体 则按 Index 递增扫描 World 的 AliveList 只处理存活槽位

这一方案的主要成本约为候选数量乘类型检查数量 不需要为每个 Query 长期维护一份成员表

首版缓存查询条件与池访问 不缓存命中的实体列表 因此结构提交后下一次执行自然反映最新成员

后续确实存在大量重复复杂查询时 才评估 LeoECS Lite 的增量 Filter 缓存

缓存成员列表会减少查询时检查 同时增加结构变更通知成本 内存占用与恢复重建成本

不能只用一个哈希值判断两个查询相同 如果以后做查询缓存 应在哈希定位后比较完整规范化条件

## 8 System 与调度

System 保存查询描述 不可变配置引用和可重建缓存 影响未来模拟结果的可变数据放入 World

没有 Unity 组件的模拟对象使用 Init 有 Unity 组件的 Adapter 使用 InitComponents 获取组件 生命周期入口只调用一次

System 的最小生命周期为 Init Tick Dispose 调度器按注册顺序调用 明确哪一阶段生产 哪一阶段消费请求

首版可以为每个 System 写明读哪些组件 写哪些组件 这个声明先作为设计与审阅依据 不立即实现自动并行调度器

例如移动系统读取 Velocity 写 Position 伤害系统读取伤害请求写 Health 死亡系统读取 Health 写既有 Lifecycle 状态

系统共享一个数据不等于都应该写它 尽量为同一业务字段确定主要写入者

纯算法可以直接调用无状态辅助方法 ECS 不要求把每个函数都拆成一个系统

## 9 结构变化的可见时间

结构变化包括创建销毁实体 添加删除组件 修改已有组件字段不属于结构变化

首版在初始化阶段可以直接建立世界 运行期所有结构变化进入 StructuralBuffer 只在 Tick 末提交一次

World 的 Add Remove Destroy 各保留一套操作体 只在初始化阶段或内部结构提交阶段执行 运行期调用者通过 StructuralBuffer 入队

已有组件字段的修改立即对后续系统可见 尚未提交的组件添加删除和实体创建销毁对查询成员不可见

如果伤害系统本 Tick 让角色死亡 后续系统可以通过既有 Lifecycle.PendingDeath 字段跳过它 不能依赖尚未提交的新 Dead 标签

本 Tick 创建的子弹下一 Tick 才参与常规系统处理 这是首版明确接受的语义

如果玩法要求当 Tick 命中 可通过当 Tick 的命中请求结算 或日后增加明确的阶段提交点 不能在遍历中偷偷即时创建

### 9.1 缓冲操作契约

首版 StructuralBuffer 按入队顺序回放 与 Arch 当前按操作类别分批回放的实现不同

首版可用类命令保存不同组件类型的 Add Remove Destroy 并共用一条 FIFO 队列 若阶段 G 测出命令分配成为瓶颈 再评估无装箱的结构体命令存储 直接把结构体放入接口类型队列仍会装箱

Add 要求目标有效且类型不存在 Remove 要求目标有效且类型存在 Destroy 要求目标仍存活

重复 Add 或 Destroy 后继续操作同一实体属于调用错误 暴露为清晰错误 不把全部非法组合吞掉作为容错

业务层保证每个实体只提出一次销毁申请 缓冲层不依赖隐藏的冲突合并来猜测业务意图

首版用带完整初始组件数据的 SpawnRequest 排队创建 提交后才产生正式 Entity 不开放未落地 Entity 的中途引用

请求携带 World 分配的 SpawnRequestId 提交后生成 SpawnResult 保存请求编号与正式 Entity 下一 Tick 由约定系统消费

请求编号只用于关联结果 不能用来 Get 组件或当作临时 Entity 尚未消费的 SpawnResult 及请求编号分配器属于快照状态

需要同一批次新实体相互引用时 再设计明确的临时句柄与解析阶段 这不是首版前置需求

提交期间不执行会递归追加结构操作的用户回调 提交结束后发布 SpawnResult 等下一 Tick 模拟结果 另行生成给表现层的只读输出 两者生命周期分别管理

## 10 数学与引擎边界

核心数学结构不引用 UnityEngine.Vector3 时间不读取 UnityEngine.Time 随机数不调用 UnityEngine.Random

首版可以用 float 和自有向量类型 固定 Tick 只解决时间离散化 不承诺跨平台浮点一致

如果碰撞结果来自 Unity 或 UE 物理 世界状态与可重复性就依赖那部分外部模拟 不能只把渲染分离后宣称完整逻辑可脱离引擎

选择独立物理实现或明确物理服务的状态边界 属于后续具体玩法接入决策

## 11 首版 API 应表达什么

以下为接口职责清单 不是已经存在或可以直接调用的实现

| 操作 | 契约 |
|---|---|
| World.Init | 注册类型 配置池和单例 初始化分配器 |
| World.IsAlive | 检查 Index 与 Generation 属于当前存活实体 |
| World.Get / Has | 类型化访问已有组件 |
| World.Query | 查询满足组件条件的实体 |
| StructuralBuffer.Spawn / Add / Remove / Destroy | 登记运行期结构请求 |
| Simulation.Tick | 输入处理 系统执行 结构提交与输出生成 |
| Simulation.Capture / Restore | 在规定 Tick 边界保存或恢复同版本模拟状态 |
| Simulation.ReadState | 返回只读数据或值拷贝 不暴露可写存储 |
| Simulation.SubmitInput / SubmitCommand | 登记外部意图 |
| Simulation.DrainViewEvents | 提交表现事件给 Adapter 消费 |

业务 Command 与 StructuralBuffer 的命令不共用一个概念 前者表达玩家意图 后者表达存储变更

## 12 开始实现前应能回答的事情

读完本文应当能解释 实体销毁后为什么需要代际 删除组件为什么要回写另一个实体的索引 为什么 ref 不能跨提交保存

也应当知道 查询成员变化与字段值变化不是同一种时机 为什么首版选择查询时筛选 为什么添加一个标签不一定当 Tick 可见

下一步按 [实施路线](05-实施路线与验收.md) 完成可运行的最小循环 通信和快照的边界见 [专门设计](04-系统通信与表现及快照.md)
