using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Pipeline;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor.Commands.Capture;
using Unity.Pipeline.Models;

namespace Unity.Pipeline.Editor.Commands
{
    /// <summary>
    /// Server-side condition waiting (AUTHAPI-25): <c>wait_for</c> blocks until a runtime/editor
    /// member condition holds — evaluated per editor frame on the main thread — then optionally acts
    /// in that same frame (<c>on_met.capture</c>/<c>pause</c>). This replaces the dominant agent
    /// anti-pattern of client-side polling loops (read a value via eval, sleep in the client, repeat),
    /// which is slow and can miss short-lived states across client round-trips.
    ///
    /// The wait runs off the main thread (<c>MainThreadRequired = false</c>) and marshals each poll
    /// onto the main thread via the server dispatcher between sleeps, so the editor itself never
    /// freezes. BUT a synchronous wait holds the exec gate for its whole duration: <c>/api/exec</c>
    /// serializes commands, so every other command — including one whose effect the condition is
    /// waiting for — queues behind it until it resolves (a self-deadlock that only breaks at the
    /// timeout). Synchronous waits are therefore only for SHORT, self-contained conditions; pass
    /// <c>async=true</c> for anything else to get a <c>wait_id</c> back immediately and poll
    /// <c>wait_status</c> / cancel with <c>wait_cancel</c> while other commands keep executing.
    /// </summary>
    static class WaitForCommands
    {
        /// <summary>Poll interval clamp: below ~a frame is pointless, above 5s risks missing states and starves the timeout check.</summary>
        internal const double MinPollIntervalMs = 16;
        internal const double MaxPollIntervalMs = 5000;

        private static readonly string[] s_ValidOps =
            { "equals", "notEquals", "greaterThan", "lessThan", "contains", "changed" };

        /// <summary>
        /// The action used for <c>on_met.capture</c>, indirected so tests can observe that it runs in
        /// the same frame the condition holds without requiring a GPU (the real capture path needs
        /// one). Mirrors <c>RecompileCommand.s_FocusAction</c>.
        /// </summary>
        internal static Func<WaitCaptureInput, WaitCaptureResult> s_CaptureAction = DefaultCapture;

        /// <summary>Grace budget for the never-observed final look on a sync timeout (see the loop).</summary>
        private const int FinalLookGraceMs = 1000;

