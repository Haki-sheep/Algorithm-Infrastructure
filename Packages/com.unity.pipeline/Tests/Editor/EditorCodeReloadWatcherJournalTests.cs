using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Pipeline.Editor;
using UnityEditor;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Tests for the code-reload watcher's applied-files journal: the SessionState-backed record of
    /// files whose overrides were applied in-editor, used to re-apply them after the domain reload
    /// that entering play mode triggers (which wipes CodeReloadRegistry's in-memory overrides).
    /// </summary>
    class EditorCodeReloadWatcherJournalTests
    {
        private string m_TempDir;

        [SetUp]
        public void SetUp()
        {
            EditorCodeReloadWatcher.ClearAppliedFilesJournal();
            m_TempDir = FileUtil.GetUniqueTempPathInProject();
            Directory.CreateDirectory(m_TempDir);
        }

        [TearDown]
        public void TearDown()
        {
            EditorCodeReloadWatcher.ClearAppliedFilesJournal();
            if (Directory.Exists(m_TempDir))
                Directory.Delete(m_TempDir, recursive: true);
        }

        private string CreateTempScript(string name)
        {
            var path = Path.GetFullPath(Path.Combine(m_TempDir, name));
            File.WriteAllText(path, "// test");
            return path;
        }

        [Test]
        public void Journal_RoundTripsAppliedFiles()
        {
            var a = CreateTempScript("A.cs");
            var b = CreateTempScript("B.cs");

            EditorCodeReloadWatcher.JournalAppliedFile(a);
            EditorCodeReloadWatcher.JournalAppliedFile(b);

            var journaled = EditorCodeReloadWatcher.GetJournaledAppliedFiles();
            CollectionAssert.AreEquivalent(new[] { a, b }, journaled);
        }

        [Test]
        public void Journal_DeduplicatesRepeatedApplies()
        {
            var a = CreateTempScript("A.cs");

            EditorCodeReloadWatcher.JournalAppliedFile(a);
            EditorCodeReloadWatcher.JournalAppliedFile(a);

            Assert.AreEqual(1, EditorCodeReloadWatcher.GetJournaledAppliedFiles().Length);
        }

        [Test]
        public void ClearJournal_RemovesAllEntries()
        {
            EditorCodeReloadWatcher.JournalAppliedFile(CreateTempScript("A.cs"));

            EditorCodeReloadWatcher.ClearAppliedFilesJournal();

            Assert.IsEmpty(EditorCodeReloadWatcher.GetJournaledAppliedFiles());
        }

        [Test]
        public void ComputeResumeReapplies_NotPlaying_IsEmpty()
        {
            // Outside play mode the domain reload came from a real recompile, so the compiled code
            // already matches disk — nothing must be re-applied.
            var a = CreateTempScript("A.cs");

            var reapplies = EditorCodeReloadWatcher.ComputeResumeReapplies(
                isPlaying: false, journaledFiles: new[] { a });

            Assert.IsEmpty(reapplies);
        }

        [Test]
        public void ComputeResumeReapplies_Playing_ReturnsOnlyExistingFiles()
        {
            var existing = CreateTempScript("A.cs");
            var deleted = CreateTempScript("B.cs");
            File.Delete(deleted);

            var reapplies = EditorCodeReloadWatcher.ComputeResumeReapplies(
                isPlaying: true, journaledFiles: new[] { existing, deleted });

            Assert.AreEqual(new[] { existing }, reapplies.ToArray());
        }
    }
}
