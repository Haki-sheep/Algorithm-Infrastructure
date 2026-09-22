using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class VerifyAstarHookup
{
    public static string Prefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab");
        var controller = prefab.GetComponent<GridSearchController>();
        var so = new SerializedObject(controller);
        var astarToggle = so.FindProperty("astarToggle").objectReferenceValue as Toggle;
        string name = astarToggle != null ? astarToggle.gameObject.name : "null";
        return "prefab astar=" + name;
    }

    public static string Live()
    {
        var view = Object.FindObjectOfType<SearchVisualization>();
        if (view == null)
            return "no view";

        var controller = view.GetComponent<GridSearchController>();
        var so = new SerializedObject(controller);
        var astarToggle = so.FindProperty("astarToggle").objectReferenceValue as Toggle;
        string name = astarToggle != null ? astarToggle.gameObject.name : "null";
        return "live astar=" + name + " playing=" + Application.isPlaying;
    }
}