        [CliCommand("wait_for",
            "Wait server-side until a member condition holds, then optionally act in the same frame. Sync waits hold the exec queue (every other command waits behind them, including one the condition may depend on) — keep them short and self-contained; set async=true and poll wait_status for anything else.",
            MainThreadRequired = false, Tags = new[] { "wait" })]
        public static object WaitFor(
            [CliArg("condition", "Condition to wait for: { member, target?, findType?, op, value }. member is a dotted path (static, or instance under target/findType); op is equals|notEquals|greaterThan|lessThan|contains|changed.", Required = true)]
            WaitConditionInput condition = null,
            [CliArg("timeout_s", "Maximum seconds to wait (default 30, clamped to 0-600; must be finite).")]
            double timeoutS = 30,
            [CliArg("poll_interval_ms", "How often to re-check the condition, in milliseconds (default 100, clamped to 16-5000; must be finite).")]
            double pollIntervalMs = 100,
            [CliArg("on_met", "Follow-up run in the same editor frame the condition first holds: { capture: { view, source, save_path, width, height, include_image }, pause }.")]
            WaitOnMetInput onMet = null,
            [CliArg("return_history", "Include the observed value/timestamp samples (a ring buffer of the last 200, so the tail near the transition is kept).")]
            bool returnHistory = false,
            [CliArg("tolerate_missing", "Treat resolution failures (type/member/instance not found) as condition-not-met and retry until timeout, instead of failing fast. Enables waiting for something to exist (e.g. a spawned object via findType).")]
            bool tolerateMissing = false,
            [CliArg("async", "Return a wait_id immediately and evaluate in the background; poll wait_status and cancel with wait_cancel. Use this for long waits and whenever the condition depends on a command you still need to issue — a sync wait blocks it.")]
            bool async = false)
        {
            var validationError = ValidateCondition(condition);
            if (validationError != null)
                throw new ArgumentException(validationError);

            if (double.IsNaN(timeoutS) || double.IsInfinity(timeoutS))
                throw new ArgumentException("timeout_s must be a finite number of seconds (0-600).");
            if (double.IsNaN(pollIntervalMs) || double.IsInfinity(pollIntervalMs))
                throw new ArgumentException($"poll_interval_ms must be a finite number of milliseconds ({MinPollIntervalMs}-{MaxPollIntervalMs}).");

            var timeoutMs = Clamp(timeoutS, 0, 600) * 1000.0;
            var interval = Clamp(pollIntervalMs, MinPollIntervalMs, MaxPollIntervalMs);

            var job = new WaitJob
            {
                Id = Guid.NewGuid().ToString("N"),
                Condition = condition,
                Plan = new WaitConditionPlan(condition),
                OnMet = onMet,
                ReturnHistory = returnHistory,
                TolerateMissing = tolerateMissing,
                IsAsync = async,
                TimeoutMs = timeoutMs,
                PollIntervalMs = interval,
                StartedAt = DateTime.UtcNow
            };

            if (!WaitRegistry.TryRegister(job, out var registerError))
                throw new InvalidOperationException(registerError);

            if (async)
                return job.ToResult(includeWaitId: true);

            try
            {
                while (true)
                {
                    if (PipelineCancellation.IsCancellationRequested)
                    {
                        // A detached ("job": true) sync wait stays cancelable via /api/job/cancel.
                        job.MarkCanceled("Wait canceled by job cancellation request.");
                        break;
                    }

                    try
                    {
                        // Bound the per-poll marshal by the wait's own remaining budget (min 1 ms,
                        // capped at the dispatcher default) instead of inheriting the dispatcher's
                        // 60s default: a stalled main thread must not hold the exec gate for 60s
                        // when this wait's whole timeout_s is shorter.
                        var marshalBudgetMs = (int)Math.Max(1, Math.Min(timeoutMs - job.ElapsedMs, 60000));
                        RunOnMainThread(job.PollOnce, marshalBudgetMs);
                    }
                    catch (TimeoutException)
                    {
                        // A long main-thread stall (import, modal bake, ...) timed out the per-poll
                        // marshal after the dispatcher's default budget. That is a missed poll, not
                        // a failed wait — keep going until the wait's own budget expires.
                    }
                    catch (OperationCanceledException)
                    {
                        // The dispatcher is shutting down; never fall back to evaluating Unity
                        // state on this pool thread.
                        job.MarkInterrupted("Wait interrupted: the server is stopping.");
                        break;
                    }

                    if (job.IsTerminal)
                        break;

                    var remainingMs = timeoutMs - job.ElapsedMs;
                    if (remainingMs <= 0)
                    {
                        // Never resolve timedOut having observed NOTHING: if every earlier poll
                        // missed (main thread busy, tight budget), grant one final look with a
                        // small grace so the result carries a real value/framesObserved — parity
                        // with the Tick path's final look.
                        if (job.FramesObserved == 0)
                        {
                            try { RunOnMainThread(job.PollOnce, FinalLookGraceMs); }
                            catch (TimeoutException) { /* still stalled; report the bare timeout */ }
                            catch (OperationCanceledException) { job.MarkInterrupted("Wait interrupted: the server is stopping."); break; }
                        }
                        if (!job.IsTerminal)
                            job.MarkTimedOut();
                        break;
                    }

                    // Never sleep past the remaining budget (a large interval must not stretch a
                    // short timeout), and wake early if the surrounding job is canceled.
                    if (!SleepUnlessCanceled(Math.Min(interval, remainingMs), job))
                    {
                        job.MarkCanceled("Wait canceled by job cancellation request.");
                        break;
                    }
                }

                return job.ToResult(includeWaitId: false);
            }
            finally
            {
                // A synchronous wait is fully reported in its own response; do not retain it.
                WaitRegistry.Remove(job.Id);
            }
        }

        /// <summary>
        /// Sleep for <paramref name="ms"/> in short slices, returning false as soon as cooperative
        /// job cancellation is requested, and returning early (true) the moment
        /// <paramref name="job"/> turns terminal — a domain-reload/play-mode interruption landing
        /// mid-sleep must not wait out the rest of a poll interval (up to 5s) before the caller
        /// notices; the loop re-checks IsTerminal at its top.
        /// </summary>
        private static bool SleepUnlessCanceled(double ms, WaitJob job)
        {
            var remaining = ms;
            while (remaining > 0)
            {
                if (PipelineCancellation.IsCancellationRequested)
                    return false;
                if (job.IsTerminal)
                    return true;
                // Ceiling, never truncation: a fractional remaining (e.g. timeout_s=32.3 ends
                // as ...999.9996 ms) would truncate to a 0 slice that never decrements
                // `remaining` — an infinite spin that never releases the exec gate.
                var slice = (int)Math.Ceiling(Math.Min(100.0, remaining));
                Thread.Sleep(slice);
                remaining -= slice;
            }
            return !PipelineCancellation.IsCancellationRequested;
        }

