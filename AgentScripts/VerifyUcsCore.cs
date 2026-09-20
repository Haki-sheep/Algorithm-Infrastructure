using UnityEngine;

public static class VerifyUcsCore
{
    public static string Run()
    {
        bool[] line = { true, true, true, true };
        float[] cost = { 1f, 1f, 1f, 1f };
        var data = new UCSData();
        var core = new UCSCore();

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

        bool[] open = { true, true, true, true, true, true };
        float[] dear = { 1f, 3f, 1f, 1f, 1f, 1f };
        data.Init(3, 2, open, dear, new Vector2Int(0, 0), new Vector2Int(2, 0));
        core.Init(data);
        bool foundDear = core.Search();
        float gDear = data.GList[data.ToIndex(new Vector2Int(2, 0))];

        return $"line={foundLine}/{pathLine}/g{gLine:0.###} start={foundStart}/{pathStart} blocked={foundBlocked}/{pathBlocked} dear={foundDear}/g{gDear:0.###}";
    }
}
