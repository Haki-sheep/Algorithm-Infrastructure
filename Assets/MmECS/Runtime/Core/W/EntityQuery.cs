using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 缓存世界及包含排除条件的组件池并在执行时筛选最新成员
    /// </summary>
    public sealed class EntityQuery
    {
        /// <summary> 查询所属世界 </summary>
        private readonly World world;

        /// <summary> 按注册顺序排列的必需组件池 </summary>
        private readonly IComponentStorage[] allList;

        /// <summary> 必须不存在的组件池 </summary>
        private readonly IComponentStorage[] noneList;

        /// <summary>
        /// 绑定已复制并规范化的查询条件
        /// </summary>
        internal EntityQuery(World World, IComponentStorage[] AllList, IComponentStorage[] NoneList)
        {
            world = World;
            allList = AllList;
            noneList = NoneList;
        }

        /// <summary>
        /// 选择最小必需池并将当前匹配实体写入调用方列表
        /// </summary>
        public void Execute(List<Entity> ResultList)
        {
            ResultList.Clear();
            if (allList.Length == 0)
            {
                world.Query(ResultList);
                int count = 0;
                for (int index = 0; index < ResultList.Count; index++)
                    if (Matches(ResultList[index].Index))
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
                if (Matches(index))
                    ResultList.Add(world.EntityAt(index));
            }
        }

        /// <summary>
        /// 验证候选实体包含全部必需组件且没有任何排除组件
        /// </summary>
        private bool Matches(int index)
        {
            for (int type = 0; type < allList.Length; type++)
                if (!allList[type].Has(index))
                    return false;
            for (int type = 0; type < noneList.Length; type++)
                if (noneList[type].Has(index))
                    return false;
            return true;
        }
    }
}
