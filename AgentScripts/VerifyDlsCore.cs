using UnityEngine;

public static class VerifyDlsCore
{
    public static string Run()
    {
        bool[] walkable = { true, true, true, true };
        var data = new DLSData();
        var core = new DLSCore();

        data.Init(4, 1, walkable, new Vector2Int(0, 0), new Vector2Int(3, 0), 3);
        core.Init(data);
        bool foundAt3 = core.Search();
        int pathAt3 = data.PathList.Count;

        data.Init(4, 1, walkable, new Vector2Int(0, 0), new Vector2Int(3, 0), 2);
        core.Init(data);
        bool foundAt2 = core.Search();
        int pathAt2 = data.PathList.Count;

        return $"found3={foundAt3} path3={pathAt3} found2={foundAt2} path2={pathAt2}";
    }
}
