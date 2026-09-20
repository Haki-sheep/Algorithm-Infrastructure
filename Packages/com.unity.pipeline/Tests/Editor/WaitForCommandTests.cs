using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Unity.Pipeline;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor;
using Unity.Pipeline.Editor.Commands;
using Unity.Pipeline.Models;
using UnityEditor;
using UnityEngine;

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Tests for the wait_for / wait_status / wait_cancel commands (AUTHAPI-25): server-side
    /// condition waiting that replaces client polling loops.
    ///
    /// The core cases are exercised by calling the command/evaluator directly on the test's main
    /// thread (deterministic, no HTTP/GPU dependency); a couple of ViaClient tests then confirm the
    /// full HTTP + off-main-thread marshaling path. Async waits are driven by pumping the registry's
    /// Tick() directly so they resolve without depending on the live editor update loop.
    ///
    /// NOTE: authored but not yet executed against a live editor (coordinated run pending).
    /// </summary>
    class WaitForCommandTests
    {
        private ProbeBehaviourAsset m_Probe;

        [SetUp]
        public void SetUp()
        {
            CommandRegistry.SetDiscovery(new TypeCacheCommandDiscovery());
            WaitRegistry.ResetForTests();
            WaitForProbe.Reset();
            WaitForCommands.s_CaptureAction = FakeCapture;
            s_FakeCaptureCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            WaitRegistry.ResetForTests();
            WaitForProbe.Reset();
            WaitForCommands.ResetCaptureActionForTests();
            if (m_Probe != null)
            {
                UnityEngine.Object.DestroyImmediate(m_Probe);
                m_Probe = null;
            }
        }

        private static int s_FakeCaptureCount;

        private static WaitCaptureResult FakeCapture(WaitCaptureInput input)
        {
            s_FakeCaptureCount++;
            return new WaitCaptureResult { SavedPath = "Assets/__fake_wait_capture.png", Source = input?.View ?? "game" };
        }

        // ----------------------------------------------------------------------------------------
        // Discovery / registration
        // ----------------------------------------------------------------------------------------

        [Test]
        public void WaitCommands_AreDiscovered_WithWaitTagAndRequiredCondition()
        {
            var commands = CommandRegistry.DiscoverCommands().ToList();

            var waitFor = commands.FirstOrDefault(c => c.Name == "wait_for");
            Assert.IsNotNull(waitFor, "wait_for should be discovered");
            CollectionAssert.Contains(waitFor.Tags, "wait", "wait_for should carry the 'wait' tag");
            Assert.IsFalse(waitFor.MainThreadRequired,
                "wait_for must not require the main thread (it marshals per-poll instead of holding it)");
            var condition = waitFor.Parameters.FirstOrDefault(p => p.Name == "condition");
            Assert.IsNotNull(condition, "wait_for should expose a 'condition' parameter");
            Assert.IsTrue(condition.Required, "'condition' must be required");
            Assert.IsNotNull(waitFor.Parameters.FirstOrDefault(p => p.Name == "tolerate_missing"),
                "wait_for should expose 'tolerate_missing'");

            Assert.IsNotNull(commands.FirstOrDefault(c => c.Name == "wait_status"), "wait_status should be discovered");
            Assert.IsNotNull(commands.FirstOrDefault(c => c.Name == "wait_cancel"), "wait_cancel should be discovered");
        }

        // ----------------------------------------------------------------------------------------
        // Sync: already true / becomes true / timeout
        // ----------------------------------------------------------------------------------------

        [Test]
        public void WaitFor_ConditionAlreadyTrue_ReturnsMetImmediately()
        {
            var result = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysTrue", "equals", true), timeoutS: 5, pollIntervalMs: 50);

            Assert.IsTrue(result.Met, "should be met immediately");
            Assert.IsFalse(result.TimedOut, "must not time out");
            Assert.AreEqual("met", result.State);
            Assert.GreaterOrEqual(result.FramesObserved, 1);
        }

        [Test]
        public void WaitFor_EnumConditionFlips_ReturnsWithinTwoPollIntervals()
        {
            // Latency contract: an enum-valued member flips at a known time; the wait must return
            // within flip + 2 * poll_interval + slack (CI-safe bound, well under the timeout).
            WaitForProbe.FlipAt = DateTime.UtcNow.AddMilliseconds(200);

            var result = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("TimedState", "equals", "Done"), timeoutS: 5, pollIntervalMs: 100);

            Assert.IsTrue(result.Met, "should meet the condition after the enum flips");
            Assert.IsFalse(result.TimedOut);
            Assert.AreEqual("Done", result.Value?.ToString());
            Assert.GreaterOrEqual(result.ElapsedMs, 150, "should not resolve before the flip time");
            Assert.LessOrEqual(result.ElapsedMs, 1500,
                "should return within flip (200ms) + 2 poll intervals (200ms) + generous CI slack");
        }

        [Test]
        public void WaitFor_TimeoutElapsesWithoutCondition_ReturnsTimedOutWithLastValue()
        {
            var result = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysFalse", "equals", true), timeoutS: 0.3, pollIntervalMs: 50);

            Assert.IsFalse(result.Met, "must not be met");
            Assert.IsTrue(result.TimedOut, "must report a timeout");
            Assert.AreEqual("timedOut", result.State);
            Assert.IsNotNull(result.Value, "the last observed value must be reported on timeout");
            Assert.GreaterOrEqual(result.FramesObserved, 1);
        }

        [Test]
        public void WaitFor_PollIntervalLargerThanTimeout_StillHonorsTimeout()
        {
            // The sleep is bounded by the remaining budget (and the interval clamp), so a huge
            // poll_interval_ms must not stretch a 1-second timeout.
            var result = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysFalse", "equals", true), timeoutS: 1, pollIntervalMs: 60000);

            Assert.IsTrue(result.TimedOut, "must time out");
            Assert.GreaterOrEqual(result.ElapsedMs, 900, "must wait roughly the requested second");
            Assert.Less(result.ElapsedMs, 2500, "a 60s poll interval must not stretch a 1s timeout");
        }

        // ----------------------------------------------------------------------------------------
        // Invalid / missing parameters
        // ----------------------------------------------------------------------------------------

        private static IEnumerable<TestCaseData> InvalidWaitForInputs()
        {
            yield return new TestCaseData((TestDelegate)(() => WaitForCommands.WaitFor(null)), null)
                .SetName("WaitFor_NullCondition_Throws");
            yield return new TestCaseData((TestDelegate)(() => WaitForCommands.WaitFor(
                new WaitConditionInput { Member = "", Op = "equals", Value = true })), null)
                .SetName("WaitFor_MissingMember_Throws");
            yield return new TestCaseData((TestDelegate)(() => WaitForCommands.WaitFor(
                new WaitConditionInput { Member = "X.Y", Op = "startsWith", Value = "a" })), null)
                .SetName("WaitFor_InvalidOperator_Throws");
            // equals/notEquals accept an explicit null operand (wait-until-destroyed); the
            // ordering/substring ops still require a value.
            yield return new TestCaseData((TestDelegate)(() => WaitForCommands.WaitFor(
                new WaitConditionInput { Member = "X.Y", Op = "greaterThan", Value = null })), null)
                .SetName("WaitFor_MissingValueForComparisonOp_Throws");
            // Omitting the operand entirely must be rejected even for equals — only an
            // EXPLICIT JSON null is a valid equality operand (wait-until-destroyed).
            yield return new TestCaseData((TestDelegate)(() => WaitForCommands.WaitFor(
                new WaitConditionInput { Member = "X.Y", Op = "equals" })), "required")
                .SetName("WaitFor_OmittedValueForEquals_Throws");
            yield return new TestCaseData((TestDelegate)(() => WaitForCommands.WaitFor(
                StaticCondition("AlwaysTrue", "equals", true), timeoutS: double.NaN)), "timeout_s")
                .SetName("WaitFor_NaNTimeout_Throws");
            yield return new TestCaseData((TestDelegate)(() => WaitForCommands.WaitFor(
                StaticCondition("AlwaysTrue", "equals", true), pollIntervalMs: double.NaN)), "poll_interval_ms")
                .SetName("WaitFor_NaNPollInterval_Throws");
            yield return new TestCaseData((TestDelegate)(() => WaitForCommands.WaitFor(
                StaticCondition("AlwaysTrue", "equals", true), pollIntervalMs: double.PositiveInfinity)), "poll_interval_ms")
                .SetName("WaitFor_InfinitePollInterval_Throws");
        }

        [TestCaseSource(nameof(InvalidWaitForInputs))]
        public void WaitFor_InvalidInput_Throws(TestDelegate call, string messageContains)
        {
            var ex = Assert.Throws<ArgumentException>(call);
            if (messageContains != null)
                StringAssert.Contains(messageContains, ex.Message);
        }

        // ----------------------------------------------------------------------------------------
        // Operator coverage (evaluator-level, deterministic)
        // ----------------------------------------------------------------------------------------

        [TestCase("equals", ProbeState.Done, "Done", typeof(ProbeState), true, TestName = "Evaluate_Equals_OnEnum_True")]
        [TestCase("equals", ProbeState.Idle, "Done", typeof(ProbeState), false, TestName = "Evaluate_Equals_OnEnum_False")]
        [TestCase("equals", 5, 5, typeof(int), true, TestName = "Evaluate_Equals_OnInt")]
        [TestCase("notEquals", 5, 6, typeof(int), true, TestName = "Evaluate_NotEquals_OnInt")]
        [TestCase("equals", "hi", "hi", typeof(string), true, TestName = "Evaluate_Equals_OnString")]
        [TestCase("contains", "hello", "ell", typeof(string), true, TestName = "Evaluate_Contains_OnString_True")]
        [TestCase("contains", "hello", "xyz", typeof(string), false, TestName = "Evaluate_Contains_OnString_False")]
        [TestCase("greaterThan", 0.75f, 0.5, typeof(float), true, TestName = "Evaluate_GreaterThan_OnFloat_True")]
        [TestCase("greaterThan", 0.25f, 0.5, typeof(float), false, TestName = "Evaluate_GreaterThan_OnFloat_False")]
        [TestCase("lessThan", 0.25f, 0.5, typeof(float), true, TestName = "Evaluate_LessThan_OnFloat")]
        public void Evaluate_Operator_ProducesExpected(string op, object current, object operand, Type memberType, bool expected)
        {
            Assert.AreEqual(expected, WaitConditionEvaluator.Evaluate(op, current, operand, memberType, null, false, out var err));
            Assert.IsNull(err);
        }

        [Test]
        public void Evaluate_Equals_FloatAgainstDoubleOperand_ComparesAtFloatPrecision()
        {
            // With an object-typed member the operand stays a double (0.9), while the observed
            // value is a float (0.9f). Widening the float to double manufactures noise digits, so
            // a double-precision compare never matches — the comparison must drop to float precision.
            Assert.IsTrue(WaitConditionEvaluator.Evaluate("equals", 0.9f, (object)0.9d, typeof(object), null, false, out var err),
                "0.9f must equal the operand 0.9 at float precision");
            Assert.IsNull(err);
            Assert.IsFalse(WaitConditionEvaluator.Evaluate("equals", 0.8f, (object)0.9d, typeof(object), null, false, out _));
        }

        [Test]
        public void WaitFor_Equals_OnFloatMember_PointNine_Matches()
        {
            WaitForProbe.Ratio = 0.9f;

            var result = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("Ratio", "equals", 0.9), timeoutS: 2, pollIntervalMs: 50);

            Assert.IsTrue(result.Met, "equals on a float member with value 0.9 must match");
            Assert.AreEqual("met", result.State);
        }

        [Test]
        public void Evaluate_Changed_MetOnFirstChangeFromInitial()
        {
            // First observation: no baseline yet -> not "changed".
            Assert.IsFalse(WaitConditionEvaluator.Evaluate("changed", false, null, typeof(bool), null, false, out _));
            // Later observation differs from the initial value -> changed.
            Assert.IsTrue(WaitConditionEvaluator.Evaluate("changed", true, null, typeof(bool), false, true, out _));
            // Same as initial -> not changed.
            Assert.IsFalse(WaitConditionEvaluator.Evaluate("changed", false, null, typeof(bool), false, true, out _));
        }

        [Test]
        public void Evaluate_ContainsOnNonString_ReturnsError()
        {
            Assert.IsFalse(WaitConditionEvaluator.Evaluate("contains", 5, "5", typeof(int), null, false, out var err));
            Assert.IsNotNull(err);
        }

        // ----------------------------------------------------------------------------------------
        // Member resolution
        // ----------------------------------------------------------------------------------------

        [Test]
        public void ReadMember_StaticPath_ResolvesValue()
        {
            WaitForProbe.Count = 7;
            var value = WaitConditionEvaluator.ReadMember(
                new WaitConditionInput { Member = "Unity.Pipeline.Tests.Editor.WaitForProbe.Count" }, out var type);

            Assert.AreEqual(7, value);
            Assert.AreEqual(typeof(int), type);
        }

        [Test]
        public void ReadMember_PlanCachesTypeResolution_AcrossPolls()
        {
            // Repeated reads through one plan must keep returning fresh values (the cached type +
            // memoized member chain must not freeze the observed value).
            var plan = new WaitConditionPlan(
                new WaitConditionInput { Member = "Unity.Pipeline.Tests.Editor.WaitForProbe.Count" });

            WaitForProbe.Count = 1;
            Assert.AreEqual(1, WaitConditionEvaluator.ReadMember(plan, out _));
            Assert.IsNotNull(plan.StaticType, "the resolved type must be cached on the plan");

            WaitForProbe.Count = 2;
            Assert.AreEqual(2, WaitConditionEvaluator.ReadMember(plan, out _),
                "a cached plan must still observe the live value");
        }

        [Test]
        public void ReadMember_WriteOnlyMember_IsRejected()
        {
            Assert.Throws<WaitEvaluationException>(() =>
                WaitConditionEvaluator.ReadMember(
                    new WaitConditionInput { Member = "Unity.Pipeline.Tests.Editor.WaitForProbe.WriteOnly" }, out _));
        }

        [Test]
        public void ReadMember_UnknownMember_IsRejected()
        {
            Assert.Throws<WaitEvaluationException>(() =>
                WaitConditionEvaluator.ReadMember(
                    new WaitConditionInput { Member = "Unity.Pipeline.Tests.Editor.WaitForProbe.DoesNotExist" }, out _));
        }

        [Test]
        public void ReadMember_ViaFindType_ResolvesInstanceMember()
        {
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();
            m_Probe.Ratio = 0.9f;

            var value = WaitConditionEvaluator.ReadMember(new WaitConditionInput
            {
                FindType = typeof(ProbeBehaviourAsset).FullName,
                Member = "Ratio"
            }, out var type);

            Assert.AreEqual(0.9f, (float)value, 0.0001f);
            Assert.AreEqual(typeof(float), type);
        }

        [Test]
        public void ReadMember_ViaTargetInstanceId_ResolvesInstanceMember()
        {
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();
            m_Probe.Label = "victory";

            var value = WaitConditionEvaluator.ReadMember(new WaitConditionInput
            {
                Target = new ObjectRef { InstanceId = PipelineUtils.GetObjectId(m_Probe) },
                Member = "Label"
            }, out var type);

            Assert.AreEqual("victory", value);
            Assert.AreEqual(typeof(string), type);
        }

        // ----------------------------------------------------------------------------------------
        // Review regressions: fractional budgets, structured getter failures, timeout parity
        // ----------------------------------------------------------------------------------------

        [Test]
        public void WaitFor_FractionalTimeout_TerminatesInsteadOfSpinning()
        {
            // A fractional timeout_s (non-integer milliseconds) used to truncate the sleep slice
            // to 0 once the remaining budget dropped below 1 ms — an infinite spin that never
            // released the exec gate. The wait must time out and return.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Member = "UnityEngine.Application.isPlaying",
                Op = "equals",
                Value = true
            }, timeoutS: 0.1234, pollIntervalMs: 50);
            sw.Stop();

            Assert.IsTrue(result.TimedOut, "the never-true condition must time out");
            Assert.Less(sw.ElapsedMilliseconds, 5000,
                "a ~123.4ms budget must terminate promptly, never spin on a 0ms slice");
        }

        [Test]
        public void WaitFor_DestroyedObjectInPath_FailsStructured()
        {
            // A destroyed UnityEngine.Object mid-path passes an object-typed null check (fake
            // null); the traversal must fail with a structured evaluation error, not a raw
            // MissingReferenceException from the next getter.
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();
            var go = new GameObject("WaitFor_DestroyedRef");
            m_Probe.Target = go;
            UnityEngine.Object.DestroyImmediate(go);

            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Target = new ObjectRef { InstanceId = PipelineUtils.GetObjectId(m_Probe) },
                Member = "Target.name",
                Op = "equals",
                Value = "x"
            }, timeoutS: 5, pollIntervalMs: 50);

            Assert.AreEqual("failed", result.State, $"expected structured failure, got {result.State}: {result.Error}");
            StringAssert.Contains("destroyed Unity object", result.Error);
        }

        [Test]
        public void WaitFor_ThrowingMemberGetter_FailsStructured()
        {
            // A member getter that throws must resolve the wait as a structured failure (with the
            // real exception surfaced), never escape WaitFor raw or vanish in the async Tick path.
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();

            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Target = new ObjectRef { InstanceId = PipelineUtils.GetObjectId(m_Probe) },
                Member = "Explodes",
                Op = "equals",
                Value = "x"
            }, timeoutS: 5, pollIntervalMs: 50);

            Assert.AreEqual("failed", result.State, $"expected structured failure, got {result.State}: {result.Error}");
            StringAssert.Contains("InvalidOperationException", result.Error);
            StringAssert.Contains("probe getter boom", result.Error);
        }

        [Test]
        public void WaitFor_Async_SubTickTimeout_ObservesConditionAtLeastOnce()
        {
            // Parity with the sync path: an async wait whose budget expires before the first Tick
            // must still poll once before resolving timedOut, so the result carries a real
            // value/framesObserved instead of null/0.
            var submit = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Member = "UnityEngine.Time.timeScale",
                Op = "equals",
                Value = -1
            }, timeoutS: 0.001, pollIntervalMs: 16, async: true);

            Thread.Sleep(10); // let the tiny budget expire before the first tick
            WaitRegistry.Tick();

            var status = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("timedOut", status.State);
            Assert.GreaterOrEqual(status.FramesObserved, 1,
                "the condition must be observed at least once before a timeout resolves");
            Assert.IsNotNull(status.Value, "the last observed value must be reported");
        }

        [Test]
        public void WaitFor_Interruption_OverwritesStaleToleratedError()
        {
            // A tolerated resolution failure records its message on the job; a later interruption
            // must overwrite it — wait_status should report why the wait ENDED, not the last retry.
            var submit = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Member = "No.Such.Type.Member",
                Op = "equals",
                Value = 1
            }, timeoutS: 60, pollIntervalMs: 16, tolerateMissing: true, async: true);

            WaitRegistry.Tick(); // records the tolerated resolution error
            var pending = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("pending", pending.State, "precondition: tolerated failure keeps the wait pending");

            WaitRegistry.SimulatePlayModeChangeForTests(PlayModeStateChange.ExitingPlayMode);

            var status = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("interrupted", status.State);
            StringAssert.Contains("interrupted", status.Error,
                "the interruption cause must win over the stale resolution message");
            StringAssert.DoesNotContain("Could not resolve", status.Error);
        }

        // ----------------------------------------------------------------------------------------
        // Review round 2: on_met failures, final-look parity, validation, destroyed terminals
        // ----------------------------------------------------------------------------------------

        [Test]
        public void WaitFor_OnMetFollowupThrows_ReportsMetWithOnMetError()
        {
            // The condition HELD but the follow-up failed: the wait must resolve met with the
            // follow-up failure on its own onMetError field (never on error, which docs define as
            // the reason a wait FAILED) — and an unknown capture view must throw, not silently
            // capture the Game View.
            WaitForCommands.ResetCaptureActionForTests(); // an earlier test may have faked capture

            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Member = "UnityEngine.Application.isPlaying",
                Op = "equals",
                Value = false
            }, timeoutS: 5, pollIntervalMs: 50, onMet: new WaitOnMetInput
            {
                Capture = new WaitCaptureInput { View = "seen" }
            });

            Assert.AreEqual("met", result.State, $"the condition held; got {result.State}: {result.Error}");
            Assert.IsTrue(result.Met);
            Assert.IsNull(result.Error, "error stays reserved for failed waits");
            Assert.IsNotNull(result.OnMetError, "the follow-up failure must be reported");
            StringAssert.Contains("Unknown view", result.OnMetError);
            Assert.IsNull(result.Capture, "no capture was produced");
        }

        [Test]
        public void WaitFor_Async_ConditionFlipsJustBeforeDeadline_ResolvesMetOnFinalLook()
        {
            // Full parity with the sync loop: the deadline tick takes a final look BEFORE
            // resolving timedOut, so a transition that lands between the last scheduled poll and
            // the deadline is caught, not dropped.
            var submit = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Member = "UnityEngine.Time.timeScale",
                Op = "equals",
                Value = 0.66
            }, timeoutS: 0.05, pollIntervalMs: 5000, async: true);

            try
            {
                Time.timeScale = 0.66f;   // flips true after registration, before any scheduled poll
                Thread.Sleep(60);         // let the budget expire
                WaitRegistry.Tick();

                var status = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
                Assert.AreEqual("met", status.State,
                    "the deadline tick must observe the flipped condition, not resolve timedOut");
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        [Test]
        public void WaitFor_TargetAndFindTypeTogether_Rejected()
        {
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();
            var ex = Assert.Throws<ArgumentException>(() => WaitForCommands.WaitFor(new WaitConditionInput
            {
                Target = new ObjectRef { InstanceId = PipelineUtils.GetObjectId(m_Probe) },
                FindType = typeof(ProbeBehaviourAsset).FullName,
                Member = "Label",
                Op = "equals",
                Value = "x"
            }, timeoutS: 1));
            StringAssert.Contains("mutually exclusive", ex.Message);
        }

        [Test]
        public void WaitFor_DestroyedTerminalValue_ObservesNull()
        {
            // A destroyed Unity object as the LEAF value is observed as null (Unity's own ==
            // semantics), so "wait until destroyed" is expressible as { op: equals, value: null }.
            // (Traversal THROUGH a destroyed object still fails structured — covered elsewhere.)
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();
            var go = new GameObject("WaitFor_DestroyedTerminal");
            m_Probe.Target = go;
            UnityEngine.Object.DestroyImmediate(go);

            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Target = new ObjectRef { InstanceId = PipelineUtils.GetObjectId(m_Probe) },
                Member = "Target",
                Op = "equals",
                Value = JValue.CreateNull() // EXPLICIT null: an omitted value is rejected
            }, timeoutS: 5, pollIntervalMs: 50);

            Assert.AreEqual("met", result.State,
                $"a destroyed terminal value must observe as null, got {result.State}: {result.Error}");
        }

        [Test]
        public void ResolveType_AmbiguousSimpleName_BindsDeterministicallyWithNote()
        {
            // "Debug" exists in UnityEngine and System.Diagnostics (at least): the bare name must
            // bind deterministically (lowest full name) and surface an ambiguity note; the
            // fully-qualified name resolves silently.
            var ambiguous = WaitConditionEvaluator.ResolveType("Debug", out var note);
            Assert.IsNotNull(ambiguous, "a simple-name match must still resolve");
            Assert.IsNotNull(note, "multiple matches must surface an ambiguity note");
            StringAssert.Contains("matched", note);
            StringAssert.Contains("fully-qualified", note);

            var exact = WaitConditionEvaluator.ResolveType("UnityEngine.Debug", out var exactNote);
            Assert.AreEqual(typeof(Debug), exact);
            Assert.IsNull(exactNote, "an exact full name is unambiguous");
        }

        // ----------------------------------------------------------------------------------------
        // Review round 4: tolerate scope, cancel-vs-met race, integer precision
        // ----------------------------------------------------------------------------------------

        [Test]
        public void WaitFor_TolerateMissing_GetterThrow_StillFailsStructured()
        {
            // tolerate_missing is scoped to structured RESOLUTION failures; a member getter that
            // throws is a real failure and must not be retried into a timeout that buries the
            // exception.
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();

            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Target = new ObjectRef { InstanceId = PipelineUtils.GetObjectId(m_Probe) },
                Member = "Explodes",
                Op = "equals",
                Value = "x"
            }, timeoutS: 5, pollIntervalMs: 50, tolerateMissing: true);

            Assert.AreEqual("failed", result.State,
                $"a throwing getter must fail even under tolerate_missing, got {result.State}: {result.Error}");
            StringAssert.Contains("InvalidOperationException", result.Error);
        }

        [Test]
        public void WaitFor_CancelLandingDuringOnMetFollowup_StaysMet()
        {
            // Review round 5: the job claims the met transition (m_Resolving) BEFORE running the
            // on_met follow-up, precisely so a cancel landing DURING it (this simulates the
            // request-thread race) is too late to retroactively undo a side effect (the capture)
            // that already ran for real. It must resolve met with that capture retained, and the
            // racing wait_cancel call — serviced WHILE the follow-up is still in flight, before the
            // final commit — must report the honest state at that instant ("pending": the cancel
            // had no effect and the job had not yet committed), never a fabricated "canceled".
            var submit = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Member = "UnityEngine.Application.isPlaying",
                Op = "equals",
                Value = false
            }, timeoutS: 30, pollIntervalMs: 16, async: true,
               onMet: new WaitOnMetInput { Capture = new WaitCaptureInput() });

            WaitResult raceCancelReply = null;
            try
            {
                WaitForCommands.s_CaptureAction = _ =>
                {
                    // Simulates the request-thread cancel arriving mid-follow-up.
                    raceCancelReply = (WaitResult)WaitForCommands.WaitCancel(submit.WaitId);
                    return new WaitCaptureResult { Width = 1, Height = 1 };
                };

                WaitRegistry.Tick();

                var status = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
                Assert.AreEqual("met", status.State,
                    "the job already committed to the follow-up before the cancel landed");
                Assert.IsNotNull(status.Capture, "the capture that actually ran must not be silently discarded");
                Assert.AreEqual("pending", raceCancelReply.State,
                    "a cancel serviced before the in-flight follow-up commits must report the honest " +
                    "pending state, not a fabricated canceled");
            }
            finally
            {
                WaitForCommands.ResetCaptureActionForTests();
            }
        }

        [Test]
        public void WaitFor_CancelLandingBeforeOnMetFollowup_StaysCanceled()
        {
            // The companion race: a cancel that lands BEFORE the job claims the met transition
            // (i.e. before PollOnce even starts the follow-up) must still win — cancel is only
            // "too late" once the follow-up's side effects are already committed to running.
            var submit = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Member = "UnityEngine.Application.isPlaying",
                Op = "equals",
                Value = false
            }, timeoutS: 30, pollIntervalMs: 16, async: true,
               onMet: new WaitOnMetInput { Capture = new WaitCaptureInput() });

            WaitForCommands.WaitCancel(submit.WaitId);
            WaitRegistry.Tick();

            var status = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("canceled", status.State, "a cancel that landed before the poll must still win");
            Assert.IsNull(status.Capture, "a canceled wait must never have run the follow-up");
        }

        [TestCase("equals", false, TestName = "Evaluate_LongEquals_BeyondDoublePrecision_IsExact")]
        [TestCase("notEquals", true, TestName = "Evaluate_LongNotEquals_BeyondDoublePrecision_IsExact")]
        public void Evaluate_LongComparison_BeyondDoublePrecision(string op, bool expected)
        {
            // 9007199254740993 and 9007199254740992 collapse to the same double (2^53); the
            // comparison must stay exact for integral operands.
            Assert.AreEqual(expected, WaitConditionEvaluator.Evaluate(
                op, 9007199254740993L, 9007199254740992L, typeof(long), null, false, out var err));
            Assert.IsNull(err);
        }

        [Test]
        public void Evaluate_LongOrdering_BeyondDoublePrecision_IsExact()
        {
            Assert.IsTrue(WaitConditionEvaluator.Evaluate(
                "greaterThan", 9007199254740993L, 9007199254740992L, typeof(long), null, false, out var err));
            Assert.IsNull(err);
            Assert.IsFalse(WaitConditionEvaluator.Evaluate(
                "lessThan", 9007199254740993L, 9007199254740992L, typeof(long), null, false, out _));
        }

        // ----------------------------------------------------------------------------------------
        // Review round 5: tolerated evaluation errors
        // ----------------------------------------------------------------------------------------

        [Test]
        public void WaitFor_TolerateMissing_EvaluationError_RetriesUntilTimeout()
        {
            // An ordering op against a member that is still null is "cannot compare YET" — under
            // tolerate_missing it must retry (the wait-until-nullable-field-is-set case), with the
            // last evaluation error surfaced on timeout, not fail on the first poll.
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>(); // Target stays null

            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Target = new ObjectRef { InstanceId = PipelineUtils.GetObjectId(m_Probe) },
                Member = "Target",
                Op = "greaterThan",
                Value = 1
            }, timeoutS: 0.4, pollIntervalMs: 50, tolerateMissing: true);

            Assert.AreEqual("timedOut", result.State,
                $"a tolerated evaluation error must run out the clock, got {result.State}: {result.Error}");
            Assert.Greater(result.FramesObserved, 1, "the condition must have been retried");
            Assert.IsNotNull(result.Error, "the last evaluation error is surfaced on timeout");
        }

        [Test]
        public void WaitFor_EvaluationError_WithoutTolerate_FailsFast()
        {
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();

            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Target = new ObjectRef { InstanceId = PipelineUtils.GetObjectId(m_Probe) },
                Member = "Target",
                Op = "greaterThan",
                Value = 1
            }, timeoutS: 5, pollIntervalMs: 50);

            Assert.AreEqual("failed", result.State, "an evaluation error is terminal by default");
            Assert.IsFalse(result.TimedOut);
        }

        // ----------------------------------------------------------------------------------------
        // tolerate_missing: fail-fast vs retry-until-timeout
        // ----------------------------------------------------------------------------------------

        [Test]
        public void WaitFor_UnresolvableMember_FailsFast_ByDefault()
        {
            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Member = "No.Such.Type.Member",
                Op = "equals",
                Value = true
            }, timeoutS: 5, pollIntervalMs: 50);

            Assert.AreEqual("failed", result.State, "resolution failure must be terminal by default");
            Assert.IsFalse(result.Met);
            Assert.IsFalse(result.TimedOut, "a resolution failure must not masquerade as a timeout");
            Assert.IsNotNull(result.Error);
        }

        [Test]
        public void WaitFor_UnresolvableMember_TolerateMissing_RetriesUntilTimeout()
        {
            // Budget note: the FIRST poll pays the whole-appdomain scan for the unresolvable prefix
            // (can be several hundred ms in a large editor domain); retries after it are cheap
            // because a failed scan is negative-cached until a new assembly loads. The budget must
            // comfortably exceed one cold scan or the loop legitimately exits after a single poll.
            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Member = "No.Such.Type.Member",
                Op = "equals",
                Value = true
            }, timeoutS: 2, pollIntervalMs: 50, tolerateMissing: true);

            Assert.AreEqual("timedOut", result.State, "tolerated resolution failures must run out the clock, not fail");
            Assert.IsFalse(result.Met);
            Assert.IsTrue(result.TimedOut);
            Assert.IsNotNull(result.Error, "the last resolution error must be surfaced on timeout");
            Assert.Greater(result.FramesObserved, 1, "the condition must have been retried");
        }

        [Test]
        public void WaitFor_TolerateMissing_MalformedTargetGlobalId_FailsFast()
        {
            // A globalId that fails to even PARSE is a property of the input STRING itself -- no
            // amount of retrying makes it parseable -- unlike a well-formed globalId/instanceId
            // that simply doesn't resolve to a loaded object yet (covered by the next test).
            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Target = new ObjectRef { GlobalId = "not-a-real-global-object-id" },
                Member = "name",
                Op = "equals",
                Value = "x"
            }, timeoutS: 5, pollIntervalMs: 50, tolerateMissing: true);

            Assert.AreEqual("failed", result.State,
                $"a malformed target.globalId must fail fast even under tolerate_missing, got {result.State}: {result.Error}");
            Assert.AreEqual(1, result.FramesObserved, "must fail on the first poll, never retried");
        }

        [Test]
        public void WaitFor_TolerateMissing_TargetNotYetLoaded_RetriesUntilTimeout()
        {
            // A well-formed target that simply hasn't appeared yet (the spawn-wait use case
            // `target` shares with `findType`) must retry, not fail fast like a malformed handle.
            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Target = new ObjectRef { InstanceId = ObjectId.FromRaw(9999999) },
                Member = "name",
                Op = "equals",
                Value = "x"
            }, timeoutS: 0.3, pollIntervalMs: 50, tolerateMissing: true);

            Assert.AreEqual("timedOut", result.State,
                $"a not-yet-loaded target must retry under tolerate_missing, got {result.State}: {result.Error}");
            Assert.Greater(result.FramesObserved, 1, "the condition must have been retried, not failed on the first poll");
        }

        [Test]
        public void WaitFor_Async_TolerateMissing_ResolvesWhenInstanceAppears()
        {
            // The spawn-wait use case: wait for an object that does not exist yet.
            var submit = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                FindType = typeof(ProbeBehaviourAsset).FullName,
                Member = "Label",
                Op = "equals",
                Value = "spawned"
            }, timeoutS: 30, pollIntervalMs: 16, tolerateMissing: true, async: true);

            WaitRegistry.Tick(); // no instance yet -> tolerated, still pending
            var pending = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("pending", pending.State, "a missing instance must not fail a tolerant wait");

            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();
            m_Probe.Label = "spawned";

            Thread.Sleep(20); // let the poll interval elapse
            WaitRegistry.Tick();

            var status = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("met", status.State, "the wait must resolve once the instance appears");
            Assert.IsTrue(status.Met);
            Assert.IsNull(status.Error, "a successful resolution must clear the tolerated error");
        }

        [Test]
        public void WaitFor_TolerateMissing_FindTypeBoundInstanceLacksMember_RetriesInsteadOfFailingFast()
        {
            // Which concrete instance findType binds to is a deterministic-but-arbitrary tie-break
            // among whichever live candidates exist right now (FindInstance's lowest-instance-id
            // pick) -- not a fixed property of the wait's target the way a `target` handle or a
            // static path are. If the member doesn't exist on the ONE instance that happens to be
            // bound today, a different (already-existing or not-yet-spawned) instance of a
            // different subtype might still have it -- so this must retry under tolerate_missing,
            // not fail fast like every other TryReadMember failure.
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();

            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                FindType = typeof(ProbeBehaviourAsset).FullName,
                Member = "NoSuchMember",
                Op = "equals",
                Value = true
            }, timeoutS: 0.3, pollIntervalMs: 50, tolerateMissing: true);

            Assert.AreEqual("timedOut", result.State,
                $"a missing member on the bound findType instance must retry under tolerate_missing, got {result.State}: {result.Error}");
            Assert.Greater(result.FramesObserved, 1, "the condition must have been retried, not failed on the first poll");
        }

        [Test]
        public void WaitFor_TolerateMissing_FindTypeNestedMemberMissing_StillFailsFast()
        {
            // The leniency above is scoped to the FIRST segment off the bound instance (the
            // ambiguous-candidate pick). A member missing one segment further in is a permanent
            // property of whatever concrete type that first segment's value turns out to be --
            // already resolved, and no more mutable than a target/static path's member shape.
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();
            m_Probe.Target = m_Probe; // give Target a real, non-null UnityEngine.Object value

            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                FindType = typeof(ProbeBehaviourAsset).FullName,
                Member = "Target.NoSuchMember",
                Op = "equals",
                Value = true
            }, timeoutS: 5, pollIntervalMs: 50, tolerateMissing: true);

            Assert.AreEqual("failed", result.State,
                $"a missing member one segment past the bound instance must still fail fast, got {result.State}: {result.Error}");
            Assert.AreEqual(1, result.FramesObserved, "must fail on the first poll, never retried");
        }

        // ----------------------------------------------------------------------------------------
        // Review round 5: permanent vs. transient resolution failures under tolerate_missing
        // ----------------------------------------------------------------------------------------

        private static IEnumerable<TestCaseData> PermanentResolutionFailureConditions()
        {
            // A static path with fewer than 2 segments is a property of the input STRING — no
            // amount of retrying (no assembly load, no elapsed time) ever makes it resolvable.
            // tolerate_missing's contract is "does not exist YET"; this can never exist.
            yield return new TestCaseData(
                    new WaitConditionInput { Member = "OnlySegment", Op = "equals", Value = true })
                .SetName("WaitFor_TolerateMissing_MalformedStaticPath_FailsFast");
            // findType naming a non-UnityEngine.Object type is a permanent misconfiguration: it can
            // never bind to a "live instance" regardless of how long the wait retries.
            yield return new TestCaseData(
                    new WaitConditionInput { FindType = typeof(string).FullName, Member = "Length", Op = "equals", Value = 0 })
                .SetName("WaitFor_TolerateMissing_FindTypeNotUnityObject_FailsFast");
            // A member that is write-only on the resolved type is a property of that TYPE's shape,
            // which cannot change without a domain reload (which the wait would not survive anyway).
            yield return new TestCaseData(
                    new WaitConditionInput { Member = "Unity.Pipeline.Tests.Editor.WaitForProbe.WriteOnly", Op = "equals", Value = true })
                .SetName("WaitFor_TolerateMissing_WriteOnlyMember_FailsFast");
        }

        [TestCaseSource(nameof(PermanentResolutionFailureConditions))]
        public void WaitFor_TolerateMissing_PermanentResolutionFailure_FailsFast(WaitConditionInput condition)
        {
            var result = (WaitResult)WaitForCommands.WaitFor(condition, timeoutS: 5, pollIntervalMs: 50, tolerateMissing: true);

            Assert.AreEqual("failed", result.State,
                $"a permanent resolution failure must fail fast even under tolerate_missing, got {result.State}: {result.Error}");
            Assert.AreEqual(1, result.FramesObserved, "must fail on the first poll, never retried");
        }

        // ----------------------------------------------------------------------------------------
        // Review round 5: op normalization parity between ValidateCondition and Evaluate
        // ----------------------------------------------------------------------------------------

        [Test]
        public void Evaluate_EmptyStringOp_NormalizesToEquals()
        {
            // ValidateCondition treats op:"" as "equals" and lets the wait register; Evaluate must
            // agree, or a condition that passed validation would fail every poll on "Unknown op ''".
            Assert.IsTrue(WaitConditionEvaluator.Evaluate(
                "", 1, 1, typeof(int), null, false, out var err));
            Assert.IsNull(err);
        }

        [Test]
        public void WaitFor_EmptyStringOp_BehavesAsEquals()
        {
            var result = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Member = "Unity.Pipeline.Tests.Editor.WaitForProbe.AlwaysTrue",
                Op = "",
                Value = JToken.FromObject(true)
            }, timeoutS: 5, pollIntervalMs: 50);

            Assert.AreEqual("met", result.State,
                $"op:'' must behave as 'equals' end-to-end, got {result.State}: {result.Error}");
        }

        // ----------------------------------------------------------------------------------------
        // on_met follow-up (capture) — same call that first observes the condition true
        // ----------------------------------------------------------------------------------------

        [Test]
        public void WaitFor_OnMetCapture_RunsWhenConditionHolds()
        {
            var onMet = new WaitOnMetInput { Capture = new WaitCaptureInput { View = "game", SavePath = "Screenshots/x.png" } };

            var result = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysTrue", "equals", true), timeoutS: 5, pollIntervalMs: 50, onMet: onMet);

            Assert.IsTrue(result.Met);
            Assert.IsNotNull(result.Capture, "on_met.capture should populate the capture result");
            Assert.AreEqual("Assets/__fake_wait_capture.png", result.Capture.SavedPath);
            Assert.AreEqual(1, s_FakeCaptureCount, "capture must run exactly once, on the frame the condition first holds");
        }

        [Test]
        public void WaitFor_NoCapture_WhenConditionNeverHolds()
        {
            var onMet = new WaitOnMetInput { Capture = new WaitCaptureInput { View = "game" } };

            var result = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysFalse", "equals", true), timeoutS: 0.3, pollIntervalMs: 50, onMet: onMet);

            Assert.IsTrue(result.TimedOut);
            Assert.IsNull(result.Capture, "no capture should run when the condition never holds");
            Assert.AreEqual(0, s_FakeCaptureCount);
        }

        [Test]
        public void WaitFor_Async_OneFrameTrueCondition_IsCapturedInThatTick()
        {
            // AC: a condition true for exactly ONE registry tick (the probe consumes the pulse on
            // read) must still be observed, captured, and reported as met.
            var submit = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("Pulse", "equals", true), timeoutS: 30, pollIntervalMs: 16, async: true,
                onMet: new WaitOnMetInput { Capture = new WaitCaptureInput { View = "game" } });

            WaitRegistry.Tick(); // pulse not armed -> observed false
            Assert.AreEqual(0, s_FakeCaptureCount);

            Thread.Sleep(20); // let the poll interval elapse
            WaitForProbe.PulseArmed = true;
            WaitRegistry.Tick(); // the ONE tick the condition is true

            Assert.AreEqual(1, s_FakeCaptureCount, "on_met.capture must run during the single tick the condition held");
            Assert.IsFalse(WaitForProbe.PulseArmed, "the probe must have consumed the pulse (one-frame-true)");

            var status = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("met", status.State, "the one-frame-true condition must be reported as met");
            Assert.IsTrue(status.Met);

            Thread.Sleep(20);
            WaitRegistry.Tick(); // terminal: no further polls / captures
            Assert.AreEqual(1, s_FakeCaptureCount, "a terminal wait must not poll or capture again");
        }

        // ----------------------------------------------------------------------------------------
        // return_history
        // ----------------------------------------------------------------------------------------

        [Test]
        public void WaitFor_ReturnHistory_RecordsSamples()
        {
            var result = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysTrue", "equals", true), timeoutS: 5, pollIntervalMs: 50, returnHistory: true);

            Assert.IsNotNull(result.History, "history should be populated when return_history is set");
            Assert.GreaterOrEqual(result.History.Count, 1);
        }

        // ----------------------------------------------------------------------------------------
        // Async: wait_id / status / cancel / cap / interruption
        // ----------------------------------------------------------------------------------------

        [Test]
        public void WaitFor_Async_ReturnsWaitIdThenReachesMet()
        {
            var submit = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysTrue", "equals", true), timeoutS: 30, pollIntervalMs: 16, async: true);

            Assert.IsNotNull(submit.WaitId, "async submission should return a wait_id");
            Assert.AreEqual("pending", submit.State);

            WaitRegistry.Tick(); // one frame of evaluation

            var status = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.IsTrue(status.Met, "the async wait should reach 'met' after a tick");
            Assert.AreEqual("met", status.State);
        }

        [Test]
        public void WaitStatus_AfterTerminal_ElapsedMsIsFrozen()
        {
            var submit = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysTrue", "equals", true), timeoutS: 30, pollIntervalMs: 16, async: true);

            WaitRegistry.Tick(); // -> met

            var first = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("met", first.State);

            Thread.Sleep(60);
            var second = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual(first.ElapsedMs, second.ElapsedMs,
                "elapsedMs must freeze at the terminal transition, not keep growing while retained");
        }

        [Test]
        public void WaitStatus_AfterObservedObjectDestroyed_SerializesAsNull()
        {
            // A terminal job's LastValue is cached at the poll that resolved it; the object it
            // referenced can be destroyed afterward (e.g. scene unload) before a LATER wait_status
            // call re-serializes it. Jsonify must re-check Unity's fake-null at serialization time,
            // the same way ReadMember normalizes a destroyed leaf to null at READ time — a stale
            // `!= null` check would otherwise call .ToString() on a destroyed reference instead of
            // honoring the documented "a destroyed object observes as null" contract.
            m_Probe = ScriptableObject.CreateInstance<ProbeBehaviourAsset>();
            var target = new GameObject("WaitFor_Jsonify_Destroyed");
            m_Probe.Target = target;

            var submit = (WaitResult)WaitForCommands.WaitFor(new WaitConditionInput
            {
                Target = new ObjectRef { InstanceId = PipelineUtils.GetObjectId(m_Probe) },
                Member = "Target",
                Op = "notEquals",
                Value = JValue.CreateNull()
            }, timeoutS: 5, pollIntervalMs: 50, async: true);

            WaitRegistry.Tick(); // observes Target != null -> met; caches the live GameObject

            var metStatus = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("met", metStatus.State, $"precondition: {metStatus.Error}");

            UnityEngine.Object.DestroyImmediate(target); // destroyed AFTER the wait already resolved

            WaitResult afterDestroy = null;
            Assert.DoesNotThrow(() => afterDestroy = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId),
                "re-serializing a cached value whose object was destroyed since must not throw");
            Assert.IsNull(afterDestroy.Value,
                "a destroyed cached reference must serialize as null, matching read-time semantics");
        }

        [Test]
        public void WaitCancel_Async_TransitionsToCanceled()
        {
            var submit = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysFalse", "equals", true), timeoutS: 60, pollIntervalMs: 16, async: true);

            var canceled = (WaitResult)WaitForCommands.WaitCancel(submit.WaitId);
            Assert.AreEqual("canceled", canceled.State);

            var status = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("canceled", status.State);
        }

        [Test]
        public void WaitCancel_AlreadyResolved_ReportsRealStateNotFakeCanceled()
        {
            // Review round 5: wait_cancel arriving after the wait already reached a terminal state
            // must report that real state, not force-report "canceled" — a caller polling wait_status
            // right after would otherwise see a state that contradicts what wait_cancel just claimed.
            var submit = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysTrue", "equals", true), timeoutS: 30, pollIntervalMs: 16, async: true);

            WaitRegistry.Tick(); // -> met, before the cancel arrives

            var cancelReply = (WaitResult)WaitForCommands.WaitCancel(submit.WaitId);
            Assert.AreEqual("met", cancelReply.State,
                "cancel has no effect on an already-terminal wait and must not fabricate 'canceled'");
            Assert.IsTrue(cancelReply.Met);
        }

        [Test]
        public void WaitStatus_UnknownId_ReturnsNotFound()
        {
            var status = (WaitResult)WaitForCommands.WaitStatus("does-not-exist");
            Assert.AreEqual("not_found", status.State);
        }

        [Test]
        public void PruneTerminal_RetainsMostRecentlyFinished_NotMostRecentlySubmitted()
        {
            // Regression: PruneTerminal used to order terminal jobs by StartedAt (submission time)
            // instead of when they actually finished. A long-running wait submitted FIRST but
            // resolved LAST -- exactly the "one long wait among many short ones" pattern the
            // process-wide registry is meant to support -- was evicted the instant it became
            // terminal, purely for having the oldest StartedAt, while short waits that had finished
            // long before it (but were submitted later) survived.
            var longWait = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysFalse", "equals", true), timeoutS: 300, pollIntervalMs: 100, async: true);

            for (var i = 0; i < WaitRegistry.MaxRetainedTerminal; i++)
            {
                var shortWait = (WaitResult)WaitForCommands.WaitFor(
                    StaticCondition("AlwaysTrue", "equals", true), timeoutS: 5, pollIntervalMs: 50, async: true);
                WaitRegistry.Tick(); // resolves immediately -> terminal, well before longWait
                var shortStatus = (WaitResult)WaitForCommands.WaitStatus(shortWait.WaitId);
                Assert.AreEqual("met", shortStatus.State, "precondition: each short wait must resolve before the long one");
            }

            // The long wait finishes LAST (the largest terminal timestamp of any job here) despite
            // having the SMALLEST StartedAt in the registry.
            var canceled = (WaitResult)WaitForCommands.WaitCancel(longWait.WaitId);
            Assert.AreEqual("canceled", canceled.State);

            // One more registration triggers PruneTerminal with MaxRetainedTerminal terminal jobs
            // already retained (the short waits + the long one) -- exactly one must be evicted.
            var trigger = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysFalse", "equals", true), timeoutS: 5, pollIntervalMs: 100, async: true);
            try
            {
                var longStatus = (WaitResult)WaitForCommands.WaitStatus(longWait.WaitId);
                Assert.AreEqual("canceled", longStatus.State,
                    "the long wait just finished and must survive pruning, not be evicted for having the oldest StartedAt");
            }
            finally
            {
                WaitForCommands.WaitCancel(trigger.WaitId);
            }
        }

        [Test]
        public void WaitFor_Async_ConcurrencyCap_IsEnforced()
        {
            for (var i = 0; i < WaitRegistry.MaxConcurrentWaits; i++)
            {
                var submit = (WaitResult)WaitForCommands.WaitFor(
                    StaticCondition("AlwaysFalse", "equals", true), timeoutS: 300, pollIntervalMs: 100, async: true);
                Assert.AreEqual("pending", submit.State);
            }

            var ex = Assert.Throws<InvalidOperationException>(() => WaitForCommands.WaitFor(
                StaticCondition("AlwaysFalse", "equals", true), timeoutS: 300, pollIntervalMs: 100, async: true));
            StringAssert.Contains("concurrent", ex.Message);
            StringAssert.Contains("wait_cancel", ex.Message, "the cap violation must tell the caller how to free a slot");
        }

        [Test]
        public void WaitFor_Async_Interrupted_TransitionsToInterrupted()
        {
            var submit = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysFalse", "equals", true), timeoutS: 60, pollIntervalMs: 16, async: true);

            WaitRegistry.InterruptAllForTests(); // simulates a domain reload / play-mode exit

            var status = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("interrupted", status.State);
            Assert.IsTrue(status.Interrupted ?? false);
        }

        [Test]
        public void WaitFor_SurvivesEnteringPlayMode_InterruptedOnExit()
        {
            // Ticket scope: only EXITING play mode (and domain reloads via beforeAssemblyReload)
            // interrupt a wait. Entering play mode must not (waits survive play entry when domain
            // reload is disabled; with reload enabled, beforeAssemblyReload interrupts anyway).
            var submit = (WaitResult)WaitForCommands.WaitFor(
                StaticCondition("AlwaysFalse", "equals", true), timeoutS: 60, pollIntervalMs: 16, async: true);

            WaitRegistry.SimulatePlayModeChangeForTests(PlayModeStateChange.ExitingEditMode);
            WaitRegistry.SimulatePlayModeChangeForTests(PlayModeStateChange.EnteredPlayMode);
            var afterEnter = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("pending", afterEnter.State, "entering play mode must not interrupt an active wait");

            WaitRegistry.SimulatePlayModeChangeForTests(PlayModeStateChange.ExitingPlayMode);
            var afterExit = (WaitResult)WaitForCommands.WaitStatus(submit.WaitId);
            Assert.AreEqual("interrupted", afterExit.State, "exiting play mode must interrupt an active wait");
        }

        // ----------------------------------------------------------------------------------------
        // ViaClient: full HTTP + off-main-thread marshaling
        // ----------------------------------------------------------------------------------------

        [Test]
        public void WaitFor_ViaClient_SyncConditionAlreadyTrue_ReturnsMet()
        {
            using (var server = new PipelineTestServer())
            {
                var parameters = new
                {
                    condition = new { member = "Unity.Pipeline.Tests.Editor.WaitForProbe.AlwaysTrue", op = "equals", value = true },
                    timeout_s = 5,
                    poll_interval_ms = 50
                };

                var response = server.Execute("wait_for", parameters, timeoutMs: 15000);

                Assert.IsTrue(response.HasValidJson, $"expected JSON, got: {response.RawResponse}");
                var met = response.JsonResponse["result"]?["met"]?.ToObject<bool>();
                Assert.IsTrue(met.GetValueOrDefault(), $"wait_for should report met=true; body: {response.RawResponse}");
            }
        }

        [Test]
        public void WaitFor_ViaClient_AsyncWait_DoesNotBlockOtherCommands()
        {
            // AC: while an async wait is pending, other commands keep executing (a sync wait would
            // hold the exec gate instead).
            using (var server = new PipelineTestServer())
            {
                var submit = server.Execute("wait_for", new
                {
                    condition = new { member = "Unity.Pipeline.Tests.Editor.WaitForProbe.AlwaysFalse", op = "equals", value = true },
                    timeout_s = 30,
                    poll_interval_ms = 100,
                    @async = true // '@' escapes the keyword; the serialized property name is "async"
                }, timeoutMs: 15000);

                Assert.IsTrue(submit.IsSuccess, $"async submission should succeed: {submit.Error}");
                var waitId = submit.JsonResponse["result"]?["waitId"]?.ToString();
                Assert.IsFalse(string.IsNullOrEmpty(waitId), $"async submission must return a waitId; body: {submit.RawResponse}");

                var status = server.Execute("editor_status", null, timeoutMs: 15000);
                Assert.IsTrue(status.IsSuccess,
                    $"editor_status must execute while the async wait is pending: {status.Error}");

                var cancel = server.Execute("wait_cancel", new { wait_id = waitId }, timeoutMs: 15000);
                Assert.IsTrue(cancel.IsSuccess, $"wait_cancel should succeed: {cancel.Error}");
                Assert.AreEqual("canceled", cancel.JsonResponse["result"]?["state"]?.ToString());
            }
        }

        // ----------------------------------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------------------------------

        private static WaitConditionInput StaticCondition(string staticMember, string op, object value)
        {
            return new WaitConditionInput
            {
                Member = "Unity.Pipeline.Tests.Editor.WaitForProbe." + staticMember,
                Op = op,
                Value = value == null ? null : JToken.FromObject(value)
            };
        }
    }

    /// <summary>Static probe with members of each shape wait_for observes; used for static-path tests.</summary>
    static class WaitForProbe
    {
        public static ProbeState State;
        public static int Count;
        public static float Ratio;
        public static string Label = string.Empty;
        public static bool Ready;
        public static DateTime FlipAt = DateTime.MaxValue;
        public static bool PulseArmed;

        public static bool AlwaysTrue => true;
        public static bool AlwaysFalse => false;
        public static bool Flipped => DateTime.UtcNow >= FlipAt;

        /// <summary>Enum member that flips Running -> Done at <see cref="FlipAt"/> (latency tests).</summary>
        public static ProbeState TimedState => DateTime.UtcNow >= FlipAt ? ProbeState.Done : ProbeState.Running;

        /// <summary>True exactly once per arm: reading it consumes the pulse (one-frame-true seam).</summary>
        public static bool Pulse
        {
            get
            {
                if (!PulseArmed)
                    return false;
                PulseArmed = false;
                return true;
            }
        }

        // Write-only: must be rejected as unobservable.
        public static bool WriteOnly { set { } }

        public static void Reset()
        {
            State = ProbeState.Idle;
            Count = 0;
            Ratio = 0f;
            Label = string.Empty;
            Ready = false;
            FlipAt = DateTime.MaxValue;
            PulseArmed = false;
        }
    }

    enum ProbeState { Idle, Running, Done }

    /// <summary>UnityEngine.Object probe for findType / target instance-member resolution tests.</summary>
    class ProbeBehaviourAsset : ScriptableObject
    {
        public float Ratio;
        public string Label = string.Empty;
        public ProbeState State;

        /// <summary>Holds a (possibly destroyed) Unity object for fake-null traversal tests.</summary>
        public UnityEngine.Object Target;

        /// <summary>A member whose getter throws, for structured-failure tests.</summary>
        public string Explodes => throw new InvalidOperationException("probe getter boom");
    }
}
