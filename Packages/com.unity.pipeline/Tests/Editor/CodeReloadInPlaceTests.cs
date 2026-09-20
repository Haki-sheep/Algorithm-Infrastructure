using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Pipeline.CodeReload;
using Unity.Pipeline.Runtime.Commands;
using UnityEngine;
using UnityEngine.TestTools;

namespace FakeReload
{
    /// <summary>
    /// An unrelated attribute that happens to share the simple name <c>CodeReloadAttribute</c>.
    /// The IL weaver must match the attribute's full name, so methods tagged with this never get
    /// a dispatch prologue (runtime discovery only honors the real Unity.Pipeline.CodeReload type).
    /// Lives in its own namespace so it cannot shadow the real attribute elsewhere in this file.
    /// </summary>
    public class CodeReloadAttribute : System.Attribute
    {
    }
}

namespace Unity.Pipeline.Tests.Editor
{
    /// <summary>
    /// Top-level probe type woven by the CodeReloadInPlace ILPostProcessor. Must be top-level (not
    /// nested) so the generated override can reference it by name from a separate assembly.
    /// </summary>
    public class InPlaceReloadProbe
    {
        /// <summary>Value set by the original (unmodified) body of <see cref="Compute"/>.</summary>
        public int baseline;
        /// <summary>Value set by the reloaded override body of <see cref="Compute"/>.</summary>
        public int hotValue;

        /// <summary>Test target for the in-place code-reload weaving/dispatch mechanism.</summary>
        [CodeReload]
        public void Compute()
        {
            baseline = 1; // original body
        }
    }

    /// <summary>
    /// Probe with a PRIVATE field, reloaded through the Assembly.Load backend. Documents the backend
    /// split: the interpreter reaches private members (reflection), but the Assembly.Load override is
    /// real IL in a separate assembly, and Unity's Mono enforces field accessibility at JIT time — the
    /// dispatch would throw FieldAccessException, so AccessibilityValidator rejects the reload up
    /// front. A public getter exposes the private field for asserts.
    /// </summary>
    class InPlacePrivateReloadProbe
    {
        private int m_Secret;
        public int Secret => m_Secret;

        [CodeReload]
        public void Compute()
        {
            m_Secret = 1; // original body
        }
    }

    /// <summary>
    /// Probe for the woven null-result guard: an override that returns null for a value-type
    /// return has no value to unbox — the prologue must throw an actionable error, not the
    /// opaque NullReferenceException a bare unbox.any produces.
    /// </summary>
    class NullResultWeaveTarget
    {
        public int ticks;

#pragma warning disable MS023 // value-returning on purpose: exercises the weaver's result prologue
        [CodeReload]
        public int Tick()
#pragma warning restore MS023
        {
            ticks++;      // original body
            return 7;     // original result
        }
    }

    /// <summary>
    /// Probe for the nested-type gate: the weaver weaves <c>Inner.Boom</c> too (Cecil's
    /// <c>GetTypes()</c> is recursive), but the reload pipeline only lifts methods declared
    /// directly in the top-level class — a nested [CodeReload] must be skipped LOUDLY, never
    /// silently dropped.
    /// </summary>
    public class NestedReloadProbe
    {
        public int ticks;

        [CodeReload]
        public void Tick()
        {
            ticks = 1; // original body
        }

        public class Inner
        {
            public int booms;

            [CodeReload]
            public void Boom()
            {
                booms = 1; // original body
            }
        }
    }

    /// <summary>
    /// Probe mirroring the Pong sample's layout: the <c>[OnCodeReload]</c> callback is declared
    /// BEFORE the <c>[CodeReload]</c> method. The callback is a post-reload hook, not a reload
    /// target — the extractor must not push it (the player registry doesn't hold it, and its name
    /// landing first in the pushed list used to fail the whole push).
    /// </summary>
    public class OnCodeReloadPushProbe
    {
        public int ticked;
        public string reloadMarker = "none";

        [OnCodeReload]
        public void OnReloaded()
        {
            reloadMarker = "reloaded";
        }

        [CodeReload]
        public void Tick()
        {
            ticked = 1;
        }
    }

    /// <summary>
    /// Probe whose compiled body mutates state; the empty-body reload test replaces it with a no-op.
    /// </summary>
    public class InPlaceEmptyBodyProbe
    {
        public int computedValue;

