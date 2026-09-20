using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class VerifyBfsHookup
{
    public static string Prefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab");
        var view = prefab.GetComponent<SearchVisualization>();
        var controller = prefab.GetComponent<GridSearchController>();
        var soView = new SerializedObject(view);
        var soCtrl = new SerializedObject(controller);
        var grid = soView.FindProperty("grid").objectReferenceValue;
        var boundController = soView.FindProperty("searchController").objectReferenceValue;
        var bfsToggle = soCtrl.FindProperty("bfsToggle").objectReferenceValue as Toggle;
        var statusText = soCtrl.FindProperty("statusText").objectReferenceValue as Text;
        var boundGrid = soCtrl.FindProperty("grid").objectReferenceValue;
        bool hasPlay = false;
        bool hasStep = false;
        foreach (var button in prefab.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name == "开始搜索")
                hasPlay = button.onClick.GetPersistentEventCount() > 0;
            if (button.gameObject.name == "单步")
                hasStep = button.onClick.GetPersistentEventCount() > 0;
        }

        return $"grid={grid != null} ctrl={controller != null} boundCtrl={boundController == controller} bfsToggle={bfsToggle != null && bfsToggle.gameObject.name == "BFS"} status={statusText != null} ctrlGrid={boundGrid == grid} play={hasPlay} step={hasStep}";
    }
}
