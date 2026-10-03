using System;
using System.Collections.Generic;

namespace MmECS.Samples
{
    /// <summary>
    /// 验证失败终止契约与系统生命周期清理
    /// </summary>
    public static partial class CoreVerification
    {
        /// <summary>
        /// 故意改变组件并抛错后验证原错保留且不允许推进或恢复
        /// </summary>
        private static void VerifySystemFailure()
        {
            var World = CreateWorld();
            var Entity = CreateEntity(World, 1);
            var Failure = new InvalidOperationException("测试系统失败");
            using var Runner = new Simulation(World, 0.5d);
            Runner.Register(new ActionSystem(CurrentWorld =>
            {
                if (CurrentWorld.Tick == 0) return;
                CurrentWorld.Get<Position>(Entity).Value = 99;
                var Message = new Position { Value = 99 };
                CurrentWorld.Events.Emit(in Message);
                throw Failure;
            }));
            Runner.Tick();
            var Snapshot = Runner.Capture();
            Check(ReferenceEquals(Throws<InvalidOperationException>(() => Runner.Tick()), Failure),
                "系统原始异常丢失");
            Check(World.Get<Position>(Entity).Value == 99 && World.Tick == 1,
                "失败 Tick 被误认为原子回滚或已完成");
            VerifyFaultedRunner(Runner, Snapshot);
        }

        /// <summary>
        /// 结构提交中途失败时验证已提交部分与队列清理及终止状态
        /// </summary>
        private static void VerifyStructuralFailure()
        {
            var World = CreateWorld();
            var Entity = CreateEntity(World, 1);
            using var Runner = new Simulation(World, 0.5d);
            Runner.Tick();
            var Snapshot = Runner.Capture();
            World.Structural.Remove<Position>(Entity);
            World.Structural.Remove<Position>(Entity);
            World.Structural.Destroy(Entity);
            Throws<InvalidOperationException>(() => Runner.Tick());
            Check(!World.Has<Position>(Entity) && World.IsAlive(Entity), "未保留失败前的部分提交");
            Check(Runner.LastStructuralCount == 1 && World.Structural.PendingCount == 0,
                "失败后提交计数或未执行命令清理错误");
            VerifyFaultedRunner(Runner, Snapshot);
        }

        /// <summary>
        /// 验证失败模拟拒绝继续 Tick 与 Advance 及快照和事件消费
        /// </summary>
        private static void VerifyFaultedRunner(Simulation Runner, Simulation.Snapshot Snapshot)
        {
            Throws<InvalidOperationException>(() => Runner.Tick());
            Throws<InvalidOperationException>(() => Runner.Advance(0.5d));
            Throws<InvalidOperationException>(() => Runner.Capture());
            Throws<InvalidOperationException>(() => Runner.Restore(Snapshot));
            Throws<InvalidOperationException>(() => Runner.DispatchEvents());
        }

        /// <summary>
        /// 验证初始化一次与注册顺序及逆序幂等释放
        /// </summary>
        private static void VerifyLifecycle()
        {
            var TraceList = new List<string>();
            using var Runner = new Simulation(CreateWorld(), 0.5d);
            var First = new LifecycleSystem("A", TraceList);
            Runner.Register(First);
            Throws<InvalidOperationException>(() => Runner.Register(First));
            Runner.Register(new LifecycleSystem("B", TraceList));
            Runner.Init();
            Runner.Init();
            Throws<InvalidOperationException>(() => Runner.Register(new LifecycleSystem("C", TraceList)));
            Runner.Tick();
            Runner.Dispose();
            Runner.Dispose();
            Check(string.Join("|", TraceList) == "Init A|Init B|Tick A|Tick B|Dispose B|Dispose A",
                "生命周期顺序或调用次数错误");
            Throws<ObjectDisposedException>(() => Runner.Tick());
        }

        /// <summary>
        /// 初始化失败时包含抛错系统并跳过尚未进入初始化的系统
        /// </summary>
        private static void VerifyInitializationFailure()
        {
            var TraceList = new List<string>();
            var Failure = new InvalidOperationException("测试初始化失败");
            using var Runner = new Simulation(CreateWorld(), 0.5d);
            Runner.Register(new LifecycleSystem("A", TraceList));
            Runner.Register(new LifecycleSystem("B", TraceList, Failure));
            Runner.Register(new LifecycleSystem("C", TraceList));
            var Aggregate = Throws<AggregateException>(() => Runner.Init());
            Check(Aggregate.InnerExceptions.Count == 1 && ReferenceEquals(Aggregate.InnerExceptions[0], Failure),
                "初始化原错丢失");
            Runner.Dispose();
            Check(string.Join("|", TraceList) == "Init A|Init B|Dispose B|Dispose A",
                "初始化失败清理范围错误");
            Throws<ObjectDisposedException>(() => Runner.Tick());
        }

        /// <summary>
        /// 释放异常聚合后仍清理其他系统且不会再次释放
        /// </summary>
        private static void VerifyDisposalFailure()
        {
            var TraceList = new List<string>();
            var Failure = new InvalidOperationException("测试释放失败");
            using var Runner = new Simulation(CreateWorld(), 0.5d);
            Runner.Register(new LifecycleSystem("A", TraceList));
            Runner.Register(new LifecycleSystem("B", TraceList, null, Failure));
            Runner.Init();
            var Aggregate = Throws<AggregateException>(() => Runner.Dispose());
            Check(Aggregate.InnerExceptions.Count == 1 && ReferenceEquals(Aggregate.InnerExceptions[0], Failure),
                "释放原错丢失");
            Runner.Dispose();
            Check(string.Join("|", TraceList) == "Init A|Init B|Dispose B|Dispose A",
                "释放错误导致后续系统未清理或重复清理");
        }

        /// <summary>
        /// 记录生命周期并在指定阶段注入原始异常
        /// </summary>
        private sealed class LifecycleSystem : ISimulationLifecycle
        {
            /// <summary> 用于检查调用顺序的系统名称 </summary>
            private readonly string name;
            /// <summary> 本用例独占的调用记录 </summary>
            private readonly List<string> traceList;
            /// <summary> 初始化阶段注入的错误 </summary>
            private readonly Exception initFailure;
            /// <summary> 释放阶段注入的错误 </summary>
            private readonly Exception disposeFailure;

            /// <summary>
            /// 保存调用记录及可选失败注入
            /// </summary>
            public LifecycleSystem(string name, List<string> TraceList,
                Exception InitFailure = null, Exception DisposeFailure = null)
            {
                this.name = name;
                traceList = TraceList;
                initFailure = InitFailure;
                disposeFailure = DisposeFailure;
            }

            /// <summary>
            /// 记录初始化并在配置失败时抛出原错
            /// </summary>
            public void Init(World World)
            {
                traceList.Add($"Init {name}");
                if (initFailure != null) throw initFailure;
            }

            /// <summary>
            /// 记录系统推进顺序
            /// </summary>
            public void Tick(World World, float stepSeconds) => traceList.Add($"Tick {name}");

            /// <summary>
            /// 记录释放并在配置失败时抛出原错
            /// </summary>
            public void Dispose(World World)
            {
                traceList.Add($"Dispose {name}");
                if (disposeFailure != null) throw disposeFailure;
            }
        }
    }
}
