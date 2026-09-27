using System;

namespace MmECS
{
    /// <summary>
    /// 在稳定结构内单次遍历双组件交集并直接交付当前实体的组件引用
    /// </summary>
    public sealed partial class World
    {
        /// <summary> 当前直接组件遍历的嵌套层数 </summary>
        private int queryDepth;

        /// <summary>
        /// 按原双组件查询顺序处理匹配成员 引用仅在 Execute 内使用 结构变化须入队
        /// </summary>
        public void ForEach<TFirst, TSecond, TAction>(ref TAction Action)
            where TFirst : unmanaged
            where TSecond : unmanaged
            where TAction : struct, IComponentAction<TFirst, TSecond>
        {
            var FirstStorage = GetStorage<TFirst>();
            var SecondStorage = GetStorage<TSecond>();
            bool useSecond = PreferSecond<TFirst, TSecond>(FirstStorage, SecondStorage);
            IComponentStorage CandidateStorage = useSecond ? (IComponentStorage)SecondStorage : FirstStorage;
            queryDepth++;
            try
            {
                for (int row = 0; row < CandidateStorage.Count; row++)
                {
                    int entityIndex = CandidateStorage.EntityIndexAt(row);
                    int firstRow = useSecond ? FirstStorage.RowOf(entityIndex) : row;
                    int secondRow = useSecond ? row : SecondStorage.RowOf(entityIndex);
                    if (firstRow < 0 || secondRow < 0) continue;
                    // 候选池保证实体存活 匹配行已验证且结构被冻结 无需逐组件重复校验
                    Action.Execute(EntityAt(entityIndex), ref FirstStorage.ValueAt(firstRow), ref SecondStorage.ValueAt(secondRow));
                }
            }
            finally
            {
                queryDepth--;
            }
        }

        /// <summary>
        /// 阻止直接遍历期间改动结构或切换世界状态
        /// </summary>
        private void EnsureNotIterating()
        {
            if (queryDepth != 0)
                throw new InvalidOperationException("World is iterating components");
        }
    }

    /// <summary>
    /// 定义单个匹配实体的处理逻辑 由结构体实现以避免装箱和捕获分配
    /// </summary>
    public interface IComponentAction<TFirst, TSecond>
        where TFirst : unmanaged
        where TSecond : unmanaged
    {
        /// <summary>
        /// 读取本世界实体身份并修改已有组件 不保留组件引用
        /// </summary>
        public void Execute(Entity Entity, ref TFirst First, ref TSecond Second);
    }
}
