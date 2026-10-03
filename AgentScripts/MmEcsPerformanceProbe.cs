using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MmECS;
using Newtonsoft.Json;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.Profiling;

/// <summary>
/// 在 Editor 主线程测量 MmECS 耗时与分配并恢复 Profiler 设置
/// </summary>
public static class MmEcsPerformanceProbe
{
    /// <summary> 固定逻辑步长为每秒六十步 </summary>
    private const double StepSeconds = 1d / 60d;
    /// <summary> 普通路径每轮正式采样次数 </summary>
    private const int TimingSamples = 100;
    /// <summary> 分配路径正式采样次数 </summary>
    private const int AllocationSamples = 5;

    #region 执行与耗时
    /// <summary>
    /// 串行执行三轮耗时与一次 GC 专项及历史窗口和事件积压测试
    /// </summary>
    public static async Task<string> Run()
    {
        if (ProfilerDriver.deepProfiling)
            throw new InvalidOperationException("性能探测要求关闭 Deep Profiling");
        int MainThread = Thread.CurrentThread.ManagedThreadId;
        bool Enabled = ProfilerDriver.enabled;
        bool ProfileEditor = ProfilerDriver.profileEditor;
        bool CpuEnabled = Profiler.GetAreaEnabled(ProfilerArea.CPU);
        var CaseList = new List<Scenario>();
        try
        {
            ProfilerDriver.enabled = false;
            var Environment = new
            {
                Date = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("O"),
                Unity = UnityEngine.Application.unityVersion,
                Platform = UnityEngine.Application.platform.ToString(),
                Cpu = UnityEngine.SystemInfo.processorType,
                RamMiB = UnityEngine.SystemInfo.systemMemorySize,
                MainThread,
                StepSeconds,
                TimingSamples,
                AllocationSamples
            };
            string AcceptanceBefore = MmECS.Samples.CoreVerification.Run();
            AddScenarios(CaseList);
            var TimingList = new List<object>();
            for (int Round = 1; Round <= 3; Round++)
            {
                // 轮换顺序减小固定执行顺序造成的热状态偏差
                for (int Offset = 0; Offset < CaseList.Count; Offset++)
                {
                    int Index = (Offset + (Round - 1) * 5) % CaseList.Count;
                    TimingList.Add(MeasureTiming(CaseList[Index], Round));
                    await Task.Delay(20);
                    Check(Thread.CurrentThread.ManagedThreadId == MainThread, "测量离开 Editor 主线程");
                }
            }
            var MemoryList = new List<object>
            {
                MeasureHistory(10000, 120), MeasureHistory(50000, 120),
                MeasureBacklog(false, 120), MeasureBacklog(true, 120)
            };
            var AllocationList = await MeasureAllocations(CaseList, MainThread);
            foreach (var Case in CaseList) Case.Fixture.Verify();
            string AcceptanceAfter = MmECS.Samples.CoreVerification.Run();
            return JsonConvert.SerializeObject(new
            {
                Environment, Timings = TimingList, Allocations = AllocationList,
                RetainedMemory = MemoryList, AcceptanceBefore, AcceptanceAfter
            });
        }
        finally
        {
            foreach (var Case in CaseList) Case.Fixture.Dispose();
            ProfilerDriver.enabled = Enabled;
            ProfilerDriver.profileEditor = ProfileEditor;
            Profiler.SetAreaEnabled(ProfilerArea.CPU, CpuEnabled);
        }
    }

    /// <summary>
    /// 预热后记录逐次耗时与 Tick 数并在计时外验证世界状态
    /// </summary>
    private static object MeasureTiming(Scenario Case, int round)
    {
        int Warmup = Case.Samples == TimingSamples ? 10 : 3;
        for (int Index = 0; Index < Warmup; Index++) Case.Execute();
        Collect();
        var ElapsedList = new double[Case.Samples];
        var TickDeltaList = new long[Case.Samples];
        for (int Index = 0; Index < Case.Samples; Index++)
        {
            long BeforeTick = Case.Fixture.World.Tick;
            long Start = Stopwatch.GetTimestamp();
            Case.Execute();
            long End = Stopwatch.GetTimestamp();
            ElapsedList[Index] = (End - Start) * 1000d / Stopwatch.Frequency;
            TickDeltaList[Index] = Case.Fixture.World.Tick - BeforeTick;
        }
        Case.Fixture.Verify();
        var SortedList = (double[])ElapsedList.Clone();
        Array.Sort(SortedList);
        return new
        {
            Round = round, Case.Name, Warmup, Samples = Case.Samples,
            MeanMs = ElapsedList.Average(), MedianMs = SortedList[SortedList.Length / 2],
            P95Ms = SortedList[(int)Math.Ceiling(SortedList.Length * 0.95d) - 1],
            P99Ms = SortedList[(int)Math.Ceiling(SortedList.Length * 0.99d) - 1],
            MaxMs = SortedList[SortedList.Length - 1], ElapsedMs = ElapsedList, TickDeltaList
        };
    }

