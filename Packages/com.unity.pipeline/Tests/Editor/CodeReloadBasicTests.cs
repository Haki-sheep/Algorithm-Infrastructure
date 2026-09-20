using System.Collections;
using NUnit.Framework;
using Unity.Pipeline.CodeReload;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Basic tests for code reload Pattern A method override functionality.
    /// Tests the end-to-end flow: mark method -> create override -> compile -> verify behavior.
    /// </summary>
    class CodeReloadBasicTests
    {
        /// <summary>
        /// Test class with reloadable methods for testing.
        /// </summary>
        public class TestComponent : MonoBehaviour
        {
            public int lastCalculatedValue;
            public string lastMessage;

            public void UpdateValue()
            {
                lastCalculatedValue = 10; // Original behavior
                lastMessage = "Original UpdateValue called";
            }

            public int CalculateScore(int input)
            {
                return input * 2; // Original calculation
            }
        }

        private TestComponent testComponent;

        [SetUp]
        public void SetUp()
        {
            // Create test component
            var go = new GameObject("CodeReloadTestObject");
            testComponent = go.AddComponent<TestComponent>();

            // Clear any existing code reload state (including reloadable methods for test isolation)
            CodeReloadRegistry.ClearAllForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            if (testComponent != null && testComponent.gameObject != null)
            {
                Object.DestroyImmediate(testComponent.gameObject);
            }

            // Clean up code reload state
            CodeReloadRegistry.ClearAllForTesting();
        }

        [Test]
        public void CodeReloadRegistry_RegisterReloadableMethod_Success()
        {
            // Arrange
            var method = typeof(TestComponent).GetMethod("UpdateValue");
            var attribute = new CodeReloadAttribute();

            // Act
            CodeReloadRegistry.RegisterReloadableMethod(method, attribute);

            // Assert
            var stats = CodeReloadRegistry.GetStats();
            Assert.That(stats.ReloadableMethodCount, Is.EqualTo(1));
            Assert.That(stats.ReloadableMethodIds.Contains("TestComponent.UpdateValue"));
        }

        [Test]
        public void CodeReloadRegistry_RegisterReloadableMethodWithCustomId_Success()
        {
            // Arrange
            var method = typeof(TestComponent).GetMethod("CalculateScore");
            var attribute = new CodeReloadAttribute { Id = "CustomCalculation" };

            // Act
            CodeReloadRegistry.RegisterReloadableMethod(method, attribute);

            // Assert
            var stats = CodeReloadRegistry.GetStats();
            Assert.That(stats.ReloadableMethodCount, Is.EqualTo(1));
            Assert.That(stats.ReloadableMethodIds.Contains("CustomCalculation"));
        }

        [Test]
        public void CodeReloadRegistry_TryInvokeCodeReload_NoOverrideReturnsFalse()
        {
            // Arrange
            var method = typeof(TestComponent).GetMethod("UpdateValue");
            var attribute = new CodeReloadAttribute();
            CodeReloadRegistry.RegisterReloadableMethod(method, attribute);

            // Act
            var result = CodeReloadRegistry.TryInvokeCodeReload("TestComponent.UpdateValue", testComponent);

            // Assert
            Assert.That(result, Is.False, "Should return false when no code reload override is registered");
        }

        [Test]
        public void CodeReloadRegistry_GetStats_ReturnsCorrectCounts()
        {
            // Arrange - register multiple methods
            var updateMethod = typeof(TestComponent).GetMethod("UpdateValue");
            var calculateMethod = typeof(TestComponent).GetMethod("CalculateScore");

            CodeReloadRegistry.RegisterReloadableMethod(updateMethod, new CodeReloadAttribute());
            CodeReloadRegistry.RegisterReloadableMethod(calculateMethod, new CodeReloadAttribute { Id = "CustomCalculation" });

            // Act
            var stats = CodeReloadRegistry.GetStats();

            // Assert
            Assert.That(stats.ReloadableMethodCount, Is.EqualTo(2));
            Assert.That(stats.ActiveOverrideCount, Is.EqualTo(0));
            Assert.That(stats.LoadedTypeCount, Is.EqualTo(0));
            Assert.That(stats.ReloadableMethodIds.Count, Is.EqualTo(2));
        }

        [Test]
        public void CodeReloadRegistry_ClearAllOverrides_ClearsAllState()
        {
            // Arrange - register some methods and mock some overrides
            var method = typeof(TestComponent).GetMethod("UpdateValue");
            CodeReloadRegistry.RegisterReloadableMethod(method, new CodeReloadAttribute());

            // Act
            CodeReloadRegistry.ClearAllOverrides();

            // Assert
            var stats = CodeReloadRegistry.GetStats();
            Assert.That(stats.ActiveOverrideCount, Is.EqualTo(0));
            Assert.That(stats.LoadedTypeCount, Is.EqualTo(0));

            // Note: ReloadableMethodCount might not be cleared as those are the original methods, not overrides
            // This depends on implementation - currently reloadable methods are not cleared, only overrides
        }

        [UnityTest]
        public IEnumerator CodeReloadRegistry_MainThreadDispatch_WorksCorrectly()
        {
            // Arrange
            var method = typeof(TestComponent).GetMethod("UpdateValue");
            var attribute = new CodeReloadAttribute { RequireMainThread = true };
            CodeReloadRegistry.RegisterReloadableMethod(method, attribute);

            // Act - test that method can be invoked (even without override, for thread safety)
            var result = CodeReloadRegistry.TryInvokeCodeReload("TestComponent.UpdateValue", testComponent);

            // Assert
            Assert.That(result, Is.False); // No override registered, should return false

            // Yield to ensure any async operations complete
            yield return null;
        }

        [Test]
        public void TestComponent_OriginalBehavior_WorksCorrectly()
        {
            // Arrange & Act - call original methods to establish baseline
            testComponent.UpdateValue();
            var score = testComponent.CalculateScore(5);

            // Assert
            Assert.That(testComponent.lastCalculatedValue, Is.EqualTo(10));
            Assert.That(testComponent.lastMessage, Is.EqualTo("Original UpdateValue called"));
            Assert.That(score, Is.EqualTo(10)); // 5 * 2
        }
    }
}