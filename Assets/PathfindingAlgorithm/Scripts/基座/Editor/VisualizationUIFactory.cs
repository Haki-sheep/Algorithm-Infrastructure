using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PathfindingAlgorithm.Visualization.Editor
{
    internal sealed class VisualizationUIFactory
    {
        /// <summary>
        /// 中文字体资源
        /// </summary>
        private readonly Font font;

        /// <summary>
        /// 正文颜色
        /// </summary>
        private readonly Color ink = new Color32(32, 48, 66, 255);

        /// <summary>
        /// 主强调色
        /// </summary>
        private readonly Color accent = new Color32(18, 130, 123, 255);

        /// <summary>
        /// 接收字体依赖
        /// </summary>
        public VisualizationUIFactory(Font textFont)
        {
            font = textFont;
        }

        /// <summary>
        /// 创建矩形节点
        /// </summary>
        public RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>
        /// 拉伸并设置内边距
        /// </summary>
        public void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>
        /// 从左上角定位固定尺寸节点
        /// </summary>
        public void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// 顶部横向拉伸并保持固定高度
        /// </summary>
        public void TopStretch(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-left - right, height);
            rect.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
        }

        /// <summary>
        /// 添加纯色底图
        /// </summary>
        public Image Fill(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>
        /// 添加文本
        /// </summary>
        public Text Label(string value, Transform parent, int size = 18)
        {
            var rect = Rect("Label", parent);
            Stretch(rect);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = ink;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// 添加小数输入框
        /// </summary>
        public InputField NumberInput(Transform parent, string value)
        {
            var rect = Rect("WeightInput", parent);
            var image = Fill(rect, new Color32(232, 242, 241, 255), true);
            var input = rect.gameObject.AddComponent<InputField>();
            var text = Label("", rect, 16);
            text.supportRichText = false;
            text.alignment = TextAnchor.MiddleLeft;
            Stretch(text.rectTransform, 6f, 2f, 4f, 2f);
            var placeholder = Label(value, rect, 16);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color32(120, 140, 150, 180);
            Stretch(placeholder.rectTransform, 6f, 2f, 4f, 2f);
            input.targetGraphic = image;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.contentType = InputField.ContentType.DecimalNumber;
            input.text = value;
            return input;
        }

        /// <summary>
        /// 添加按钮
        /// </summary>
        public Button Button(string label, Transform parent)
        {
            var rect = Rect(label, parent);
            var image = Fill(rect, new Color32(232, 242, 241, 255), true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = Label(label, rect);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = accent;
            return button;
        }

        /// <summary>
        /// 添加复选框
        /// </summary>
        public Toggle Toggle(string label, Transform parent, Sprite swatch = null)
        {
            var rect = Rect(label, parent);
            var target = Fill(rect, Color.white, true);
            var toggle = rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = target;
            var box = Rect("Box", rect);
            Place(box, 8f, 10f, 18f, 18f);
            Fill(box, new Color32(220, 230, 233, 255));
            var check = Rect("Check", box);
            Stretch(check, 4f, 4f, 4f, 4f);
            toggle.graphic = Fill(check, accent);
            float textLeft = 36f;
            if (swatch != null)
            {
                var colorRect = Rect("Swatch", rect);
                Place(colorRect, 35f, 10f, 18f, 18f);
                Fill(colorRect, Color.white).sprite = swatch;
                textLeft = 61f;
            }
            var text = Label(label, rect, 16);
            Stretch(text.rectTransform, textLeft, 0f, 6f, 0f);
            toggle.isOn = false;
            return toggle;
        }

        /// <summary>
        /// 为纵向列表配置自动高度
        /// </summary>
        public void Vertical(RectTransform rect, float spacing)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>
        /// 设置列表元素高度
        /// </summary>
        public void Height(GameObject item, float value)
        {
            item.AddComponent<LayoutElement>().preferredHeight = value;
        }

        /// <summary>
        /// 添加带裁剪和滚动条的滚动区域
        /// </summary>
        public ScrollRect Scroll(Transform parent, bool horizontal)
        {
            var rect = Rect("Scroll", parent);
            Stretch(rect);
            Fill(rect, new Color32(226, 233, 237, 255), true);
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            var viewport = Rect("Viewport", rect);
            Stretch(viewport, 0f, 0f, 12f, horizontal ? 12f : 0f);
            Fill(viewport, Color.white, true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport);
            Place(content, 0f, 0f, 0f, 0f);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = Scrollbar(rect, false);
            if (horizontal)
                scroll.horizontalScrollbar = Scrollbar(rect, true);
            return scroll;
        }

        /// <summary>
        /// 构建滚动条
        /// </summary>
        private Scrollbar Scrollbar(RectTransform parent, bool horizontal)
        {
            var rect = Rect(horizontal ? "HorizontalScrollbar" : "VerticalScrollbar", parent);
            rect.anchorMin = horizontal ? Vector2.zero : new Vector2(1f, 0f);
            rect.anchorMax = horizontal ? new Vector2(1f, 0f) : Vector2.one;
            rect.pivot = horizontal ? Vector2.zero : Vector2.one;
            rect.sizeDelta = horizontal ? new Vector2(-12f, 10f) : new Vector2(10f, -12f);
            rect.anchoredPosition = Vector2.zero;
            Fill(rect, new Color32(227, 234, 238, 255), true);
            var handle = Rect("Handle", rect);
            Stretch(handle);
            var graphic = Fill(handle, new Color32(158, 180, 190, 255), true);
            var bar = rect.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = graphic;
            bar.direction = horizontal ? UnityEngine.UI.Scrollbar.Direction.LeftToRight : UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            return bar;
        }

        /// <summary>
        /// 设置序列化对象引用
        /// </summary>
        public void Bind(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 设置序列化对象引用集合
        /// </summary>
        public void BindList(Object target, string field, Object[] valueList)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            property.arraySize = valueList.Length;
            for (int index = 0; index < valueList.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = valueList[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
