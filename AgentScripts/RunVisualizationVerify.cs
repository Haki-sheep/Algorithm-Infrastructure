using PathfindingAlgorithm.Visualization.Editor;

public static class RunVisualizationVerify
{
    public static string All()
    {
        VisualizationVerifier.Verify();
        return "ok";
    }
}
