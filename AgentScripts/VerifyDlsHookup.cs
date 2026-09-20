using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class VerifyDlsHookup
{
    public static string Prefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab");
        var controller = prefab.GetComponent<GridSearchController>();
        var so = new SerializedObject(controller);
        var bfsToggle = so.FindProperty("bfsToggle").objectReferenceValue as Toggle;
        var dfsToggle = so.FindProperty("dfsToggle").objectReferenceValue as Toggle;
        var dlsToggle = so.FindProperty("dlsToggle").objectReferenceValue as Toggle;
        int dlsLimit = so.FindProperty("dlsLimit").intValue;
        return $"bfs={bfsToggle != null && bfsToggle.gameObject.name == "BFS"} dfs={dfsToggle != null && dfsToggle.gameObject.name == "DFS"} dls={dlsToggle != null && dlsToggle.gameObject.name == "DLS"} limit={dlsLimit}";
    }
}
