using NUnit.Framework;
using Unity.Pipeline.Editor.Commands.Observability;

namespace Unity.Pipeline.Tests.Editor.Observability
{
    /// <summary>
    /// Tests for get_performance_stats, exercised directly and via PipelineClient. The console
    /// commands live in the runtime assembly; their tests are in ConsoleCommandTests.
    /// </summary>
    class ObservabilityCommandsTests
    {
        [Test]
        public void GetPerformanceStats_ReturnsMemory()
        {
            var stats = PerformanceCommands.GetPerformanceStats();

            Assert.IsNotNull(stats, "Performance stats should not be null");
            Assert.IsNotNull(stats.Memory, "Memory stats should be present");
            Assert.Greater(stats.Memory.TotalAllocatedBytes, 0,
                "Total allocated memory should always be positive at runtime");
            Assert.IsNotNull(stats.Render, "Render stats should be present");
            Assert.IsNotNull(stats.FrameTiming, "Frame timing block should be present");
        }

        [Test]
        public void GetPerformanceStats_ViaClient_Succeeds()
        {
            using (var server = new PipelineTestServer())
            {
                var response = server.Execute("get_performance_stats", null);

                Assert.IsTrue(response.IsSuccess, $"get_performance_stats should succeed: {response.Error}");
                Assert.IsTrue(response.HasValidJson, "Response should have valid JSON");
                Assert.IsTrue(response.JsonResponse.ContainsKey("result"), "Should have result field");
            }
        }
    }
}
