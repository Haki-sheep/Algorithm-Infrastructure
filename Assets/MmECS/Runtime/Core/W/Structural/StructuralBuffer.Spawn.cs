namespace MmECS
{
    /// <summary>
    /// 将两种初始组件复制为延迟创建请求并在提交后发布实体结果
    /// </summary>
    public sealed partial class StructuralBuffer
    {
        /// <summary>
        /// 保存两种初始组件并返回创建请求编号
        /// </summary>
        public SpawnRequestId Spawn<TFirst, TSecond>(TFirst first,
                                                    TSecond second)
                                                    where TFirst : unmanaged
                                                    where TSecond : unmanaged
        {
            var RequestId = world.AllocateSpawnRequestId();

            commandQueue.Enqueue(
                new SpawnCommand<TFirst, TSecond>(
                    RequestId, first, second));

            return RequestId;
        }

        /// <summary>
        /// 保存请求编号与两种组件值并按创建添加发布顺序执行
        /// </summary>
        private sealed class SpawnCommand<TFirst, TSecond> : AbsStructuralCommand
                                                            where TFirst : unmanaged
                                                            where TSecond : unmanaged
        {
            /// <summary> 当前创建请求的编号 </summary>
            private readonly SpawnRequestId requestId;

            /// <summary> 第一种组件的初始值 </summary>
            private readonly TFirst first;

            /// <summary> 第二种组件的初始值 </summary>
            private readonly TSecond second;

            /// <summary>
            /// 复制请求编号和两种初始组件
            /// </summary>
            public SpawnCommand(
                SpawnRequestId RequestId,
                TFirst first,
                TSecond second)
            {
                requestId = RequestId;
                this.first = first;
                this.second = second;
            }

            /// <summary>
            /// 创建实体并添加初始组件后发布结果
            /// </summary>
            public override void Apply(World World)
            {
                var Entity = World.Create();

                World.Add(Entity, first);
                World.Add(Entity, second);

                World.PublishSpawnResult(requestId, Entity);
            }
        }
    }
}