        [CodeReload]
        public void Compute()
        {
            computedValue = 42; // original body
        }
    }

    /// <summary>
    /// Tests for the in-place code reload workflow, covering all three layers:
    ///  - compile-time IL weaving + runtime dispatch (the ILPostProcessor),
    ///  - the Roslyn semantic-model transform + accessibility validation,
    ///  - the full reload_file pipeline end to end.
    /// </summary>
    class CodeReloadInPlaceTests
    {
        // -------- weaving fixtures --------

        public class WeaveTarget
        {
            public int ticks;
            public bool overrideRan;

            [CodeReload]
            public void Tick()
            {
                ticks++; // original body
            }
        }

        public static class WeaveOverrides
        {
            [CodeReloadOverrideMethod("WeaveTarget.Tick")]
            public static void Tick(WeaveTarget instance)
            {
                instance.overrideRan = true;
            }
        }

        // Tagged with FakeReload.CodeReloadAttribute — same simple name, wrong type. The weaver
        // must not weave a dispatch prologue into Tick, so even a registered override never runs.
        public class ForeignWeaveTarget
        {
            public int ticks;
            public bool overrideRan;

            [FakeReload.CodeReload]
            public void Tick()
            {
                ticks++; // original body
            }
        }

        public static class ForeignWeaveOverrides
        {
            [CodeReloadOverrideMethod("ForeignWeaveTarget.Tick")]
            public static void Tick(ForeignWeaveTarget instance)
            {
                instance.overrideRan = true;
            }
        }

        // Value-returning [CodeReload] method: exercises the weaver's result prologue
        // (out-param dispatch + unbox.any) and the registry's TryInvokeCodeReloadResult path.
        public class ValueWeaveTarget
        {
            public int ticks;

#pragma warning disable MS023 // value-returning on purpose: exercises the weaver's result prologue
            [CodeReload]
            public int Tick(int d)
#pragma warning restore MS023
            {
                ticks++;      // original body
                return d + 1; // original result
            }
        }

        public static class ValueWeaveOverrides
        {
            [CodeReloadOverrideMethod("ValueWeaveTarget.Tick")]
            public static int Tick(ValueWeaveTarget instance, int d)
            {
                return d + 100; // override result (distinct from the original body's d + 1)
            }
        }

        // Override that raises IMGUI's control-flow exception mid-body. Dispatch must propagate
        // it with its type intact (IMGUI catches ExitGUIException BY TYPE) instead of swallowing
        // it and falling back to the original body.
        public static class ExitGuiWeaveOverrides
        {
            [CodeReloadOverrideMethod("WeaveTarget.Tick")]
            public static void Tick(WeaveTarget instance)
            {
                instance.overrideRan = true;
                GUIUtility.ExitGUI();
            }
        }

        private const string PublicSource = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;

public class Demo : MonoBehaviour
{
    public float speed = 1f;

    [CodeReload]
    void Tick()
    {
        var dt = Time.deltaTime;
        transform.position += Vector3.right * speed * dt;
    }
}";

        // Exercises references that rely on the declaring type's scope and so must be re-qualified when
        // the body is lifted into the standalone override class: an inherited static (FindObjectsByType,
        // declared on UnityEngine.Object) and a nested type (the private enum Mode).
        private const string ScopedSource = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;

public class Scoped : MonoBehaviour
{
    enum Mode { Idle, Run }

    private Mode m_Mode = Mode.Idle;

    [CodeReload]
    void Tick()
    {
        var all = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        m_Mode = all.Length > 0 ? Mode.Run : Mode.Idle;
    }
}";

        // Exercises a nested type in positions where the grammar requires a TypeSyntax (foreach
        // declaration, local declaration, array type). The qualifier must produce a QualifiedName
        // there — wrapping in a MemberAccessExpression makes the rewriter's typed rebuild throw
        // InvalidCastException (as hit with TrackManager's nested MultiplierModifier delegate).
        private const string TypePositionSource = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;

public class Typed : MonoBehaviour
{
    public delegate int Handler(int current);
    public Handler OnChanged;

    enum Mode { Idle, Run }

