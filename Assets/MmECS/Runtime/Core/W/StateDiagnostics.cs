using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace MmECS
{
    /// <summary>
    /// 为低频诊断展开值类型字段并生成可定位到字段的状态差异
    /// </summary>
    public static class StateDiagnostics
    {
        /// <summary>
        /// 按稳定路径记录值类型字段且浮点数保留往返精度
        /// </summary>
        public static void WriteValue(IDictionary<string, string> StateDict, string path, object Value)
        {
            var ValueType = Value.GetType();
            if (Value is float single)
                StateDict[path] = single.ToString("R", CultureInfo.InvariantCulture) +
                    " [" + BitConverter.SingleToInt32Bits(single).ToString("X8", CultureInfo.InvariantCulture) + "]";
            else if (Value is double number)
                StateDict[path] = number.ToString("R", CultureInfo.InvariantCulture) +
                    " [" + BitConverter.DoubleToInt64Bits(number).ToString("X16", CultureInfo.InvariantCulture) + "]";
            else if (ValueType.IsPrimitive || ValueType.IsEnum || Value is decimal || Value is string)
                StateDict[path] = Convert.ToString(Value, CultureInfo.InvariantCulture);
            else
            {
                var FieldList = ValueType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                // 空标签同样留下记录以区分组件存在和不存在
                StateDict[path] = ValueType.FullName;
                foreach (var Field in FieldList)
                {
                    string name = Field.Name;
                    if (name.StartsWith("<", StringComparison.Ordinal) && name.EndsWith(">k__BackingField", StringComparison.Ordinal))
                        name = name.Substring(1, name.IndexOf('>') - 1);
                    WriteValue(StateDict, path + "/" + name, Field.GetValue(Value));
                }
            }
        }

        /// <summary>
        /// 比较两个状态投影并返回新增删除和数值变化的完整路径
        /// </summary>
        public static List<string> Compare(IDictionary<string, string> BeforeDict, IDictionary<string, string> AfterDict)
        {
            var PathHashList = new SortedSet<string>(BeforeDict.Keys, StringComparer.Ordinal);
            PathHashList.UnionWith(AfterDict.Keys);
            var DifferenceList = new List<string>();
            foreach (string path in PathHashList)
            {
                bool beforeExists = BeforeDict.TryGetValue(path, out string before);
                bool afterExists = AfterDict.TryGetValue(path, out string after);
                if (!beforeExists || !afterExists || before != after)
                    DifferenceList.Add(path + "  " + (beforeExists ? before : "<absent>") +
                        " -> " + (afterExists ? after : "<absent>"));
            }
            return DifferenceList;
        }
    }
}
