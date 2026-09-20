using NUnit.Framework;
using Unity.Pipeline.CodeReload;
using Unity.Pipeline.Runtime.Commands;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Integration tests for code reload CLI commands.
    /// Tests the complete workflow: CLI commands → compiler → registry → runtime behavior.
    /// </summary>
    class CodeReloadCommandTests
    {
        [SetUp]
        public void SetUp()
        {
            // Clear code reload state
            CodeReloadRegistry.ClearAllForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            CodeReloadRegistry.ClearAllForTesting();
        }

        [Test]
        public void CodeReloadStatus_ReturnsStats()
        {
            // Arrange - register a test method
            var method = typeof(SimpleTestClass).GetMethod("TestMethod");
            CodeReloadRegistry.RegisterReloadableMethod(method, new CodeReloadAttribute());

            // Act
            var result = CodeReloadCommands.CodeReloadStatus();

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Contains.Substring("Reloadable Methods: 1"));
            Assert.That(result.Message, Contains.Substring("Active Overrides: 0"));
        }

        [Test]
        public void ReloadFile_WithInvalidFilename_ReturnsBadRequest()
        {
            // Act
            var result = CodeReloadCommands.ReloadFile("");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo("Bad Request"));
            Assert.That(result.ErrorDetails, Contains.Substring("cannot be empty"));
        }

        [Test]
        public void ReloadFile_WithNonExistentFile_ReturnsFileNotFound()
        {
            // Act
            var result = CodeReloadCommands.ReloadFile("NonExistentFile.cs");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo("File Not Found"));
            Assert.That(result.ErrorDetails, Contains.Substring("NonExistentFile.cs"));
        }

        [Test]
        public void CodeReloadResponse_Success_CreatesCorrectResponse()
        {
            // Act
            var response = CodeReloadResponse.CmdSuccess("test-assembly", "Test message", new System.Collections.Generic.List<string> { "method1", "method2" }, 1500);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.AssemblyName, Is.EqualTo("test-assembly"));
            Assert.That(response.Message, Is.EqualTo("Test message"));
            Assert.That(response.Items.Count, Is.EqualTo(2));
            Assert.That(response.ExecutionTimeMs, Is.EqualTo(1500));
        }

        [Test]
        public void CodeReloadResponse_Failure_CreatesCorrectResponse()
        {
            // Act
            var response = CodeReloadResponse.CmdFailure("Test Error", "Detailed error message", 2000);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Error, Is.EqualTo("Test Error"));
            Assert.That(response.ErrorDetails, Is.EqualTo("Detailed error message"));
            Assert.That(response.ExecutionTimeMs, Is.EqualTo(2000));
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
        }
    }
}