        [CliCommand("wait_status",
            "Get the status/result of an async wait started with wait_for (async=true).",
            MainThreadRequired = false, Tags = new[] { "wait" })]
        public static object WaitStatus(
            [CliArg("wait_id", "The wait id returned by wait_for when async=true.", Required = true)]
            string waitId = null)
        {
            if (string.IsNullOrEmpty(waitId))
                throw new ArgumentException("wait_id is required.");

            if (!WaitRegistry.TryGet(waitId, out var job))
                return new WaitResult
                {
                    WaitId = waitId,
                    State = "not_found",
                    Error = "No wait with that id (async waits do not survive domain reloads; completed waits are retained up to a bounded count and pruned oldest-first when a new wait is submitted)."
                };

            return job.ToResult(includeWaitId: true);
        }

        [CliCommand("wait_cancel",
            "Cancel an async wait started with wait_for (async=true).",
            MainThreadRequired = false, Tags = new[] { "wait" })]
        public static object WaitCancel(
            [CliArg("wait_id", "The wait id to cancel.", Required = true)]
            string waitId = null)
        {
            if (string.IsNullOrEmpty(waitId))
                throw new ArgumentException("wait_id is required.");

            if (!WaitRegistry.Cancel(waitId, out var job))
                return new WaitResult
                {
                    WaitId = waitId,
                    State = "not_found",
                    Error = "No wait with that id."
                };

            return job.ToResult(includeWaitId: true);
        }

        /// <summary>Validate the condition's shape up front so bad parameters fail fast as a 400 (not a wait result).</summary>
        internal static string ValidateCondition(WaitConditionInput condition)
        {
            if (condition == null)
                return "condition is required.";
            if (string.IsNullOrWhiteSpace(condition.Member))
                return "condition.member is required.";
            if (condition.Target != null && !condition.Target.IsEmpty && !string.IsNullOrEmpty(condition.FindType))
                return "condition.target and condition.findType are mutually exclusive — set one or the other " +
                    "(the evaluator would otherwise silently prefer target and observe the wrong object).";

            var op = string.IsNullOrEmpty(condition.Op) ? "equals" : condition.Op.Trim();
            if (Array.IndexOf(s_ValidOps, op) < 0)
                return $"condition.op '{op}' is invalid. Use one of: {string.Join(", ", s_ValidOps)}.";

            // Value is a JToken precisely so an OMITTED key (null reference) is distinguishable
            // from an EXPLICIT JSON null (JTokenType.Null): omitting the operand is always an
            // error outside op=changed (a forgotten value must not silently become an
            // equals-null wait that can resolve immediately), while an explicit null is a
            // meaningful equality operand — "wait until destroyed/null".
            if (op != "changed" && condition.Value == null)
                return $"condition.value is required for op '{op}' (send an explicit null to compare against null).";
            if ((op == "greaterThan" || op == "lessThan" || op == "contains")
                && condition.Value != null && condition.Value.Type == JTokenType.Null)
                return $"condition.value cannot be null for op '{op}'.";

            return null;
        }

        /// <summary>
        /// Run <paramref name="action"/> on the editor main thread via the executing server's
        /// dispatcher (or directly when already on it / no server, e.g. a direct unit-test call),
        /// waiting at most <paramref name="timeoutMs"/> for the marshal — callers bound it by the
        /// wait's own remaining budget so a stalled main thread cannot hold the exec gate past
        /// this wait's timeout_s (the resulting <see cref="TimeoutException"/> is a missed poll).
        /// Throws <see cref="OperationCanceledException"/> when the dispatcher is shut down instead
        /// of falling back to evaluating Unity state on this pool thread — the caller resolves the
        /// wait as interrupted ("server stopping").
        /// </summary>
        private static void RunOnMainThread(Action action, int timeoutMs)
        {
            var server = BasePipelineServer.CurrentServer;
            if (server == null)
            {
                // Direct in-process invocation (unit tests) on the test's main thread.
                action();
                return;
            }

            if (server.Dispatcher.IsMainThread())
            {
                action();
                return;
            }

            if (!server.Dispatcher.IsInitialized)
                throw new OperationCanceledException("The server's main-thread dispatcher is shut down (server stopping).");

            try
            {
                server.Dispatcher.Invoke(action, timeoutMs);
            }
            catch (InvalidOperationException)
            {
                // Shutdown() can flip Dispatcher.IsInitialized to false in the window between the
                // check above and this call; Invoke then throws InvalidOperationException instead
                // of running. Same outcome as catching it above: report as interrupted, not an
                // unhandled fault escaping the command.
                throw new OperationCanceledException("The server's main-thread dispatcher is shut down (server stopping).");
            }
        }

