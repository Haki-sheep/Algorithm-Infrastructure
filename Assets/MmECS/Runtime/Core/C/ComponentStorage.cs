using System;

namespace MmECS
{
    /// <summary>
    /// 管理单一组件类型的 Sparse Set 存储及独立复制恢复
    /// unmanaged 限制组件不含托管引用
    /// 时间复杂度按固定组件类型计算 单个组件的复制与清零视为 O(1)
    /// </summary>
    /// <typeparam name="T">不含托管引用的组件类型</typeparam>
    public class ComponentStorage<T> : IComponentStorage where T : unmanaged
    {
        /// <summary>
        /// 实体 -> 行号 +1 1:1
        /// +1是因为我们规定 value = 0代表没有组件
        /// 但是行号=0本身是合法的
        /// </summary>
        private int[] sparseList = Array.Empty<int>();

        /// <summary>
        /// 行号 -> 实体编号 1:1
        /// </summary>
        private int[] denseEntityList = Array.Empty<int>();

        /// <summary>
        /// 行号 -> 组件数据 1:1
        /// </summary>
        private T[] denseValueList = Array.Empty<T>();

        /// <summary>
        /// 组件数量
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// 根据稠密行号取得实体编号 时间复杂度 O(1)
        /// </summary>
        public int EntityIndexAt(int row)
        {
            return denseEntityList[row];
        }

        /// <summary>
        /// 返回仅用于诊断的组件副本而不暴露数组引用 时间复杂度 O(1) 会产生装箱分配
        /// </summary>
        public object ReadBoxed(int row) => denseValueList[row];

        /// <summary>
        /// 根据实体编号判断组件是否存在 时间复杂度 O(1)
        /// </summary>
        public bool Has(int entityIndex)
        {
            return entityIndex < sparseList.Length
                && sparseList[entityIndex] != 0;
        }

        /// <summary>
        /// 将世界提供的非负实体编号映射为稠密行 缺少组件时返回负一 时间复杂度 O(1)
        /// </summary>
        internal int RowOf(int entityIndex) => entityIndex < sparseList.Length ? sparseList[entityIndex] - 1 : -1;

        /// <summary>
        /// 供世界在冻结结构的遍历内访问已验证的稠密行 时间复杂度 O(1)
        /// </summary>
        internal ref T ValueAt(int row) => ref denseValueList[row];

        /// <summary>
        /// 为实体添加组件并建立稀疏索引
        /// 不扩容时 O(1) 稀疏表扩容 O(S) 稠密表扩容 O(D) 同时扩容 O(S + D)
        /// S 与 D 分别为扩容后的稀疏表容量与稠密表容量 包含新数组清零成本
        /// 稠密表倍增追加均摊 O(1) 稀疏表成本取决于实体编号跨度 不保证每次 Add 均摊 O(1)
        /// </summary>
        public void Add(int entityIndex, T value)
        {
            // 如果该实体已经有相同组件 则抛出异常
            if (Has(entityIndex))
            {
                throw new InvalidOperationException("Component already exists");
            }

            // 如果稀疏表长度不够则扩容
            if (entityIndex >= sparseList.Length)
            {
                int capacity = Math.Max(entityIndex + 1, sparseList.Length * 2);
                Array.Resize(ref sparseList, capacity);
            }
            // 如果组件表长度不够则扩容
            if (Count >= denseValueList.Length)
            {
                int capacity = Math.Max(Count + 1, denseValueList.Length * 2);
                Array.Resize(ref denseEntityList, capacity);
                Array.Resize(ref denseValueList, capacity);
            }

            // 更新索引
            denseEntityList[Count] = entityIndex;
            denseValueList[Count] = value;
            sparseList[entityIndex] = Count + 1;
            Count++;
        }

        /// <summary>
        /// 根据实体编号取得组件引用 时间复杂度 O(1)
        /// </summary>
        public ref T Get(int entityIndex)
        {
            if (!Has(entityIndex))
                throw new InvalidOperationException("Component does not exist");

            int row = sparseList[entityIndex] - 1;
            return ref denseValueList[row];
        }

        /// <summary>
        /// 删除实体组件并保持有效行连续 时间复杂度 O(1) 仅用末行填洞并更新索引
        /// </summary>
        public void Remove(int entityIndex)
        {
            // 如果没有该实体组件 则抛出异常
            if (!Has(entityIndex))
                throw new InvalidOperationException("Component does not exist");

            int row = sparseList[entityIndex] - 1;
            int lastRow = Count - 1;

            if (row != lastRow)
            {
                // 如果不是最后一个 将最后一个组件移动到该组件的位置
                int movedEntityIndex = denseEntityList[lastRow];
                denseEntityList[row] = movedEntityIndex;
                denseValueList[row] = denseValueList[lastRow];
                sparseList[movedEntityIndex] = row + 1;
            }

            // 将最后一个实体组件干掉
            denseEntityList[lastRow] = default;
            denseValueList[lastRow] = default;
            sparseList[entityIndex] = 0;
            Count--;
        }

        /// <summary>
        /// 复制组件数据和索引并返回独立组件池
        /// 时间复杂度 O(S + N) S 为稀疏表容量 N 为有效组件数量 Count
        /// </summary>
        public IComponentStorage Clone()
        {
            var Storage = new ComponentStorage<T>();

            Storage.sparseList = (int[])sparseList.Clone();
            // Dense 仅复制有效行 空余容量不属于快照状态
            Storage.denseEntityList = new int[Count];
            Storage.denseValueList = new T[Count];
            Array.Copy(denseEntityList, Storage.denseEntityList, Count);
            Array.Copy(denseValueList, Storage.denseValueList, Count);
            Storage.Count = Count;

            return Storage;
        }

        /// <summary>
        /// 从同类型组件池副本恢复数据和索引
        /// 时间复杂度 O(S + D) S 与 D 分别为恢复后的稀疏表容量与稠密表容量
        /// 复用数组仍需复制快照并清零剩余容量 成本不只取决于快照有效数量
        /// </summary>
        public void Restore(IComponentStorage Snapshot)
        {
            var Storage = (ComponentStorage<T>)Snapshot;

            // 复制到活动数组而不引用快照数组 容量足够时复用已有内存
            RestoreArray(Storage.sparseList, ref sparseList);
            RestoreArray(Storage.denseEntityList, ref denseEntityList);
            RestoreArray(Storage.denseValueList, ref denseValueList);
            Count = Storage.Count;
        }

        /// <summary>
        /// 复制快照数组并清除活动数组超出快照的尾部 防止旧时间线数据残留
        /// 时间复杂度 O(L) L 为源数组长度与调用前目标数组长度中的较大值
        /// </summary>
        private static void RestoreArray<TValue>(TValue[] SourceList, ref TValue[] TargetList)
        {
            if (TargetList.Length < SourceList.Length)
                Array.Resize(ref TargetList, SourceList.Length);
            Array.Copy(SourceList, TargetList, SourceList.Length);
            Array.Clear(TargetList, SourceList.Length, TargetList.Length - SourceList.Length);
        }
    }
}
