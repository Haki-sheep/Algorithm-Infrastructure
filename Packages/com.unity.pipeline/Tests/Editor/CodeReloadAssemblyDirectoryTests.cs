using System.IO;
using NUnit.Framework;
using Unity.Pipeline.Compilation;
using Unity.Pipeline.CodeReload;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Tests for the assemblyDir parameter of code reload cleanup.
    /// Verifies that cleanup operates on the specified directory rather than the default temp directory.
    /// </summary>
    class CodeReloadAssemblyDirectoryTests
    {
        private string testAssemblyDir;

        [SetUp]
        public void SetUp()
        {
            testAssemblyDir = Path.Combine("TestAssemblies", "CodeReload");
            Directory.CreateDirectory(testAssemblyDir);

            // Clear state
            CodeReloadRegistry.ClearAllForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test files and directories
            if (Directory.Exists(testAssemblyDir))
            {
                Directory.Delete(testAssemblyDir, true);
            }
            if (Directory.Exists("TestAssemblies"))
            {
                Directory.Delete("TestAssemblies", true);
            }

            // Clean up code reload state
            CodeReloadRegistry.ClearAllForTesting();
            CodeReloadCompiler.CleanupCodeReloadDlls(); // Default cleanup
        }

        [Test]
        public void CleanupCodeReloadDlls_WithAssemblyDir_CleansUpDiskFiles()
        {
            // Arrange - Create some test assembly files
            var testAssembly1 = Path.Combine(testAssemblyDir, "TestAssembly_001.dll");
            var testAssembly2 = Path.Combine(testAssemblyDir, "TestAssembly_002.dll");
            var testAssembly3 = Path.Combine(testAssemblyDir, "TestAssembly_003.dll");

            File.WriteAllText(testAssembly1, "fake dll content 1");
            File.WriteAllText(testAssembly2, "fake dll content 2");
            File.WriteAllText(testAssembly3, "fake dll content 3");

            // Verify files exist before cleanup
            Assert.IsTrue(File.Exists(testAssembly1), "Test file 1 should exist before cleanup");
            Assert.IsTrue(File.Exists(testAssembly2), "Test file 2 should exist before cleanup");
            Assert.IsTrue(File.Exists(testAssembly3), "Test file 3 should exist before cleanup");

            // Act - Cleanup the specified directory
            var result = CodeReloadCompiler.CleanupCodeReloadDlls(testAssemblyDir);

            // Assert - Cleanup should succeed and keep only the latest version
            Assert.IsTrue(result.Success, $"Cleanup should succeed. Message: {result.Message}");
            Assert.AreEqual(2, result.DeletedFiles.Count, "Should delete the two older versions");
            Assert.IsFalse(File.Exists(testAssembly1), "Version 001 should be deleted");
            Assert.IsFalse(File.Exists(testAssembly2), "Version 002 should be deleted");
            Assert.IsTrue(File.Exists(testAssembly3), "Latest version should be kept");
        }

        [Test]
        public void CleanupCodeReloadDlls_WithMissingAssemblyDir_SucceedsWithoutDeleting()
        {
            // Arrange
            var missingDir = Path.Combine(testAssemblyDir, "DoesNotExist");
            Assert.IsFalse(Directory.Exists(missingDir), "Precondition: directory must not exist");

            // Act
            var result = CodeReloadCompiler.CleanupCodeReloadDlls(missingDir);

            // Assert
            Assert.IsTrue(result.Success, $"Cleanup should succeed for a missing directory. Message: {result.Message}");
            Assert.AreEqual(0, result.DeletedFiles.Count, "Should not delete any files");
            Assert.That(result.Message, Does.Contain("No code reload directory"));
        }
    }
}
