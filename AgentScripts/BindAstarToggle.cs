using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BindAstarToggle
{
    public static string Apply()
    {
        const string path = "Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var controller = root.GetComponent<GridSearchController>();
        var categoryList = root.GetComponentsInChildren<AlgorithmCategoryView>(true);
        Toggle astarToggle = FindAstar(categoryList);
        string astarName = astarToggle != null ? astarToggle.gameObject.name : "null";

        var so = new SerializedObject(controller);
        so.FindProperty("astarToggle").objectReferenceValue = astarToggle;
        so.ApplyModifiedPropertiesWithoutUndo();
        bool bound = so.FindProperty("astarToggle").objectReferenceValue == astarToggle;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return $"prefab astar={astarName} bound={bound}";
    }

    public static string BindLive()
    {
        var view = Object.FindObjectOfType<SearchVisualization>();
        if (view == null)
            return "no view";

        var controller = view.GetComponent<GridSearchController>();
        var categoryList = view.GetComponentsInChildren<AlgorithmCategoryView>(true);
        Toggle astarToggle = FindAstar(categoryList);

        var so = new SerializedObject(controller);
        so.FindProperty("astarToggle").objectReferenceValue = astarToggle;
        so.ApplyModifiedPropertiesWithoutUndo();
        string astarName = astarToggle != null ? astarToggle.gameObject.name : "null";
        return $"live astar={astarName}";
    }

    private static Toggle FindAstar(AlgorithmCategoryView[] categoryList)
    {
        if (categoryList == null || categoryList.Length < 2)
            return null;
        Toggle[] optionList = categoryList[1].Options;
        if (optionList == null)
            return null;
        for (int i = 0; i < optionList.Length; i++)
        {
            Toggle toggle = optionList[i];
            if (toggle != null && toggle.gameObject.name == "A*")
                return toggle;
        }
        return optionList.Length > 1 ? optionList[1] : null;
    }
}