        /// <summary>Reduce an observed value to a small, JSON-friendly form (enum names, primitives, or ToString()).</summary>
        internal static object Jsonify(object value)
        {
            if (value == null)
                return null;
            if (value is Enum e)
                return e.ToString();
            if (value is string || value is bool || value is decimal || value.GetType().IsPrimitive)
                return value;
            if (value is UnityEngine.Object unityObject)
                // A cached LastValue/InitialValue can hold a reference that was alive when read
                // but has since been destroyed (e.g. a retained terminal job's value re-serialized
                // by a later wait_status call) — `!= null` misses Unity's fake-null and .ToString()
                // on a destroyed native object throws instead of serializing.
                return unityObject ? unityObject.ToString() : null;
            return value.ToString();
        }

        internal static WaitCaptureResult RunCapture(WaitCaptureInput capture) => s_CaptureAction(capture);

        /// <summary>Test hook: restore the real capture action after a test swaps in a stub.</summary>
        internal static void ResetCaptureActionForTests() => s_CaptureAction = DefaultCapture;

        private static WaitCaptureResult DefaultCapture(WaitCaptureInput capture)
        {
            var view = (capture.View ?? "game").Trim().ToLowerInvariant();
            if (view != "game" && view != "scene")
                throw new ArgumentException(
                    $"Unknown view '{capture.View}' for on_met.capture. Use 'game' or 'scene'.");
            var width = capture.Width <= 0 ? 1280 : capture.Width;
            var height = capture.Height <= 0 ? 720 : capture.Height;
            var source = string.IsNullOrWhiteSpace(capture.Source) ? "camera" : capture.Source.Trim().ToLowerInvariant();
            if (source != "camera" && source != "screen")
                throw new ArgumentException(
                    $"Unknown source '{capture.Source}' for on_met.capture. Use 'camera' or 'screen'.");

            CaptureResult result = view == "scene"
                ? CaptureCommands.CaptureSceneView(width, height, capture.SavePath, capture.IncludeImage, 0)
                : CaptureCommands.CaptureGameView(width, height, null, capture.SavePath, capture.IncludeImage, 0, source);

            return new WaitCaptureResult
            {
                SavedPath = result.SavedPath,
                Base64 = result.Base64,
                Bytes = result.Bytes,
                Source = result.Source,
                Width = result.Width,
                Height = result.Height
            };
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>The condition <c>wait_for</c> polls: which member to read, on which object, and how to compare it.</summary>
    class WaitConditionInput : IStructuredCommandInput
    {
        [CliArg("member", "Dotted member path. With no target/findType it is a fully-qualified static path (e.g. \"UnityEditor.EditorApplication.isPlaying\"); with target/findType it walks instance members (e.g. \"State\" or \"Health.CurrentHealth\"). Read-only members only.", Required = true)]
        public string Member { get; set; }

        [CliArg("target", "Object handle (instanceId / hierarchyPath / guid / path / globalId) to read instance members from. Omit for a static member path or to use findType.")]
        public ObjectRef Target { get; set; }

        [CliArg("findType", "Fully-qualified UnityEngine.Object type name; the member walks from its first loaded instance. Alternative to target.")]
        public string FindType { get; set; }

        [CliArg("op", "Comparison: equals (default) | notEquals | greaterThan | lessThan | contains (strings) | changed (met on first change from the initially observed value; needs no value). changed is for value-like members (primitives/enums/strings); content mutations inside a reference-typed member are not detected.")]
        public string Op { get; set; } = "equals";

        [CliArg("value", "Comparison operand, coerced to the member's type (primitive, enum name, or string). Required for every op except 'changed'. equals/notEquals accept an EXPLICIT null (e.g. wait-until-destroyed: a destroyed leaf value observes as null); omitting the key entirely is rejected.")]
        public JToken Value { get; set; }
    }

    /// <summary>Follow-up actions run in the same editor frame the condition first holds.</summary>
    class WaitOnMetInput : IStructuredCommandInput
    {
        [CliArg("capture", "Capture the game or scene view the moment the condition holds (reuses the capture command internals).")]
        public WaitCaptureInput Capture { get; set; }

        [CliArg("pause", "Pause play mode on the frame the condition holds (no-op outside play mode).")]
        public bool Pause { get; set; }
    }

    /// <summary>Capture options for <c>on_met.capture</c> (mirrors the capture commands' parameters).</summary>
    class WaitCaptureInput : IStructuredCommandInput
    {
        [CliArg("view", "Which view to capture: game (default) or scene.")]
        public string View { get; set; } = "game";

        [CliArg("source", "Game view only: 'camera' (default) renders a camera and misses Screen Space - Overlay UI; 'screen' captures the composited game view incl. overlay canvases (HUDs), Play Mode only.")]
        public string Source { get; set; }

        [CliArg("save_path", "Project-relative path to write the PNG (e.g. Screenshots/victory.png).")]
        public string SavePath { get; set; }

        [CliArg("width", "Output width in px (default 1280).")]
        public int Width { get; set; } = 1280;

        [CliArg("height", "Output height in px (default 720).")]
        public int Height { get; set; } = 720;

        [CliArg("include_image", "Also return the PNG inline as base64 (default false: path-only when save_path is set).")]
        public bool IncludeImage { get; set; }
    }

    /// <summary>Result of a <c>wait_for</c> (sync response, or a snapshot from <c>wait_status</c>/<c>wait_cancel</c>).</summary>
    [Serializable]
    class WaitResult
    {
        /// <summary>Async wait id (present for async submissions and status/cancel snapshots).</summary>
        [JsonProperty("waitId", NullValueHandling = NullValueHandling.Ignore)]
        public string WaitId { get; set; }

        /// <summary>pending | met | timedOut | interrupted | canceled | failed | not_found.</summary>
        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("met")]
        public bool Met { get; set; }

        [JsonProperty("timedOut")]
        public bool TimedOut { get; set; }

        [JsonProperty("interrupted", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Interrupted { get; set; }

        [JsonProperty("member", NullValueHandling = NullValueHandling.Ignore)]
        public string Member { get; set; }

        [JsonProperty("op", NullValueHandling = NullValueHandling.Ignore)]
        public string Op { get; set; }

        /// <summary>The most recently observed value (the value at the moment the wait resolved).</summary>
        [JsonProperty("value")]
        public object Value { get; set; }

        /// <summary>The first observed value (baseline for op=changed).</summary>
        [JsonProperty("initialValue")]
        public object InitialValue { get; set; }

        [JsonProperty("elapsedMs")]
        public long ElapsedMs { get; set; }

        [JsonProperty("framesObserved")]
        public int FramesObserved { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; set; }

        /// <summary>Set when the condition HELD but the on_met follow-up (capture/pause) threw —
        /// the wait is still met; check this before trusting <c>capture</c>.</summary>
        [JsonProperty("onMetError", NullValueHandling = NullValueHandling.Ignore)]
        public string OnMetError { get; set; }

        /// <summary>Non-fatal resolution note (e.g. an ambiguous findType match bound deterministically).</summary>
        [JsonProperty("note", NullValueHandling = NullValueHandling.Ignore)]
        public string Note { get; set; }

        [JsonProperty("capture", NullValueHandling = NullValueHandling.Ignore)]
        public WaitCaptureResult Capture { get; set; }

        [JsonProperty("history", NullValueHandling = NullValueHandling.Ignore)]
        public List<WaitHistorySample> History { get; set; }
    }

    /// <summary>The PNG written / rendered by an <c>on_met.capture</c>.</summary>
    [Serializable]
    class WaitCaptureResult
    {
        [JsonProperty("savedPath", NullValueHandling = NullValueHandling.Ignore)]
        public string SavedPath { get; set; }

        [JsonProperty("base64", NullValueHandling = NullValueHandling.Ignore)]
        public string Base64 { get; set; }

        [JsonProperty("bytes")]
        public int Bytes { get; set; }

        [JsonProperty("source", NullValueHandling = NullValueHandling.Ignore)]
        public string Source { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }

    /// <summary>One observed sample (value + elapsed time) recorded when return_history is set.</summary>
    [Serializable]
    class WaitHistorySample
    {
        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("elapsedMs")]
        public long ElapsedMs { get; set; }
    }
}
