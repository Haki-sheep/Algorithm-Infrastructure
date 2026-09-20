using UnityEngine;

public static class VerifyIddfsCore
{
    public static string Run()
    {
        bool[] line = { true, true, true, true };
        var data = new IDDFSData();
        var core = new IDDFSCore();

        data.Init(4, 1, line, new Vector2Int(0, 0), new Vector2Int(3, 0));
        core.Init(data);
        bool foundLine = core.Search();
        int pathLine = data.PathList.Count;
        int limitLine = data.Limit;

        data.Init(4, 1, line, new Vector2Int(0, 0), new Vector2Int(0, 0));
        core.Init(data);
        bool foundStart = core.Search();
        int pathStart = data.PathList.Count;

        bool[] blocked = { true, false, true, true };
        data.Init(4, 1, blocked, new Vector2Int(0, 0), new Vector2Int(3, 0));
        core.Init(data);
        bool foundBlocked = core.Search();
        int pathBlocked = data.PathList.Count;

        return $"line={foundLine}/{pathLine}/L{limitLine} start={foundStart}/{pathStart} blocked={foundBlocked}/{pathBlocked}";
    }
}
