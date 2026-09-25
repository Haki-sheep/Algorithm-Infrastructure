using System;
using System.Collections.Generic;

namespace MmECS
{
    public sealed partial class World
    {
        #region 启动
        /// <summary>
        /// 是否已开始模拟
        /// </summary>
        private bool simulationStarted;

        /// <summary>
        /// 是否正在提交结构命令
        /// </summary>
        private bool committingStructural;

        /// <summary>
        /// 首次 Tick 开始时结束初始化阶段
        /// </summary>
        internal void BeginSimulation()
        {
            simulationStarted = true;
        }

        /// <summary>
        /// 阻止运行期即时修改结构
        /// </summary>
        private void EnsureInitializing()
        {
            if (simulationStarted)
                throw new InvalidOperationException(
                    "Runtime structural changes must use StructuralBuffer");
        }

        /// <summary>
        /// 仅允许初始化或结构提交阶段即时修改结构
        /// </summary>
        private void EnsureStructuralChangeAllowed()
        {
            if (simulationStarted && !committingStructural)
                throw new InvalidOperationException(
                    "Runtime structural changes must use StructuralBuffer");
        }
        #endregion

        #region 推进步
        /// <summary>
        /// 在 Tick 末提交缓冲命令
        /// </summary>
        internal void CommitStructural()
        {
            committingStructural = true;
            try
            {
                Structural.Commit(this);
            }
            finally
            {
                committingStructural = false;
            }
        }

        public long Tick { get; private set; }

        /// <summary>
        /// 推进已完成 Tick 编号
        /// </summary>
        internal void CompleteTick()
        {
            Tick++;
        }
        #endregion

        #region 世界实体管理
        /// <summary>
        /// 每个实体槽位当前的代际
        /// </summary>
        private readonly List<uint> generationList = new List<uint>();

        /// <summary>
        /// 每个实体槽位是否存活
        /// </summary>
        private readonly List<bool> aliveList = new List<bool>();

        /// <summary>
        /// 可复用的实体编号
        /// 当实体被销毁时入这个表
        /// </summary>
        private readonly List<int> freeIndexList = new List<int>();

        /// <summary>
        /// World的命令队列
        /// </summary>
        public StructuralBuffer Structural { get; } = new StructuralBuffer();

        /// <summary>
        /// 创建实体并返回当前身份
        /// </summary>
        public Entity Create()
        {
            EnsureInitializing();

            // 如果可以复用 直接拿最后一个出来
            if (freeIndexList.Count > 0)
            {
                int index = freeIndexList[freeIndexList.Count - 1];
                freeIndexList.RemoveAt(freeIndexList.Count - 1);

                aliveList[index] = true;
                return new Entity(index, generationList[index]);
            }

            // 如果没有可复用的 则创建新的实体 都在表末尾追加
            int newIndex = aliveList.Count;
            aliveList.Add(true);
            generationList.Add(1);
            return new Entity(newIndex, 1);
        }

        /// <summary>
        /// 检查实体编号和代际是否属于当前存活槽位
        /// </summary>
        public bool IsAlive(Entity entity)
        {
            int index = entity.Index;

            // 判断索引范围合理和 判断索引对应的存活,代数是否一致
            return index >= 0
            && index < aliveList.Count
            && aliveList[index]
            && generationList[index] == entity.Generation;
        }

        /// <summary>
        /// 销毁实体并回收编号
        /// </summary>
        public void Destroy(Entity entity)
        {
            EnsureStructuralChangeAllowed();
            if (!IsAlive(entity))
                throw new InvalidOperationException("Entity is not alive");

            int index = entity.Index;
            if (generationList[index] == uint.MaxValue)
                throw new InvalidOperationException("Entity generation is max value");

            RemoveAllComponents(index);
            aliveList[index] = false;
            // 销毁时 代数+1 创建时候就不用+1 反之亦然
            generationList[index]++;
            freeIndexList.Add(index);
        }

        #endregion
    }
}
