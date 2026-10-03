using System;
using System.Collections.Generic;

namespace MmECS.Samples
{
    /// <summary>
    /// 验证快照独立性与逐步重演及恢复后的宿主重绑
    /// </summary>
    public static partial class CoreVerification
    {
        /// <summary>
        /// 两次恢复同一快照后逐 Tick 比较状态与跨类型事件顺序
        /// </summary>
        private static void VerifyReplay()
        {
            var World = CreateWorld();
            var Entity = CreateEntity(World, 10);
            using var Runner = new Simulation(World, 0.5d);
            Runner.Register(new ReplaySystem(Entity));
            var EventList = new List<string>();
            long SequenceBase = -1;
            // 输出序号恢复时归零 比较分支内相对顺序而不是跨批次身份
            using var FirstSubscription = Runner.Events.Subscribe<Position>((Frame, Message) =>
            {
                if (SequenceBase < 0) SequenceBase = Frame.Sequence;
                EventList.Add($"{Frame.Tick}/{Frame.Sequence - SequenceBase}/Position/{Message.Value}");
            });
            using var SecondSubscription = Runner.Events.Subscribe<Velocity>((Frame, Message) =>
            {
                if (SequenceBase < 0) SequenceBase = Frame.Sequence;
                EventList.Add($"{Frame.Tick}/{Frame.Sequence - SequenceBase}/Velocity/{Message.Value}");
            });
            Check(Runner.Advance(0.75d) == 1, "初次累计时间推进数量错误");
            Runner.DispatchEvents();
            var Snapshot = Runner.Capture();
            var SnapshotStateDict = Runner.InspectState();
            var StateList = new List<SortedDictionary<string, string>>();
            var ExpectedEventList = new List<string>();
            int InputCount = 4;
            for (int Pass = 0; Pass < 3; Pass++)
            {
                if (Pass > 0)
                {
                    var OldHandle = World.GetEntityHandle(Entity);
                    Runner.Restore(Snapshot);
                    Check(!World.TryResolve(OldHandle, out _), "恢复后旧实体句柄仍有效");
                    Check(StateDiagnostics.Compare(SnapshotStateDict, Runner.InspectState()).Count == 0,
                        "快照被后续执行污染或恢复不完整");
                }
                EventList.Clear();
                SequenceBase = -1;
                for (int Index = 0; Index < InputCount; Index++)
                {
                    double Elapsed = Index == 0 ? 0.25d : 0.5d;
                    Check(Runner.Advance(Elapsed) == 1, "恢复未保留累计时间");
                    Runner.DispatchEvents();
                    var StateDict = Runner.InspectState();
                    if (Pass == 0)
                        StateList.Add(StateDict);
                    else
                    {
                        var DifferenceList = StateDiagnostics.Compare(StateList[Index], StateDict);
                        Check(DifferenceList.Count == 0,
                            $"重演第 {Index + 1} 步状态不同 {string.Join(" | ", DifferenceList)}");
                    }
                }
                Check(EventList.Count == InputCount * 3, "重演事件数量错误");
                if (Pass == 0)
                    ExpectedEventList.AddRange(EventList);
                else
                    Check(string.Join("|", ExpectedEventList) == string.Join("|", EventList),
                        "重演事件 Tick 顺序或载荷不同");
            }
        }

        /// <summary>
        /// 恢复未消费创建结果并以新请求句柄重新领取
        /// </summary>
        private static void VerifySpawnRestore()
        {
            var World = CreateWorld();
            using var Runner = new Simulation(World, 0.5d);
            var RequestId = World.Structural.Spawn(new Position { Value = 4 }, new Velocity { Value = 2 });
            var OldRequest = World.GetSpawnRequestHandle(RequestId);
            Runner.Tick();
            var Snapshot = Runner.Capture();
            Check(World.TryConsumeSpawnResult(OldRequest, out var OldEntityHandle), "创建结果未发布");
            Check(World.TryResolve(OldEntityHandle, out var Entity), "创建结果的实体无效");
            Check(!World.TryConsumeSpawnResult(OldRequest, out _), "创建结果可重复消费");
            World.Structural.Destroy(Entity);
            Runner.Tick();
            Runner.Restore(Snapshot);
            Check(!World.TryConsumeSpawnResult(OldRequest, out _), "旧请求句柄可消费恢复结果");
            Check(!World.TryResolve(OldEntityHandle, out _), "旧创建实体句柄跨恢复有效");
            var RequestList = new List<SpawnRequestHandle>();
            var HandleList = new List<EntityHandle>();
            World.ReadSpawnRequestHandles(RequestList);
            World.ReadEntityHandles(HandleList);
            Check(RequestList.Count == 1 && HandleList.Count == 1, "恢复后重绑列表错误");
            Check(World.TryConsumeSpawnResult(RequestList[0], out var NewHandle) &&
                World.TryResolve(NewHandle, out var RestoredEntity) &&
                World.Get<Position>(RestoredEntity).Value == 4, "重新绑定未还原创建设定");
        }

        /// <summary>
        /// 拒绝初始捕获与跨实例恢复及非空结构队列边界
        /// </summary>
        private static void VerifySnapshotBoundary()
        {
            var World = CreateWorld();
            var Entity = CreateEntity(World, 1);
            using var Runner = new Simulation(World, 0.5d);
            Throws<InvalidOperationException>(() => Runner.Capture());
            Runner.Tick();
            var Snapshot = Runner.Capture();
            using var OtherRunner = new Simulation(CreateWorld(), 0.5d);
            OtherRunner.Tick();
            var BeforeDict = OtherRunner.InspectState();
            Throws<InvalidOperationException>(() => OtherRunner.Restore(Snapshot));
            Check(StateDiagnostics.Compare(BeforeDict, OtherRunner.InspectState()).Count == 0,
                "跨实例拒绝改变模拟状态");
            ulong Epoch = World.StateEpoch;
            World.Structural.Destroy(Entity);
            Throws<InvalidOperationException>(() => Runner.Capture());
            Throws<InvalidOperationException>(() => Runner.Restore(Snapshot));
            Check(World.StateEpoch == Epoch && World.Structural.PendingCount == 1,
                "边界拒绝改变批次或丢弃结构请求");
            Runner.Tick();
        }

        /// <summary>
        /// 使用世界随机源与单例驱动组件 信号和交替类型事件
        /// </summary>
        private sealed class ReplaySystem : ISimulationSystem, ISignalSystem, ISignalHandler<Position>
        {
            /// <summary> 同一时间线内受信任的模拟实体身份 </summary>
            private readonly Entity entity;

            /// <summary>
            /// 保存初始化时分配的内部实体身份
            /// </summary>
            public ReplaySystem(Entity Entity) => entity = Entity;

            /// <summary>
            /// 在唯一初始化入口登记同步信号处理
            /// </summary>
            public void BindSignals(SignalBus Signals) => Signals.Register<Position>(this);

            /// <summary>
            /// 更新组件与随机源后同步发送状态副本
            /// </summary>
            public void Tick(World World, float stepSeconds)
            {
                World.Get<Position>(entity).Value += World.NextRandomInt(100);
                var Message = World.Get<Position>(entity);
                World.Signals.Emit(in Message);
            }

            /// <summary>
            /// 修改可快照单例并按固定跨类型顺序生成事件
            /// </summary>
            public void OnSignal(World World, in Position Signal)
            {
                World.GetSingleton<int>()++;
                var Other = new Velocity { Value = World.GetSingleton<int>() };
                World.Events.Emit(in Signal);
                World.Events.Emit(in Other);
                World.Events.Emit(in Signal);
            }
        }
    }
}