    /// <summary>
    /// 构建明确规模的移动 快照 结构 通信与追帧工作负载
    /// </summary>
    private static void AddScenarios(List<Scenario> CaseList)
    {
        foreach (int Count in new[] { 1000, 10000, 50000 })
            CaseList.Add(new Scenario($"Tick/{Count}", new Fixture(Count), Item => Item.Runner.Tick()));
        foreach (int Count in new[] { 10000, 50000 })
        {
            CaseList.Add(new Scenario($"Capture/{Count}", new Fixture(Count),
                Item => Item.LastSnapshot = Item.Runner.Capture()));
            CaseList.Add(new Scenario($"Restore/{Count}", new Fixture(Count),
                Item => Item.Runner.Restore(Item.Baseline)));
        }
        CaseList.Add(new Scenario("Capture/10000/10%", new Fixture(10000, 10),
            Item => Item.LastSnapshot = Item.Runner.Capture()));
        CaseList.Add(new Scenario("Restore/10000/10%", new Fixture(10000, 10),
            Item => Item.Runner.Restore(Item.Baseline)));
        CaseList.Add(new Scenario("Structural/1000pairs", new Fixture(10000), ApplyStructural));
        CaseList.Add(new Scenario("RestoreThenStructural/1000pairs", new Fixture(10000), Item =>
        {
            Item.Runner.Restore(Item.Baseline);
            ApplyStructural(Item);
        }));
        CaseList.Add(new Scenario("Events/1000/same", new Fixture(0, 1, 1), Item =>
        {
            Item.Runner.Tick();
            Check(Item.Runner.DispatchEvents() == 1, "连续同类型事件批次数错误");
        }));
        CaseList.Add(new Scenario("Events/1000/alternate", new Fixture(0, 1, 2), Item =>
        {
            Item.Runner.Tick();
            Check(Item.Runner.DispatchEvents() == 1000, "交替类型事件批次数错误");
        }));
        foreach (double Elapsed in new[] { 0.25d, 2d })
            CaseList.Add(new Scenario($"Advance/{Elapsed:R}s/10000", new Fixture(10000),
                Item => Item.Runner.Advance(Elapsed), 20));
    }

    /// <summary>
    /// 入队一千组标签添加移除并完成一次真实 Tick
    /// </summary>
    private static void ApplyStructural(Fixture Item)
    {
        for (int Index = 0; Index < 1000; Index++)
        {
            Item.World.Structural.Add(Item.EntityList[Index], new Tag { Value = Index });
            Item.World.Structural.Remove<Tag>(Item.EntityList[Index]);
        }
        Item.Runner.Tick();
        Check(Item.Runner.LastStructuralCount == 2000, "结构提交数量错误");
    }
    #endregion

    #region 分配采样
    /// <summary>
    /// 用空区间和已知数组校准 RawFrameDataView 后读取区间内 GC.Alloc
    /// </summary>
    private static async Task<List<object>> MeasureAllocations(List<Scenario> CaseList, int mainThread)
    {
        ProfilerDriver.profileEditor = true;
        Profiler.SetAreaEnabled(ProfilerArea.CPU, true);
        ProfilerDriver.enabled = true;
        await Task.Delay(150);
        var CalibrationList = new List<AllocationRecord[]>
        {
            await SampleAllocations("Calibration/empty", () => { }, mainThread),
            await SampleAllocations("Calibration/4096", () => GC.KeepAlive(new byte[4096]), mainThread),
            await SampleAllocations("Calibration/8192", () => GC.KeepAlive(new byte[8192]), mainThread),
            await SampleAllocations("Calibration/two4096", () =>
            {
                GC.KeepAlive(new byte[4096]);
                GC.KeepAlive(new byte[4096]);
            }, mainThread)
        };
        for (int Index = 0; Index < AllocationSamples; Index++)
        {
            var Empty = CalibrationList[0][Index];
            var Small = CalibrationList[1][Index];
            var Large = CalibrationList[2][Index];
            var Twice = CalibrationList[3][Index];
            Check(Empty.Bytes == 0 && Empty.Objects == 0 && Small.Bytes >= 4096 &&
                Large.Bytes - Small.Bytes == 4096 && Twice.Bytes == Small.Bytes * 2 &&
                Small.Objects == 1 && Large.Objects == 1 && Twice.Objects == 2,
                "Profiler 分配校准失败 不报告零分配");
        }
        var ResultList = new List<object>();
        foreach (var Calibration in CalibrationList) ResultList.Add(Calibration);
        foreach (var Case in CaseList)
        {
            for (int Index = 0; Index < 5; Index++) Case.Execute();
            ResultList.Add(await SampleAllocations(Case.Name, Case.Execute, mainThread));
        }
        return ResultList;
    }