    [CodeReload]
    void Tick()
    {
        foreach (Handler h in OnChanged.GetInvocationList())
        {
            h(1);
        }

        Mode m = Mode.Idle;
        var modes = new Mode[2];
        modes[0] = m;
    }
}";

        private static Dictionary<string, string> Bodies(params string[] names)
        {
            var d = new Dictionary<string, string>();
            foreach (var n in names) d[n] = "";
            return d;
        }

        [SetUp]
        public void Setup() => CodeReloadRegistry.ClearAllForTesting();

        [TearDown]
        public void TearDown() => CodeReloadRegistry.ClearAllForTesting();

        // -------- weaving + dispatch --------

        [Test]
        public void NoOverride_RunsOriginalBody()
        {
            var t = new WeaveTarget();
            t.Tick();

            Assert.AreEqual(1, t.ticks, "Original body should run when no override is registered");
            Assert.IsFalse(t.overrideRan);
        }

        [Test]
        public void WithOverride_WovenDispatchInvokesOverride()
        {
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(WeaveTarget).GetMethod(nameof(WeaveTarget.Tick)),
                new CodeReloadAttribute());

            var registered = CodeReloadRegistry.RegisterMethodOverride(
                typeof(WeaveOverrides).GetMethod(nameof(WeaveOverrides.Tick)),
                new CodeReloadOverrideMethodAttribute("WeaveTarget.Tick"),
                typeof(WeaveOverrides),
                out var reason);

            Assert.IsTrue(registered, reason);

            var t = new WeaveTarget();
            t.Tick();

            Assert.IsTrue(t.overrideRan, "Woven dispatch should have routed Tick() to the override");
            Assert.AreEqual(0, t.ticks, "Original body should be skipped when the override runs");
        }

        [Test]
        public void ValueReturning_NoOverride_RunsOriginalBody()
        {
            var t = new ValueWeaveTarget();
            int r = t.Tick(5);

            Assert.AreEqual(6, r, "Original body (d + 1) should run when no override is registered");
            Assert.AreEqual(1, t.ticks);
        }

        [Test]
        public void ValueReturning_WithOverride_ReturnsOverrideResult()
        {
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(ValueWeaveTarget).GetMethod(nameof(ValueWeaveTarget.Tick)),
                new CodeReloadAttribute());

            var registered = CodeReloadRegistry.RegisterMethodOverride(
                typeof(ValueWeaveOverrides).GetMethod(nameof(ValueWeaveOverrides.Tick)),
                new CodeReloadOverrideMethodAttribute("ValueWeaveTarget.Tick"),
                typeof(ValueWeaveOverrides),
                out var reason);

            Assert.IsTrue(registered, reason);

            var t = new ValueWeaveTarget();
            int r = t.Tick(5);

