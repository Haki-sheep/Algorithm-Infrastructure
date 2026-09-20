using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class VerifyDfsHookup
{
    public static string Prefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab");
        var controller = prefab.GetComponent<GridSearchController>();
        var so = new SerializedObject(controller);
        var bfsToggle = so.FindProperty("bfsToggle").objectReferenceValue as Toggle;
        var dfsToggle = so.FindProperty("dfsToggle").objectReferenceValue as Toggle;
        return $"bfs={bfsToggle != null && bfsToggle.gameObject.name == "BFS"} dfs={dfsToggle != null && dfsToggle.gameObject.name == "DFS"}";
    }

    public static string Live()
    {
        var view = Object.FindObjectOfType<SearchVisualization>();
        if (view == null)
            return "no view";

        var controller = view.GetComponent<GridSearchController>();
        var so = new SerializedObject(controller);
        var bfsToggle = so.FindProperty("bfsToggle").objectReferenceValue as Toggle;
        var dfsToggle = so.FindProperty("dfsToggle").objectReferenceValue as Toggle;
        return $"bfs={bfsToggle != null && bfsToggle.gameObject.name == "BFS"} dfs={dfsToggle != null && dfsToggle.gameObject.name == "DFS"} playing={Application.isPlaying}";
    }
}
