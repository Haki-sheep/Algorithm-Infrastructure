#if !UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Analytics;

namespace Unity.Pipeline.Editor.Compatibility
{
    internal interface LegacyAnalytic
    {
        public interface IData { }

        public bool TryGatherData(out IData Data, out Exception Error);
    }

    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class LegacyAnalyticInfoAttribute : Attribute
    {
        public string EventName { get; }
        public string VendorKey { get; }
        public int MaxEventsPerHour { get; }
        public int MaxNumberOfElements { get; }

        public LegacyAnalyticInfoAttribute(string eventName, string vendorKey, int maxEventsPerHour, int maxNumberOfElements)
        {
            EventName = eventName;
            VendorKey = vendorKey;
            MaxEventsPerHour = maxEventsPerHour;
            MaxNumberOfElements = maxNumberOfElements;
        }
    }

    internal static class LegacyAnalytics
    {
        private static readonly HashSet<string> RegisteredEventHashList = new HashSet<string>();

        public static void Send(LegacyAnalytic Analytic)
        {
            var Info = (LegacyAnalyticInfoAttribute)Attribute.GetCustomAttribute(Analytic.GetType(), typeof(LegacyAnalyticInfoAttribute));
            if (!RegisteredEventHashList.Contains(Info.EventName))
            {
                var Result = EditorAnalytics.RegisterEventWithLimit(Info.EventName, Info.MaxEventsPerHour, Info.MaxNumberOfElements, Info.VendorKey);
                if (Result != AnalyticsResult.Ok)
                    return;
                RegisteredEventHashList.Add(Info.EventName);
            }

            if (!Analytic.TryGatherData(out var Data, out var Error))
                throw Error;
            EditorAnalytics.SendEventWithLimit(Info.EventName, Data);
        }
    }
}
#endif