            Assert.AreEqual(105, r, "Woven dispatch should return the override's result (d + 100)");
            Assert.AreEqual(0, t.ticks, "Original body should be skipped when the override runs");
        }

        [Test] // A null override result cannot unbox to int — the woven guard must throw an
               // actionable error instead of the opaque NullReferenceException of a bare unbox.
        public void ValueReturning_NullOverrideResult_ThrowsActionable()
        {
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(NullResultWeaveTarget).GetMethod(nameof(NullResultWeaveTarget.Tick)),
                new CodeReloadAttribute());
            var ok = CodeReloadRegistry.RegisterInterpreterMethodOverride(
                "NullResultWeaveTarget.Tick", (instance, args) => null, out var reason);
            Assert.IsTrue(ok, reason);

            var t = new NullResultWeaveTarget();
            var ex = Assert.Throws<System.InvalidOperationException>(() => t.Tick());
            StringAssert.Contains("NullResultWeaveTarget.Tick", ex.Message);
            StringAssert.Contains("returned null", ex.Message);
            Assert.AreEqual(0, t.ticks, "Original body must not run after the override dispatched");
        }

        [Test]
        public void WithThrowingOverride_PropagatesOriginalException()
        {
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(WeaveTarget).GetMethod(nameof(WeaveTarget.Tick)),
                new CodeReloadAttribute());

            var registered = CodeReloadRegistry.RegisterMethodOverride(
                typeof(ExitGuiWeaveOverrides).GetMethod(nameof(ExitGuiWeaveOverrides.Tick)),
                new CodeReloadOverrideMethodAttribute("WeaveTarget.Tick"),
                typeof(ExitGuiWeaveOverrides),
                out var reason);

            Assert.IsTrue(registered, reason);

            var t = new WeaveTarget();
            var ex = Assert.Throws<ExitGUIException>(() => t.Tick(),
                "The override body's exception must propagate with its type intact, not be " +
                "swallowed into a fallback (IMGUI catches ExitGUIException by type)");

            Assert.IsTrue(t.overrideRan, "The override ran up to the throw");
            Assert.AreEqual(0, t.ticks,
                "The original body must NOT run as a fallback when the override body throws");
            StringAssert.Contains(nameof(ExitGuiWeaveOverrides), ex.StackTrace,
                "ExceptionDispatchInfo must preserve the original throw-site frames");
        }

        [Test]
        public void Weaver_IgnoresForeignAttributeWithSameSimpleName()
        {
            // Even with a reloadable registration and a bound override in place, a method tagged
            // with FakeReload.CodeReloadAttribute must run its original body: the weaver matches the
            // real Unity.Pipeline.CodeReload.CodeReloadAttribute by full name and never weaves a
            // dispatch prologue for the impostor.
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(ForeignWeaveTarget).GetMethod(nameof(ForeignWeaveTarget.Tick)),
                new CodeReloadAttribute());

            var registered = CodeReloadRegistry.RegisterMethodOverride(
                typeof(ForeignWeaveOverrides).GetMethod(nameof(ForeignWeaveOverrides.Tick)),
                new CodeReloadOverrideMethodAttribute("ForeignWeaveTarget.Tick"),
                typeof(ForeignWeaveOverrides),
                out var reason);

            Assert.IsTrue(registered, reason);

            var t = new ForeignWeaveTarget();
            t.Tick();

            Assert.IsFalse(t.overrideRan,
                "No dispatch prologue may be woven for a foreign attribute named CodeReloadAttribute");
            Assert.AreEqual(1, t.ticks, "Original body should run");
        }

        // -------- semantic transform + accessibility --------

        [Test]
        public void Transform_QualifiesInstanceMembers_LeavesLocalsAndStatics()
        {
            var output = SourceCodeTransformer.TransformMethodBodies(
                Bodies("Tick"), "Demo", new Dictionary<string, MethodSignatureInfo>(), PublicSource);

            StringAssert.Contains("[CodeReloadOverrideMethod(\"Demo.Tick\")]", output);
            StringAssert.Contains("public static void Tick(Demo instance", output);

            // Instance members are qualified.
            StringAssert.Contains("instance.transform", output);
            StringAssert.Contains("instance.speed", output);

            // Statics and locals are left alone.
            StringAssert.Contains("Time.deltaTime", output);
            StringAssert.Contains("Vector3.right", output);
            StringAssert.DoesNotContain("instance.dt", output);
            StringAssert.DoesNotContain("instance.Time", output);
            StringAssert.DoesNotContain("instance.Vector3", output);
        }

        private const string InterpolatedSource = @"
using UnityEngine;
public class Interp : MonoBehaviour
{
    public int hp;
    void Tick() { Debug.Log($""hp={hp}""); }
}";

        [Test] // The interpreter can't run the compiler's interpolated-string lowering.
        public void Transform_RewritesInterpolation_ForInterpreterBackend()
        {
            var output = SourceCodeTransformer.TransformMethodBodies(
                Bodies("Tick"), "Interp", new Dictionary<string, MethodSignatureInfo>(),
                InterpolatedSource);

            StringAssert.Contains("string.Format(", output);
            StringAssert.DoesNotContain("$\"", output);
        }

        [Test] // The Assembly.Load backend compiles interpolations natively — no rewrite.
        public void Transform_KeepsInterpolation_ForCompiledBackend()
        {
            var output = SourceCodeTransformer.TransformMethodBodies(
                Bodies("Tick"), "Interp", new Dictionary<string, MethodSignatureInfo>(),
                InterpolatedSource, rewriteInterpolations: false);

            StringAssert.Contains("$\"", output);
            StringAssert.DoesNotContain("string.Format(", output);
        }

        // -------- nested-type gate --------

        private const string NestedOnlySource = @"
