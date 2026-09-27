using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 生成包含分配器组件单例和随机源的诊断投影且排除宿主身份
    /// </summary>
    public sealed partial class World
    {
        /// <summary>
        /// 复制当前状态为稳定字段路径和值且不保留可写模拟引用
        /// </summary>
        public SortedDictionary<string, string> InspectState()
        {
            var StateDict = new SortedDictionary<string, string>(StringComparer.Ordinal);
            StateDiagnostics.WriteValue(StateDict, "World/Tick", Tick);
            StateDiagnostics.WriteValue(StateDict, "World/Random", random.State);
            StateDiagnostics.WriteValue(StateDict, "World/LastSpawnId", lastSpawnRequestId);
            for (int index = 0; index < aliveList.Count; index++)
            {
                StateDiagnostics.WriteValue(StateDict, $"Entity/{index}/Alive", aliveList[index]);
                StateDiagnostics.WriteValue(StateDict, $"Entity/{index}/Generation", generationList[index]);
            }
            for (int index = 0; index < freeIndexList.Count; index++)
                StateDiagnostics.WriteValue(StateDict, $"Free/{index}", freeIndexList[index]);
            foreach (var Pair in storageDict)
            {
                string path = "Component/" + Pair.Key.AssemblyQualifiedName;
                StateDiagnostics.WriteValue(StateDict, path + "/TypeOrder", componentOrderDict[Pair.Key]);
                StateDiagnostics.WriteValue(StateDict, path + "/Count", Pair.Value.Count);
                for (int row = 0; row < Pair.Value.Count; row++)
                {
                    int entityIndex = Pair.Value.EntityIndexAt(row);
                    StateDiagnostics.WriteValue(StateDict, path + $"/Row/{row}", entityIndex);
                    StateDiagnostics.WriteValue(StateDict, path + $"/Entity/{entityIndex}", Pair.Value.ReadBoxed(row));
                }
            }
            foreach (var Pair in ownedTypeDict)
                for (int index = 0; index < Pair.Value.Count; index++)
                    StateDict[$"Owner/{Pair.Key}/{index}"] = Pair.Value[index].AssemblyQualifiedName;
            foreach (var Pair in resourceDict)
                StateDiagnostics.WriteValue(StateDict, "Singleton/" + Pair.Key.AssemblyQualifiedName, Pair.Value.ReadBoxed());
            foreach (var Pair in spawnResultDict)
                StateDiagnostics.WriteValue(StateDict, $"Spawn/{Pair.Key}", Pair.Value);
            return StateDict;
        }
    }
}
