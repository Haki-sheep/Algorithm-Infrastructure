using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PatchComplexityLayout
{
    public static string Apply()
    {
        const string path = "Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var controller = root.GetComponent<GridSearchController>();
        var so = new SerializedObject(controller);
        var status = (Text)so.FindProperty("statusText").objectReferenceValue;
        status.alignment = TextAnchor.UpperLeft;
        status.verticalOverflow = VerticalWrapMode.Overflow;
        status.horizontalOverflow = HorizontalWrapMode.Wrap;
        var statusRect = status.rectTransform;
        statusRect.sizeDelta = new Vector2(290f, 58f);
        statusRect.anchoredPosition = new Vector2(20f, 122f);

        var arrows = root.transform.Find("AlgorithmPanel/显示方向箭头") as RectTransform;
        arrows.anchoredPosition = new Vector2(12f, 176f);

        var categories = root.transform.Find("AlgorithmPanel/Categories") as RectTransform;
        categories.offsetMin = new Vector2(categories.offsetMin.x, 188f);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return "ok";
    }
}
