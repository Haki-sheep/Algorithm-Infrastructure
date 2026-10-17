using System.Collections.Generic;
using UnityEngine;

namespace RaindropStop
{
    /// <summary>
    /// 挡雨物体 伞 雨棚 车顶 登记后写入俯视遮挡高度图 雨滴在其表面溅开而非穿透
    /// </summary>
    public class RainOccluder : MonoBehaviour
    {
        private static readonly List<Renderer> rendererList = new List<Renderer>();

        public static IReadOnlyList<Renderer> RendererList => rendererList;

        /// <summary> 本物体及子物体的渲染器 OnEnable 登记 OnDisable 移除 </summary>
        private Renderer[] ownRendererList;

        private void Awake() => InitComponents();

        private void InitComponents() => ownRendererList = GetComponentsInChildren<Renderer>();

        private void OnEnable() => rendererList.AddRange(ownRendererList);

        private void OnDisable()
        {
            foreach (var Item in ownRendererList)
            {
                rendererList.Remove(Item);
            }
        }
    }
}
