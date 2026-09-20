using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Pipeline.CodeReload;
using Unity.Pipeline.Runtime.Commands;
using UnityEngine;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Probe for the baseline-aware in-place reload tests: two independent [CodeReload] methods so
    /// a reload of one can be asserted to leave the other running compiled.
    /// </summary>
    public class BaselineReloadProbe
    {
        public int first;
        public int second;

        [CodeReload]
        public void First()
        {
            first = 1; // original body
        }

        [CodeReload]
        public void Second()
        {
            second = 1; // original body
        }
    }

    /// <summary>
    /// Tests for <see cref="CodeReloadBaseline"/> — the per-method snapshot that lets a reload skip
    /// methods still matching the compiled code — and for its use by InPlaceReloadProcessor
    /// (skip unchanged, unregister reverted).
    /// </summary>
    class CodeReloadBaselineTests
    {
        private const string FakePath = "Assets/FakeBaselineProbe.cs";

        private const string BaselineSource = @"
using Unity.Pipeline.CodeReload;
namespace Unity.Pipeline.Tests.Editor
{
    public class BaselineReloadProbe
    {
        public int first;
        public int second;

        [CodeReload]
        public void First()
        {
            first = 1; // original body
        }

        [CodeReload]
        public void Second()
        {
            second = 1; // original body
        }
    }
}";

        [TearDown]
        public void TearDown()
        {
            CodeReloadBaseline.Clear();
            CodeReloadRegistry.UnregisterMethodOverride("BaselineReloadProbe.First");
            CodeReloadRegistry.UnregisterMethodOverride("BaselineReloadProbe.Second");
        }

        // -------- classification --------

        [Test]
        public void NoBaseline_ClassifiesNothing()
        {
            Assert.IsNull(CodeReloadBaseline.GetUnchangedMethods(FakePath, BaselineSource),
                "Without a captured baseline every method must be treated as changed.");
            Assert.IsFalse(CodeReloadBaseline.IsFileUpToDate(FakePath, BaselineSource));
        }

        [Test]
        public void IdenticalSource_AllMethodsUnchanged()
        {
            Assert.IsTrue(CodeReloadBaseline.CaptureFromSource(FakePath, BaselineSource));

            var unchanged = CodeReloadBaseline.GetUnchangedMethods(FakePath, BaselineSource);
            Assert.IsNotNull(unchanged);
            CollectionAssert.AreEquivalent(new[] { "First", "Second" }, unchanged);
            Assert.IsTrue(CodeReloadBaseline.IsFileUpToDate(FakePath, BaselineSource));
        }

        [Test]
        public void WhitespaceAndCommentEdits_DoNotCountAsChanges()
        {
            CodeReloadBaseline.CaptureFromSource(FakePath, BaselineSource);

            var reformatted = BaselineSource
                .Replace("first = 1; // original body", "first =\n                1; /* reflowed, comment changed */")
                .Replace("public void Second()", "public   void   Second()");

            var unchanged = CodeReloadBaseline.GetUnchangedMethods(FakePath, reformatted);
            CollectionAssert.AreEquivalent(new[] { "First", "Second" }, unchanged);
            Assert.IsTrue(CodeReloadBaseline.IsFileUpToDate(FakePath, reformatted));
        }

        [Test]
        public void BodyEdit_MarksOnlyThatMethodChanged()
        {
            CodeReloadBaseline.CaptureFromSource(FakePath, BaselineSource);

            var edited = BaselineSource.Replace("second = 1;", "second = 99;");
            var unchanged = CodeReloadBaseline.GetUnchangedMethods(FakePath, edited);

            CollectionAssert.AreEquivalent(new[] { "First" }, unchanged);
            Assert.IsFalse(CodeReloadBaseline.IsFileUpToDate(FakePath, edited));
        }

        [Test]
        public void SignatureEdit_MarksMethodChanged()
        {
            CodeReloadBaseline.CaptureFromSource(FakePath, BaselineSource);

            // Renaming a parameterless method's parameter list is not possible; add a default arg.
            var edited = BaselineSource.Replace("public void Second()", "public void Second(int times = 1)");
            var unchanged = CodeReloadBaseline.GetUnchangedMethods(FakePath, edited);

            // The signature change also changes the class context (the signature is context), so
            // conservatively nothing is skippable.
            CollectionAssert.DoesNotContain(unchanged, "Second");
        }

        [Test]
        public void FieldEdit_ChangesContext_NothingSkippable()
        {
            CodeReloadBaseline.CaptureFromSource(FakePath, BaselineSource);

            // An untouched body can read this field; the override compiles against the current
            // file, so a declaration change conservatively invalidates every method.
            var edited = BaselineSource.Replace("public int second;", "public int second = 5;");
            var unchanged = CodeReloadBaseline.GetUnchangedMethods(FakePath, edited);

            Assert.IsNotNull(unchanged);
            Assert.AreEqual(0, unchanged.Count);
        }

        [Test]
        public void HelperBodyEdit_DoesNotChangeContext()
        {
            var withHelper = BaselineSource.Replace(
                "public int first;",
                "public int first;\n        private int Helper() { return 3; }\n        private int Arrow => 4;");
            CodeReloadBaseline.CaptureFromSource(FakePath, withHelper);

            // Overrides call helpers through the compiled host either way, so a helper body edit
            // can't change what an override does — it must not mark reloadable methods changed.
            var edited = withHelper
                .Replace("return 3;", "return 30;")
                .Replace("Arrow => 4;", "Arrow => 40;");
            var unchanged = CodeReloadBaseline.GetUnchangedMethods(FakePath, edited);

            CollectionAssert.AreEquivalent(new[] { "First", "Second" }, unchanged);
        }

        [Test]
        public void MustReloadMethods_BlockUpToDateShortCircuit()
        {
            CodeReloadBaseline.CaptureFromSource(FakePath, BaselineSource);

            // A device already running an override for Second can't unregister it remotely — the
            // file must not be reported up to date even though the source matches the baseline.
            var pushed = new HashSet<string> { "Second" };
            Assert.IsFalse(CodeReloadBaseline.IsFileUpToDate(FakePath, BaselineSource, null, pushed));
        }

        [Test]
        public void SerializeRestore_RoundTripsClassification()
        {
            CodeReloadBaseline.CaptureFromSource(FakePath, BaselineSource);
            var serialized = CodeReloadBaseline.Serialize();

            CodeReloadBaseline.Clear();
            Assert.IsNull(CodeReloadBaseline.GetUnchangedMethods(FakePath, BaselineSource));

            CodeReloadBaseline.Restore(serialized);
            var edited = BaselineSource.Replace("second = 1;", "second = 99;");
            CollectionAssert.AreEquivalent(new[] { "First" },
                CodeReloadBaseline.GetUnchangedMethods(FakePath, edited));
        }

        // -------- processor integration --------

        [Test]
        public void Reload_SkipsUnchangedMethods_AndUnregistersReverted()
        {
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(BaselineReloadProbe).GetMethod(nameof(BaselineReloadProbe.First)),
                new CodeReloadAttribute());
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(BaselineReloadProbe).GetMethod(nameof(BaselineReloadProbe.Second)),
                new CodeReloadAttribute());

            var path = Path.Combine(Application.temporaryCachePath, "BaselineReloadProbe_edit.cs");
            try
            {
                // Watch start: disk matches the compiled probe.
                CodeReloadBaseline.CaptureFromSource(path, BaselineSource);

                // Save 1: untouched file → nothing compiles, nothing registers.
                File.WriteAllText(path, BaselineSource);
                var result = InPlaceReloadProcessor.ProcessSourceFileOnMainThread(path);
                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.IsTrue(result.AllUpToDate);
                CollectionAssert.IsEmpty(result.RegisteredMethods);
                CollectionAssert.AreEquivalent(new[] { "First", "Second" }, result.UpToDateMethods);
                Assert.IsFalse(CodeReloadRegistry.HasOverride("BaselineReloadProbe.Second"));

                // Save 2: only Second edited → First stays compiled, Second gets an override.
                File.WriteAllText(path, BaselineSource.Replace("second = 1;", "second = 99;"));
                result = InPlaceReloadProcessor.ProcessSourceFileOnMainThread(path);
                Assert.IsTrue(result.Success, result.ErrorMessage);
                CollectionAssert.AreEquivalent(new[] { "BaselineReloadProbe.Second" }, result.RegisteredMethods);
                CollectionAssert.AreEquivalent(new[] { "First" }, result.UpToDateMethods);
                Assert.IsFalse(CodeReloadRegistry.HasOverride("BaselineReloadProbe.First"));

                var probe = new BaselineReloadProbe();
                probe.Second();
                Assert.AreEqual(99, probe.second, "Edited body should run via the woven dispatch");
                probe.First();
                Assert.AreEqual(1, probe.first, "Unchanged body should run compiled");

                // Save 3: the edit is undone → the stale override is removed, compiled body runs.
                File.WriteAllText(path, BaselineSource);
                result = InPlaceReloadProcessor.ProcessSourceFileOnMainThread(path);
                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.IsTrue(result.AllUpToDate);
                CollectionAssert.AreEquivalent(new[] { "BaselineReloadProbe.Second" }, result.RevertedMethods);
                Assert.IsFalse(CodeReloadRegistry.HasOverride("BaselineReloadProbe.Second"));

                var reverted = new BaselineReloadProbe();
                reverted.Second();
                Assert.AreEqual(1, reverted.second, "Reverted method should run the original compiled body");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test] // The reload_file command's contract for the baseline paths: an up-to-date file is
               // an explicit "up to date" success (never "successful with 0 methods"), and stale
               // overrides it reverts are reported, not silently unregistered.
        public void ReloadFileCommand_UpToDate_ReportsUpToDateAndReverted()
        {
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(BaselineReloadProbe).GetMethod(nameof(BaselineReloadProbe.First)),
                new CodeReloadAttribute());
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(BaselineReloadProbe).GetMethod(nameof(BaselineReloadProbe.Second)),
                new CodeReloadAttribute());

            var path = Path.Combine(Application.temporaryCachePath, "BaselineReloadProbe_cmd.cs");
            try
            {
                CodeReloadBaseline.CaptureFromSource(path, BaselineSource);

                // Edit Second → a normal reload that registers one override.
                File.WriteAllText(path, BaselineSource.Replace("second = 1;", "second = 99;"));
                var edited = CodeReloadCommands.ReloadFile(path);
                Assert.IsTrue(edited.Success, edited.Message);
                StringAssert.Contains("1 methods", edited.Message);

                // Undo the edit → explicit up-to-date success that names the reverted override.
                File.WriteAllText(path, BaselineSource);
                var upToDate = CodeReloadCommands.ReloadFile(path);
                Assert.IsTrue(upToDate.Success, upToDate.Message);
                StringAssert.Contains("Up to date", upToDate.Message);
                StringAssert.Contains("reverted 1 stale override(s): BaselineReloadProbe.Second", upToDate.Message);
                StringAssert.DoesNotContain("0 methods", upToDate.Message);
                Assert.IsFalse(CodeReloadRegistry.HasOverride("BaselineReloadProbe.Second"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Reload_WithoutBaseline_ReloadsEverything()
        {
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(BaselineReloadProbe).GetMethod(nameof(BaselineReloadProbe.First)),
                new CodeReloadAttribute());
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(BaselineReloadProbe).GetMethod(nameof(BaselineReloadProbe.Second)),
                new CodeReloadAttribute());

            var path = Path.Combine(Application.temporaryCachePath, "BaselineReloadProbe_nobaseline.cs");
            try
            {
                // No baseline captured (no watch): pre-baseline behavior — every method reloads.
                File.WriteAllText(path, BaselineSource);
                var result = InPlaceReloadProcessor.ProcessSourceFileOnMainThread(path);
                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.IsFalse(result.AllUpToDate);
                CollectionAssert.AreEquivalent(
                    new[] { "BaselineReloadProbe.First", "BaselineReloadProbe.Second" },
                    result.RegisteredMethods);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