using Unity.Pipeline.CodeReload;
namespace Unity.Pipeline.Tests.Editor
{
    public class NestedOnlyOuter
    {
        public class Inner
        {
            [CodeReload]
            public void Boom() { }
        }
    }
}";

        private const string MixedNestedSource = @"
using Unity.Pipeline.CodeReload;
namespace Unity.Pipeline.Tests.Editor
{
    public class NestedReloadProbe
    {
        public int ticks;

        [CodeReload]
        public void Tick()
        {
            ticks = 2; // edited body
        }

        public class Inner
        {
            public int booms;

            [CodeReload]
            public void Boom()
            {
                booms = 1; // original body
            }
        }
    }
}";

        [Test] // A file whose only [CodeReload] method is nested must fail with the real reason,
               // not "No [CodeReload] methods found".
        public void Reload_NestedOnlyCodeReload_FailsLoudly()
        {
            var path = Path.Combine(Application.temporaryCachePath, "NestedOnlyOuter_edit.cs");
            try
            {
                File.WriteAllText(path, NestedOnlySource);
                var result = InPlaceReloadProcessor.ProcessSourceFileOnMainThread(path);

                Assert.IsFalse(result.Success);
                StringAssert.Contains("'Boom'", result.ErrorMessage);
                StringAssert.Contains("nested type 'NestedOnlyOuter.Inner'", result.ErrorMessage);
                StringAssert.DoesNotContain("No [CodeReload] methods found", result.ErrorMessage);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test] // The woven-but-unreloadable nested method must surface as a diagnostic while the
               // top-level method still reloads.
        public void Reload_NestedCodeReload_SkippedLoudly_OuterStillReloads()
        {
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(NestedReloadProbe).GetMethod(nameof(NestedReloadProbe.Tick)),
                new CodeReloadAttribute());

            var path = Path.Combine(Application.temporaryCachePath, "NestedReloadProbe_edit.cs");
            try
            {
                File.WriteAllText(path, MixedNestedSource);
                var result = InPlaceReloadProcessor.ProcessSourceFileOnMainThread(path);

                Assert.IsTrue(result.Success, result.ErrorMessage);
                CollectionAssert.AreEquivalent(new[] { "NestedReloadProbe.Tick" }, result.RegisteredMethods);
                Assert.IsTrue(result.CompilationDiagnostics.Any(d =>
                        d.Contains("'Boom'") && d.Contains("nested type 'NestedReloadProbe.Inner'")),
                    "Nested [CodeReload] must be skipped with an explicit diagnostic; got: " +
                    string.Join(" | ", result.CompilationDiagnostics));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Transform_QualifiesInheritedStaticsAndNestedTypes()
        {
            // The override class has no base type and isn't nested in Scoped, so bare `FindObjectsByType`
            // (inherited static from UnityEngine.Object) and bare `Mode` (nested enum) would not bind.
            // The transform must qualify them with their containing type so the override compiles without
            // any change to the user's source.
            var output = SourceCodeTransformer.TransformMethodBodies(
                Bodies("Tick"), "Scoped", new Dictionary<string, MethodSignatureInfo>(), ScopedSource);

            StringAssert.Contains("global::UnityEngine.Object.FindObjectsByType<Transform>", output);
            StringAssert.Contains("global::Scoped.Mode.Run", output);
            StringAssert.Contains("global::Scoped.Mode.Idle", output);
            // The instance field is still qualified with instance, not treated as a static/type.
            StringAssert.Contains("instance.m_Mode", output);
            // The bare forms must be gone (no unqualified name left to fail resolution).
            StringAssert.DoesNotContain("= FindObjectsByType<", output);
        }

        [Test]
        public void Transform_QualifiesNestedTypesInTypePositions()
        {
            var output = SourceCodeTransformer.TransformMethodBodies(
                Bodies("Tick"), "Typed", new Dictionary<string, MethodSignatureInfo>(), TypePositionSource);

            // Type positions: foreach declaration, local declaration, array creation.
            StringAssert.Contains("global::Typed.Handler h", output);
            StringAssert.Contains("global::Typed.Mode m", output);
            StringAssert.Contains("new global::Typed.Mode[2]", output);
            // Expression position still qualifies via member access.
            StringAssert.Contains("global::Typed.Mode.Idle", output);
            StringAssert.Contains("instance.OnChanged", output);
        }

        [Test]
        public void Transform_WithLineDirectives_MapsBodyToOriginalFile()
        {
            // The body's opening brace sits on line 11 of PublicSource.
            var path = @"C:\proj\Demo.cs";
            var output = SourceCodeTransformer.TransformMethodBodies(
                Bodies("Tick"), "Demo", new Dictionary<string, MethodSignatureInfo>(), PublicSource,
                emitLineDirectives: true, originalFilePath: path);

            // #line maps the body back to the original file (backslashes escaped for the literal),
            // bracketed by #line hidden so the generated scaffolding isn't attributed to user code.
            StringAssert.Contains("#line 11 \"C:\\\\proj\\\\Demo.cs\"", output);
            StringAssert.Contains("#line hidden", output);

            // The instance qualification still happens in this mode.
            StringAssert.Contains("instance.transform", output);
            StringAssert.Contains("instance.speed", output);
        }

        [Test]
        public void Transform_WithoutLineDirectives_EmitsNoLineDirectives()
        {
            var output = SourceCodeTransformer.TransformMethodBodies(
                Bodies("Tick"), "Demo", new Dictionary<string, MethodSignatureInfo>(), PublicSource);

            StringAssert.DoesNotContain("#line", output);
        }

        // `var` binds to the inferred type. When that type is nested in the reloaded class the
        // qualifier must leave the keyword alone instead of emitting `Outer.var` (CubeFlipr:
        // `var inp = new BufferedInput { ... }` became `CubeMover.var inp`). Same for an alias.
        private const string InferredNestedSource = @"
using UnityEngine;
using Unity.Pipeline.CodeReload;
using Alias = Inferred.Payload;

public class Inferred : MonoBehaviour
{
    public struct Payload { public int dir; }
    public Payload? pending;

    static bool TryGet(out Payload p) { p = default; return true; }

    [CodeReload]
    void Tick()
    {
        var inp = new Payload { dir = 1 };
        pending = inp;
        var again = pending.Value;
        TryGet(out var got);
        Alias aliased = got;
        foreach (var each in new Payload[1]) { }
    }
}";

        [Test]
        public void Transform_LeavesVarAndAliasesAlone_WhenInferredTypeIsNested()
        {
            var output = SourceCodeTransformer.TransformMethodBodies(
                Bodies("Tick"), "Inferred", new Dictionary<string, MethodSignatureInfo>(), InferredNestedSource);

            StringAssert.DoesNotContain(".var", output);
            StringAssert.DoesNotContain("Inferred.Alias", output);
            StringAssert.Contains("var inp = new global::Inferred.Payload", output);
            StringAssert.Contains("var again = instance.pending.Value", output);
            StringAssert.Contains("TryGet(out var got)", output);
            StringAssert.Contains("foreach (var each in new global::Inferred.Payload[1])", output);
        }

        [Test] // A compile error in the generated override must point at the user's file AND show
               // the generated line — the rewritten source is what actually failed to compile.
        public void ReloadFileInPlace_CompileError_ReportsUserLineAndGeneratedSource()
        {
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(InPlaceReloadProbe).GetMethod(nameof(InPlaceReloadProbe.Compute)),
                new CodeReloadAttribute());

            var editedSource = @"
using Unity.Pipeline.CodeReload;
namespace Unity.Pipeline.Tests.Editor
{
    public class InPlaceReloadProbe
    {
        public int baseline;
        public int hotValue;

        [CodeReload]
        public void Compute()
        {
            hotValue = NoSuchSymbol;
        }
    }
}";
            var path = Path.Combine(Application.temporaryCachePath, "InPlaceReloadProbe_broken.cs");
            File.WriteAllText(path, editedSource);
            LogAssert.Expect(LogType.Error, new Regex("In-place reload compilation failed"));

            try
            {
                var result = InPlaceReloadProcessor.ProcessSourceFileOnMainThread(path, useInterpreter: true);

                Assert.IsFalse(result.Success);
                var all = string.Join(" | ", result.CompilationDiagnostics);
                var diag = result.CompilationDiagnostics.FirstOrDefault(d => d.Contains("CS0103"));
                Assert.IsNotNull(diag, "expected CS0103, got: " + all);
                // The statement sits on line 13 of the edited file, reached through the #line mapping.
                StringAssert.Contains("InPlaceReloadProbe_broken.cs(13): error CS0103", diag);
                StringAssert.Contains("generated line", diag);
                StringAssert.Contains("instance.hotValue = NoSuchSymbol;", diag);

                // The whole generated source is written out for inspection.
                const string marker = "generated source: ";
                StringAssert.Contains(marker, result.ErrorMessage);
                var dump = result.ErrorMessage.Substring(result.ErrorMessage.IndexOf(marker) + marker.Length).TrimEnd(')');
                Assert.IsTrue(File.Exists(dump), "dump not found: " + dump);
                StringAssert.Contains("NoSuchSymbol", File.ReadAllText(dump));
                File.Delete(dump);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        // -------- end to end (full reload_file pipeline) --------

        [Test]
        public void ReloadFileInPlace_EditedBody_AppliesViaWovenDispatch()
        {
            // Auto-discovery would do this at play start; register the target explicitly for the test.
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(InPlaceReloadProbe).GetMethod(nameof(InPlaceReloadProbe.Compute)),
                new CodeReloadAttribute());

            // The "edited" source: Compute now writes hotValue instead of baseline.
            var editedSource = @"
using Unity.Pipeline.CodeReload;
namespace Unity.Pipeline.Tests.Editor
{
    public class InPlaceReloadProbe
    {
        public int baseline;
        public int hotValue;

        [CodeReload]
        public void Compute()
        {
            hotValue = 99;
        }
    }
}";
            var path = Path.Combine(Application.temporaryCachePath, "InPlaceReloadProbe_edit.cs");
            File.WriteAllText(path, editedSource);

            try
            {
                var response = CodeReloadCommands.ReloadFile(path);
                Assert.IsTrue(response.Success, $"reload_file should succeed. Error: {response.ErrorDetails}");

                var probe = new InPlaceReloadProbe();
                probe.Compute();

                Assert.AreEqual(99, probe.hotValue, "Edited [CodeReload] body should run via the woven dispatch");
                Assert.AreEqual(0, probe.baseline, "Original body should be skipped when the override runs");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void ReloadFileInPlace_EmptyBody_AppliesNoOpOverride()
        {
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(InPlaceEmptyBodyProbe).GetMethod(nameof(InPlaceEmptyBodyProbe.Compute)),
                new CodeReloadAttribute());

            // Emptying a [CodeReload] body is a legitimate edit (e.g. deleting a Debug.Log): the
            // reload must bind a no-op override, not fail with "No [CodeReload] methods found"
            // (which would leave the previous override live).
            var editedSource = @"
using Unity.Pipeline.CodeReload;
namespace Unity.Pipeline.Tests.Editor
{
    public class InPlaceEmptyBodyProbe
    {
        public int computedValue;

        [CodeReload]
        public void Compute()
        {
        }
    }
}";
            var path = Path.Combine(Application.temporaryCachePath, "InPlaceEmptyBodyProbe_edit.cs");
            File.WriteAllText(path, editedSource);

            try
            {
                var response = CodeReloadCommands.ReloadFile(path);
                Assert.IsTrue(response.Success,
                    $"reload of an emptied [CodeReload] body should succeed. Error: {response.ErrorDetails}");

                var probe = new InPlaceEmptyBodyProbe();
                probe.Compute();

                Assert.AreEqual(0, probe.computedValue,
                    "Empty override should replace the original body (which sets 42)");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void ReloadFileInPlace_PrivateFieldAccess_FailsValidationUpFront()
        {
            // KNOWN LIMITATION of the Assembly.Load backend (the interpreter backend supports this —
            // see IlInterpreterCodeReloadInterpreterTests). Roslyn's IgnoreAccessibility only disables
            // COMPILE-time access checks; Unity's Mono (6.13) still enforces field accessibility when
            // JIT-ing the loaded override IL, and does not honor [assembly: IgnoresAccessChecksTo]
            // (verified empirically) — the failure would only surface on first dispatch as a
            // FieldAccessException followed by a silent fallback to the original body. So
            // AccessibilityValidator rejects the reload UP FRONT with an actionable error. If the
            // runtime ever gains private access, drop the gate (enforcePublicAccess) and flip this
            // test to assert Secret == 77.
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(InPlacePrivateReloadProbe).GetMethod(nameof(InPlacePrivateReloadProbe.Compute)),
                new CodeReloadAttribute());

            var editedSource = @"
using Unity.Pipeline.CodeReload;
namespace Unity.Pipeline.Tests.Editor
{
    public class InPlacePrivateReloadProbe
    {
        private int m_Secret;
        public int Secret => m_Secret;

        [CodeReload]
        public void Compute()
        {
            m_Secret = 77;
        }
    }
}";
            var path = Path.Combine(Application.temporaryCachePath, "InPlacePrivateReloadProbe_edit.cs");
            File.WriteAllText(path, editedSource);

            try
            {
                var response = CodeReloadCommands.ReloadFile(path);

                Assert.IsFalse(response.Success,
                    "reload_file on the compiled backend must reject non-public access up front — " +
                    "Mono would JIT-enforce accessibility at dispatch and silently fall back.");
                StringAssert.Contains("m_Secret", response.ErrorDetails,
                    "The validation error should name the offending member");
                StringAssert.Contains("reload_file_editor_interpreter", response.ErrorDetails,
                    "The validation error should point at the interpreter backend as the fix");

                var probe = new InPlacePrivateReloadProbe();
                probe.Compute();

                Assert.AreEqual(1, probe.Secret,
                    "The rejected reload must leave the original body (m_Secret = 1) in effect");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void ReloadFileInPlace_WithPdb_StillAppliesViaWovenDispatch()
        {
            // Functional parity: the --pdb path (#line directives + portable PDB, unoptimized) must
            // still compile and dispatch exactly like the default path.
            CodeReloadRegistry.RegisterReloadableMethod(
                typeof(InPlaceReloadProbe).GetMethod(nameof(InPlaceReloadProbe.Compute)),
                new CodeReloadAttribute());

            var editedSource = @"
using Unity.Pipeline.CodeReload;
namespace Unity.Pipeline.Tests.Editor
{
    public class InPlaceReloadProbe
    {
        public int baseline;
        public int hotValue;

        [CodeReload]
        public void Compute()
        {
            hotValue = 99;
        }
    }
}";
            var path = Path.Combine(Application.temporaryCachePath, "InPlaceReloadProbe_pdb_edit.cs");
            File.WriteAllText(path, editedSource);

            try
            {
                var response = CodeReloadCommands.ReloadFile(path, pdb: true);
                Assert.IsTrue(response.Success, $"reload_file --pdb should succeed. Error: {response.ErrorDetails}");

                var probe = new InPlaceReloadProbe();
                probe.Compute();

                Assert.AreEqual(99, probe.hotValue, "Edited body should run via woven dispatch even with --pdb");
                Assert.AreEqual(0, probe.baseline, "Original body should be skipped when the override runs");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void CompileOverrideForPush_ExcludesOnCodeReloadCallback()
        {
            // [OnCodeReload] ends with "CodeReload", so a suffix-based attribute match extracts the
            // callback as a reload target. Pushed first (declaration order), an unregistered name
            // fails the player-side type resolution and the WHOLE file is skipped — the Pong sample
            // failure. Only [CodeReload] methods may be pushed.
            var editedSource = @"
using Unity.Pipeline.CodeReload;
namespace Unity.Pipeline.Tests.Editor
{
    public class OnCodeReloadPushProbe
    {
        public int ticked;
        public string reloadMarker = ""none"";

        [OnCodeReload]
        public void OnReloaded()
        {
            reloadMarker = ""reloaded-edited"";
        }

        [CodeReload]
        public void Tick()
        {
            ticked = 2;
        }
    }
}";
            var path = Path.Combine(Application.temporaryCachePath, "OnCodeReloadPushProbe_edit.cs");
            File.WriteAllText(path, editedSource);

            try
            {
                var result = InPlaceReloadProcessor.CompileOverrideForPush(path);

                Assert.IsTrue(result.Success, $"Push compile should succeed. Error: {result.Error}");
                CollectionAssert.Contains(result.MethodNames, "Tick",
                    "[CodeReload] methods must be pushed.");
                CollectionAssert.DoesNotContain(result.MethodNames, "OnReloaded",
                    "[OnCodeReload] is a post-reload callback, not a reload target — pushing it makes " +
                    "the player skip the entire file as unregistered.");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

    }
}
