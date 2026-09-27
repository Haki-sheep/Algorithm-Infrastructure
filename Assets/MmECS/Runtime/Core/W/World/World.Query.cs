using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 创建固定条件查询并提供全部实体和单组件查询入口
    /// </summary>
    public sealed partial class World
    {
        /// <summary>
        /// 将已注册类型条件复制为可反复执行的世界专属查询
        /// </summary>
        public EntityQuery CreateQuery(Type[] AllList, Type[] NoneList)
        {
            return new EntityQuery(this, ResolveQueryStorage(AllList), ResolveQueryStorage(NoneList));
        }

        /// <summary>
        /// 按注册顺序绑定去重后的组件池并拒绝未注册类型
        /// </summary>
        private IComponentStorage[] ResolveQueryStorage(Type[] TypeList)
        {
            var OrderedList = new List<Type>();
            foreach (var ComponentType in TypeList)
            {
                if (!storageDict.ContainsKey(ComponentType))
                    throw new InvalidOperationException("Component type is not registered");
                if (!OrderedList.Contains(ComponentType))
                    OrderedList.Add(ComponentType);
            }
            OrderedList.Sort((Left, Right) => componentOrderDict[Left].CompareTo(componentOrderDict[Right]));
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
