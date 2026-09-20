using PathfindingAlgorithm.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PlayResetDemo
{
    public static string Run()
    {
        var view = Object.FindObjectOfType<SearchVisualization>();
        var grid = view.Grid;
        var controller = view.GetComponent<GridSearchController>();
        foreach (var toggle in view.GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.gameObject.name == "BFS")
                toggle.isOn = true;
        }

        grid.Clear();
        grid.SetCellState(new Vector2Int(0, 0), eCellState.Start);
        grid.SetCellState(new Vector2Int(3, 0), eCellState.End);
        grid.SetCellState(new Vector2Int(1, 0), eCellState.Obstacle);

        var so = new SerializedObject(controller);
        so.FindProperty("stepInterval").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        controller.PlaySearch();
        eCellState eAroundBefore = grid.GetCellState(new Vector2Int(1, 1));
        controller.ResetRound();

        eCellState eStart = grid.GetCellState(new Vector2Int(0, 0));
        eCellState eEnd = grid.GetCellState(new Vector2Int(3, 0));
        eCellState eWall = grid.GetCellState(new Vector2Int(1, 0));
        eCellState eAround = grid.GetCellState(new Vector2Int(1, 1));
        bool hasReset = view.transform.Find("BrushToolbar/重置") != null;
        var status = so.FindProperty("statusText").objectReferenceValue as Text;
        return $"btn={hasReset} before={eAroundBefore} start={eStart} end={eEnd} wall={eWall} around={eAround} status={status.text}";
    }
}