    /// <summary>
    /// 独占名称标记五个同步区间 待帧完成后查找主线程分配子树
    /// </summary>
    private static async Task<AllocationRecord[]> SampleAllocations(string name, Action Operation, int mainThread)
    {
        Check(Thread.CurrentThread.ManagedThreadId == mainThread, "GC 采样离开主线程");
        var RecordList = new AllocationRecord[AllocationSamples];
        var MarkerDict = new Dictionary<string, AllocationRecord>();
        string Prefix = "MmECSProbe/" + Guid.NewGuid().ToString("N") + "/";
        for (int Index = 0; Index < RecordList.Length; Index++)
        {
            var Record = new AllocationRecord { Name = name, Sample = Index };
            RecordList[Index] = Record;
            MarkerDict.Add(Prefix + Index, Record);
        }
        int FirstFrame = ProfilerDriver.lastFrameIndex;
        for (int Index = 0; Index < RecordList.Length; Index++)
        {
            Profiler.BeginSample(Prefix + Index);
            try { Operation(); }
            finally { Profiler.EndSample(); }
        }
        await Task.Delay(100);
        Check(Thread.CurrentThread.ManagedThreadId == mainThread, "GC 读取离开主线程");
        int LastFrame = ProfilerDriver.lastFrameIndex;
        for (int Frame = Math.Max(FirstFrame, ProfilerDriver.firstFrameIndex); Frame <= LastFrame; Frame++)
        {
            using var View = ProfilerDriver.GetRawFrameDataView(Frame, 0);
            if (!View.valid) continue;
            for (int Sample = 0; Sample < View.sampleCount; Sample++)
            {
                if (!MarkerDict.TryGetValue(View.GetSampleName(Sample), out var Record)) continue;
                Record.Matches++;
                int End = Sample + 1 + View.GetSampleChildrenCountRecursive(Sample);
                for (int Child = Sample + 1; Child < End; Child++)
                {
                    if (View.GetSampleName(Child) != "GC.Alloc") continue;
                    Check(View.GetSampleMetadataCount(Child) == 1, "GC.Alloc 元数据数量异常");
                    long Bytes = View.GetSampleMetadataAsLong(Child, 0);
                    Check(Bytes > 0, "GC.Alloc 分配大小无效");
                    Record.Bytes += Bytes;
                    Record.Objects++;
                }
                Sample = End - 1;
            }
        }
        foreach (var Record in RecordList)
            Check(Record.Matches == 1, $"采样区间缺失或重复 {name} #{Record.Sample} matches={Record.Matches}");
        return RecordList;
    }
    #endregion

    #region 保留内存
    /// <summary>
    /// 保留完整历史窗口并在强制回收后测托管堆增量和捕获总耗时
    /// </summary>
    private static object MeasureHistory(int entityCount, int frames)
    {
        using var Item = new Fixture(entityCount);
        for (int Index = 0; Index < 5; Index++) Item.LastSnapshot = Item.Runner.Capture();
        Item.LastSnapshot = null;
        var SnapshotList = new Simulation.Snapshot[frames];
        long Before = GC.GetTotalMemory(true);
        long Start = Stopwatch.GetTimestamp();
        for (int Index = 0; Index < frames; Index++) SnapshotList[Index] = Item.Runner.Capture();
        double TotalMs = (Stopwatch.GetTimestamp() - Start) * 1000d / Stopwatch.Frequency;
        long After = GC.GetTotalMemory(true);
        Item.Runner.Restore(SnapshotList[0]);
        Item.Verify();
        GC.KeepAlive(SnapshotList);
        return new { Name = $"History/{entityCount}/{frames}", HeapDeltaBytes = After - Before, TotalMs };
    }

