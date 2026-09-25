using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MmECS
{
    public sealed partial class World
    {
        /// <summary>
        /// 查询同时拥有两种组件的实体
        /// </summary>
        public void Query<TFirst, TSecond>(List<Entity> resultList)
            where TFirst : unmanaged
            where TSecond : unmanaged
        {
            var FirstStorage = GetStorage<TFirst>();
            var SecondStorage = GetStorage<TSecond>();
            resultList.Clear();

            // 候选池是数量较少的那个
            IComponentStorage CandidateStorage = FirstStorage;
            IComponentStorage OtherStorage = SecondStorage;
            if (SecondStorage.Count < FirstStorage.Count)
            {
                CandidateStorage = SecondStorage;
                OtherStorage = FirstStorage;
            }

            // 遍历候选池 减少计算次数 如果另一个池也有这个实体 则添加到结果列表
            for (int row = 0; row < CandidateStorage.Count; row++)
            {
                int entityIndex = CandidateStorage.EntityIndexAt(row);
                if (OtherStorage.Has(entityIndex))
                    resultList.Add(new Entity(entityIndex, generationList[entityIndex]));
            }
        }

        /// <summary>
        /// 查询拥A & B 且没有C的实体
        /// </summary>
        public void Query<TFirst, TSecond, TExclude>(List<Entity> resultList)
            where TFirst : unmanaged
            where TSecond : unmanaged
            where TExclude : unmanaged
        {
            var FirstStorage = GetStorage<TFirst>();
            var SecondStorage = GetStorage<TSecond>();
            var ExcludedStorage = GetStorage<TExclude>();
            resultList.Clear();

            IComponentStorage CandidateStorage = FirstStorage;
            IComponentStorage OtherStorage = SecondStorage;
            if (SecondStorage.Count < FirstStorage.Count)
            {
                CandidateStorage = SecondStorage;
                OtherStorage = FirstStorage;
            }

            for (int row = 0; row < CandidateStorage.Count; row++)
            {
                int entityIndex = CandidateStorage.EntityIndexAt(row);
                if (OtherStorage.Has(entityIndex) && !ExcludedStorage.Has(entityIndex))
                    resultList.Add(new Entity(entityIndex, generationList[entityIndex]));
            }
        }
    }
}