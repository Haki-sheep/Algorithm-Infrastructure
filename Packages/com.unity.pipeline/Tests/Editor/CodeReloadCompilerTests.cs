using System.IO;
using System.Reflection;
using NUnit.Framework;
using Unity.Pipeline.Compilation;
using Unity.Pipeline.CodeReload;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Tests for CodeReloadCompiler's cleanup of versioned code reload assemblies.
    /// </summary>
    class CodeReloadCompilerTests
    {
        private string testAssemblyDir;

        [SetUp]
        public void SetUp()
        {
            // Keep test files OUTSIDE Assets/ (project-root Temp/, which Unity ignores) so the
            // editor never imports them — an import triggers a domain reload that tears down the
            // live pipeline server.
            testAssemblyDir = Path.GetFullPath(Path.Combine("Temp", "PipelineCodeReloadTests", "CompilerTests"));
            Directory.CreateDirectory(testAssemblyDir);

            // Clear code reload state
            CodeReloadRegistry.ClearAllForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test files
            if (Directory.Exists(testAssemblyDir))
            {
                Directory.Delete(testAssemblyDir, true);
            }

            // Clean up code reload state and temp files
            CodeReloadRegistry.ClearAllForTesting();
            CodeReloadCompiler.CleanupCodeReloadDlls();
        }

        [Test]
        public void CleanupCodeReloadDlls_WithNoFiles_SucceedsGracefully()
        {
            // Act
            var result = CodeReloadCompiler.CleanupCodeReloadDlls();

            // Assert
            Assert.IsTrue(result.Success, "Cleanup should succeed even when no files exist");
            Assert.AreEqual(0, result.DeletedFiles.Count, "Should not delete any files");
            Assert.That(result.Message, Does.Contain("No code reload directory").Or.Contain("0 old DLL versions"));
        }

        [Test]
        public void CleanupCodeReloadDlls_WithVersionedDlls_KeepsOnlyLatestVersion()
        {
            // Arrange - three versions of one assembly, plus an unversioned DLL cleanup must leave alone
            var v1 = Path.Combine(testAssemblyDir, "Sample_001.dll");
            var v2 = Path.Combine(testAssemblyDir, "Sample_002.dll");
            var v3 = Path.Combine(testAssemblyDir, "Sample_003.dll");
            var unversioned = Path.Combine(testAssemblyDir, "Plain.dll");
            File.WriteAllText(v1, "fake dll v1");
            File.WriteAllText(v2, "fake dll v2");
            File.WriteAllText(v3, "fake dll v3");
            File.WriteAllText(unversioned, "fake dll");

            // Act
            var result = CodeReloadCompiler.CleanupCodeReloadDlls(testAssemblyDir);

            // Assert
            Assert.IsTrue(result.Success, $"Cleanup should succeed. Message: {result.Message}");
            Assert.GreaterOrEqual(result.ExecutionTimeMs, 0, "Should record execution time (0ms is acceptable for fast cleanup)");
            Assert.IsFalse(File.Exists(v1), "Version 001 should be deleted");
            Assert.IsFalse(File.Exists(v2), "Version 002 should be deleted");
            Assert.IsTrue(File.Exists(v3), "Latest version should be kept");
            Assert.IsTrue(File.Exists(unversioned), "Unversioned DLLs should be left alone");
            CollectionAssert.AreEquivalent(new[] { "Sample_001.dll", "Sample_002.dll" }, result.DeletedFiles);
        }

        [Test]
        public void CleanupCodeReloadDlls_WithActiveOverrides_ClearsRegistry()
        {
            // Arrange - register a reloadable target and an override for it
            var target = typeof(CleanupTarget).GetMethod(nameof(CleanupTarget.Tick));
            CodeReloadRegistry.RegisterReloadableMethod(target, new CodeReloadAttribute { Id = "CleanupTarget.Tick" });

            var overrideMethod = typeof(CleanupOverrides).GetMethod(nameof(CleanupOverrides.Tick));
            var overrideAttribute = overrideMethod.GetCustomAttribute<CodeReloadOverrideMethodAttribute>();
            Assert.IsTrue(CodeReloadRegistry.RegisterMethodOverride(overrideMethod, overrideAttribute, typeof(CleanupOverrides)),
                "Override should register against the reloadable target");
            Assert.Greater(CodeReloadRegistry.GetStats().ActiveOverrideCount, 0, "Precondition: an override is active");

            // Act
            var result = CodeReloadCompiler.CleanupCodeReloadDlls(testAssemblyDir);

            // Assert
            Assert.IsTrue(result.Success, $"Cleanup should succeed. Message: {result.Message}");
            Assert.AreEqual(0, CodeReloadRegistry.GetStats().ActiveOverrideCount, "Registry should be cleared after cleanup");
        }

        /// <summary>
        /// Target whose method the cleanup test registers as reloadable.
        /// </summary>
        public class CleanupTarget
        {
            public int value;

            public void Tick()
            {
                value = 42;
            }
        }

        /// <summary>
        /// Override for <see cref="CleanupTarget.Tick"/>, in the shape the reload pipeline generates.
        /// </summary>
        public static class CleanupOverrides
        {
            [CodeReloadOverrideMethod("CleanupTarget.Tick")]
            public static void Tick(CleanupTarget instance)
            {
                instance.value = 99;
            }
        }
    }
}