    /// <summary>
    /// 暂停消费一百二十 Tick 后测积压并核对全部事件可派发
    /// </summary>
    private static object MeasureBacklog(bool alternate, int ticks)
    {
        using var Item = new Fixture(0, 1, alternate ? 2 : 1);
        for (int Index = 0; Index < 5; Index++)
        {
            Item.Runner.Tick();
            Item.Runner.DispatchEvents();
        }
        long Before = GC.GetTotalMemory(true);
        long Start = Stopwatch.GetTimestamp();
        for (int Index = 0; Index < ticks; Index++) Item.Runner.Tick();
        double TotalMs = (Stopwatch.GetTimestamp() - Start) * 1000d / Stopwatch.Frequency;
        long After = GC.GetTotalMemory(true);
        int Batches = Item.Runner.DispatchEvents();
        Check(Batches == ticks * (alternate ? 1000 : 1), "积压事件数量错误");
        return new { Name = $"Backlog/{(alternate ? "alternate" : "same")}/{ticks}",
            Events = ticks * 1000, Batches, HeapDeltaBytes = After - Before, TotalMs };
    }

    /// <summary>
    /// 在采样区间外清理前一组临时分配
    /// </summary>
    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// 核验结果并在失败时立即停止探测
    /// </summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    #endregion

    /// <summary>
    /// 保存一个同步测量操作与其独立模拟实例
    /// </summary>
    private sealed class Scenario
    {
        /// <summary> 明确工作量的测量名称 </summary>
        public readonly string Name;
        /// <summary> 此项独占的世界与调度器 </summary>
        public readonly Fixture Fixture;
        /// <summary> 每轮正式计时次数 </summary>
        public readonly int Samples;
        /// <summary> 在计时和分配区间内执行的操作 </summary>
        private readonly Action<Fixture> operation;

        /// <summary>
        /// 保存工作负载与正式计时次数
        /// </summary>
        public Scenario(string name, Fixture Fixture, Action<Fixture> Operation, int samples = TimingSamples)
        {
            Name = name;
            this.Fixture = Fixture;
            operation = Operation;
            Samples = samples;
        }

        /// <summary>
        /// 执行工作负载 不在测量时创建委托
        /// </summary>
        public void Execute() => operation(Fixture);
    }

    /// <summary>
    /// 保存一个独占采样区间的原始分配记录
    /// </summary>
    private sealed class AllocationRecord
    {
        /// <summary> 此区间的工作负载名称 </summary>
        public string Name;
        /// <summary> 此项的样本序号 </summary>
        public int Sample;
        /// <summary> 区间内分配字节总数 </summary>
        public long Bytes;
        /// <summary> 区间内分配对象总数 </summary>
        public int Objects;
        /// <summary> 原始帧中出现的次数 必须为一 </summary>
        public int Matches;
    }

    /// <summary>
    /// 持有独立世界并在计时外初始化与核验及释放
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        /// <summary> 测量实例的世界 </summary>
        public readonly World World;
        /// <summary> 测量实例的固定步调度器 </summary>
        public readonly Simulation Runner;
        /// <summary> 初始化时分配的内部实体身份 </summary>
        public readonly Entity[] EntityList;
        /// <summary> 初次完成 Tick 时的独立快照 </summary>
        public readonly Simulation.Snapshot Baseline;
        /// <summary> 捕获测量最后一次产生的快照 </summary>
        public Simulation.Snapshot LastSnapshot;
        /// <summary> 每隔多少实体附加两种组件 </summary>
        private readonly int stride;
        /// <summary> 此实例是否逐 Tick 移动组件 </summary>
        private readonly bool moving;

