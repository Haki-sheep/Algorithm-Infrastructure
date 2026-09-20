using System.Reflection;
using NUnit.Framework;
using Unity.Pipeline.CodeReload;
using UnityEngine;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Tests for CodeReloadRegistry to verify method registration and statistics.
    /// Helps diagnose issues with code reload method registration.
    /// </summary>
    class CodeReloadRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            CodeReloadRegistry.ClearAllForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            CodeReloadRegistry.ClearAllForTesting();
        }

        [Test]
        public void RegisterMethodOverride_DirectCall_UpdatesStats()
        {
            // Test direct registration to verify registry behavior

            var method = typeof(TestRegistryClass).GetMethod("TestMethod");
            var attribute = method.GetCustomAttribute<CodeReloadOverrideMethodAttribute>();
            var type = typeof(TestRegistryClass);

            Assert.IsNotNull(method, "Test method should exist");
            Assert.IsNotNull(attribute, "Test attribute should exist");

            // Get initial stats
            var initialStats = CodeReloadRegistry.GetStats();

            // CRITICAL: First register the target method as reloadable (this was missing!)
            // Create a mock target method that matches the attribute's TargetMethodId
            var targetMethod = typeof(MockTargetClass1).GetMethod("TargetMethod1");
            var reloadableAttr = new CodeReloadAttribute { Id = "TargetClass1.TargetMethod1" };
            CodeReloadRegistry.RegisterReloadableMethod(targetMethod, reloadableAttr);

            // Register the type and method override
            CodeReloadRegistry.RegisterCodeReloadType(type, "TestAssembly");
            CodeReloadRegistry.RegisterMethodOverride(method, attribute, type);

            // Get final stats
            var finalStats = CodeReloadRegistry.GetStats();

            // Verify stats changed
            Assert.Greater(finalStats.ActiveOverrideCount, initialStats.ActiveOverrideCount, "Active override count should increase");
            Assert.Greater(finalStats.LoadedTypeCount, initialStats.LoadedTypeCount, "Loaded type count should increase");
            Assert.Greater(finalStats.ReloadableMethodCount, initialStats.ReloadableMethodCount, "Reloadable method count should increase");
        }

        [Test]
        public void GetStats_AfterRegistration_ReturnsCorrectCounts()
        {
            // Test that GetStats returns accurate counts

            var stats1 = CodeReloadRegistry.GetStats();
            Assert.AreEqual(0, stats1.ActiveOverrideCount, "Should start with 0 active overrides");

            // Register target methods as reloadable first (this was missing!)
            var targetMethod1 = typeof(MockTargetClass1).GetMethod("TargetMethod1");
            var reloadableAttr1 = new CodeReloadAttribute { Id = "TargetClass1.TargetMethod1" };
            CodeReloadRegistry.RegisterReloadableMethod(targetMethod1, reloadableAttr1);

            var targetMethod2 = typeof(MockTargetClass2).GetMethod("TargetMethod2");
            var reloadableAttr2 = new CodeReloadAttribute { Id = "TargetClass2.TargetMethod2" };
            CodeReloadRegistry.RegisterReloadableMethod(targetMethod2, reloadableAttr2);

            // Register multiple methods
            var method1 = typeof(TestRegistryClass).GetMethod("TestMethod");
            var attribute1 = method1.GetCustomAttribute<CodeReloadOverrideMethodAttribute>();
            CodeReloadRegistry.RegisterCodeReloadType(typeof(TestRegistryClass), "TestAssembly1");
            CodeReloadRegistry.RegisterMethodOverride(method1, attribute1, typeof(TestRegistryClass));

            var method2 = typeof(TestRegistryClass2).GetMethod("AnotherTestMethod");
            var attribute2 = method2.GetCustomAttribute<CodeReloadOverrideMethodAttribute>();
            CodeReloadRegistry.RegisterCodeReloadType(typeof(TestRegistryClass2), "TestAssembly2");
            CodeReloadRegistry.RegisterMethodOverride(method2, attribute2, typeof(TestRegistryClass2));

            var finalStats = CodeReloadRegistry.GetStats();

            Assert.AreEqual(2, finalStats.ActiveOverrideCount, "Should have 2 active overrides");
            Assert.AreEqual(2, finalStats.LoadedTypeCount, "Should have 2 loaded types");
            Assert.AreEqual(2, finalStats.ReloadableMethodCount, "Should have 2 reloadable methods");
        }

        [Test]
        public void ClearAllForTesting_ResetsStats()
        {
            // Register target method as reloadable first (this was missing!)
            var targetMethod = typeof(MockTargetClass1).GetMethod("TargetMethod1");
            var reloadableAttr = new CodeReloadAttribute { Id = "TargetClass1.TargetMethod1" };
            CodeReloadRegistry.RegisterReloadableMethod(targetMethod, reloadableAttr);

            // Register something first
            var method = typeof(TestRegistryClass).GetMethod("TestMethod");
            var attribute = method.GetCustomAttribute<CodeReloadOverrideMethodAttribute>();
            CodeReloadRegistry.RegisterCodeReloadType(typeof(TestRegistryClass), "TestAssembly");
            CodeReloadRegistry.RegisterMethodOverride(method, attribute, typeof(TestRegistryClass));

            var statsBeforeClear = CodeReloadRegistry.GetStats();
            Assert.Greater(statsBeforeClear.ActiveOverrideCount, 0, "Should have active overrides before clear");
            Assert.Greater(statsBeforeClear.ReloadableMethodCount, 0, "Should have reloadable methods before clear");

            // Clear and verify
            CodeReloadRegistry.ClearAllForTesting();

            var statsAfterClear = CodeReloadRegistry.GetStats();
            Assert.AreEqual(0, statsAfterClear.ActiveOverrideCount, "Should have 0 active overrides after clear");
            Assert.AreEqual(0, statsAfterClear.LoadedTypeCount, "Should have 0 loaded types after clear");
            Assert.AreEqual(0, statsAfterClear.ReloadableMethodCount, "Should have 0 reloadable methods after clear");
        }

        /// <summary>
        /// Test class with CodeReloadMethod attribute for registry testing.
        /// </summary>
        public static class TestRegistryClass
        {
            [CodeReloadOverrideMethod("TargetClass1.TargetMethod1")]
            public static void TestMethod(MockTargetClass1 instance)
            {
            }
        }

        /// <summary>
        /// Second test class for multi-registration testing.
        /// </summary>
        public static class TestRegistryClass2
        {
            [CodeReloadOverrideMethod("TargetClass2.TargetMethod2")]
            public static void AnotherTestMethod(MockTargetClass2 instance)
            {
            }
        }

        /// <summary>
        /// Mock target class 1 for testing reloadable method registration.
        /// </summary>
        public class MockTargetClass1
        {
            public int value = 42;

            public void TargetMethod1()
            {
                value = 123;
            }
        }

        /// <summary>
        /// Mock target class 2 for testing reloadable method registration.
        /// </summary>
        public class MockTargetClass2
        {
            public string text = "original";

            public void TargetMethod2()
            {
                text = "modified";
            }
        }
    }
}