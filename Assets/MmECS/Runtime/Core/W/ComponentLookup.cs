using System;

namespace MmECS
{
    /// <summary>
    /// 绑定世界及组件池以复用类型查找 仅供模拟内部访问已有组件
    /// </summary>
    public readonly struct ComponentLookup<T> where T : unmanaged
    {
        /// <summary> 检查实体存活和代际的所属世界 </summary>
        private readonly World world;

        /// <summary> 保持对象身份且在扩容和恢复时替换内部数组的组件池 </summary>
        private readonly ComponentStorage<T> storage;

        /// <summary>
        /// 由世界绑定已注册的组件池 不保存数组或行地址
        /// </summary>
        internal ComponentLookup(World World, ComponentStorage<T> Storage)
        {
            world = World;
            storage = Storage;
        }

        /// <summary>
        /// 校验本世界实体并返回组件引用 引用不得跨越结构提交或快照恢复
        /// </summary>
        public ref T Get(Entity Entity)
        {
            if (!world.IsAlive(Entity))
                throw new InvalidOperationException("Entity is not alive");
            return ref storage.Get(Entity.Index);
        }
    }
}
