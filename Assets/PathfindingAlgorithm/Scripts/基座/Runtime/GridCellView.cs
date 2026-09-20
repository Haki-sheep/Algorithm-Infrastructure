using UnityEngine;
using UnityEngine.UI;

namespace PathfindingAlgorithm.Visualization
{
    public enum eCellState
    {
        Empty,
        Obstacle,
        Explored,
        Path,
        Start,
        End
    }

    public sealed class GridCellView : MonoBehaviour
    {
        /// <summary>
        /// 格子底图
        /// </summary>
        [SerializeField]
        private Image background;

        /// <summary>
        /// 方向箭头
        /// </summary>
        [SerializeField]
        private Image arrow;

        /// <summary>
        /// 六种状态对应的素材
        /// </summary>
        [SerializeField]
        private Sprite[] stateSpriteList;

        /// <summary>
        /// 当前格子状态
        /// </summary>
        [SerializeField]
        private eCellState eState;

        /// <summary>
        /// 是否设置过方向
        /// </summary>
        [SerializeField]
        private bool hasDirection;

        public eCellState State => eState;
        public bool HasDirection => hasDirection;

        /// <summary>
        /// 设置格子状态
        /// </summary>
        public void SetState(eCellState eValue)
        {
            eState = eValue;
            background.sprite = stateSpriteList[(int)eValue];
            arrow.color = eValue == eCellState.Start ? Color.white : new Color32(30, 43, 62, 255);
        }

        /// <summary>
        /// 设置方向 方向坐标与网格一致 零向量移除箭头
        /// </summary>
        public void SetDirection(Vector2 direction, bool visible)
        {
            hasDirection = direction != Vector2.zero;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-direction.y, direction.x) * Mathf.Rad2Deg);
            SetArrowVisible(visible);
        }

        /// <summary>
        /// 切换已有箭头的可见性
        /// </summary>
        public void SetArrowVisible(bool visible)
        {
            arrow.enabled = visible && hasDirection;
        }
    }
}
