using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class PatchBaseUi
{
    public static string Apply()
    {
        const string path = "Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        Patch(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return "prefab ok";
    }

    public static string VerifyPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PathfindingAlgorithm/Prefab/SearchVisualization.prefab");
        var view = prefab.GetComponent<SearchVisualization>();
        var so = new SerializedObject(view);
        int height = so.FindProperty("mapHeight").intValue;
        var gridPanel = prefab.transform.Find("GridPanel");
        var scroll = gridPanel.GetComponentInChildren<ScrollRect>(true);
        var bar = scroll.transform.Find("VerticalScrollbar");
        var hbar = scroll.transform.Find("HorizontalScrollbar");
        bool hBarOn = hbar != null && hbar.gameObject.activeSelf;
        var panel = prefab.transform.Find("AlgorithmPanel");
        bool pause = panel.Find("暂停") != null;
        bool undo = panel.Find("上一步") != null;
        bool pauseBound = false;
        bool undoBound = false;
        foreach (var button in panel.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name == "暂停")
                pauseBound = button.onClick.GetPersistentEventCount() > 0;
            if (button.gameObject.name == "上一步")
                undoBound = button.onClick.GetPersistentEventCount() > 0;
        }

        return $"height={height} h={scroll.horizontal} v={scroll.vertical} vbar={bar.gameObject.activeSelf} hbar={hBarOn} pause={pause}/{pauseBound} undo={undo}/{undoBound} rows={view.Grid.Rows}";
    }

    public static string ApplyLive()
    {
        var view = Object.FindObjectOfType<SearchVisualization>();
        Patch(view.gameObject);
        return "live ok";
    }

    private static void Patch(GameObject root)
    {
        HideGridScroll(root.transform.Find("GridPanel"));
        LayoutPlayback(root);
        BindExclusiveToggles(root);
        var view = root.GetComponent<SearchVisualization>();
        var so = new SerializedObject(view);
        so.FindProperty("mapHeight").intValue = 24;
        so.ApplyModifiedPropertiesWithoutUndo();
        view.RefreshInterface();
        ClearSelectHints(root);
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            if (text.text.Contains("滚轮") || text.text.Contains("滚动条"))
                text.text = "左键绘制  /  右键擦除";
        }
    }

    private static void HideGridScroll(Transform gridPanel)
    {
        var scroll = gridPanel.GetComponentInChildren<ScrollRect>(true);
        scroll.horizontal = false;
        scroll.vertical = false;
        scroll.horizontalScrollbar = null;
        scroll.verticalScrollbar = null;
        Transform horizontalBar = scroll.transform.Find("HorizontalScrollbar");
        Transform verticalBar = scroll.transform.Find("VerticalScrollbar");
        if (horizontalBar != null)
            horizontalBar.gameObject.SetActive(false);
        if (verticalBar != null)
            verticalBar.gameObject.SetActive(false);
        var viewport = scroll.viewport;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
    }

    private static void LayoutPlayback(GameObject root)
    {
        var panel = root.transform.Find("AlgorithmPanel");
        var controller = root.GetComponent<GridSearchController>();
        var play = panel.Find("开始搜索");
        var step = panel.Find("单步");
        PlaceBottomLeft(play, 12f, 56f, 160f, 38f);
        PlaceBottomLeft(step, 12f, 12f, 160f, 38f);

        var pause = EnsureButton(panel, step, "暂停", controller.PauseSearch);
        PlaceBottomLeft(pause, 180f, 56f, 136f, 38f);
        var undo = EnsureButton(panel, step, "上一步", controller.UndoStep);
        PlaceBottomLeft(undo, 180f, 12f, 136f, 38f);

        var arrows = panel.Find("显示方向箭头");
        PlaceBottomLeft(arrows, 12f, 190f, 292f, 38f);
        var categories = (RectTransform)panel.Find("Categories");
        categories.offsetMin = new Vector2(categories.offsetMin.x, 236f);
        categories.offsetMax = new Vector2(categories.offsetMax.x, -70f);

        var so = new SerializedObject(controller);
        var status = so.FindProperty("statusText").objectReferenceValue as Text;
        PlaceBottomLeft(status.rectTransform, 20f, 102f, 292f, 80f);
    }

    private static Transform EnsureButton(Transform panel, Transform template, string name, UnityAction action)
    {
        var existing = panel.Find(name);
        if (existing != null)
            return existing;

        var clone = Object.Instantiate(template.gameObject, panel);
        clone.name = name;
        clone.GetComponentInChildren<Text>().text = name;
        var button = clone.GetComponent<Button>();
        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        return clone.transform;
    }

    private static void ClearSelectHints(GameObject root)
    {
        var labels = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            string value = labels[i].text;
            if (value.Contains("勾选 BFS") || value.Contains("请勾选"))
            {
                if (labels[i].transform.parent != null && labels[i].transform.parent.name == "AlgorithmPanel")
                    labels[i].gameObject.SetActive(false);
                else if (value.Contains("后开始") || value.Contains("后点开始"))
                    labels[i].text = value.Contains("无信息搜索") ? "无信息搜索" : "";
            }
        }

        var so = new SerializedObject(root.GetComponent<GridSearchController>());
        var status = so.FindProperty("statusText").objectReferenceValue as Text;
        status.gameObject.SetActive(true);
        if (status.text.Contains("勾选"))
            status.text = "";
    }

    private static void BindExclusiveToggles(GameObject root)
    {
        var viewList = root.GetComponentsInChildren<AlgorithmCategoryView>(true);
        for (int i = 0; i < viewList.Length; i++)
        {
            Toggle[] optionList = viewList[i].Options;
            var options = optionList[0].transform.parent.gameObject;
            var group = options.GetComponent<ToggleGroup>();
            if (group == null)
                group = options.AddComponent<ToggleGroup>();
            group.allowSwitchOff = true;
            for (int j = 0; j < optionList.Length; j++)
                optionList[j].group = group;
        }
    }

    private static void PlaceBottomLeft(Transform target, float x, float y, float width, float height)
    {
        var rect = (RectTransform)target;
        rect.pivot = Vector2.zero;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
