using System;
using System.Text;

namespace MmECS.Samples
{
    /// <summary>
    /// 通过公开 API 验证核心契约 首个失败保留异常并终止
    /// </summary>
    public static partial class CoreVerification
    {
        /// <summary>
        /// 创建独立测试世界并返回全部通过的用例名称
        /// </summary>
        public static string Run()
        {
            var Output = new StringBuilder();
            RunCase(Output, "存储填洞与查询", VerifyStorage);
            RunCase(Output, "实体回收与世界隔离", VerifyEntityIdentity);
            RunCase(Output, "遍历保护与延迟提交", VerifyIteration);
            RunCase(Output, "结构命令 FIFO", VerifyStructuralOrder);
            RunCase(Output, "逐 Tick 状态与事件重演", VerifyReplay);
            RunCase(Output, "创建结果恢复与重新绑定", VerifySpawnRestore);
            RunCase(Output, "快照边界与归属拒绝", VerifySnapshotBoundary);
            RunCase(Output, "系统失败后终止", VerifySystemFailure);
            RunCase(Output, "结构提交失败后终止", VerifyStructuralFailure);
            RunCase(Output, "生命周期顺序与幂等释放", VerifyLifecycle);
            RunCase(Output, "初始化失败清理", VerifyInitializationFailure);
            RunCase(Output, "释放失败仍清理后续系统", VerifyDisposalFailure);
            RunCase(Output, "事件积压与恢复清理", VerifyEventBacklog);
            RunCase(Output, "事件回调失败不重播", VerifyEventFailure);
            return Output.ToString();
        }

        /// <summary>
        /// 执行单个用例 成功才记录结果 失败附带用例上下文
        /// </summary>
        private static void RunCase(StringBuilder Output, string name, Action Test)
        {
            try { Test(); }
            catch (Exception Failure)
            {
                throw new InvalidOperationException($"核心回归失败 {name}", Failure);
            }
            Output.AppendLine($"PASS {name}");
        }

        /// <summary>
        /// 验证条件并在失败时抛出带检查项的异常
        /// </summary>
        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        /// <summary>
        /// 要求操作抛出指定类型异常并返回原始异常供进一步断言
        /// </summary>
        private static TException Throws<TException>(Action Operation) where TException : Exception
        {
            try { Operation(); }
            catch (TException Failure) { return Failure; }
            throw new InvalidOperationException($"预期抛出 {typeof(TException).Name}");
        }

        /// <summary>
        /// 创建已注册两种组件和计数单例的独立世界
        /// </summary>
        private static World CreateWorld()
        {
            var World = new World(7);
            World.Register<Position>();
            World.Register<Velocity>();
            World.RegisterSingleton(0);
            return World;
        }

        /// <summary>
        /// 在初始化边界创建具有两种组件的实体
        /// </summary>
        private static Entity CreateEntity(World World, int value)
        {
            var Entity = World.Create();
            World.Add(Entity, new Position { Value = value });
            World.Add(Entity, new Velocity { Value = 2 });
            return Entity;
        }

        /// <summary>
        /// 保存可快照的位置与主事件载荷
        /// </summary>
        private struct Position
        {
            /// <summary> 当前数值 </summary>
            public int Value;
        }

        /// <summary>
        /// 保存速度与第二种事件载荷
        /// </summary>
        private struct Velocity
        {
            /// <summary> 当前数值 </summary>
            public int Value;
        }

        /// <summary>
        /// 将测试操作接入真实 Tick 调度
        /// </summary>
        private sealed class ActionSystem : ISimulationSystem
        {
            /// <summary> 本用例注入的系统操作 </summary>
            private readonly Action<World> action;

            /// <summary>
            /// 保存测试操作
            /// </summary>
            public ActionSystem(Action<World> Action) => action = Action;

            /// <summary>
            /// 在系统执行范围调用测试操作
            /// </summary>
            public void Tick(World World, float stepSeconds) => action(World);
        }
    }
}
