# LeoECS Lite 源码精读：从数据结构推导 MmECS

你不需要先读懂原仓库。本篇已经把核心实现整理成可以独立学习的说明，源码链接用于追溯细节。

阅读目标是理解一个小型 ECS 怎样管理身份、组件与查询，再明确哪些设计适合放进 MmECS。

## 1 阅读基线与结论

- 仓库：[Leopotam/ecslite](https://github.com/Leopotam/ecslite)。
- 本地源码：[参考源码/ecslite](参考源码/ecslite)。
- 固定提交：`d46f3b209ea163810823aeea7d2610d38ad6cdf8`。
- 提交日期：2026-04-25，提交说明为 `Merge branch 'release/2026.4.25-0'`。
- 本篇实际阅读范围：README、LICENSE，以及 `worlds.cs`、`components.cs`、`filters.cs`、`entities.cs`、`systems.cs` 五个核心文件。
- 验证方式：源码静态阅读与调用关系核对，没有运行该仓库的测试或性能基准。

当前 README 明示停止维护、推荐后继项目 EcsProto，并明确框架不支持线程安全。这里把它作为可读性较高的研究样本。

当前许可证是 **MIT-Red，不是标准 MIT**。它包含额外使用限制与保留版权声明的要求；若直接复用源码，应按本地 [LICENSE.md](参考源码/ecslite/LICENSE.md) 判断使用条件。本篇主要提炼设计思想。

最值得先记住的三个事实：

1. 组件池按实体编号查找组件，但删除组件会留下可复用槽位，不会搬动末尾组件来填洞。
2. Filter 缓存符合条件的实体编号，删除 Filter 成员才使用末尾覆盖。
3. 遍历期间的锁只保护 Filter 成员表，不会冻结 World、组件或实体生命期。

## 2 五类对象分别拥有些什么

| 对象 | 实际拥有的数据 | 不负责什么 |
| --- | --- | --- |
| `EcsWorld` | 实体元数据、回收编号、组件池、Filter 缓存与反向索引 | 不自动驱动游戏 Tick |
| `EcsPool<T>` | 一种组件的槽位数组、实体到槽位的索引、空闲槽位栈 | 不保存完整 Entity 对象 |
| `EcsFilter` | 查询条件、匹配实体数组、实体到数组位置的索引 | 不保存查询到的组件副本 |
| `EcsSystems` | 系统注册表、Run 与 PostRun 列表、World 引用、Shared 对象 | 不自动推导读写依赖 |
| `EcsPackedEntity` | 实体编号与代际 | 不保证拿到任意 World 都能正确解包 |

因此，实体本身只是 `int` 身份索引。组件属于 World 下的对应组件池，系统通过实体编号取得这些组件。

例如一个角色同时具有 Position 和 Health，两个组件分别存在两个数组里；Filter 只返回角色编号。

## 3 World 怎样保存实体

核心依据：[worlds.cs 第 15 行起](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/worlds.cs#L15)。

World 使用一个 `short[]` 保存所有实体的元数据，每个实体占用相同步长的一段：

`组件数量 | 代际 | 组件类型编号 0 | 组件类型编号 1 | ……`

实体编号乘以步长，就得到这段数据的起始位置。组件内容不在这里，这里只记录“该实体拥有哪些组件类型”。

还存在两个计数：

- 已分配编号的上界：新编号从这里继续增加，不等于当前存活实体数。
- 回收编号栈的长度：删除实体后，编号进入栈，下一次创建优先弹出。

`NewEntity()` 的主要步骤，见 [第 148 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/worlds.cs#L148)：

1. 回收栈不空时取出一个编号，把其负代际翻成正代际。
2. 否则分配新编号，新实体代际设为 1。
3. 实体容量不足时扩容元数据，同时扩容所有组件池与 Filter 的稀疏索引。
4. 返回编号；调用方随后添加组件。

新实体暂时没有组件，但 LeoECS Lite 不允许业务长期保留空实体。调试版本在系统执行边界检查这种遗漏。

还有一项成本：某实体拥有的组件种类超出当前步长时，`ExtendEntitiesCache()` 会扩大所有实体的元数据步长并复制已有元数据。它不是只扩当前实体。

**给 MmECS 的设计判断：** 是否允许空实体应当写成公开契约，不应让业务根据调试异常猜测。首版可以采用显式销毁实体，让“删除最后一个组件”和“销毁身份”成为两个清楚的操作。

## 4 组件池不是没有空洞的密集集合

核心依据：[components.cs 第 38 行起](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/components.cs#L38)。

每个 `EcsPool<T>` 保存三组数组：

| 源码字段 | 内容 | 特殊约定 |
| --- | --- | --- |
| `_denseItems` | `T` 组件槽位 | 下标 0 不作为有效组件槽位 |
| `_sparseItems` | 实体编号到组件槽位的映射 | 0 表示该实体没有此组件 |
| `_recycledItems` | 可复用的组件槽位编号 | 按栈的次序回收与分配 |

以下是说明数据布局的示例，编号只用于演算：

| 实体编号 | Health 池稀疏索引 | Health 实际内容 |
| --- | --- | --- |
| 7 | 1 | 槽位 1 的血量为 100 |
| 12 | 2 | 槽位 2 的血量为 80 |
| 19 | 3 | 槽位 3 的血量为 60 |

查询实体 12 的 Health，先查 `_sparseItems[12]` 得到 2，再访问 `_denseItems[2]`。

删除实体 12 的 Health 后，槽位 2 被重置并放入回收栈，实体 19 的组件仍然位于槽位 3。

下次给实体 25 添加 Health，会优先复用槽位 2。槽位编号因此不是永久身份。

`_denseItemsCount` 是已使用过的槽位区间上界，包含保留的 0 号位置和删除后留下的洞，不能当成当前活跃组件数量。

这种布局避免了删除一个组件时搬动其他实体的组件，但不能直接遍历整个所谓 dense 数组就断言每项都有效。

性能上的代价是空洞、稀疏索引访问，以及每种组件都按实体容量分配索引数组。组件种类多而且大多只挂在少数实体上时，这部分内存需要测量。

## 5 添加与读取组件的完整调用关系

核心依据：[GetPool 第 253 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/worlds.cs#L253)、[Add 第 180 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/components.cs#L180)、[Get 第 206 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/components.cs#L206)。

`World.GetPool<Health>()` 首次执行时，根据类型创建组件池，并分配这个 World 内的组件类型编号；再次获取返回同一个池。

类型编号按首次创建组件池的顺序分配，不能直接用作跨版本存档中永久稳定的类型标识。

`HealthPool.Add(EntityId)` 的执行顺序是：

1. 从组件回收栈取槽位，或者从数组尾部开新槽位；必要时扩容组件数组。
2. 新开槽位时调用可选的 `AutoReset`，回收槽位已在上次删除时重置。
3. 写入实体到槽位的稀疏映射，让 `Has(EntityId)` 开始返回 true。
4. 通知 World 更新与 Health 有关的 Filter。
5. 在实体元数据中记录 Health 类型编号，并增加组件数量。
6. 发出可选 World 监听通知，最后把组件槽位以 `ref T` 返回调用方。

调用方通过返回的 `ref` 写入血量，修改的就是池中的组件内容，无须调用额外的 Set。

但 Filter 已经在 Add 返回之前更新。结构变更监听器不能把“组件刚挂上”理解成“调用方已填写完整业务数据”。

`Get(EntityId)` 本身只执行稀疏索引加数组取引用；缓存组件池可以避免热循环中重复按类型获取组件池。

## 6 Filter 为什么只构建一次就能持续更新

核心依据：[Mask.End 第 611 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/worlds.cs#L611)、[GetFilterInternal 第 411 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/worlds.cs#L411)。

条件如“包含 Position 和 Velocity，不包含 Frozen”，会先转换成组件类型编号列表。

构建时执行：排序 Include 与 Exclude、计算哈希、查询已有 Filter；首次创建才扫描已有实体，并把匹配编号加入 Filter。

同时建立两种反向索引：每种组件类型对应“包含此组件的 Filter 列表”和“排除此组件的 Filter 列表”。

因此增加或删除 Frozen 时，World 只重新判断与 Frozen 有关的 Filter，不需要扫描全部 Filter 和全部实体。

Filter 内部也是两级索引：连续的实体编号数组，以及实体编号到数组位置加一的稀疏映射。0 表示不属于这个 Filter。

这里删除成员采用末尾覆盖。例如实体列表 `[7, 12, 19]` 删除 12 后变成 `[7, 19]`，并修正 19 的反向位置。

这会改变查询遍历顺序，所以不能默认 Filter 会永久按创建时间或实体编号排序。

组件变化时的判断，见 [OnEntityChangeInternal 第 449 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/worlds.cs#L449)：

| 变化 | 对 Include 此组件的 Filter | 对 Exclude 此组件的 Filter |
| --- | --- | --- |
| 添加组件 | 其余条件满足时加入 | 其余条件满足时移除 |
| 删除组件 | 原先条件满足时移除 | 假定该组件不存在后其余条件满足时加入 |

添加时稀疏索引已经写好，删除通知则发生在稀疏索引清零之前。因此删除分支需要按“即将删除”的语义检查 Exclude，不能随意颠倒操作顺序。

代价也要看到：查询遍历减少了条件检查，结构变更则承担 Filter 维护成本；每个 Filter 另有按实体容量分配的稀疏索引。

**不宜照抄的一处实现：** 该版本 Filter 缓存只用 32 位哈希作为键，命中后不再次比较完整 Include 与 Exclude。哈希相等不证明条件相同，MmECS 应以完整条件相等作为最终判定。这是源码可见的设计缺口，本次未运行碰撞测试。

## 7 Filter 锁到底保护了什么

核心依据：[filters.cs 第 70 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/filters.cs#L70)、[第 109 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/filters.cs#L109)、[第 181 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/filters.cs#L181)。

`foreach` 取得枚举器时增加 Filter 的锁计数。枚举器保存当前实体数组和当前数量。

锁计数大于 0 时，Filter 的添加与删除成员操作进入延迟操作数组，暂时不修改成员表。最外层枚举器 Dispose 后，才按记录顺序执行这些操作。

这里的锁是遍历深度计数，不是线程同步锁，也没有为整个世界提供事务。

假设当前 Filter 中有实体 7 和 12。在处理 7 时删除 12 的 Health，池中的 Health 会立即消失，但 Filter 暂时仍包含 12；接下来枚举到 12 再读 Health 就可能违反访问契约。

同样，新实体即使已经拥有所需组件，也不会立即加入正在枚举的这个 Filter；其他未被锁定的 Filter 则可能已经更新。

因此“允许遍历时删除当前元素”不能扩展解释为“遍历时任意修改所有实体都安全”。

MmECS 首版更容易解释和验证的选择是：系统执行阶段允许写已有组件数值，增删组件与实体写入结构变更缓冲，在指定阶段统一提交。

这会改变可见性：提交前创建或删除的结构对后续查询是否可见，应明确由提交点决定，而不是套用 LeoECS Lite 的 Filter 局部锁规则。

## 8 删除组件与销毁实体怎样收尾

核心依据：[Pool.Del 第 222 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/components.cs#L222)、[World.DelEntity 第 185 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/worlds.cs#L185)。

删除单个组件：通知 Filter、回收槽位、重置数据、清除稀疏索引，再从实体元数据移除组件类型编号。

实体元数据中的类型编号列表也采用末尾覆盖，避免在这个小列表里整体移动后续元素。

如果删除的是最后一个组件，组件池会继续调用 `World.DelEntity()`，回收实体编号并推进代际。

主动调用 `World.DelEntity()` 时，它根据实体元数据逆序删除所有组件。最后一个组件删除时会再次进入 `DelEntity()`，此时组件数量为零，才执行实体编号回收。

这是一次有意安排的回调式收尾；自研时可以把销毁流程集中管理，以减少理解和维护这类重入关系的成本。

`AutoReset` 允许组件自行清理或重建内部数据；没有它时直接赋 `default`。但实现自定义回收逻辑并不自动解决存档、快照或深拷贝。

## 9 编号为什么需要代际

核心依据：[entities.cs 第 13 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/entities.cs#L13)、[Unpack 第 88 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/entities.cs#L88)。

假设目标实体是编号 7、代际 1。它死亡后，编号 7 被回收，新角色可能再次使用编号 7。

只保存整数 7 的技能目标会错误指向新角色；保存 `(7, 1)` 后，解包检查发现当前代际已变成 2，就能拒绝这次访问。

LeoECS Lite 的存活代际为正数，删除后写入下一代际的负数，编号复用时翻为正数。

底层代际存放于 `short`。达到 `short.MaxValue` 后会回到 1，意味着长期持有的过时代际经过足够多次编号复用后可能再次相等。

`EcsPackedEntity` 没有 World 身份，调用方必须传入正确 World；`EcsPackedEntityWithWorld` 则额外保存 World 对象引用。

MmECS 可以采用更宽的代际并明确溢出策略。View、跨 Tick 请求、实体之间的引用应保存带代际的句柄，不能长期只保留裸编号。

快照回滚还有额外问题：外部 View 持有的是旧时间线句柄，即使恢复后编号和代际碰巧相等，也可能需要按 World 恢复版本重新绑定。代际机制不能独自识别时间线切换。

## 10 `ref` 的真实有效范围

`ref T` 指向某个数组对象中的槽位，它不会随着池的内部数组被替换而自动改指向新数组。

| 后续操作 | 原先取得的组件 ref 会怎样 |
| --- | --- |
| 只修改该组件字段 | 仍然指向当前组件 |
| 同类组件池扩容 | 池改用新数组，旧 ref 仍指向旧数组，后续写入不再更新池中的组件 |
| 删除 ref 对应的组件 | 原槽位被清理，该 ref 已失去业务有效性 |
| 删除后槽位被复用 | 原 ref 可能读写另一个实体的新组件 |
| 只删除别的实体的同类组件 | 当前版本不会通过末尾覆盖搬动当前组件 |
| 换 World 或恢复时替换存储 | 原 ref 不会自动重新绑定 |

托管数组扩容通常不会制造可直接类比原生悬空指针的内存错误，但会制造更隐蔽的错误对象写入或脱离池的写入。

实用契约是：ref 仅供当前局部处理使用，取得后到使用完毕之间不执行可能影响其存储的结构变更，不向 View 或跨 Tick 缓存暴露组件 ref。

## 11 Systems 只规定顺序，没有自动理解业务

核心依据：[systems.cs 第 102 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/systems.cs#L102)、[第 120 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/systems.cs#L120)。

注册系统时，容器按其实现的接口加入 Run 和 PostRun 列表。

初始化先按注册顺序执行所有 PreInit，再执行所有 Init；每次 Run 先执行全部 Run，再执行全部 PostRun；销毁阶段按反向注册顺序处理。

它不会分析哪个系统写 Health，也不会自动保证伤害先于死亡检测。顺序由调用方注册与组织。

`Shared` 只是传入的一份对象引用，可以承载配置或外部服务。若把随机状态、计时器、待处理指令藏进 Shared，这些数据不会因为存在 World 就自动纳入快照。

同理，可选的 World 和 Filter 监听器通知的是结构变化；已有组件血量从 100 改成 90 并不会自动触发它们。UI 受击事件需要由业务显式产生。

## 12 稳定快照为什么不能只复制组件数组

恢复以后如果还要继续模拟，就要恢复所有会影响未来结果的数据：

| 数据类别 | 必须考虑的内容 |
| --- | --- |
| 实体分配 | 编号上界、存活状态、代际、回收栈及其顺序 |
| 组件结构 | 实体持有哪些组件、稀疏索引、槽位计数、回收槽位及其顺序 |
| 组件数据 | 所有有效组件字段，以及可变引用字段指向的内容 |
| 世界业务状态 | Tick、随机源、计时器、业务单例、未消费请求 |
| 查询相关 | 查询条件、恢复后的缓存一致性、业务是否依赖遍历顺序 |
| 世界外状态 | 系统或 Shared 中是否隐藏了影响未来计算的数据 |

Filter 成员表通常可以从组件重新构建，但重建得到的顺序不一定等于恢复前顺序。如果业务按“第一个匹配对象”选择目标，顺序变化就会改变结果。

解决办法是保留所需顺序状态，或者让业务使用明确的排序与选择规则；不能只说“Filter 是缓存，随便重建即可”。

组件约束只有 `struct`，仍允许包含 List、数组、类引用。默认组件 Copy 使用赋值，引用字段共享同一对象，不是独立快照。

`IEcsAutoCopy<T>` 提供定制复制入口，但 World 没有因此自动获得完整快照协议。`CopyEntity()` 也是把源实体组件复制到目标实体，不是恢复整个世界。

MmECS 可以从 Tick 边界快照开始：结构变更已提交、枚举已结束、临时输出处理规则已确定，再保存身份分配和业务状态。恢复后统一使外部查询缓存与 View 绑定失效并重建。

## 13 把研究结果转成 MmECS 的具体要求

直接借鉴：World 隔离、每类组件独立存储、实体代际、系统显式排序、查询条件与组件数据分离、热路径缓存查询和组件池。

需要自己确定：空实体是否允许、组件删除是否搬动数据、结构变更何时生效、ref 生命周期、查询顺序是否稳定、快照覆盖哪些业务状态。

需要改进后采用：Filter 缓存比较完整条件、实体代际宽度与溢出规则、外部句柄的 World 与恢复版本归属。

先不要照搬：条件编译后移除大量检查、直接暴露内部数组与计数引用、依赖组件回调的销毁重入、把结构监听通知当作完整业务事件系统。

下一步写代码时应验证的关键行为：删除后编号复用不误认旧实体、组件扩容前后访问符合约定、查询增删成员正确、结构变更提交可见性一致、快照恢复后相同输入产生预期状态。

这些是后续实现的验收项目，不代表本次已经执行了相应测试。

## 14 源码入口索引

| 阅读主题 | 本地文件 | 固定版本入口 |
| --- | --- | --- |
| 实体创建销毁与组件元数据 | [worlds.cs](参考源码/ecslite/src/worlds.cs) | [第 148 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/worlds.cs#L148) |
| 组件存储与增删访问 | [components.cs](参考源码/ecslite/src/components.cs) | [第 180 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/components.cs#L180) |
| 查询成员与遍历锁 | [filters.cs](参考源码/ecslite/src/filters.cs) | [第 109 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/filters.cs#L109) |
| 实体句柄与代际校验 | [entities.cs](参考源码/ecslite/src/entities.cs) | [第 78 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/entities.cs#L78) |
| 系统生命周期与运行顺序 | [systems.cs](参考源码/ecslite/src/systems.cs) | [第 102 行](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/src/systems.cs#L102) |
| 使用契约与许可证 | [README](参考源码/ecslite/README.md)、[LICENSE](参考源码/ecslite/LICENSE.md) | [固定版本 README](https://github.com/Leopotam/ecslite/blob/d46f3b209ea163810823aeea7d2610d38ad6cdf8/README.md) |
