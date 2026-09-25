# Arch 源码精读：先理解数据怎样移动 再决定要借哪些设计

你不需要先读完 Arch。本篇把创建、查询、迁移、销毁这几条核心调用链拆开，并说明它们对 MmECS 的意义，喵～

## 1 阅读基线与结论范围

| 项目 | 本次核实结果 |
| --- | --- |
| 仓库 | [genaray/Arch](https://github.com/genaray/Arch) |
| 本地副本 | [参考源码/Arch](参考源码/Arch/) |
| 分支 | master |
| 固定提交 | `9eb9ff27caf071825b48be907095170ba95ecac1` |
| 提交日期 | 2026-09-22 |
| 项目版本 | `Arch.csproj` 声明 `2.1.0`，README 安装示例写 `2.1.0-beta` |
| 目标框架 | net8.0、net6.0、netstandard2.1 |
| 语言版本 | csproj 最后生效的声明为 C# 12 |
| 许可证 | [Apache-2.0 原文](参考源码/Arch/LICENSE.MD) |
| 核实方式 | 阅读核心源码、生成的泛型 API 和对应测试源码；本篇未运行 Arch 测试或性能基准 |

这份分析对应上面的提交，不把当前 master 自动当成已发布稳定包。`netstandard2.1` 兼容也不等于把 C# 12 源码直接复制进任意 Unity 版本即可编译。

网上 1.x 教程经常单独介绍 `EntityReference`。本次 2.x 源码的 `Entity` 已经带 `Version`，不能把旧版实体语义原样搬过来。README 的示例还写着 `using Arch`，实际核心命名空间是 `Arch.Core`，说明示例同样需要对照源码。

## 2 Arch 实际解决什么问题

Arch 核心把“哪些实体具有相同组件组合”作为存储分组依据，使多组件批量遍历能在已匹配的数据数组上直接执行。

它提供 World、实体句柄、组件类型注册、Archetype、Chunk、查询、结构变更缓冲以及相关并行 API。游戏系统生命周期、业务阶段、UI 通信协议、固定 Tick 和完整快照契约仍需要项目自己设计，部分上层能力另有扩展包。

| 名称 | 先记住这个职责 |
| --- | --- |
| Entity | 身份，不直接持有组件 |
| EntityInfoStorage | 根据 Entity.Id 找当前存储位置与代际 |
| Signature | 一组组件类型 |
| Archetype | 容纳组件组合完全相同的实体 |
| Chunk | 一批实体及它们各自对应的组件数组 |
| QueryDescription | 声明组件筛选条件 |
| Query | 缓存匹配到的 Archetype 并提供遍历 |
| CommandBuffer | 暂存对 World 的修改，在规定时机回放 |

## 3 Entity 怎样找到组件

常规构建中的 `Entity` 有 `Id`、`WorldId`、`Version`。`PURE_ECS` 构建保留 `Id` 和 `Version`，省去 `WorldId`。

实体本身不记录“第几个 Chunk 第几行”，因为增删组件和删除其他实体都可能改变物理位置。稳定身份与可变地址必须分开。

调用 `World.Get<Position>(Entity)` 时，实际执行的层次是：

1. 用 `Entity.Id` 访问 `EntityInfoStorage`，取得 `EntityData`。
2. `EntityData` 保存 `Archetype`、`Slot`、`Version`。
3. `Slot.ChunkIndex` 定位 Chunk，`Slot.Index` 定位其中一行。
4. 组件类型 ID 经过 `ComponentIdToArrayIndex` 找到 `Position[]`。
5. 返回该数组对应元素的 `ref Position`。

因此一个 Entity 可以保持不变，而组件地址随迁移变化。跨结构变更长期保存组件 `ref`，会把身份与旧地址混在一起；MmECS 应明确禁止这种使用方式。

这里还有一个容易误解的细节：`World.Get<T>` 的这条热路径按 ID 直接取位置，没有先调用 `IsAlive`。拥有版本字段不代表所有访问函数自动校验旧句柄。

MmECS 应分别定义“外部句柄校验入口”与“已验证查询内部的快速访问”，让调用者知道安全契约，避免每次业务函数随意增加检查。

源码定位：[Entity.cs 本地](参考源码/Arch/src/Arch/Core/Entity.cs) · [固定提交第 146 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/Entity.cs#L146)；[EntityInfo.cs 本地](参考源码/Arch/src/Arch/Core/EntityInfo.cs) · [第 20 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/EntityInfo.cs#L20)；[World.cs 本地](参考源码/Arch/src/Arch/Core/World.cs) · [Get 第 1155 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/World.cs#L1155)。

## 4 Chunk 的连续存储到底连续在哪里

假设某个 Archetype 的组件组合为 `{Position, Velocity, Health}`。其中每个 Chunk 有如下并列数组：

| 数组 | 第 0 行 | 第 1 行 | 第 2 行 |
| --- | --- | --- | --- |
| Entity[] | E10 | E21 | E35 |
| Position[] | E10 的位置 | E21 的位置 | E35 的位置 |
| Velocity[] | E10 的速度 | E21 的速度 | E35 的速度 |
| Health[] | E10 的血量 | E21 的血量 | E35 的血量 |

同一行属于同一实体，同一组件类型的数据位于同一个数组。这是 SoA 思路在组件粒度上的应用。

**源码中的 Chunk 是一个管理多组托管数组的结构，不是把全部组件打包进一整块 unmanaged 内存。** 构造时创建 `Entity[]`、`Array[]`，再为每种组件取得相应的 `T[]`。

如果 `Position` 是结构体，数组连续保存结构体值；如果组件是 class，数组连续保存的是引用，实例数据仍可能分散在托管堆上。结构体中含 `List<T>` 等引用字段，也不会让那些列表内容一并变成连续内存。

Arch 默认基准 Chunk 大小是 16,384 字节，并考虑默认至少 100 个实体。实际容量由组件大小和实体大小估算，必要时向上取基准大小的整数倍。它不是“任何组件组合都精确分配 16KB”，也不包含所有数组对象头及引用对象的完整内存成本。

MmECS 要借的是按实际访问方式组织数据的思想；16KB、最少 100 个实体都是 Arch 的具体配置，不是 ECS 必须遵守的常数。

源码定位：[Chunk.cs 本地](参考源码/Arch/src/Arch/Core/Chunk.cs) · [构造第 162 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/Chunk.cs#L162)；[Archetype.cs 本地](参考源码/Arch/src/Arch/Core/Archetype.cs) · [容量计算第 827 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/Archetype.cs#L827)；[ComponentRegistry.cs 本地](参考源码/Arch/src/Arch/Core/ComponentRegistry.cs) · [类型大小第 314 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/ComponentRegistry.cs#L314)。

## 5 创建实体：身份分配与数据落位是两件事

以同时创建 Position 和 Velocity 为例，生成 API `World.Create<T0, T1>` 完成这些工作：

1. 取得这两个组件构成的 Signature。
2. 从回收队列获取可复用的 ID 与下一代 Version；没有回收项时分配新身份。
3. 找到该 Signature 对应的 Archetype；首次出现就创建 Archetype 和初始 Chunk。
4. 在当前有容量的 Chunk 尾部追加 Entity，并写入两个组件值。
5. 把实体对应的 Archetype、Slot、Version 写回 EntityInfoStorage。

当当前 Chunk 满了，使用下一块已分配 Chunk，容量不足再扩充。正常追加不需要为之前的每个实体重新做一份对象结构。

理解这里就能拆开三个成本：类型组合首次出现的准备成本、Chunk 容量增长成本、已有容量内的普通追加成本。不能用一句“创建实体 O(1)”掩盖前两种开销。

源码定位：[生成的 World.Create.cs 本地](参考源码/Arch/src/Arch/Templates/World.Create.cs) · [双组件创建第 42 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Templates/World.Create.cs#L42)；[World 身份分配第 266 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/World.cs#L266)；[Archetype.Add 第 451 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/Archetype.cs#L451)。

## 6 查询：缓存组合 然后遍历数据

`QueryDescription` 的四组条件分别表示：

| 条件 | 含义 |
| --- | --- |
| All | 这些组件全部存在 |
| Any | 指定集合非空时 至少有其中一个 |
| None | 这些组件全部不存在 |
| Exclusive | 组件组合精确相同 不与另外三组混用 |

Arch 有两层缓存：

1. `World.QueryCache` 用 QueryDescription 查找已有 Query，避免重复创建查询对象。
2. Query 保存匹配的 Archetype 集合，避免每帧重新对所有实体做组件存在性判断。

Query 进入遍历前调用 `Match()`，比较 Archetypes 集合当前 hash 与上次记录值。值未变化时直接使用匹配列表；变化后清空旧匹配列表，再检查全部 Archetype 的 BitSet。

**这里是集合变化后的全量重匹配，不是“每增加一个 Archetype 就只检查新增项”的实现。** 在已有两个 Archetype 之间迁移实体，通常不改变 Archetype 集合，所以无需重新判断两个组合是否符合筛选条件。

进入匹配 Chunk 后，生成的 `World.Query<T0,T1>` 先各取一次组件数组首元素引用，再按行取得两个组件引用并调用处理函数。热循环不需要每个实体都去组件池里分别查询“你有没有 Position 和 Velocity”。

查得快的前提是组合较稳定、批量处理足够多。若存在大量稀疏组合，匹配列表、空闲 Chunk 和不同组合之间的遍历开销都会增长。

MmECS 首版如果采用 Sparse Set，无需照搬这套查询缓存。以后实现 Archetype 后，可用显式的组合集合版本号表达缓存失效，比依赖集合 hash 更容易定义和测试。

源码定位：[Query.cs 本地](参考源码/Arch/src/Arch/Core/Query.cs) · [匹配与缓存第 596 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/Query.cs#L596)；[World.Query 第 411 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/World.cs#L411)；[World.Query.cs 本地](参考源码/Arch/src/Arch/Templates/World.Query.cs) · [双组件热循环第 23 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Templates/World.Query.cs#L23)。

## 7 增删组件：为什么一个 Add 会搬动其他组件

假设 E21 目前位于 `{Position, Velocity}`，现在添加 Health。目标组合变成 `{Position, Velocity, Health}`，因此 E21 必须迁往另一个 Archetype。

`World.Add<Health>` 的主要过程是：

1. 从 E21 的 EntityData 取得源 Archetype 与旧 Slot。
2. 查询源 Archetype 对 Health 的 AddEdge，取得目标 Archetype；首次发生才计算新 Signature 并缓存这条边。
3. 在目标 Archetype 尾部为 E21 分配新 Slot。
4. 复制两个组合共有的 Position 和 Velocity。
5. 删除源位置，用源 Archetype 最后一行补这个空位。
6. 把补位实体的 EntityInfo 更新到旧 Slot，再把 E21 的 EntityInfo 更新到目标 Slot。
7. 带组件值的 Add 重载把 Health 写进新位置。

移除组件同理，只是目标组合少一种类型，复制公共组件时跳过目标不存在的类型。

### 补位到底移动了谁

假设源 Chunk 原有 `[E10, E21, E35]`，迁走 E21 后，E35 被复制到它的行，有效部分变成 `[E10, E35]`。E35 的身份没变，但它的物理行号从 2 变成 1。

如果源 Archetype 横跨多个 Chunk，取来补位的是整个源 Archetype 当前最后一个有效 Chunk 的最后一行，不局限于被删行所在的 Chunk。

**迁移时至少要维护两份位置映射：迁走实体的新地址，以及补位实体的新地址。** 只复制组件却忘记其中一份映射，会出现按 Entity 读取到其他实体数据的问题。

这种删除通常称为 swap-back，保住数组有效区域的密集性，但不保留原始遍历顺序。需要顺序敏感的战斗结算时，顺序必须由业务明确规定，不能默认物理行顺序就是稳定游戏规则。

迁移的开销还包括共有组件复制、源末行所有组件补位、目标容量可能增长。组件越多或越大，频繁增删组件的成本越明显。

因此“频繁变化的数值”放字段，例如血量和冷却剩余量；是否通过组件存在与否表达状态，要结合变化频率与查询收益判断，不能每个布尔值都自动拆成 Tag。

源码定位：[World.Move 第 348 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/World.cs#L348)；[World.Edges.cs 本地](参考源码/Arch/src/Arch/Core/Edges/World.Edges.cs) · [AddEdge 第 16 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/Edges/World.Edges.cs#L16)；[Archetype.Remove 第 528 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/Archetype.cs#L528)；[Chunk 公共组件复制第 621 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/Chunk.cs#L621)；[Chunk.Transfer 第 650 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/Chunk.cs#L650)。

## 8 销毁与代际：旧句柄何时失效

`World.Destroy` 先从 Archetype 删除并完成补位回写，再调用 `DestroyEntityInternal`：

- 将 `{Id, Version + 1}` 放入回收队列。
- 从 EntityInfoStorage 删除该 ID 的有效记录。
- 减少当前实体数量。

后续创建可复用这个 ID，但携带递增后的 Version。例如旧句柄是 `{Id=21, Version=1}`，新实体是 `{Id=21, Version=2}`。

`World.IsAlive` 要求版本为正、该 ID 有记录、记录的版本与句柄版本相同。因此销毁前取得的旧句柄不会因为 ID 被复用就再次通过代际检查。

但该提交的 `World.IsAlive(Entity)` 方法本身没有比较 `Entity.WorldId`。调用者仍应把 Entity 交回它所属的 World；普通扩展方法通过 Entity.WorldId 选择 World。MmECS 若公开多 World API，应明确世界归属的校验规则。

不要把原库的 `unchecked(Version + 1)` 理解成“代际永远不会耗尽”。这是版本位宽与生命周期策略的一部分，首版可先定义规则，不必提前做无限代际机制。

源码定位：[World.Destroy 第 382 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/World.cs#L382)；[回收代际第 279 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/World.cs#L279)；[IsAlive 第 1676 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/World.cs#L1676)。

## 9 CommandBuffer：延迟修改不等于业务命令系统

遍历 Position 数组时直接删除或迁走正在遍历的实体，会改变当前数组内容与实体位置。CommandBuffer 先记下这些修改，让查询结束、工作任务完成之后再统一执行。

Arch 会记录创建、添加、设置、移除、销毁。尚未真正创建的实体使用负 ID 作为临时句柄，Playback 创建真实实体后再解析它。

这个实现有非常具体的顺序语义：**Playback 按 Create、Add、Set、Remove、Destroy 分组执行，不按每一次 API 调用的全局先后顺序逐条回放。** 同一实体同类型的 Set 会覆盖先前缓存的值。

所以“先记录 Remove<Health>，再记录 Add<Health>”不能简单按调用顺序推断最终一定有 Health。设计自己的缓冲时必须先决定冲突规则，再决定如何合并；首版保持显式顺序通常更容易教学和验证。

Playback 的源码注释要求主线程执行。即便记录阶段使用锁，也不能据此推断 World 支持任意线程任意时刻直接改结构。

| 对象 | 表达内容 | 谁负责判断合法性 |
| --- | --- | --- |
| 业务命令 `EquipItem` | 玩家希望装备某件物品 | 装备系统检查拥有者、槽位、规则 |
| 结构操作 `Add<Equipment>` | 对实体存储添加一种组件 | ECS 按已定义的结构约束执行 |
| 领域结果 `ItemEquipped` | 本次装备已经成功 | 业务系统产生 其他逻辑或 View 消费 |

不能把网络传来的“装备”请求直接翻译为对组件的任意 Add/Set；也不要让 UI 直接持有底层 CommandBuffer 并操纵模拟存储。

MmECS 应让业务命令先进入所属系统，系统计算结果后根据需要记录结构变更。首版统一在 Tick 末提交一次；未来如果改变为多阶段提交，需要同时修改核心契约与验收条件。

源码定位：[CommandBuffer.cs 本地](参考源码/Arch/src/Arch/Buffer/CommandBuffer.cs) · [临时实体第 173 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Buffer/CommandBuffer.cs#L173) · [记录覆盖语义第 207 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Buffer/CommandBuffer.cs#L207) · [Playback 第 283 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Buffer/CommandBuffer.cs#L283)。

## 10 托管组件与快照：不能把 Copy 注释当完整契约

Arch 的组件泛型入口没有统一要求 `unmanaged`。类型注册明确区分值类型大小与引用大小，数组工厂也能创建相应的 `T[]`，所以它支持托管引用组件。

这有方便接入的好处，同时意味着快照必须额外规定引用归属。例如组件含 `List<int>`，复制组件数组得到的两份组件仍指向同一个列表；后续修改会相互影响。

该提交确实有 `World.Copy()`，方法注释写 deep copy，但逐层展开实现可以确认：

1. 创建新的 World。
2. 只遍历非空 Archetype，为目标建立同类型组合。
3. 通过 Archetype.Copy 与 Chunk.Copy 复制数组内容和数量。
4. 这条方法路径没有复制或重建实体 EntityInfo 映射，也没有复制回收 ID 队列。
5. Chunk.Copy 复制的 Entity 值保留原 WorldId；没有看到该路径为新 World 重写它。
6. 组件元素复制落到 Array.Copy，托管引用对象不会被递归深拷贝。

这是静态源码核实的覆盖范围，本篇没有运行复现实验。它足以说明：**不能拿这个方法的名称与注释，就认定可以直接充当 MmECS 的完整世界快照或跨 World 克隆。**

其 Copy 测试主要比较 World 容量、实体数、Archetype 数量及每组数量，没有在这两项测试中验证复制后 Entity 访问、回收分配连续性、托管对象隔离或回滚后继续模拟。

另外，ComponentRegistry 的 ID 依赖运行期注册过程。进程内部可以拿它快速索引，但长期存档应有稳定的组件类型标识与版本迁移规则，不应直接假设这种运行期 ID 永远一致。

对 MmECS 的直接要求是：快照必须覆盖会影响后续结果的完整状态，包含实体代际与分配器、组件、世界资源、随机数状态、Tick 和尚未消费的业务数据。可重建查询缓存可以不保存，但必须能正确重建。

源码定位：[World.Copy 第 1789 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/World.cs#L1789)；[Archetype.Copy 第 892 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/Archetype.cs#L892)；[Chunk.Copy 第 599 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch/Core/Chunk.cs#L599)；[WorldTest.cs 本地](参考源码/Arch/src/Arch.Tests/WorldTest.cs) · [Copy 测试第 894 行](https://github.com/genaray/Arch/blob/9eb9ff27caf071825b48be907095170ba95ecac1/src/Arch.Tests/WorldTest.cs#L894)。

## 11 取舍：MmECS 现在借什么

| 设计 | 对 MmECS 的建议 | 原因 |
| --- | --- | --- |
| 身份与地址分开 | 现在采用 | Sparse Set 与 Archetype 都需要 |
| Entity 代际 | 现在采用 | 解决 ID 复用后的旧引用识别 |
| 组件访问保留 ref | 在明确有效期后采用 | 避免结构体反复复制 |
| 结构变更提交边界 | 现在采用 | 让查询及系统间可见性有明确定义 |
| swap-back 后回写映射 | 首版稠密存储就要正确实现 | 这是数据正确性要求 |
| Archetype 与 Chunk | 有实际联合查询瓶颈再做 | 引入迁移、组合管理与更多容量策略 |
| AddEdge/RemoveEdge | 后续优化 | 加速重复迁移目标查找 不消除迁移本身 |
| 并行查询及任务调度 | 后置 | 需要读写冲突声明与同步规则 |
| 全局 World 注册表 | 不照搬为业务访问方式 | 多实例归属与销毁关系应当显式 |
| 运行期组件类型 ID | 仅作内部索引 | 不当成跨版本存档协议 |
| World.Copy 实现 | 不作为快照实现移植 | 覆盖范围与完整快照契约不同 |

Archetype 的性能优势与迁移成本是同一个存储选择带来的。大量实体长期保持少数组件组合、系统连续处理其中几类组件时，它很值得采用；频繁更换组合、实体较少或组合高度分散时，收益需要实际测量。

因此首版建议仍是 Sparse Set，先完成清晰的 World、查询、生命周期与 View 边界。把存储访问集中在 ECS 内部，业务系统不要长期依赖组件池数组布局，后续才有替换存储的余地。

## 12 已阅读的测试能教你什么

| 原库测试入口 | 本次看到的检查内容 | MmECS 可借的验收意图 |
| --- | --- | --- |
| [WorldTest.cs](参考源码/Arch/src/Arch.Tests/WorldTest.cs) | 创建 销毁 ID 回收 Add/Remove Copy 数量 | 句柄与存储一致 ID 复用后旧句柄失效 |
| [ArchetypeTest.cs](参考源码/Arch/src/Arch.Tests/ArchetypeTest.cs) | 多 Chunk 分配 删除补位 迁移 复制 | 补位实体回写正确 公共组件保留 |
| [QueryTest.cs](参考源码/Arch/src/Arch.Tests/QueryTest.cs) | All/Any/None/Exclusive 与组合查询 | 筛选语义正确 组合变化后查询更新 |
| [CommandBufferTest.cs](参考源码/Arch/src/Arch.Tests/CommandBufferTest.cs) | 已有实体 暂存创建 创建后销毁 组合修改 | 延迟生效 临时实体解析 冲突语义明确 |

本次是源码与测试意图提炼，不把上游存在测试等同于本机测试通过，更不据此声称 Unity、IL2CPP 或 UE 集成已经验证。

你已经可以先按自己的需求实现身份、组件池与结构变更规则；需要回看 Arch 时，只打开本篇对应的几个函数即可，不需要通读整个仓库。
