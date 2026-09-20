using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Pipeline.Console;
using Unity.Pipeline.Editor.Commands;
using Unity.Pipeline.Models;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Tests that the recompile status never claims a clean state while compile errors stand. The
    /// status file lives under Temp/, which Unity deletes at every editor launch, and a failed
    /// editor compile suppresses the domain reload that would rewrite it — so the file alone is not
    /// enough and the native compile-failure flag has to win.
    ///
    /// No test triggers a real compile: the flag is read through an injected probe, and the ground
    /// truth the status overlays is set directly.
    /// </summary>
    class RecompileStatusTests
    {
        const string StatusFile = "Temp/pipeline_recompile_status.json";

        ConsoleGroundTruth m_PreviousTruth;
        Func<bool> m_PreviousProbe;
        string m_PreviousStatus;

        [SetUp]
        public void SetUp()
        {
            m_PreviousTruth = ConsoleLogCapture.GroundTruth;
            m_PreviousProbe = RecompileCommand.s_CompilationFailedProbe;
            m_PreviousStatus = File.Exists(StatusFile) ? File.ReadAllText(StatusFile) : null;
        }

        [TearDown]
        public void TearDown()
        {
            ConsoleLogCapture.GroundTruth = m_PreviousTruth;
            RecompileCommand.s_CompilationFailedProbe = m_PreviousProbe;

            if (m_PreviousStatus != null)
                File.WriteAllText(StatusFile, m_PreviousStatus);
            else if (File.Exists(StatusFile))
                File.Delete(StatusFile);
        }

        [Test]
        public void RecompileStatus_ReportsFailedWhenGroundTruthSaysCompilationFailed()
        {
            // "idle" is what the file reads after an editor restart, since Temp/ is deleted.
            WriteStatus("idle", false);
            ConsoleLogCapture.GroundTruth = Sample(compilationFailed: true);

            var status = JObject.Parse(RecompileCommand.RecompileStatus());

            Assert.IsTrue(status["failed"].ToObject<bool>(), "The native flag must win over a stale status file");
            Assert.IsTrue(status["compilationFailed"].ToObject<bool>());
            Assert.AreEqual("completed", status["status"].ToString());
        }

        [Test]
        public void RecompileStatus_DoesNotOverrideCompletedWithTheNativeFlag()
        {
            // The native flag still reads true for a moment after a fixing compile finishes; it
            // clears with the domain reload that follows. A "completed" status lists what the compile
            // that just ran actually produced, so it wins.
            WriteStatus("completed", false);
            ConsoleLogCapture.GroundTruth = Sample(compilationFailed: true);

            var status = JObject.Parse(RecompileCommand.RecompileStatus());

            Assert.IsFalse(status["failed"].ToObject<bool>(),
                "A build that has just been fixed must not be reported as broken");
            Assert.AreEqual("completed", status["status"].ToString());
            Assert.IsTrue(status["compilationFailed"].ToObject<bool>(),
                "The flag is still reported, so a client can see the disagreement");
        }

        [Test]
        public void RecompileStatus_LeavesCleanStatusAloneWhenCompilationSucceeded()
        {
            WriteStatus("up_to_date", false);
            ConsoleLogCapture.GroundTruth = Sample(compilationFailed: false);

            var status = JObject.Parse(RecompileCommand.RecompileStatus());

            Assert.IsFalse(status["failed"].ToObject<bool>());
            Assert.IsFalse(status["compilationFailed"].ToObject<bool>());
            Assert.AreEqual("up_to_date", status["status"].ToString());
        }

        [Test]
        public void Recompile_DoesNotOverwriteFailedWithUpToDate()
        {
            // The reported symptom: a repeat recompile with nothing changed compiles nothing, and
            // "up_to_date" used to erase the failure that was still true.
            WriteStatus("completed", true, "Assets/Broken.cs(1,1): error CS1002: ; expected");
            RecompileCommand.s_CompilationFailedProbe = () => true;

            var result = RecompileCommand.Recompile();

            Assert.AreEqual("failed", result.GetType().GetProperty("status").GetValue(result));

            var status = JObject.Parse(RecompileCommand.RecompileStatus());
            Assert.IsTrue(status["failed"].ToObject<bool>(), "The recorded failure must survive a no-op recompile");
            Assert.That(status["errors"].ToObject<string[]>(), Has.Exactly(1).Contains("CS1002"),
                "The errors from the failed compile must survive too");
        }

        [Test]
        public void RecompileStatus_IgnoresStaleGroundTruth()
        {
            // Sampling stops while the Editor is blocked, so an old sample says nothing about now.
            WriteStatus("idle", false);
            ConsoleLogCapture.GroundTruth = new ConsoleGroundTruth
            {
                CompilationFailed = true,
                SampledUtc = DateTime.UtcNow.AddSeconds(-30)
            };

            var status = JObject.Parse(RecompileCommand.RecompileStatus());

            Assert.IsFalse(status["failed"].ToObject<bool>(),
                "A stale sample must not report a build that has just been fixed as broken");
            Assert.IsFalse(status["compilationFailed"].ToObject<bool>());
        }

        static ConsoleGroundTruth Sample(bool compilationFailed) => new ConsoleGroundTruth
        {
            CompilationFailed = compilationFailed,
            SampledUtc = DateTime.UtcNow
        };

        static void WriteStatus(string status, bool failed, params string[] errors)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(StatusFile, new JObject
            {
                ["status"] = status,
                ["failed"] = failed,
                ["errors"] = new JArray(errors)
            }.ToString());
        }
    }
}
