using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Pipeline.CodeReload;
using UnityEngine;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Simple verification tests for code reload infrastructure.
    /// These tests verify the core functionality works correctly in isolation.
    /// </summary>
    class CodeReloadVerificationTests
    {
        [SetUp]
        public void SetUp()
        {
            // Ensure clean state for each test
            CodeReloadRegistry.ClearAllForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up after each test
            CodeReloadRegistry.ClearAllForTesting();
        }

        [Test]
        public void CodeReloadRegistry_EmptyState_HasZeroStats()
        {
            // Act
            var stats = CodeReloadRegistry.GetStats();

            // Assert
            Assert.That(stats.ReloadableMethodCount, Is.EqualTo(0));
            Assert.That(stats.ActiveOverrideCount, Is.EqualTo(0));
            Assert.That(stats.LoadedTypeCount, Is.EqualTo(0));
        }

        [Test]
        public void CodeReloadRegistry_RegisterSingleMethod_CorrectCount()
        {
            // Arrange
            var testType = typeof(SimpleTestClass);
            var testMethod = testType.GetMethod("TestMethod");
            var attribute = new CodeReloadAttribute();

            // Act
            CodeReloadRegistry.RegisterReloadableMethod(testMethod, attribute);

            // Assert
            var stats = CodeReloadRegistry.GetStats();
            Assert.That(stats.ReloadableMethodCount, Is.EqualTo(1));
            Assert.That(stats.ReloadableMethodIds.Count, Is.EqualTo(1));
            Assert.That(stats.ReloadableMethodIds.Contains("SimpleTestClass.TestMethod"));
        }

        [Test]
        public void CodeReloadRegistry_RegisterMultipleMethods_CorrectCount()
        {
            // Arrange
            var testType = typeof(SimpleTestClass);
            var method1 = testType.GetMethod("TestMethod");
            var method2 = testType.GetMethod("AnotherTestMethod");

            // Act
            CodeReloadRegistry.RegisterReloadableMethod(method1, new CodeReloadAttribute());
            CodeReloadRegistry.RegisterReloadableMethod(method2, new CodeReloadAttribute { Id = "CustomId" });

            // Assert
            var stats = CodeReloadRegistry.GetStats();
            Assert.That(stats.ReloadableMethodCount, Is.EqualTo(2));
            Assert.That(stats.ReloadableMethodIds.Count, Is.EqualTo(2));
            Assert.That(stats.ReloadableMethodIds.Contains("SimpleTestClass.TestMethod"));
            Assert.That(stats.ReloadableMethodIds.Contains("CustomId"));
        }

        [Test]
        public void CodeReloadRegistry_TryInvokeWithoutOverride_ReturnsFalse()
        {
            // Arrange
            var testType = typeof(SimpleTestClass);
            var testMethod = testType.GetMethod("TestMethod");
            CodeReloadRegistry.RegisterReloadableMethod(testMethod, new CodeReloadAttribute());

            var testInstance = new SimpleTestClass();

            // Act
            var result = CodeReloadRegistry.TryInvokeCodeReload("SimpleTestClass.TestMethod", testInstance);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void CodeReloadRegistry_ClearForTesting_RemovesAllState()
        {
            // Arrange - add some state
            var testType = typeof(SimpleTestClass);
            var testMethod = testType.GetMethod("TestMethod");
            CodeReloadRegistry.RegisterReloadableMethod(testMethod, new CodeReloadAttribute());

            // Verify state exists
            var statsBefore = CodeReloadRegistry.GetStats();
            Assert.That(statsBefore.ReloadableMethodCount, Is.EqualTo(1));

            // Act
            CodeReloadRegistry.ClearAllForTesting();

            // Assert
            var statsAfter = CodeReloadRegistry.GetStats();
            Assert.That(statsAfter.ReloadableMethodCount, Is.EqualTo(0));
            Assert.That(statsAfter.ActiveOverrideCount, Is.EqualTo(0));
            Assert.That(statsAfter.LoadedTypeCount, Is.EqualTo(0));
        }

        /// <summary>
        /// Simple test class for code reload testing.
        /// </summary>
        public class SimpleTestClass
        {
            public int value;

            public void TestMethod()
            {
                value = 42;
            }

            public int AnotherTestMethod(int input)
            {
                return input * 2;
            }
        }
    }
}