        /// <summary>
        /// 构建指定数量实体与组件密度并完成初次 Tick
        /// </summary>
        public Fixture(int count, int stride = 1, int eventMode = 0)
        {
            this.stride = stride;
            moving = eventMode == 0;
            World = new World();
            World.Register<Position>();
            World.Register<Velocity>();
            World.Register<Tag>();
            EntityList = new Entity[count];
            for (int Index = 0; Index < count; Index++)
            {
                var Entity = World.Create();
                EntityList[Index] = Entity;
                if (Index % stride != 0) continue;
                World.Add(Entity, new Position());
                World.Add(Entity, new Velocity { X = 1f, Y = 2f, Z = 3f });
            }
            Runner = new Simulation(World, StepSeconds);
            if (moving) Runner.Register(new MovementSystem());
            else Runner.Register(new EventSystem(eventMode == 2));
            Runner.Tick();
            Runner.DispatchEvents();
            Baseline = Runner.Capture();
        }

        /// <summary>
        /// 在测量区间外验证实体数量与移动值及结构队列和标签清理
        /// </summary>
        public void Verify()
        {
            Check(World.Structural.PendingCount == 0, "测量后残留结构命令");
            var ResultList = new List<Entity>();
            World.Query(ResultList);
            Check(ResultList.Count == EntityList.Length, "测量改变实体数量");
            if (!moving) return;
            float Expected = 0f;
            for (long Tick = 0; Tick < World.Tick; Tick++) Expected += (float)StepSeconds;
            foreach (var Entity in EntityList)
            {
                Check(!World.Has<Tag>(Entity), "结构测量残留标签");
                if (Entity.Index % stride == 0)
                    Check(World.Get<Position>(Entity).X == Expected, "移动值与实际 Tick 数不一致");
                else Check(!World.Has<Position>(Entity), "稀疏实体意外获得组件");
            }
        }

        /// <summary>
        /// 释放系统和通信状态
        /// </summary>
        public void Dispose() => Runner.Dispose();
    }

    /// <summary>
    /// 每步通过双组件交集执行三轴移动
    /// </summary>
    private sealed class MovementSystem : ISimulationSystem
    {
        /// <summary>
        /// 创建无装箱遍历操作并推进全部匹配组件
        /// </summary>
        public void Tick(World World, float stepSeconds)
        {
            var Action = new MoveAction { Step = stepSeconds };
            World.ForEach<Position, Velocity, MoveAction>(ref Action);
        }
    }

    /// <summary>
    /// 以固定负载产生连续或交替类型表现事件
    /// </summary>
    private sealed class EventSystem : ISimulationSystem
    {
        /// <summary> 是否每条消息交替改变事件类型 </summary>
        private readonly bool alternate;

        /// <summary>
        /// 保存消息类型分布
        /// </summary>
        public EventSystem(bool alternate) => this.alternate = alternate;

        /// <summary>
        /// 在真实 Tick 内产生一千条按编号排序的纯数据事件
        /// </summary>
        public void Tick(World World, float stepSeconds)
        {
            for (int Index = 0; Index < 1000; Index++)
            {
                if (alternate && (Index & 1) != 0)
                {
                    var Message = new Velocity { X = Index };
                    World.Events.Emit(in Message);
                }
                else
                {
                    var Message = new Position { X = Index };
                    World.Events.Emit(in Message);
                }
            }
        }
    }

    /// <summary>
    /// 携带当前步长并直接写入三轴位置
    /// </summary>
    private struct MoveAction : IComponentAction<Position, Velocity>
    {
        /// <summary> 当前逻辑步秒数 </summary>
        public float Step;

        /// <summary>
        /// 读取速度并修改组件位置 不改变结构
        /// </summary>
        public void Execute(Entity Entity, ref Position Position, ref Velocity Velocity)
        {
            Position.X += Velocity.X * Step;
            Position.Y += Velocity.Y * Step;
            Position.Z += Velocity.Z * Step;
        }
    }

    /// <summary>
    /// 连续存储三轴位置并作为第一种事件载荷
    /// </summary>
    private struct Position
    {
        /// <summary> 横轴位置 </summary>
        public float X;
        /// <summary> 纵轴位置 </summary>
        public float Y;
        /// <summary> 深度位置 </summary>
        public float Z;
    }

    /// <summary>
    /// 连续存储三轴速度并作为第二种事件载荷
    /// </summary>
    private struct Velocity
    {
        /// <summary> 横轴速度 </summary>
        public float X;
        /// <summary> 纵轴速度 </summary>
        public float Y;
        /// <summary> 深度速度 </summary>
        public float Z;
    }

    /// <summary>
    /// 为结构请求提供带值标签组件
    /// </summary>
    private struct Tag
    {
        /// <summary> 入队时复制的标签值 </summary>
        public int Value;
    }
}
