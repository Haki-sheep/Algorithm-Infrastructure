using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 提供实体查询入口 支持简单查询和缓存热查询
    /// </summary>
    public sealed partial class World
    {
        /// <summary> 需要在结构提交后失效的热查询列表 </summary>
        private readonly List<CachedEntityQuery> cachedQueryList = new List<CachedEntityQuery>();

        /// <summary>
        /// 根据必需与排除组件池筛选实体 清空并填充结果列表
        /// </summary>
        public void Query(Type[] AllList, Type[] NoneList, List<Entity> ResultList)
        {
            var allStorages = ResolveQueryStorage(AllList);
            var noneStorages = ResolveQueryStorage(NoneList);
            ExecuteQueryInternal(allStorages, noneStorages, ResultList);
        }

        /// <summary>
        /// 创建缓存匹配实体的热查询 适用于每 Tick 都要执行的高频查询
        /// </summary>
        public CachedEntityQuery CreateCachedQuery(Type[] AllList, Type[] NoneList)
        {
            var Query = new CachedEntityQuery(this, ResolveQueryStorage(AllList), ResolveQueryStorage(NoneList));
            cachedQueryList.Add(Query);
            return Query;
        }

        /// <summary>
        /// 标记所有热查询失效 在结构提交后调用
        /// </summary>
        internal void InvalidateCachedQueries()
        {
            foreach (var Query in cachedQueryList)
                Query.MarkDirty();
        }

        /// <summary>
        /// 执行查询逻辑 从最小必需池遍历并筛选匹配实体 供 CachedEntityQuery 调用
        /// </summary>
        /// <param name="allList">实体必须拥有的组件池</param>
        /// <param name="noneList">实体不能拥有的组件池</param>
        /// <param name="ResultList">筛选结果写入的列表</param>
        internal void ExecuteQueryInternal(IComponentStorage[] allList, IComponentStorage[] noneList, List<Entity> ResultList)
        {
            ResultList.Clear();
            if (allList.Length == 0)
            {
                Query(ResultList);
                int count = 0;
                for (int index = 0; index < ResultList.Count; index++)
                    if (MatchesQuery(ResultList[index].Index, allList, noneList))
                        ResultList[count++] = ResultList[index];
                ResultList.RemoveRange(count, ResultList.Count - count);
                return;
            }

            var Candidate = allList[0];
            for (int index = 1; index < allList.Length; index++)
                if (allList[index].Count < Candidate.Count)
                    Candidate = allList[index];
            for (int row = 0; row < Candidate.Count; row++)
            {
                int index = Candidate.EntityIndexAt(row);
                if (MatchesQuery(index, allList, noneList))
                    ResultList.Add(EntityAt(index));
            }
        }

        /// <summary>
        /// 验证实体是否包含全部必需组件且没有任何排除组件
        /// </summary>
        private bool MatchesQuery(int entityIndex, IComponentStorage[] allList, IComponentStorage[] noneList)
        {
            // 优先使用位掩码快速排除 类型数超过 64 时 mask 为 0 跳过位检查
            ulong entityMask = GetEntityComponentMask(entityIndex);
            ulong allMask = BuildTypeMask(allList);
            ulong noneMask = BuildTypeMask(noneList);

            if (allMask != 0 && (entityMask & allMask) != allMask)
                return false;
            if (noneMask != 0 && (entityMask & noneMask) != 0)
                return false;

            // 位掩码通过后再精确检查组件存在性
            for (int type = 0; type < allList.Length; type++)
                if (!allList[type].Has(entityIndex))
                    return false;
            for (int type = 0; type < noneList.Length; type++)
                if (noneList[type].Has(entityIndex))
                    return false;
            return true;
        }

        /// <summary>
        /// 根据组件池列表构建类型位掩码 超过 64 种类型时返回 0
        /// </summary>
        private ulong BuildTypeMask(IComponentStorage[] StorageList)
        {
            ulong Mask = 0;
            foreach (var Storage in StorageList)
            {
                int TypeId = GetComponentTypeId(Storage);
                if (TypeId >= 64) return 0;
                Mask |= 1UL << TypeId;
            }
            return Mask;
        }

        /// <summary>
        /// 按注册顺序绑定去重后的组件池并拒绝未注册类型
        /// </summary>
        private IComponentStorage[] ResolveQueryStorage(Type[] TypeList)
        {
            var OrderedList = new List<Type>();
            // 检查组件类型是否注册
            foreach (var ComponentType in TypeList)
            {
                if (!storageDict.ContainsKey(ComponentType))
                    throw new InvalidOperationException("Component type is not registered");
                if (!OrderedList.Contains(ComponentType))
                    OrderedList.Add(ComponentType);
            }
            // 按组件注册顺序排序
            OrderedList.Sort((Left, Right) => componentOrderDict[Left].CompareTo(componentOrderDict[Right]));
            // 创建组件池列表 填入已存储的组件池中的组件并返回
            var StorageList = new IComponentStorage[OrderedList.Count];
            for (int index = 0; index < OrderedList.Count; index++)
                StorageList[index] = storageDict[OrderedList[index]];
            return StorageList;
        }

        /// <summary>
        /// 按槽位顺序读取全部存活实体且包含空实体
        /// </summary>
        public void Query(List<Entity> ResultList)
        {
            ResultList.Clear();
            for (int index = 0; index < aliveList.Count; index++)
                if (aliveList[index])
                    ResultList.Add(new Entity(index, generationList[index]));
        }

        /// <summary>
        /// 按指定组件池的紧密行顺序读取匹配实体
        /// </summary>
        public void Query<T>(List<Entity> ResultList) where T : unmanaged
        {
            var Storage = GetStorage<T>();
            ResultList.Clear();
            for (int row = 0; row < Storage.Count; row++)
                ResultList.Add(EntityAt(Storage.EntityIndexAt(row)));
        }

        /// <summary>
        /// 将已验证的存活槽位转换为当前内部实体身份
        /// </summary>
        internal Entity EntityAt(int index) => new Entity(index, generationList[index]);

        /// <summary>
        /// 在双组件池数量相等时按初始化类型顺序选择候选池
        /// </summary>
        private bool PreferSecond<TFirst, TSecond>(IComponentStorage First, IComponentStorage Second)
        {
            return Second.Count < First.Count || (Second.Count == First.Count &&
                componentOrderDict[typeof(TSecond)] < componentOrderDict[typeof(TFirst)]);
        }
    }
}
