using System;

namespace MmECS
{
    /// <summary>
    /// 保存世界与累计时间和系统配置 并让派生快照独立承载业务状态
    /// </summary>
    public partial class Simulation
    {
        /// <summary> 当前模拟实例专属的快照归属标记 </summary>
        private readonly object snapshotOwner = new object();

        /// <summary>
        /// 保存通用模拟状态 派生快照在相同归属和执行边界内恢复业务
        /// </summary>
        public class Snapshot
        {
            /// <summary> 创建此快照的模拟实例标记 </summary>
            internal readonly object owner;
            /// <summary> 独立保存的世界核心状态 </summary>
            internal readonly World.Snapshot worldState;
            /// <summary> 尚未用于逻辑推进的秒数 </summary>
            internal readonly double accumulatedSeconds;
            /// <summary> 捕获时按执行顺序保存的系统引用 </summary>
            internal readonly ISystem[] systemList;

            public long Tick => worldState.Tick;

            /// <summary>
            /// 复制世界与时间状态并记录所属实例和系统配置
            /// </summary>
            protected internal Snapshot(Simulation Simulation)
            {
                owner = Simulation.snapshotOwner;
                worldState = Simulation.world.CaptureState();
                accumulatedSeconds = Simulation.accumulatedSeconds;
                systemList = Simulation.systemList.ToArray();
            }

            /// <summary>
            /// 在宿主完成归属验证后恢复世界与累计时间 派生实现应先准备副本再调用基类
            /// </summary>
            protected internal virtual void RestoreState(Simulation Simulation)
            {
                Simulation.world.RestoreState(worldState);
                Simulation.accumulatedSeconds = accumulatedSeconds;
            }
        }

        /// <summary>
        /// 在成功完成的逻辑步边界保存模拟状态并保护业务捕获期间的重入
        /// </summary>
        public Snapshot Capture()
        {
            EnsureCompletedTick();
            executionInProgress = true;
            try { return CreateSnapshot(); }
            finally { executionInProgress = false; }
        }

        /// <summary>
        /// 恢复当前实例的世界与累计时间及派生快照保存的业务状态
        /// </summary>
        public void Restore(Snapshot Snapshot)
        {
            EnsureCompletedTick();
            if (!ReferenceEquals(snapshotOwner, Snapshot.owner))
                throw new InvalidOperationException("Snapshot belongs to another Simulation");
            if (systemList.Count != Snapshot.systemList.Length)
                throw new InvalidOperationException("Simulation systems changed since capture");
            for (int index = 0; index < systemList.Count; index++)
                if (!ReferenceEquals(systemList[index], Snapshot.systemList[index]))
                    throw new InvalidOperationException("Simulation systems changed since capture");

            executionInProgress = true;
            try { Snapshot.RestoreState(this); }
            finally { executionInProgress = false; }
        }

        /// <summary>
        /// 创建通用快照 派生宿主可返回追加业务数据的快照类型
        /// </summary>
        protected virtual Snapshot CreateSnapshot() => new Snapshot(this);
    }
}
