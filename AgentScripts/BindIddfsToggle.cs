using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BindIddfsToggle
{
    public static string Apply()
    {
        const string path = "Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var controller = root.GetComponent<GridSearchController>();
        var categoryList = root.GetComponentsInChildren<AlgorithmCategoryView>(true);
        Toggle iddfsToggle = categoryList[0].Options[3];

        var so = new SerializedObject(controller);
        so.FindProperty("iddfsToggle").objectReferenceValue = iddfsToggle;
        so.ApplyModifiedPropertiesWithoutUndo();
        bool bound = so.FindProperty("iddfsToggle").objectReferenceValue == iddfsToggle;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        string iddfsName = iddfsToggle != null ? iddfsToggle.gameObject.name : "null";
        return $"prefab iddfs={iddfsName} bound={bound}";
    }

    public static string BindLive()
    {
        var view = Object.FindObjectOfType<SearchVisualization>();
        if (view == null)
            return "no view";

        var controller = view.GetComponent<GridSearchController>();
        var categoryList = view.GetComponentsInChildren<AlgorithmCategoryView>(true);
        Toggle iddfsToggle = categoryList[0].Options[3];

        var so = new SerializedObject(controller);
        so.FindProperty("iddfsToggle").objectReferenceValue = iddfsToggle;
        so.ApplyModifiedPropertiesWithoutUndo();
        string iddfsName = iddfsToggle != null ? iddfsToggle.gameObject.name : "null";
        return $"live iddfs={iddfsName}";
    }
}
