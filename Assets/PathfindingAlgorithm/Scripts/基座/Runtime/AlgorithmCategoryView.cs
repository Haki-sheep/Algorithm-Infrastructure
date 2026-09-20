using UnityEngine;
using UnityEngine.UI;

namespace PathfindingAlgorithm.Visualization
{
    public sealed class AlgorithmCategoryView : MonoBehaviour
    {
        /// <summary>
        /// 算法选项容器
        /// </summary>
        [SerializeField]
        private GameObject options;

        /// <summary>
        /// 展开标记
        /// </summary>
        [SerializeField]
        private Text indicator;

        /// <summary>
        /// 具体算法勾选项
        /// </summary>
        [SerializeField]
        private Toggle[] optionList;

        /// <summary>
        /// 已选数量文本
        /// </summary>
        [SerializeField]
        private Text selectionInfo;

        public Toggle[] Options => optionList;

        /// <summary>
        /// 展开或收起算法分类
        /// </summary>
        public void ToggleExpanded()
        {
            options.SetActive(!options.activeSelf);
            indicator.text = options.activeSelf ? "−" : "+";
        }

        /// <summary>
        /// 更新当前类别已选数量
        /// </summary>
        public void RefreshSelection(bool selected)
        {
            int count = 0;
            foreach (var option in optionList)
                if (option.isOn)
                    count++;
            selectionInfo.text = $"{count:00}";
        }
    }
}
