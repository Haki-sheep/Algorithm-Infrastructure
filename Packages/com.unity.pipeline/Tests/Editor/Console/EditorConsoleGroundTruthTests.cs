using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Pipeline.Console;
using Unity.Pipeline.Editor.Console;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Pipeline.Tests.Editor.Console
{
    /// <summary>
    /// Tests for the Editor console ground-truth sampler: what it publishes, that seeding leaves the
    /// user's console filtering exactly as it found it, and that it re-arms capture when the console
    /// moves and the buffer's cursor does not.
    /// </summary>
    class EditorConsoleGroundTruthTests
    {
        [TearDown]
        public void TearDown()
        {
            // A failed watchdog test would otherwise leave capture dead for the rest of the run.
            ConsoleLogCapture.EnsureCapturing();
        }

        [Test]
        public void Sample_PublishesConsoleCountsAndCompilationFlag()
        {
            var marker = NewMarker("groundtruth");
            EmitWarning(marker);

            EditorConsoleGroundTruth.SampleForTests();

            var truth = ConsoleLogCapture.GroundTruth;
            Assert.IsNotNull(truth, "Sampling must publish a snapshot");
            Assert.Greater(truth.ConsoleWarnings, 0, "The warning just logged is in the Editor console");
            Assert.Less((DateTime.UtcNow - truth.SampledUtc).TotalSeconds, 30, "The snapshot should be the one just taken");
        }

        [Test]
        public void Seed_RestoresConsoleFlagsAndFilterText()
        {
            if (!EditorConsoleEntries.Available)
                Assert.Ignore("UnityEditor.LogEntries is not reachable on this Editor version");

            var originalFlags = GetConsoleFlags();
            var originalFilter = GetFilteringText();
            try
            {
                // Restrict the console the way a user might: hide errors and set a search filter.
                // Seeding has to force both open to see every entry, then put them back.
                SetConsoleFlags(originalFlags & ~LogLevelError);
                SetFilteringText("pipeline_seed_filter_probe");

                var restrictedFlags = GetConsoleFlags();
                var restrictedFilter = GetFilteringText();

                EditorConsoleGroundTruth.ResetForTests();
                EditorConsoleGroundTruth.SampleForTests();

                Assert.AreEqual(restrictedFlags, GetConsoleFlags(), "Seeding must leave the console's level toggles as it found them");
                Assert.AreEqual(restrictedFilter, GetFilteringText(), "Seeding must leave the console's search text as it found it");
            }
            finally
            {
                SetFilteringText(originalFilter);
                SetConsoleFlags(originalFlags);
            }
        }

        [Test]
        public void Seed_DoesNotDuplicateEntriesAlreadyCaptured()
        {
            if (!EditorConsoleEntries.Available)
                Assert.Ignore("UnityEditor.LogEntries is not reachable on this Editor version");

            var marker = NewMarker("dedupe");
            EmitError(marker);
            Assert.AreEqual(1, CountCaptures(marker), "The live callback captured it once");

            EditorConsoleGroundTruth.ResetForTests();
            EditorConsoleGroundTruth.SampleForTests();

            Assert.AreEqual(1, CountCaptures(marker), "Seeding must not re-add an entry the buffer already holds");
        }

        [Test]
        public void Seed_RecoversEntriesLoggedWhileCaptureWasDead()
        {
            if (!EditorConsoleEntries.Available)
                Assert.Ignore("UnityEditor.LogEntries is not reachable on this Editor version");

            // The repair path, and the shape of the reported symptom: the Editor console holds an
            // error the buffer never received, because capture was not listening when it was logged.
            // Editor compile errors are the real case — they are sticky, so they stand in the console
            // long after the callback missed them.
            StripSubscription();

            var marker = NewMarker("missed");
            EmitError(marker);
            Assert.AreEqual(0, CountCaptures(marker),
                "Precondition: the dead subscription must have missed it");

            ConsoleLogCapture.EnsureCapturing();
            EditorConsoleGroundTruth.ResetForTests();
            EditorConsoleGroundTruth.SampleForTests();

            Assert.AreEqual(1, CountCaptures(marker),
                "Seeding must recover the entry the console holds and the buffer missed");

            var seeded = ConsoleLogCapture.Buffer.Query(-1, 0, ConsoleLogBuffer.SeverityLog)
                .Entries.First(e => e.Message != null && e.Message.Contains(marker));
            Assert.IsTrue(seeded.Seeded,
                "A recovered entry must be marked, since its timestamp is the read time");
        }

        [Test]
        public void Seed_RecoversARepeatedEntryTheBufferMissed()
        {
            if (!EditorConsoleEntries.Available)
                Assert.Ignore("UnityEditor.LogEntries is not reachable on this Editor version");

            // Console messages repeat by design, so matching on presence alone is not enough: the
            // copy the buffer already holds would cancel out the identical copy it missed, and that
            // one would never be recovered.
            var marker = NewMarker("repeated");
            EmitError(marker);
            Assert.AreEqual(1, CountCaptures(marker), "The live callback captured the first one");

            StripSubscription();
            EmitError(marker);
            Assert.AreEqual(1, CountCaptures(marker),
                "Precondition: the second, identical entry was missed while capture was dead");

            ConsoleLogCapture.EnsureCapturing();
            EditorConsoleGroundTruth.ResetForTests();
            EditorConsoleGroundTruth.SampleForTests();

            Assert.AreEqual(2, CountCaptures(marker),
                "Both occurrences must be held after seeding, not just one");
        }

        [Test]
        public void Seed_RecoversEveryOccurrenceOfAPreCaptureRepeat()
        {
            if (!EditorConsoleEntries.Available)
                Assert.Ignore("UnityEditor.LogEntries is not reachable on this Editor version");

            // A fresh seed with nothing held: all three identical rows have to come across.
            StripSubscription();
            var marker = NewMarker("triple");
            EmitError(marker);
            EmitError(marker);
            EmitError(marker);
            Assert.AreEqual(0, CountCaptures(marker), "Precondition: capture was dead for all three");

            ConsoleLogCapture.EnsureCapturing();
            EditorConsoleGroundTruth.ResetForTests();
            EditorConsoleGroundTruth.SampleForTests();

            Assert.AreEqual(3, CountCaptures(marker), "Every occurrence must be recovered");
        }

        [Test]
        public void Watchdog_ReArmsCaptureWhenConsoleMovedAndBufferDidNot()
        {
            StripSubscription();

            // Baseline, then a log the dead subscription cannot deliver: the console's count moves
            // while the buffer's cursor stands still, which is the only symptom available.
            EditorConsoleGroundTruth.SampleForTests();
            EmitError(NewMarker("gap"));
            EditorConsoleGroundTruth.SampleForTests();
            EditorConsoleGroundTruth.SampleForTests();

            var marker = NewMarker("recovered");
            EmitError(marker);
            Assert.AreEqual(1, CountCaptures(marker), "The watchdog must have re-armed capture");
        }

        [Test]
        public void EditorConsoleEntries_ReadsRecentEntries()
        {
            if (!EditorConsoleEntries.Available)
                Assert.Ignore("UnityEditor.LogEntries is not reachable on this Editor version");

            var marker = NewMarker("read");
            EmitError(marker);

            var entries = new List<EditorConsoleEntries.Entry>();
            Assert.IsTrue(EditorConsoleEntries.TryReadRecent(200, entries));
            Assert.IsTrue(entries.Any(e => e.Message != null && e.Message.Contains(marker)),
                "The entry just logged must be readable from the Editor console store");
        }

        // ConsoleMode.kLogLevelError, as mirrored by the internal ConsoleWindow.ConsoleFlags.
        const int LogLevelError = 1 << 9;

        static readonly Type k_LogEntries = Type.GetType("UnityEditor.LogEntries,UnityEditor");

        static int GetConsoleFlags() => (int)Property("consoleFlags").GetGetMethod(true).Invoke(null, null);

        static void SetConsoleFlags(int flags) => Property("consoleFlags").GetSetMethod(true).Invoke(null, new object[] { flags });

        static string GetFilteringText() => (string)Method("GetFilteringText").Invoke(null, null) ?? string.Empty;

        static void SetFilteringText(string text) => Method("SetFilteringText").Invoke(null, new object[] { text });

        static PropertyInfo Property(string name) =>
            k_LogEntries.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        static MethodInfo Method(string name) =>
            k_LogEntries.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        static string NewMarker(string tag) => $"groundtruth_{tag}_{Guid.NewGuid():N}";

        static void EmitError(string marker)
        {
            LogAssert.Expect(LogType.Error, new Regex(".*" + Regex.Escape(marker) + ".*"));
            Debug.LogError(marker);
        }

        static void EmitWarning(string marker)
        {
            LogAssert.Expect(LogType.Warning, new Regex(".*" + Regex.Escape(marker) + ".*"));
            Debug.LogWarning(marker);
        }

        static void StripSubscription()
        {
            var method = typeof(ConsoleLogCapture).GetMethod("OnLogMessage", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "ConsoleLogCapture.OnLogMessage must exist for this test to strip its subscription");
            var handler = (Application.LogCallback)Delegate.CreateDelegate(typeof(Application.LogCallback), method);
            Application.logMessageReceivedThreaded -= handler;
        }

        static int CountCaptures(string marker) =>
            ConsoleLogCapture.Buffer.Query(-1, 0, ConsoleLogBuffer.SeverityLog)
                .Entries.Count(e => e.Message != null && e.Message.Contains(marker));
    }
}
