using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 绑定所属世界并维护结构命令 FIFO 及组件增删和实体销毁请求
    /// </summary>
    public sealed partial class StructuralBuffer
    {
        /// <summary> 结构缓冲所属的世界 </summary>
        private readonly World world;

        internal bool IsEmpty => commandQueue.Count == 0;

        public int PendingCount => commandQueue.Count;

        public int LastCommittedCount { get; private set; }

        /// <summary>
        /// 绑定结构缓冲所属的世界
        /// </summary>
        internal StructuralBuffer(World World) => world = World;


        /// <summary>
        /// 定义结构请求在所属 World 提交时执行的统一入口
        /// </summary>
        private abstract class AbsStructuralCommand
        {
            public abstract void Apply(World World);
        }
        /// <summary>
        /// 按入队顺序保存结构命令
        /// </summary>
        private readonly Queue<AbsStructuralCommand> commandQueue =
            new Queue<AbsStructuralCommand>();

        /// <summary>
        /// 登记组件添加请求
        /// </summary>
        public void Add<T>(Entity Entity, T value) where T : unmanaged
        {
            commandQueue.Enqueue(new AddCommand<T>(Entity, value));
        }
        /// <summary>
        /// 移除组件 入队
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="Entity">实体</param>
        public void Remove<T>(Entity Entity) where T : unmanaged
        {
            commandQueue.Enqueue(new RemoveCommand<T>(Entity));
        }

        /// <summary>
        /// 登记实体销毁请求
        /// </summary>
        public void Destroy(Entity Entity)
        {
            commandQueue.Enqueue(new DestroyCommand(Entity));
        }

        /// <summary>
        /// 出队 提交命令
        /// </summary>
        internal void Commit()
        {
            LastCommittedCount = 0;
            try
            { 
                while (commandQueue.Count > 0)
                {
                    commandQueue.Dequeue().Apply(this.world);
                    LastCommittedCount++;
                }
            }
            finally
            {
                commandQueue.Clear();
            }
        }

        /// <summary>
        /// 保存目标实体与组件值并在提交时添加组件
        /// </summary>
        /// <typeparam name="T">待添加的组件类型</typeparam>
        private sealed class AddCommand<T> : AbsStructuralCommand where T : unmanaged
        {
            /// <summary>
            /// 目标实体
            /// </summary>
            private readonly Entity entity;

            /// <summary>
            /// 入队时复制的组件值
            /// </summary>
            private readonly T value;

            /// <summary>
            /// 保存实体和组件值
            /// </summary>
            public AddCommand(Entity Entity, T value)
            {
                entity = Entity;
                this.value = value;
            }

            /// <summary>
            /// 提交时真正添加组件
            /// </summary>
            public override void Apply(World World)
            {
                World.Add(entity, value);
            }
        }

        /// <summary>
        /// 保存目标实体并在提交时移除指定类型组件
        /// </summary>
        /// <typeparam name="T">待移除的组件类型</typeparam>
        private sealed class RemoveCommand<T> : AbsStructuralCommand where T : unmanaged
        {
            /// <summary>
            /// 待移除组件的实体
            /// </summary>
            private readonly Entity entity;

            /// <summary>
            /// 保存目标实体
            /// </summary>
            public RemoveCommand(Entity Entity)
            {
                entity = Entity;
            }

            /// <summary>
            /// 提交时真正移除组件
            /// </summary>
            public override void Apply(World World)
            {
                World.Remove<T>(entity);
            }
        }

        /// <summary>
        /// 保存目标实体并在提交时销毁实体及其全部组件
        /// </summary>
        private sealed class DestroyCommand : AbsStructuralCommand
        {
            /// <summary>
            /// 待销毁的实体
            /// </summary>
            private readonly Entity entity;

            /// <summary>
            /// 保存目标实体
            /// </summary>
            public DestroyCommand(Entity Entity)
            {
                entity = Entity;
            }

            /// <summary>
            /// 提交时真正销毁实体
            /// </summary>
            public override void Apply(World World)
            {
                World.Destroy(entity);
            }
        }
    }
}
