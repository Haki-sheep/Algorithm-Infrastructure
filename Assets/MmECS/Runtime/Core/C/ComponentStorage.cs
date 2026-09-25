using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MmECS
{
    /// <summary>
    /// 组件存储器
    /// unmanaged 表示不含托管引用(如字符串,对象等)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ComponentStorage<T> :IComponentStorage where T : unmanaged
    {
        /// <summary>
        /// 实体 -> 行号 +1 1:1
        /// +1是因为我们规定 value = 0代表没有组件
        /// 但是行号=0本身是合法的
        /// </summary>
        private int[] sparseList = new int[0];

        /// <summary>
        /// 行号 -> 实体编号 1:1
        /// </summary>
        private int[] denseEntityList = new int[0];

        /// <summary>
        /// 行号 -> 组件数据 1:1
        /// </summary>
        private T[] denseValueList = new T[0];

        /// <summary>
        /// 组件数量
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// 根据稠密行号取得实体编号
        /// </summary>
        public int EntityIndexAt(int row)
        {
            return denseEntityList[row];
        }

        /// <summary>
        /// 根据实体编号判断组件是否存在
        /// </summary>
        public bool Has(int entityIndex)
        {
            return entityIndex < sparseList.Length
                && sparseList[entityIndex] != 0;
        }

        /// <summary>
        /// 为实体添加组件并建立稀疏索引
        /// </summary>
        public void Add(int entityIndex, T value)
        {
            // 如果该实体已经有相同组件 则抛出异常
            if (Has(entityIndex))
            {
                throw new InvalidOperationException("Component already exists");
            }

            // 如果系数表长度不够则扩容
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
        /// 根据实体编号取得组件引用
        /// </summary>
        public ref T Get(int entityIndex)
        {
            if (!Has(entityIndex))
                throw new InvalidOperationException("Component does not exist");

            int row = sparseList[entityIndex] - 1;
            return ref denseValueList[row];
        }

        /// <summary>
        /// 删除实体组件并保持有效行连续
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
    }
}
