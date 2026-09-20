using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Pipeline.Console;
using Unity.Pipeline.Models;
using UnityEngine;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Tests for <see cref="ConsoleLogBuffer"/> — the ring buffer behind the console command.
    /// Exercised directly (no Unity log capture) for determinism.
    /// </summary>
    class ConsoleLogBufferTests
    {
        static readonly DateTime k_Ts = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static ConsoleLogBuffer NewBuffer() => new ConsoleLogBuffer();

        [Test]
        public void Add_AssignsIncreasingSequenceNumbers()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Log, "a", null, k_Ts);
            buffer.Add(LogType.Log, "b", null, k_Ts);
            buffer.Add(LogType.Log, "c", null, k_Ts);

            var entries = buffer.Query(-1, 100, ConsoleLogBuffer.SeverityLog).Entries;

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, entries.Select(e => e.Message));
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, entries.Select(e => e.Seq));
        }

        [Test]
        public void Query_Snapshot_ReturnsOldestFirst()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Log, "first", null, k_Ts);
            buffer.Add(LogType.Log, "second", null, k_Ts);

            var response = buffer.Query(-1, 100, ConsoleLogBuffer.SeverityLog);

            Assert.AreEqual(2, response.Returned);
            Assert.AreEqual("first", response.Entries[0].Message);
            Assert.AreEqual("second", response.Entries[1].Message);
            Assert.AreEqual(2, response.Cursor);
            Assert.IsFalse(response.Dropped);
        }

        [Test]
        public void Query_LevelThreshold_IsMinimumSeverity()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Log, "log", null, k_Ts);
            buffer.Add(LogType.Warning, "warn", null, k_Ts);
            buffer.Add(LogType.Error, "error", null, k_Ts);
            buffer.Add(LogType.Exception, "exception", null, k_Ts);

            var warnAndUp = buffer.Query(-1, 100, ConsoleLogBuffer.SeverityWarn).Entries;
            CollectionAssert.AreEqual(new[] { "warn", "error", "exception" }, warnAndUp.Select(e => e.Message));

            var errorOnly = buffer.Query(-1, 100, ConsoleLogBuffer.SeverityError).Entries;
            CollectionAssert.AreEqual(new[] { "error", "exception" }, errorOnly.Select(e => e.Message));

            var all = buffer.Query(-1, 100, ConsoleLogBuffer.SeverityLog).Entries;
            Assert.AreEqual(4, all.Length);
        }

        [Test]
        public void Query_Tail_ReturnsMostRecentMatches()
        {
            var buffer = NewBuffer();
            for (int i = 0; i < 10; i++)
                buffer.Add(LogType.Log, $"m{i}", null, k_Ts);

            var entries = buffer.Query(-1, 3, ConsoleLogBuffer.SeverityLog).Entries;

            CollectionAssert.AreEqual(new[] { "m7", "m8", "m9" }, entries.Select(e => e.Message));
        }

        [Test]
        public void Query_Tail_AppliesAfterLevelFilter()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Error, "e0", null, k_Ts);
            buffer.Add(LogType.Log, "l0", null, k_Ts);
            buffer.Add(LogType.Error, "e1", null, k_Ts);
            buffer.Add(LogType.Log, "l1", null, k_Ts);
            buffer.Add(LogType.Error, "e2", null, k_Ts);

            // tail=2 over error-only matches => last two errors, not last two of all entries.
            var entries = buffer.Query(-1, 2, ConsoleLogBuffer.SeverityError).Entries;

            CollectionAssert.AreEqual(new[] { "e1", "e2" }, entries.Select(e => e.Message));
        }

        [Test]
        public void Query_Since_ReturnsOnlyNewerEntries()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Log, "a", null, k_Ts); // seq 1
            buffer.Add(LogType.Log, "b", null, k_Ts); // seq 2
            buffer.Add(LogType.Log, "c", null, k_Ts); // seq 3

            var response = buffer.Query(2, 100, ConsoleLogBuffer.SeverityLog);

            CollectionAssert.AreEqual(new[] { "c" }, response.Entries.Select(e => e.Message));
            Assert.AreEqual(3, response.Cursor);
            Assert.IsFalse(response.Dropped);
        }

        [Test]
        public void Query_SinceAtCursor_ReturnsEmptyButReportsCursor()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Log, "a", null, k_Ts);
            buffer.Add(LogType.Log, "b", null, k_Ts);

            var response = buffer.Query(2, 100, ConsoleLogBuffer.SeverityLog);

            Assert.AreEqual(0, response.Returned);
            Assert.AreEqual(2, response.Cursor, "Cursor must still advance so a follow client resumes correctly");
            Assert.IsFalse(response.Dropped);
        }

        [Test]
        public void Query_SinceBeyondCursor_ReportsResetAndReturnsTail()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Log, "a", null, k_Ts);

            // long.MaxValue also pins the since+1 overflow the drop check would otherwise hit.
            var response = buffer.Query(long.MaxValue, 100, ConsoleLogBuffer.SeverityLog);

            CollectionAssert.AreEqual(new[] { "a" }, response.Entries.Select(e => e.Message),
                "A cursor past the counter must be refused and the tail returned, not silently filter everything out");
            Assert.IsTrue(response.Reset);
            Assert.IsTrue(response.Dropped, "Reset implies the caller has a gap it cannot reason about");
        }

        [Test]
        public void Query_CursorReflectsLatestEvenWhenFilteredOut()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Error, "boom", null, k_Ts); // seq 1
            buffer.Add(LogType.Log, "noise", null, k_Ts);  // seq 2, filtered out by error level

            var response = buffer.Query(1, 100, ConsoleLogBuffer.SeverityError);

            Assert.AreEqual(0, response.Returned, "The only new entry is below the level threshold");
            Assert.AreEqual(2, response.Cursor, "Cursor advances past the filtered entry to avoid re-scanning it");
        }

        [Test]
        public void Eviction_DropsOldestAndFlagsDropped()
        {
            var buffer = NewBuffer();
            int total = ConsoleLogBuffer.Capacity + 50;
            for (int i = 0; i < total; i++)
                buffer.Add(LogType.Log, $"m{i}", null, k_Ts);

            Assert.AreEqual(ConsoleLogBuffer.Capacity, buffer.Count, "Buffer should be capped at Capacity");

            // since=1 points before the evicted window, so the consumer missed entries.
            var response = buffer.Query(1, ConsoleLogBuffer.Capacity, ConsoleLogBuffer.SeverityLog);
            Assert.IsTrue(response.Dropped, "Querying from an evicted cursor should set Dropped");

            // The most recent entry is always present.
            var latest = buffer.Query(-1, 1, ConsoleLogBuffer.SeverityLog).Entries.Single();
            Assert.AreEqual($"m{total - 1}", latest.Message);
        }

        [Test]
        public void Dropped_FalseWhenSinceWithinRetainedWindow()
        {
            var buffer = NewBuffer();
            for (int i = 0; i < 10; i++)
                buffer.Add(LogType.Log, $"m{i}", null, k_Ts);

            var response = buffer.Query(5, 100, ConsoleLogBuffer.SeverityLog);
            Assert.IsFalse(response.Dropped);
        }

        [Test]
        public void SaveAndLoad_RoundTripsEntriesAndCursor()
        {
            var path = Path.Combine("Temp", $"pipeline_console_test_{Guid.NewGuid():N}.json");
            try
            {
                var original = NewBuffer();
                original.Add(LogType.Log, "log", null, k_Ts);
                original.Add(LogType.Warning, "warn", "trace", k_Ts);
                original.Add(LogType.Error, "error", null, k_Ts);
                original.Save(path);

                var restored = NewBuffer();
                Assert.IsTrue(restored.Load(path));

                var entries = restored.Query(-1, 100, ConsoleLogBuffer.SeverityLog).Entries;
                CollectionAssert.AreEqual(new[] { "log", "warn", "error" }, entries.Select(e => e.Message));
                Assert.AreEqual(ConsoleLogBuffer.LevelWarn, entries[1].Level, "Severity must survive the round trip");
                Assert.AreEqual("trace", entries[1].StackTrace);

                // Sequence continues monotonically after a restore.
                restored.Add(LogType.Log, "after", null, k_Ts);
                Assert.AreEqual(4, restored.Query(3, 100, ConsoleLogBuffer.SeverityLog).Entries.Single().Seq);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Load_MissingFile_ReturnsFalseAndLeavesBufferEmpty()
        {
            var buffer = NewBuffer();
            Assert.IsFalse(buffer.Load(Path.Combine("Temp", $"does_not_exist_{Guid.NewGuid():N}.json")));
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void Load_ClampsRestoredEntriesToCapacityKeepingMostRecent()
        {
            var path = Path.Combine("Temp", $"pipeline_console_oversized_{Guid.NewGuid():N}.json");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Temp");

                var entries = Enumerable.Range(1, ConsoleLogBuffer.Capacity + 5)
                    .Select(i => new ConsoleLogEntry
                    {
                        Seq = i,
                        TimestampUtc = k_Ts,
                        Level = ConsoleLogBuffer.LevelLog,
                        Message = $"m{i}",
                        StackTrace = string.Empty
                    })
                    .ToArray();

                File.WriteAllText(path, JsonConvert.SerializeObject(new
                {
                    lastSeq = (long)(ConsoleLogBuffer.Capacity + 5),
                    entries
                }));

                var buffer = NewBuffer();
                Assert.IsTrue(buffer.Load(path));
                Assert.AreEqual(ConsoleLogBuffer.Capacity, buffer.Count);

                var restored = buffer.Query(-1, ConsoleLogBuffer.Capacity, ConsoleLogBuffer.SeverityLog).Entries;
                Assert.AreEqual("m6", restored.First().Message);
                Assert.AreEqual($"m{ConsoleLogBuffer.Capacity + 5}", restored.Last().Message);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Clear_EmptiesEntriesButKeepsSequenceMonotonic()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Log, "a", null, k_Ts);
            buffer.Add(LogType.Log, "b", null, k_Ts);

            buffer.Clear();
            Assert.AreEqual(0, buffer.Count);

            buffer.Add(LogType.Log, "c", null, k_Ts);
            Assert.AreEqual(3, buffer.LastSeq, "Sequence must not reset on Clear");
        }

        [Test]
        public void Add_IsThreadSafe()
        {
            var buffer = NewBuffer();
            const int threads = 8;
            const int perThread = 500;

            var tasks = Enumerable.Range(0, threads).Select(t => Task.Run(() =>
            {
                for (int i = 0; i < perThread; i++)
                    buffer.Add(LogType.Log, $"t{t}-{i}", null, k_Ts);
            })).ToArray();
            Task.WaitAll(tasks);

            Assert.AreEqual(threads * perThread, buffer.LastSeq, "Every add must get a unique sequence number");
            Assert.AreEqual(ConsoleLogBuffer.Capacity, buffer.Count);

            // All retained seqs are unique.
            var seqs = buffer.Query(-1, ConsoleLogBuffer.Capacity, ConsoleLogBuffer.SeverityLog)
                .Entries.Select(e => e.Seq).ToArray();
            Assert.AreEqual(seqs.Length, seqs.Distinct().Count(), "Sequence numbers must be unique");
        }

        [Test]
        public void Query_ForeignSession_ReportsResetAndReturnsTail()
        {
            var buffer = NewBuffer();
            buffer.SetSession("session-a");
            buffer.Add(LogType.Log, "a", null, k_Ts);
            buffer.Add(LogType.Log, "b", null, k_Ts);

            var response = buffer.Query(1, 100, ConsoleLogBuffer.SeverityLog, "session-b");

            CollectionAssert.AreEqual(new[] { "a", "b" }, response.Entries.Select(e => e.Message),
                "A cursor from another session names different entries, so it must be refused");
            Assert.IsTrue(response.Reset);
            Assert.AreEqual("session-a", response.Session);
        }

        [Test]
        public void Query_MatchingSession_HonorsCursor()
        {
            var buffer = NewBuffer();
            buffer.SetSession("session-a");
            buffer.Add(LogType.Log, "a", null, k_Ts);
            buffer.Add(LogType.Log, "b", null, k_Ts);

            var response = buffer.Query(1, 100, ConsoleLogBuffer.SeverityLog, "session-a");

            CollectionAssert.AreEqual(new[] { "b" }, response.Entries.Select(e => e.Message));
            Assert.IsFalse(response.Reset);
        }

        [Test]
        public void Query_ReportsRetainedCountsBeforeFilterAndTail()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Log, "l", null, k_Ts);
            buffer.Add(LogType.Warning, "w", null, k_Ts);
            buffer.Add(LogType.Error, "e0", null, k_Ts);
            buffer.Add(LogType.Exception, "e1", null, k_Ts);

            var response = buffer.Query(-1, 1, ConsoleLogBuffer.SeverityError);

            Assert.AreEqual(1, response.Returned, "tail and level still apply to the entries");
            Assert.AreEqual(2, response.Counts.Error, "Counts describe what the buffer holds, not what was returned");
            Assert.AreEqual(1, response.Counts.Warn);
            Assert.AreEqual(1, response.Counts.Log);
        }

        [Test]
        public void Stats_ReportsCountsAndCursorWithoutEntries()
        {
            var buffer = NewBuffer();
            buffer.SetSession("session-a");
            buffer.Add(LogType.Log, "l", null, k_Ts);
            buffer.Add(LogType.Error, "e", null, k_Ts);

            var stats = buffer.Stats();

            Assert.IsEmpty(stats.Entries);
            Assert.AreEqual(2, stats.Cursor);
            Assert.AreEqual("session-a", stats.Session);
            Assert.AreEqual(1, stats.Counts.Error);
            Assert.AreEqual(1, stats.Counts.Log);
        }

        [Test]
        public void Add_Seeded_MarksEntryAndKeepsLogType()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Exception, "threw", null, k_Ts, seeded: true);
            buffer.Add(LogType.Log, "live", null, k_Ts);

            var entries = buffer.Query(-1, 100, ConsoleLogBuffer.SeverityLog).Entries;

            Assert.IsTrue(entries[0].Seeded);
            Assert.AreEqual("Exception", entries[0].LogType, "The exact log type survives the severity collapse");
            Assert.AreEqual(ConsoleLogBuffer.LevelError, entries[0].Level);
            Assert.IsFalse(entries[1].Seeded);
        }

        [Test]
        public void Load_KeepsEntriesLoggedSinceSnapshot()
        {
            var path = Path.Combine("Temp", $"pipeline_console_merge_{Guid.NewGuid():N}.json");
            try
            {
                // The [NoAutoStaticsCleanup] case: the buffer survives the reload, so entries logged
                // after the snapshot are still in memory when the restore runs and must not be lost.
                var buffer = NewBuffer();
                buffer.Add(LogType.Log, "a", null, k_Ts);
                buffer.Add(LogType.Log, "b", null, k_Ts);
                buffer.Save(path);
                buffer.Add(LogType.Log, "c", null, k_Ts);
                buffer.Add(LogType.Log, "d", null, k_Ts);

                Assert.IsTrue(buffer.Load(path));

                var entries = buffer.Query(-1, 100, ConsoleLogBuffer.SeverityLog).Entries;
                CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, entries.Select(e => e.Message));
                Assert.AreEqual(4, buffer.LastSeq);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Load_NeverLowersLastSeq()
        {
            var path = Path.Combine("Temp", $"pipeline_console_stale_{Guid.NewGuid():N}.json");
            try
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(path, JsonConvert.SerializeObject(new
                {
                    lastSeq = 5L,
                    entries = Array.Empty<ConsoleLogEntry>()
                }));

                var buffer = NewBuffer();
                for (int i = 0; i < 100; i++)
                    buffer.Add(LogType.Log, $"m{i}", null, k_Ts);

                Assert.IsTrue(buffer.Load(path));
                Assert.AreEqual(100, buffer.LastSeq, "A stale snapshot must not rewind cursors already handed out");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void RaiseLastSeq_RaisesButNeverLowers()
        {
            var buffer = NewBuffer();
            buffer.Add(LogType.Log, "a", null, k_Ts);

            buffer.RaiseLastSeq(50);
            Assert.AreEqual(50, buffer.LastSeq);

            buffer.RaiseLastSeq(10);
            Assert.AreEqual(50, buffer.LastSeq);

            buffer.Add(LogType.Log, "b", null, k_Ts);
            Assert.AreEqual(51, buffer.Query(50, 100, ConsoleLogBuffer.SeverityLog).Entries.Single().Seq);
        }

        // Two shapes a domain reload takes: statics wiped, so the restore lands in a fresh buffer;
        // or statics kept ([NoAutoStaticsCleanup]), so the buffer that survives already holds the
        // entries logged after the snapshot was written. The second is where drift compounds.
        [TestCase(false, TestName = "RepeatedReloadCycles_LoseNoEntriesAndKeepSeqMonotonic(StaticsWiped)")]
        [TestCase(true, TestName = "RepeatedReloadCycles_LoseNoEntriesAndKeepSeqMonotonic(StaticsSurvive)")]
        public void RepeatedReloadCycles_LoseNoEntriesAndKeepSeqMonotonic(bool staticsSurvive)
        {
            // The agentic loop drives a domain reload per recompile, and the reported corruption was
            // drift that only showed after several. One cycle proves nothing; this walks twenty,
            // logging on both sides of every save.
            var path = Path.Combine("Temp", $"pipeline_console_cycles_{Guid.NewGuid():N}.json");
            try
            {
                var buffer = NewBuffer();
                var expected = new List<string>();
                var cursors = new List<long>();

                for (var cycle = 0; cycle < 20; cycle++)
                {
                    var before = $"c{cycle}-before";
                    buffer.Add(LogType.Log, before, null, k_Ts);
                    expected.Add(before);

                    buffer.Save(path);

                    // Logged after the snapshot: only a surviving buffer still holds this when the
                    // restore runs, and the restore must not discard it.
                    var between = $"c{cycle}-between";
                    buffer.Add(LogType.Log, between, null, k_Ts);
                    expected.Add(between);

                    if (!staticsSurvive)
                    {
                        // A wiped domain loses the entry logged after the save, exactly as the real
                        // one does — it was never persisted.
                        expected.Remove(between);
                        buffer = NewBuffer();
                    }

                    buffer.Load(path);

                    var after = $"c{cycle}-after";
                    buffer.Add(LogType.Log, after, null, k_Ts);
                    expected.Add(after);

                    cursors.Add(buffer.LastSeq);
                }

                var entries = buffer.Query(-1, 0, ConsoleLogBuffer.SeverityLog).Entries;
                CollectionAssert.AreEqual(expected, entries.Select(e => e.Message),
                    "No cycle may drop or duplicate an entry");

                var seqs = entries.Select(e => e.Seq).ToArray();
                CollectionAssert.AreEqual(seqs.OrderBy(x => x), seqs, "Sequence numbers must stay ordered");
                Assert.AreEqual(seqs.Length, seqs.Distinct().Count(), "Sequence numbers must stay unique");
                CollectionAssert.AreEqual(cursors.OrderBy(x => x), cursors,
                    "The cursor must never go backwards across a reload");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void EditorRestart_RefusesACursorFromTheSessionBefore()
        {
            // The likeliest real-world trigger: Unity deletes Temp/ at every editor launch, so the
            // snapshot is gone and the counter restarts at zero while a client still holds a cursor
            // from the session before. That cursor is far ahead of the new counter, so it used to
            // filter out every entry and read as "nothing new" indefinitely.
            var path = Path.Combine("Temp", $"pipeline_console_restart_{Guid.NewGuid():N}.json");
            try
            {
                var firstSession = NewBuffer();
                firstSession.SetSession("session-before-restart");
                for (var i = 0; i < 500; i++)
                    firstSession.Add(LogType.Log, $"old{i}", null, k_Ts);
                firstSession.Save(path);

                var clientCursor = firstSession.LastSeq;
                var clientSession = firstSession.Session;
                Assert.AreEqual(500, clientCursor);

                // The restart: Temp/ is wiped, so there is nothing to restore, and the new session
                // gets a new identifier and a counter starting from zero.
                File.Delete(path);
                var afterRestart = NewBuffer();
                afterRestart.SetSession("session-after-restart");
                Assert.IsFalse(afterRestart.Load(path), "The snapshot is gone with Temp/");
                Assert.AreEqual(0, afterRestart.LastSeq);

                afterRestart.Add(LogType.Error, "compile error after restart", null, k_Ts);

                var response = afterRestart.Query(clientCursor, 100, ConsoleLogBuffer.SeverityLog, clientSession);

                CollectionAssert.AreEqual(new[] { "compile error after restart" },
                    response.Entries.Select(e => e.Message),
                    "The stale cursor must be refused and the entry served, not silently filtered out");
                Assert.IsTrue(response.Reset);
                Assert.IsTrue(response.Dropped);
                Assert.AreEqual("session-after-restart", response.Session,
                    "The response must hand back the session the client should adopt");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void SeverityFromLevelName_ParsesNamesAndAliases()
        {
            Assert.AreEqual(ConsoleLogBuffer.SeverityError, ConsoleLogBuffer.SeverityFromLevelName("error"));
            Assert.AreEqual(ConsoleLogBuffer.SeverityError, ConsoleLogBuffer.SeverityFromLevelName("ERR"));
            Assert.AreEqual(ConsoleLogBuffer.SeverityWarn, ConsoleLogBuffer.SeverityFromLevelName("warn"));
            Assert.AreEqual(ConsoleLogBuffer.SeverityWarn, ConsoleLogBuffer.SeverityFromLevelName("Warning"));
            Assert.AreEqual(ConsoleLogBuffer.SeverityLog, ConsoleLogBuffer.SeverityFromLevelName("log"));
            Assert.AreEqual(ConsoleLogBuffer.SeverityLog, ConsoleLogBuffer.SeverityFromLevelName("nonsense"));
            Assert.AreEqual(ConsoleLogBuffer.SeverityLog, ConsoleLogBuffer.SeverityFromLevelName(null));
        }
    }
}
