#if !UNITY_6000_0_OR_NEWER
using System;
using System.Reflection;

namespace Unity.Pipeline.Editor.Compatibility
{
    internal static class LegacyConsoleCounts
    {
        private static readonly MethodInfo GetCountsMethod = typeof(UnityEditor.EditorApplication).Assembly
            .GetType("UnityEditor.LogEntries", true)
            .GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        public static void Read(out int Errors, out int Warnings, out int Logs)
        {
            if (GetCountsMethod == null)
                throw new MissingMethodException("UnityEditor.LogEntries.GetCountsByType is required for Unity 2022.3 console counts");

            object[] ArgumentList = { 0, 0, 0 };
            GetCountsMethod.Invoke(null, ArgumentList);
            Errors = (int)ArgumentList[0];
            Warnings = (int)ArgumentList[1];
            Logs = (int)ArgumentList[2];
        }
    }
}
#endif
