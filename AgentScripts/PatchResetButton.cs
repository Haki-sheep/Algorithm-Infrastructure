using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class PatchResetButton
{
    public static string Apply()
    {
        const string path = "Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var toolbar = root.transform.Find("BrushToolbar");
        if (toolbar.Find("重置") == null)
        {
            var clear = toolbar.Find("清空网格");
            var resetGo = Object.Instantiate(clear.gameObject, toolbar);
            resetGo.name = "重置";
            var resetRect = (RectTransform)resetGo.transform;
            resetRect.anchoredPosition = new Vector2(984f, -4f);
            resetRect.sizeDelta = new Vector2(88f, 38f);
            var label = resetGo.GetComponentInChildren<Text>();
            label.text = "重置";
            var button = resetGo.GetComponent<Button>();
            while (button.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            UnityEventTools.AddPersistentListener(button.onClick, root.GetComponent<GridSearchController>().ResetRound);
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return "ok";
    }
}
