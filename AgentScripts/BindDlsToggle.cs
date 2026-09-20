using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BindDlsToggle
{
    public static string Apply()
    {
        const string path = "Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var controller = root.GetComponent<GridSearchController>();
        Toggle dlsToggle = null;
        foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.gameObject.name == "DLS")
            {
                dlsToggle = toggle;
                break;
            }
        }

        var so = new SerializedObject(controller);
        so.FindProperty("dlsToggle").objectReferenceValue = dlsToggle;
        so.ApplyModifiedPropertiesWithoutUndo();
        bool bound = so.FindProperty("dlsToggle").objectReferenceValue == dlsToggle;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return $"prefab dlsToggle={dlsToggle != null} bound={bound}";
    }

    public static string BindLive()
    {
        var view = Object.FindObjectOfType<SearchVisualization>();
        if (view == null)
            return "no view";

        var controller = view.GetComponent<GridSearchController>();
        Toggle dlsToggle = null;
        foreach (var toggle in view.GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.gameObject.name == "DLS")
            {
                dlsToggle = toggle;
                break;
            }
        }

        var so = new SerializedObject(controller);
        so.FindProperty("dlsToggle").objectReferenceValue = dlsToggle;
        so.ApplyModifiedPropertiesWithoutUndo();
        string dlsName = dlsToggle != null ? dlsToggle.gameObject.name : "null";
        return $"live dls={dlsName}";
    }
}
