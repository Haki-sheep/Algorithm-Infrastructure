using UnityEngine;

public static class VerifyAstarCore
{
    public static string Run()
    {
        bool[] line = { true, true, true, true };
        float[] cost = { 1f, 1f, 1f, 1f };
        var data = new AStarData();
        var core = new AStarCore();

        data.Init(4, 1, line, cost, new Vector2Int(0, 0), new Vector2Int(3, 0));
        core.Init(data);
        bool foundLine = core.Search();
        int pathLine = data.PathList.Count;
        float gLine = data.GList[3];

        data.Init(4, 1, line, cost, new Vector2Int(0, 0), new Vector2Int(0, 0));
        core.Init(data);
        bool foundStart = core.Search();
        int pathStart = data.PathList.Count;

        bool[] blocked = { true, false, true, true };
        data.Init(4, 1, blocked, cost, new Vector2Int(0, 0), new Vector2Int(3, 0));
        core.Init(data);
        bool foundBlocked = core.Search();
        int pathBlocked = data.PathList.Count;

        bool[] open = { true, true, true, true };
        float[] unit = { 1f, 1f, 1f, 1f };
        data.Init(2, 2, open, unit, new Vector2Int(0, 0), new Vector2Int(1, 1));
        core.Init(data);
        bool foundDiag = core.Search();
        float gDiag = data.GList[data.ToIndex(new Vector2Int(1, 1))];

        return $"line={foundLine}/{pathLine}/g{gLine:0.###} start={foundStart}/{pathStart} blocked={foundBlocked}/{pathBlocked} diag={foundDiag}/g{gDiag:0.###}";
    }
}
