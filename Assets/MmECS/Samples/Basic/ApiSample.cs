using System.Text;

namespace MmECS.Samples
{
    /// <summary>
    /// 通过独立世界演示核心 API 调用 Run 获取执行轨迹 无需场景或 Unity 组件
    /// </summary>
    public static class ApiSample
    {
        /// <summary>
        /// 演示实体与遍历 系统信号与表现事件 快照重演和延迟销毁
        /// </summary>
        public static string Run()
        {
            var World = new World();
            World.Register<Position>();
            World.Register<Velocity>();
            World.RegisterSingleton(0);

            var Entity = World.Create();
            World.Add(Entity, new Position { X = 0f });
            World.Add(Entity, new Velocity { X = 2f });

            using var Runner = new Simulation(World, 0.5d);
            Runner.Register(new MovementSystem());
            Runner.Register(new MovementSignalSystem());

            var Output = new StringBuilder();
            // 订阅属于表现侧 用完释放 只有显式派发才执行回调
            using var Subscription = Runner.Events.Subscribe<Moved>((Frame, Message) =>
                Output.AppendLine($"事件 Tick={Frame.Tick} X={Message.X}"));

            Runner.Tick();
            Runner.DispatchEvents();
            var Snapshot = Runner.Capture();
            var Handle = World.GetEntityHandle(Entity);

            Runner.Advance(0.5d);
            Runner.DispatchEvents();

            // 恢复同时还原组件与单例 旧外部句柄失效 已播放的表现需由表现侧自行处理
            Runner.Restore(Snapshot);
            bool OldHandleValid = World.TryResolve(Handle, out _);
            Runner.Tick();
            Runner.DispatchEvents();
            Output.AppendLine($"重演 X={World.Get<Position>(Entity).X} 移动次数={World.GetSingleton<int>()} 旧句柄有效={OldHandleValid}");

            // 请求在下个 Tick 的系统执行后提交 因此该实体仍会完成最后一次移动
            World.Structural.Destroy(Entity);
            Output.AppendLine($"销毁请求后存活={World.IsAlive(Entity)}");
            Runner.Tick();
            Runner.DispatchEvents();
            Output.AppendLine($"结构提交后存活={World.IsAlive(Entity)}");
            return Output.ToString();
        }

        /// <summary>
        /// 保存一维位置
        /// </summary>
        private struct Position
        {
            /// <summary> 当前坐标 </summary>
            public float X;
        }

        /// <summary>
        /// 保存每秒位移
        /// </summary>
        private struct Velocity
        {
            /// <summary> 每秒移动距离 </summary>
            public float X;
        }

        /// <summary>
        /// 保存移动结果 作为同步信号传入逻辑 再作为事件副本交给表现
        /// </summary>
        private struct Moved
        {
            /// <summary> 移动后的坐标 </summary>
            public float X;
        }

        /// <summary>
        /// 每 Tick 遍历位置与速度 系统对象不保存跨 Tick 状态
        /// </summary>
        private sealed class MovementSystem : ISimulationSystem
        {
            /// <summary>
            /// 按固定步长更新匹配实体并在移动后同步发出信号
            /// </summary>
            public void Tick(World World, float stepSeconds)
            {
                var Action = new MoveAction { World = World, StepSeconds = stepSeconds };
                World.ForEach<Position, Velocity, MoveAction>(ref Action);
            }
        }

        /// <summary>
        /// 临时携带当前世界与步长 组件引用仅在处理函数内使用
        /// </summary>
        private struct MoveAction : IComponentAction<Position, Velocity>
        {
            /// <summary> 本次遍历所属世界 </summary>
            public World World;

            /// <summary> 本次逻辑步的秒数 </summary>
            public float StepSeconds;

            /// <summary>
            /// 直接写入位置并同步通知已绑定的系统
            /// </summary>
            public void Execute(Entity Entity, ref Position Position, ref Velocity Velocity)
            {
                Position.X += Velocity.X * StepSeconds;
                var Signal = new Moved { X = Position.X };
                World.Signals.Emit(in Signal);
            }
        }

        /// <summary>
        /// 仅响应信号 更新可快照的世界状态 并输出供表现消费的事件
        /// </summary>
        private sealed class MovementSignalSystem : ISignalSystem, ISignalHandler<Moved>
        {
            /// <summary>
            /// 由 Simulation 初始化入口绑定一次 强类型注册无需生成代码
            /// </summary>
            public void BindSignals(SignalBus Signals)
            {
                Signals.Register<Moved>(this);
            }

            /// <summary>
            /// 在移动信号发出点累计次数 并复制消息到当前 Tick 的表现输出
            /// </summary>
            public void OnSignal(World World, in Moved Signal)
            {
                World.GetSingleton<int>()++;
                World.Events.Emit(in Signal);
            }
        }
    }
}
