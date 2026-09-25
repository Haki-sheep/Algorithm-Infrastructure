using System;
using System.Collections.Generic;

namespace MmECS
{
    public sealed partial class World
    {
        /// <summary>
        /// 组件类型对应的存储器
        /// </summary>
        private readonly Dictionary<Type, IComponentStorage> storageDict =
            new Dictionary<Type, IComponentStorage>();

        /// <summary>
        /// 实体编号对应的已附加组件类型
        /// </summary>
        private readonly Dictionary<int, List<Type>> ownedTypeDict =
            new Dictionary<int, List<Type>>();

        /// <summary>
        /// 注册一种组件类型及其存储器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void Register<T>() where T : unmanaged
        {
            EnsureInitializing();

            Type componentType = typeof(T);

            if (storageDict.ContainsKey(componentType))
                throw new InvalidOperationException($"Component type {componentType.Name} already registered");

            storageDict.Add(componentType, new ComponentStorage<T>());
        }


        /// <summary>
        /// 取得已注册的指定类型组件池
        /// </summary>
        private ComponentStorage<T> GetStorage<T>() where T : unmanaged
        {
            if (!storageDict.TryGetValue(typeof(T), out var Storage))
                throw new InvalidOperationException("Component type is not registered");

            return (ComponentStorage<T>)Storage;
        }

        /// <summary>
        /// 为存活实体添加组件
        /// </summary>
        public void Add<T>(Entity entity, T value) where T : unmanaged
        {
            EnsureStructuralChangeAllowed();
            if (!IsAlive(entity))
                throw new InvalidOperationException("Entity is not alive");

            GetStorage<T>().Add(entity.Index, value);

            if (!ownedTypeDict.TryGetValue(entity.Index, out var TypeList))
            {
                TypeList = new List<Type>();
                ownedTypeDict.Add(entity.Index, TypeList);
            }

            TypeList.Add(typeof(T));
        }

        /// <summary>
        /// 判断存活实体是否拥有指定组件
        /// </summary>
        public bool Has<T>(Entity entity) where T : unmanaged
        {
            return IsAlive(entity) && GetStorage<T>().Has(entity.Index);
        }

        /// <summary>
        /// 取得存活实体的组件引用
        /// </summary>
        public ref T Get<T>(Entity entity) where T : unmanaged
        {
            if (!IsAlive(entity))
                throw new InvalidOperationException("Entity is not alive");

            var Storage = GetStorage<T>();
            return ref Storage.Get(entity.Index);
        }

        /// <summary>
        /// 移除存活实体的指定组件
        /// </summary>
        public void Remove<T>(Entity entity) where T : unmanaged
        {
            EnsureStructuralChangeAllowed();
            if (!IsAlive(entity))
                throw new InvalidOperationException("Entity is not alive");

            GetStorage<T>().Remove(entity.Index);

            var TypeList = ownedTypeDict[entity.Index];
            TypeList.Remove(typeof(T));
            if (TypeList.Count == 0)
                ownedTypeDict.Remove(entity.Index);
        }

        /// <summary>
        /// 移除实体拥有的全部组件
        /// </summary>
        private void RemoveAllComponents(int entityIndex)
        {
            if (!ownedTypeDict.TryGetValue(entityIndex, out var TypeList))
                return;

            foreach (var ComponentType in TypeList)
                storageDict[ComponentType].Remove(entityIndex);

            ownedTypeDict.Remove(entityIndex);
        }
    }
}
