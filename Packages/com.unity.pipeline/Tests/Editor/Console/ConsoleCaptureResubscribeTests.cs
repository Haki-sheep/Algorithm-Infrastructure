using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Pipeline.Console;
using Unity.Pipeline.Runtime.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Pipeline.Tests.Editor.Console
{
    class ConsoleCaptureResubscribeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();

            RearmConsoleLogCapture();
        }

        [UnityTest]
        public IEnumerator ConsoleCapture_SurvivesPlayModeExit()
        {
            yield return new EnterPlayMode();
            yield return new ExitPlayMode();

            var marker = NewMarker("survives_exit");
            EmitError(marker);

            var consoleResponse = ConsoleCommand.GetConsole(level: "error");
            Assert.IsTrue(consoleResponse.Entries.Any(e => e.Message != null && e.Message.Contains(marker)),
                "console must still capture entries logged after exiting play mode");
        }

        [Test]
        public void CaptureSurvivesLostSubscription_WhenRearmed()
        {
            StripConsoleLogCaptureSubscription();

            var deadMarker = NewMarker("dead");
            EmitError(deadMarker);
            Assert.AreEqual(0, CountCaptures(deadMarker),
                "Stripping the subscription should stop capture (sanity check on the strip step)");

            RearmConsoleLogCapture();

            var liveMarker = NewMarker("live");
            EmitError(liveMarker);
            Assert.AreEqual(1, CountCaptures(liveMarker),
                "Re-arming the subscription must restore capture");

            RearmConsoleLogCapture();

            var idempotentMarker = NewMarker("idempotent");
            EmitError(idempotentMarker);
            Assert.AreEqual(1, CountCaptures(idempotentMarker),
                "Re-arming an already-live subscription must not double-record a single log");
        }

        private static string NewMarker(string tag) => $"consolecapture_{tag}_{Guid.NewGuid():N}";

        private static void EmitError(string marker)
        {
            LogAssert.Expect(LogType.Error, new Regex(".*" + Regex.Escape(marker) + ".*"));
            Debug.LogError(marker);
        }

        private static void StripConsoleLogCaptureSubscription()
        {
            var method = typeof(ConsoleLogCapture).GetMethod("OnLogMessage", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "ConsoleLogCapture.OnLogMessage must exist for this test to strip its subscription");
            var handler = (Application.LogCallback)Delegate.CreateDelegate(typeof(Application.LogCallback), method);
            Application.logMessageReceivedThreaded -= handler;
        }

        private static void RearmConsoleLogCapture() => ConsoleLogCapture.EnsureCapturing();

        private static int CountCaptures(string marker) =>
            ConsoleLogCapture.Buffer.Query(-1, 0, ConsoleLogBuffer.SeverityLog)
                .Entries.Count(e => e.Message != null && e.Message.Contains(marker));
    }
}
