using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Pipeline.Editor.BuildProcessors;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Tests for the bundled-Roslyn-DLL integrity check (SHA-256 tamper detection) used by the
    /// build processor. The pure VerifyChecksums(dir, manifest) core is exercised against synthetic
    /// folders; the real package + CHECKSUMS is exercised via VerifyBundledChecksums().
    /// </summary>
    class RoslynChecksumTests
    {
        private string m_Dir;

        [SetUp]
        public void SetUp()
        {
            m_Dir = Path.Combine(Path.GetTempPath(), "pipeline_checksums_" + Path.GetRandomFileName());
            Directory.CreateDirectory(m_Dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_Dir))
                Directory.Delete(m_Dir, recursive: true);
        }

        private string WriteDll(string name, string content)
        {
            var path = Path.Combine(m_Dir, name);
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes(content));
            return path;
        }

        private string WriteChecksums(string text)
        {
            var path = Path.Combine(m_Dir, "CHECKSUMS");
            File.WriteAllText(path, text);
            return path;
        }

        /// <summary>
        /// Rewrite the hash column of the bundled Roslyn CHECKSUMS manifest from the DLLs currently
        /// on disk, so the integrity check passes again after a deliberate DLL upgrade. Regenerating
        /// re-blesses whatever is in the folder, which is why it lives in the test assembly
        /// (UNITY_INCLUDE_TESTS) and never ships to a project consuming the package.
        /// </summary>
        [MenuItem("Window/Pipeline/Tests/Regen Roslyn SHAS")]
        static void RegenRoslynChecksums()
        {
            if (!PipelineRuntimeBuildProcessor.TryGetBundledCodeAnalysisPaths(out var dir, out var checksumsPath))
            {
                Debug.LogError("Pipeline: could not locate the com.unity.pipeline package on disk to " +
                    "regenerate the Roslyn CHECKSUMS manifest.");
                return;
            }

            var summary = RegenerateChecksums(dir, checksumsPath);
            Debug.Log($"Pipeline: Roslyn CHECKSUMS {summary}\n{checksumsPath}");
        }

        /// <summary>
        /// Core, directly-testable regeneration. Rewrites only the hash token of each entry line in
        /// <paramref name="checksumsPath"/>, drops entries whose DLL is gone from
        /// <paramref name="codeAnalysisDir"/> and appends entries for DLLs that are present but
        /// unlisted, leaving comments, blank lines, filenames and each line's trailing
        /// "# &lt;package&gt; &lt;version&gt;" text untouched.
        /// </summary>
        /// <param name="codeAnalysisDir">Directory holding the DLLs to hash.</param>
        /// <param name="checksumsPath">Manifest to rewrite in place.</param>
        /// <returns>A human-readable summary of what changed, for logging.</returns>
        public static string RegenerateChecksums(string codeAnalysisDir, string checksumsPath)
        {
            var onDisk = Directory.GetFiles(codeAnalysisDir, "*.dll").ToDictionary(
                Path.GetFileName,
                PipelineRuntimeBuildProcessor.ComputeSha256,
                StringComparer.OrdinalIgnoreCase);

            var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changes = new List<string>();
            var lines = new List<string>();

            foreach (var raw in File.ReadAllLines(checksumsPath))
            {
                var trimmed = raw.Trim();
                var tokens = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                // Comments, blank lines and anything that is not an entry pass through verbatim,
                // which is what keeps the manifest's own header instructions intact.
                if (trimmed.Length == 0 || trimmed.StartsWith("#") || tokens.Length < 2)
                {
                    lines.Add(raw);
                    continue;
                }

                var name = tokens[1];
                listed.Add(name);

                if (!onDisk.TryGetValue(name, out var hash))
                {
                    changes.Add($"removed {name}");
                    continue;
                }

                if (string.Equals(tokens[0], hash, StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add(raw);
                    continue;
                }

                var hashStart = raw.IndexOf(tokens[0], StringComparison.Ordinal);
                lines.Add(raw.Substring(0, hashStart) + hash + raw.Substring(hashStart + tokens[0].Length));
                changes.Add($"updated {name}");
            }

            foreach (var name in onDisk.Keys.Where(n => !listed.Contains(n)).OrderBy(n => n, StringComparer.Ordinal))
            {
                lines.Add($"{onDisk[name]}  {name}  # {Path.GetFileNameWithoutExtension(name)} TODO");
                changes.Add($"added {name}");
            }

            // The manifest is committed with LF endings (.gitattributes pins eol=lf), so join
            // explicitly rather than let WriteAllLines emit the platform separator.
            File.WriteAllText(checksumsPath, string.Join("\n", lines) + "\n");

            return changes.Count == 0 ? "already up to date" : "regenerated: " + string.Join(", ", changes);
        }

        [Test]
        public void RegenerateChecksums_StaleManifest_RewritesEntriesAndKeepsComments()
        {
            WriteDll("a.dll", "hello");   // listed, but with a stale hash
            WriteDll("b.dll", "world");   // on disk, not listed yet
            var manifest = WriteChecksums(
                "# keep me\n" +
                "0000000000000000000000000000000000000000000000000000000000000000  a.dll  # PkgA 1.2.3\n" +
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  ghost.dll\n");

            var summary = RegenerateChecksums(m_Dir, manifest);

            Assert.IsNull(PipelineRuntimeBuildProcessor.VerifyChecksums(m_Dir, manifest),
                $"The regenerated manifest should verify. Summary was: {summary}");

            var text = File.ReadAllText(manifest);
            StringAssert.Contains("# keep me", text, "Comment lines must survive regeneration");
            StringAssert.Contains("# PkgA 1.2.3", text,
                "A line's trailing package/version text must survive so hand-filled versions are not lost");
            StringAssert.DoesNotContain("ghost.dll", text, "An entry whose DLL is gone must be dropped");
            StringAssert.Contains("b.dll", text, "A DLL present but unlisted must be added");
        }

        [Test]
        public void ComputeSha256_KnownContent_MatchesKnownHash()
        {
            var path = WriteDll("known.bin", "abc");

            // SHA-256("abc") - well-known test vector.
            Assert.AreEqual(
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                PipelineRuntimeBuildProcessor.ComputeSha256(path));
        }

        [Test]
        public void VerifyChecksums_MatchingFiles_ReturnsNull()
        {
            WriteDll("a.dll", "hello");
            var hash = PipelineRuntimeBuildProcessor.ComputeSha256(Path.Combine(m_Dir, "a.dll"));
            var manifest = WriteChecksums($"# header\n{hash}  a.dll  # pkg TODO\n");

            Assert.IsNull(PipelineRuntimeBuildProcessor.VerifyChecksums(m_Dir, manifest));
        }

        [Test]
        public void VerifyChecksums_TamperedFile_ReturnsMismatch()
        {
            WriteDll("a.dll", "hello");
            // Manifest claims a hash that does not match the file's real content.
            var manifest = WriteChecksums(
                "0000000000000000000000000000000000000000000000000000000000000000  a.dll\n");

            var error = PipelineRuntimeBuildProcessor.VerifyChecksums(m_Dir, manifest);

            Assert.IsNotNull(error);
            StringAssert.Contains("hash mismatch", error);
        }

        [Test]
        public void VerifyChecksums_UnlistedDll_ReturnsError()
        {
            WriteDll("a.dll", "hello");
            WriteDll("rogue.dll", "evil"); // present on disk but not in the manifest
            var hash = PipelineRuntimeBuildProcessor.ComputeSha256(Path.Combine(m_Dir, "a.dll"));
            var manifest = WriteChecksums($"{hash}  a.dll\n");

            var error = PipelineRuntimeBuildProcessor.VerifyChecksums(m_Dir, manifest);

            Assert.IsNotNull(error);
            StringAssert.Contains("rogue.dll", error);
        }

        [Test]
        public void VerifyChecksums_MissingListedDll_ReturnsError()
        {
            // Manifest lists a DLL that does not exist on disk.
            var manifest = WriteChecksums(
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  ghost.dll\n");

            var error = PipelineRuntimeBuildProcessor.VerifyChecksums(m_Dir, manifest);

            Assert.IsNotNull(error);
            StringAssert.Contains("missing", error);
        }

        [Test]
        public void VerifyBundledChecksums_RealPackage_DoesNotThrow()
        {
            // Happy path against the actual bundled DLLs and committed CHECKSUMS in this package.
            try
            {
                PipelineRuntimeBuildProcessor.VerifyBundledChecksums();
            }
            catch (BuildFailedException ex)
            {
                Assert.Fail($"Bundled Roslyn DLLs failed integrity check: {ex.Message}");
            }
        }
    }
}
