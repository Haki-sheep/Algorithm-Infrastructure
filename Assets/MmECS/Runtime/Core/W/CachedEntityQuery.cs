using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 缓存匹配实体列表的热查询 Filter 适用于高频查询
    /// </summary>
    public sealed class CachedEntityQuery
    {
        private readonly World world;
        private readonly IComponentStorage[] allList;
        private readonly IComponentStorage[] noneList;
        private readonly List<Entity> cachedEntities = new List<Entity>();
        private bool isDirty = true;

        /// <summary>
        /// 绑定查询条件并标记为待刷新
        /// </summary>
        internal CachedEntityQuery(World World, IComponentStorage[] AllList, IComponentStorage[] NoneList)
        {
            world = World;
            allList = AllList;
            noneList = NoneList;
        }

        /// <summary>
        /// 标记缓存失效 在结构提交后由 World 调用
        /// </summary>
        internal void MarkDirty()
        {
            isDirty = true;
        }

        /// <summary>
        /// 返回缓存的匹配实体列表 首次或失效后重建
        /// </summary>
        public List<Entity> GetEntities()
        {
            if (isDirty)
            {
                Rebuild();
                isDirty = false;
            }
            return cachedEntities;
        }

        /// <summary>
        /// 重新扫描并更新缓存列表 调用 World 的查询逻辑
        /// </summary>
        private void Rebuild()
        {
            world.ExecuteQueryInternal(allList, noneList, cachedEntities);
        }
    }
}
