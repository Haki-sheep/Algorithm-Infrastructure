using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BindDfsToggle
{
    public static string Apply()
    {
        const string path = "Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var controller = root.GetComponent<GridSearchController>();
        Toggle dfsToggle = null;
        foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.gameObject.name == "DFS")
            {
                dfsToggle = toggle;
                break;
            }
        }

        var so = new SerializedObject(controller);
        so.FindProperty("dfsToggle").objectReferenceValue = dfsToggle;
        so.ApplyModifiedPropertiesWithoutUndo();
        bool bound = so.FindProperty("dfsToggle").objectReferenceValue == dfsToggle;

        foreach (var label in root.GetComponentsInChildren<Text>(true))
        {
            if (label.text == "无信息搜索  ·  勾选 BFS 后开始")
                label.text = "无信息搜索  ·  勾选 BFS 或 DFS 后开始";
            else if (label.text == "勾选 BFS 后点开始搜索或单步")
                label.text = "勾选 BFS 或 DFS 后点开始搜索或单步";
            else if (label.text == "勾选 BFS 后点开始搜索")
                label.text = "勾选 BFS 或 DFS 后点开始搜索";
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return $"prefab dfsToggle={dfsToggle != null} bound={bound}";
    }

    public static string BindLive()
    {
        var view = Object.FindObjectOfType<SearchVisualization>();
        var controller = view.GetComponent<GridSearchController>();
        Toggle dfsToggle = null;
        foreach (var toggle in view.GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.gameObject.name == "DFS")
            {
                dfsToggle = toggle;
                break;
            }
        }

        var so = new SerializedObject(controller);
        so.FindProperty("dfsToggle").objectReferenceValue = dfsToggle;
        so.ApplyModifiedPropertiesWithoutUndo();
        string dfsName = dfsToggle != null ? dfsToggle.gameObject.name : "null";
        return $"live dfs={dfsName}";
    }
}
