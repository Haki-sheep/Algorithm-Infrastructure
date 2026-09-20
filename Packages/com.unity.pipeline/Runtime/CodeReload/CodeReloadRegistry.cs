using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Unity.Pipeline.Threading;
using UnityEngine;

namespace Unity.Pipeline.CodeReload
{
    /// <summary>
    /// Central registry for managing code reload method and component overrides.
    /// Handles runtime method resolution, override registration, and fallback logic.
    /// Thread-safe for runtime code reload operations.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT [NoAutoStaticsCleanup]: RegisterReloadableMethod appends without deduplicating,
    /// and RuntimePipelineDriver.Awake re-registers on every Play Mode entry, so opting out would
    /// accumulate duplicates across FEPM sessions. Let AutoStaticsCleanup reset these statics instead.
    /// </remarks>
    public static class CodeReloadRegistry
    {
        /// <summary>
        /// Dispatcher used to marshal main-thread-required code-reload overrides to the main thread.
        /// Injected by RuntimePipelineDriver (= its server's dispatcher). Null = run on the
        /// current thread (no marshaling).
        /// </summary>
        public static Dispatcher Dispatcher { get; set; }

        /// <summary>
        /// Absolute roots a running Player is allowed to code reload source files from (Assets folder
        /// + loaded package locations). Baked into RuntimePipelineBuildInfo at build time (a running
        /// Player cannot resolve the project layout) and published here when the runtime server
        /// starts, so reload commands can validate incoming file paths. Null until published.
        /// </summary>
        public static IReadOnlyList<string> AllowedReloadRoots { get; set; }

        // Facts established at domain-reload / Play Mode entry that only matter once the user
        // actually reloads (target count, receiver state, connected players). Collected here and
        // printed as ONE console line on the first override registration ATTEMPT of the session —
        // attempt, not success, so a reload against an empty registry still gets the summary next
        // to its skip reasons. Statics reset with the domain, so each session collects afresh.
        private static readonly List<string> s_FirstReloadNotes = new();
        private static bool s_FirstReloadNotesFlushed;

        /// <summary>
        /// Record a startup fact for the "first reload this session" summary line. Once that line
        /// has been printed, a late note (e.g. a player connecting mid-session) is logged directly.
        /// </summary>
        /// <param name="note">Short clause, no trailing period, e.g. "3 reloadable method(s) registered".</param>
        public static void NoteForFirstReload(string note)
        {
            if (string.IsNullOrEmpty(note)) return;
            lock (s_FirstReloadNotes)
            {
                if (s_FirstReloadNotesFlushed)
                {
                    Debug.Log($"CodeReload: {note}.");
                    return;
                }
                s_FirstReloadNotes.Add(note);
            }
        }

        private static void FlushFirstReloadNotes()
        {
            string line = null;
            lock (s_FirstReloadNotes)
            {
                if (s_FirstReloadNotesFlushed) return;
                s_FirstReloadNotesFlushed = true;
                if (s_FirstReloadNotes.Count > 0)
                    line = "CodeReload: first reload this session — " + string.Join("; ", s_FirstReloadNotes) + ".";
                s_FirstReloadNotes.Clear();
            }
            if (line != null)
                Debug.Log(line);
        }

        // Thread-safe collections for runtime code reload switching
        private static readonly ConcurrentDictionary<string, MethodOverride> m_MethodOverrides = new();
        private static readonly ConcurrentDictionary<string, List<MethodInfo>> m_ReloadableMethods = new();
        private static readonly ConcurrentDictionary<string, Type> m_LoadedCodeReloadTypes = new();

        /// <summary>
        /// Raised after a method override is registered or removed (once per method — a
        /// multi-method reload fires it several times back to back, so debounce reactions).
        /// Lets UI that reflects reloadable code (e.g. a debug panel overlay) rebuild when a
        /// compiled method's body is swapped.
        ///
        /// INTERNAL for now — deliberately held back from the first public API surface; make it
        /// public when the introspection surface ships (external consumers poll
        /// GetStats().ActiveOverrideCount meanwhile). The raise sites stay: register, unregister
        /// (revert), and ClearAllOverrides — the transitions a subscriber needs.
        /// </summary>
        internal static event Action OverridesChanged;

        private static void RaiseOverridesChanged()
        {
            try
            {
                OverridesChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"CodeReload: OverridesChanged handler threw: {ex}");
            }
        }

        /// <summary>
        /// Register a method as reloadable. Called during discovery phase.
        /// </summary>
        /// <param name="method">The method marked with <see cref="CodeReloadAttribute"/>.</param>
        /// <param name="attribute">The attribute instance (supplies the override id and main-thread requirement).</param>
        public static void RegisterReloadableMethod(MethodInfo method, CodeReloadAttribute attribute)
        {
            var methodId = GetMethodId(method, attribute.Id);

            if (!m_ReloadableMethods.ContainsKey(methodId))
            {
                m_ReloadableMethods[methodId] = new List<MethodInfo>();
            }

            m_ReloadableMethods[methodId].Add(method);
        }

        /// <summary>
        /// Register a code reload method override from compiled code reload assembly.
        /// Returns true if the override was registered, false if it was skipped.
        /// </summary>
        /// <param name="overrideMethod">The override method marked with <see cref="CodeReloadOverrideMethodAttribute"/>.</param>
        /// <param name="attribute">The attribute instance (identifies the target method).</param>
        /// <param name="sourceType">The type the override method was compiled into.</param>
        /// <returns>True if the override was registered.</returns>
        public static bool RegisterMethodOverride(MethodInfo overrideMethod, CodeReloadOverrideMethodAttribute attribute, Type sourceType)
        {
            return RegisterMethodOverride(overrideMethod, attribute, sourceType, out _);
        }

        /// <summary>
        /// Register a code reload method override from compiled code reload assembly.
        /// Returns true if the override was registered, false if it was skipped (target not
        /// reloadable or signature mismatch). The out parameter carries a user-facing reason
        /// when registration is skipped.
        /// </summary>
        /// <param name="overrideMethod">The override method marked with <see cref="CodeReloadOverrideMethodAttribute"/>.</param>
        /// <param name="attribute">The attribute instance (identifies the target method).</param>
        /// <param name="sourceType">The type the override method was compiled into.</param>
        /// <param name="skipReason">A user-facing reason when registration is skipped, or null on success.</param>
        /// <returns>True if the override was registered.</returns>
        public static bool RegisterMethodOverride(MethodInfo overrideMethod, CodeReloadOverrideMethodAttribute attribute, Type sourceType, out string skipReason)
        {
            FlushFirstReloadNotes();
            skipReason = null;
            var targetMethodId = attribute.TargetMethodId;

            // Validate that target method exists and is reloadable
            if (!m_ReloadableMethods.ContainsKey(targetMethodId))
            {
                Debug.LogWarning($"CodeReload: Target method '{targetMethodId}' not found or not marked [CodeReload]");
                skipReason = $"target '{targetMethodId}' is not registered as a reloadable [CodeReload] method. " +
                    "Ensure the component is in the scene and in play mode (RuntimePipelineDriver auto-discovers it).";
                return false;
            }

            var originalMethods = m_ReloadableMethods[targetMethodId];
            if (!originalMethods.Any())
            {
                Debug.LogWarning($"CodeReload: No original methods registered for '{targetMethodId}'");
                skipReason = $"no original methods registered for '{targetMethodId}'.";
                return false;
            }

            // Validate signature compatibility (basic check - instance parameter + matching return type)
            var originalMethod = originalMethods.First();
            if (!ValidateSignatureCompatibility(originalMethod, overrideMethod))
            {
                Debug.LogError($"CodeReload: Signature mismatch for '{targetMethodId}'. Override method must have instance parameter as first argument.");
                skipReason = $"signature mismatch for '{targetMethodId}'. The override must be " +
                    $"'public static {originalMethod.ReturnType.Name} {overrideMethod.Name}" +
                    $"({originalMethod.DeclaringType?.Name} instance, ...)'.";
                return false;
            }

            var methodOverride = new MethodOverride
            {
                TargetMethodId = targetMethodId,
                OverrideMethod = overrideMethod,
                SourceType = sourceType,
                RequireMainThread = GetMainThreadRequirement(originalMethod),
                Description = attribute.Description
            };

            m_MethodOverrides[targetMethodId] = methodOverride;
            RaiseOverridesChanged();
            return true;
        }

        /// <summary>
        /// Register an IL2CPP-safe override that dispatches through <paramref name="interpreterInvoke"/>
        /// (a IlInterpreter interpreter invoke) instead of a loaded <see cref="MethodInfo"/>. The target
        /// must already be registered reloadable (same precondition as <see cref="RegisterMethodOverride"/>).
        /// </summary>
        internal static bool RegisterInterpreterMethodOverride(
            string targetMethodId, Func<object, object[], object> interpreterInvoke, out string skipReason)
        {
            FlushFirstReloadNotes();
            skipReason = null;

            if (!m_ReloadableMethods.TryGetValue(targetMethodId, out var originalMethods) || originalMethods.Count == 0)
            {
                skipReason = $"target '{targetMethodId}' is not registered as a reloadable [CodeReload] method. " +
                    "Ensure the component is in the scene and in play mode (RuntimePipelineDriver auto-discovers it).";
                return false;
            }

            m_MethodOverrides[targetMethodId] = new MethodOverride
            {
                TargetMethodId = targetMethodId,
                InterpreterInvoke = interpreterInvoke,
                RequireMainThread = GetMainThreadRequirement(originalMethods.First()),
            };

            Debug.Log($"CodeReload: Registered interpreter override for '{targetMethodId}'");
            RaiseOverridesChanged();
            return true;
        }

        /// <summary>
        /// Remove one active override so the woven prologue falls through to the original compiled
        /// body again. Used when a reloaded file's method matches the compiled baseline again (the
        /// edit was reverted) — keeping the override would leave a correct but slower interpreter
        /// dispatch in place. Returns false when no override was registered for the id.
        /// </summary>
        internal static bool UnregisterMethodOverride(string targetMethodId)
        {
            if (!m_MethodOverrides.TryRemove(targetMethodId, out _))
                return false;
            Debug.Log($"CodeReload: Removed override for '{targetMethodId}' — original compiled body is active again");
            RaiseOverridesChanged();
            return true;
        }

        /// <summary>
        /// After a reload binds, invoke every <c>[OnCodeReload]</c> method on all live instances of the
        /// types that own the given reloaded methods. Lets a component re-init state when its code is
        /// swapped. Parameterless instance methods only; exceptions are logged, not propagated.
        /// </summary>
        internal static void InvokeReloadCallbacks(IEnumerable<string> reloadedTargetMethodIds)
        {
            if (reloadedTargetMethodIds == null) return;

            var types = new HashSet<Type>();
            foreach (var id in reloadedTargetMethodIds)
            {
                if (m_ReloadableMethods.TryGetValue(id, out var methods) && methods.Count > 0
                    && methods[0].DeclaringType != null)
                {
                    types.Add(methods[0].DeclaringType);
                }
            }

            foreach (var type in types)
                InvokeReloadCallbacksOnType(type);
        }

        private static void InvokeReloadCallbacksOnType(Type type)
        {
            var callbacks = type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.GetParameters().Length == 0 && m.GetCustomAttribute<OnCodeReloadAttribute>() != null)
                .ToList();
            if (callbacks.Count == 0) return;

            if (!typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                Debug.LogWarning($"CodeReload: [OnCodeReload] on '{type.Name}' ignored — only " +
                    "UnityEngine.Object-derived types are supported (instances are found via FindObjectsByType).");
                return;
            }

#if UNITY_6000_5_OR_NEWER
            var instances = UnityEngine.Object.FindObjectsByType(type);
#else
            var instances = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
#endif
            foreach (var instance in instances)
            {
                foreach (var cb in callbacks)
                {
                    try
                    {
                        cb.Invoke(instance, null);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"CodeReload: [OnCodeReload] {type.Name}.{cb.Name} threw: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>True when at least one [CodeReload] target has been registered this session.</summary>
        internal static bool HasReloadableMethods => m_ReloadableMethods.Count > 0;

        /// <summary>
        /// Resolve the runtime <see cref="Type"/> that declares a reloadable target method, so an
        /// interpreter host binding can be built for it. Returns false if not registered.
        /// </summary>
        internal static bool TryGetReloadableDeclaringType(string targetMethodId, out Type declaringType)
        {
            declaringType = null;
            if (m_ReloadableMethods.TryGetValue(targetMethodId, out var methods) && methods.Count > 0)
            {
                declaringType = methods[0].DeclaringType;
                return declaringType != null;
            }
            return false;
        }

        /// <summary>
        /// Attempt to invoke code reload override if available, otherwise invoke original method.
        /// Returns true if code reload override was invoked, false if original method should be called.
        /// </summary>
        /// <typeparam name="T">The instance's type.</typeparam>
        /// <param name="methodId">Method identifier, "TypeName.MethodName".</param>
        /// <param name="instance">The instance to invoke the override on.</param>
        /// <param name="parameters">The original call's arguments, boxed.</param>
        /// <returns>True if a code reload override was invoked.</returns>
        public static bool TryInvokeCodeReload<T>(string methodId, T instance, object[] parameters = null)
        {
            return TryInvokeCodeReload(methodId, (object)instance, parameters);
        }

        /// <summary>
        /// Cheap, allocation-free check for an active override. The woven prologue calls this
        /// before building the boxed argument array, so a woven method with no active override
        /// allocates nothing per call.
        /// </summary>
        /// <param name="methodId">Method identifier, "TypeName.MethodName".</param>
        /// <returns>True if an override is currently registered for <paramref name="methodId"/>.</returns>
        public static bool HasOverride(string methodId) => m_MethodOverrides.ContainsKey(methodId);

        /// <summary>
        /// Non-generic dispatch entry point. This is the method woven into [CodeReload]
        /// methods at compile time (a non-generic signature keeps the injected IL simple).
        /// Returns true if a code reload override was invoked, false if the original body should run.
        /// </summary>
        /// <param name="methodId">Method identifier, "TypeName.MethodName".</param>
        /// <param name="instance">The instance to invoke the override on.</param>
        /// <param name="parameters">The original call's arguments, boxed.</param>
        /// <returns>True if a code reload override was invoked.</returns>
        public static bool TryInvokeCodeReload(string methodId, object instance, object[] parameters = null)
            => TryInvokeCodeReloadResult(methodId, instance, parameters, out _);

        /// <summary>
        /// Called by the woven prologue when an override returned null for a method whose return
        /// type is a value type — unboxing null would throw an opaque NullReferenceException at
        /// the call site. Public only for woven code; not meant to be called by hand.
        /// </summary>
        /// <param name="methodId">Method identifier, "TypeName.MethodName", named in the thrown message.</param>
        public static void ThrowOverrideReturnedNull(string methodId)
        {
            throw new InvalidOperationException(
                $"CodeReload: the override for '{methodId}' returned null, but the method returns " +
                "a value type, so there is no value to unbox. Fix the override to return a value, " +
                "or revert the reload.");
        }

        /// <summary>
        /// Value-returning dispatch: woven into value-returning [CodeReload] methods. Like
        /// <see cref="TryInvokeCodeReload(string, object, object[])"/> but also hands back the
        /// override's (boxed) return value in <paramref name="result"/>. Returns true if an override
        /// ran (and the woven prologue should return <paramref name="result"/>), false if the
        /// original body should run.
        /// </summary>
        /// <param name="methodId">Method identifier, "TypeName.MethodName".</param>
        /// <param name="instance">The instance to invoke the override on.</param>
        /// <param name="parameters">The original call's arguments, boxed.</param>
        /// <param name="result">The override's (boxed) return value, if one ran.</param>
        /// <returns>True if a code reload override was invoked.</returns>
        public static bool TryInvokeCodeReloadResult(string methodId, object instance, object[] parameters, out object result)
        {
            result = null;
            if (!m_MethodOverrides.TryGetValue(methodId, out var methodOverride))
            {
                return false; // No code reload override available
            }

            CodeReloadActivity.CountOverrideCall(); // feeds the overlay's calls/s rate

            try
            {
                result = InvokeOverrideMarshalled(methodOverride, instance, parameters);
                return true;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                // Exception from mono backend - rethrow for cases like imgui ExitGui
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // unreachable
            }
            catch (IlInterpreter.ScriptRuntimeException ex) when (ex.InnerException != null)
            {
                // Exception from IlInterpreter backend - rethrow for cases like imgui ExitGui.
                // The inner exception alone has no script location; log the interpreter's
                // message (which carries "at line N (file)") first so the failure is traceable.
                if (!(ex.InnerException is ExitGUIException))
                    Debug.LogError($"CodeReload: override '{methodId}' threw {ex.Message}");
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // unreachable
            }
            catch (Exception ex)
            {
                // Invocation infrastructure failed (interpreter-detected error, reflection arg
                // marshaling): log and fall back to the original body.
                Debug.LogError($"CodeReload: Error invoking override for '{methodId}': " +
                    $"{ex.GetType().Name}: {ex.Message}");
                Debug.LogError($"CodeReload: Stack trace: {ex.StackTrace}");
                result = null;
                return false; // Fall back to original method
            }
        }

        /// <summary>
        /// Run the override, marshalling to the main thread when required, and return its (boxed)
        /// result. The common case is already on the main thread: invoke directly with no per-call
        /// closure allocation; only the rare cross-thread marshal pays for a closure.
        /// </summary>
        private static object InvokeOverrideMarshalled(MethodOverride methodOverride, object instance, object[] parameters)
        {
            if (methodOverride.RequireMainThread && Dispatcher != null && !Dispatcher.IsMainThread())
            {
                object marshalled = null;
                var mo = methodOverride;
                Dispatcher.Invoke(() => marshalled = InvokeOverride(mo, instance, parameters));
                return marshalled;
            }
            return InvokeOverride(methodOverride, instance, parameters);
        }

        /// <summary>
        /// Invoke the override: through the IlInterpreter interpreter (IL2CPP-safe; the delegate marshals
        /// args itself, so no array is built here) or via reflection on the loaded MethodInfo (which
        /// needs an instance-prepended argument array). Returns the (boxed) result, null for void.
        /// </summary>
        private static object InvokeOverride(MethodOverride methodOverride, object instance, object[] parameters)
        {
            if (methodOverride.InterpreterInvoke != null)
                return methodOverride.InterpreterInvoke(instance, parameters);

            var invokeParams = new object[(parameters?.Length ?? 0) + 1];
            invokeParams[0] = instance;
            if (parameters != null && parameters.Length > 0)
                Array.Copy(parameters, 0, invokeParams, 1, parameters.Length);
            return methodOverride.OverrideMethod.Invoke(null, invokeParams);
        }

        /// <summary>
        /// Register all methods in a type that have the [CodeReload] attribute.
        /// Uses reflection to discover and register them as reload targets.
        /// </summary>
        /// <param name="type">The type to scan for [CodeReload] methods.</param>
        public static void RegisterReloadableType(System.Type type)
        {
            if (type == null)
            {
                Debug.LogWarning("CodeReload: Cannot register null type");
                return;
            }

            int registeredCount = 0;

            // Get all instance methods (public and non-public)
            var instanceMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in instanceMethods)
            {
                var codeReloadAttr = method.GetCustomAttribute<CodeReloadAttribute>();
                if (codeReloadAttr != null)
                {
                    RegisterReloadableMethod(method, codeReloadAttr);
                    registeredCount++;
                }
            }

            // Get all static methods (public and non-public)
            var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var method in staticMethods)
            {
                var codeReloadAttr = method.GetCustomAttribute<CodeReloadAttribute>();
                if (codeReloadAttr != null)
                {
                    RegisterReloadableMethod(method, codeReloadAttr);
                    registeredCount++;
                }
            }

            if (registeredCount > 0)
            {
                Debug.Log($"CodeReload: Registered {registeredCount} reloadable methods from type {type.Name}");
            }
            else
            {
                Debug.Log($"CodeReload: No methods with [CodeReload] attribute found in type {type.Name}");
            }
        }

        /// <summary>
        /// Register a type from loaded code reload assembly for discovery scanning.
        /// </summary>
        /// <param name="type">The loaded code-reload assembly's type.</param>
        /// <param name="assemblyId">Identifier of the compiled code-reload assembly the type came from.</param>
        public static void RegisterCodeReloadType(Type type, string assemblyId)
        {
            var typeKey = $"{assemblyId}:{type.FullName}";
            m_LoadedCodeReloadTypes[typeKey] = type;
        }

        /// <summary>
        /// Clear all code reload overrides. Used by cleanup_codereload command.
        /// </summary>
        public static void ClearAllOverrides()
        {
            var overrideCount = m_MethodOverrides.Count;
            var typeCount = m_LoadedCodeReloadTypes.Count;

            m_MethodOverrides.Clear();
            m_LoadedCodeReloadTypes.Clear();
            if (overrideCount > 0)
                RaiseOverridesChanged();
        }

        /// <summary>
        /// Clear all registry state including reloadable methods.
        /// FOR TESTING ONLY - this should not be called in production code.
        /// </summary>
        public static void ClearAllForTesting()
        {
            var overrideCount = m_MethodOverrides.Count;
            var typeCount = m_LoadedCodeReloadTypes.Count;
            var reloadableCount = m_ReloadableMethods.Sum(kvp => kvp.Value.Count);

            m_MethodOverrides.Clear();
            m_LoadedCodeReloadTypes.Clear();
            m_ReloadableMethods.Clear();
        }

        /// <summary>
        /// Get statistics about current code reload state.
        /// </summary>
        /// <returns>Current reloadable/override/loaded-type counts.</returns>
        public static CodeReloadStats GetStats()
        {
            return new CodeReloadStats
            {
                ReloadableMethodCount = m_ReloadableMethods.Sum(kvp => kvp.Value.Count),
                ActiveOverrideCount = m_MethodOverrides.Count,
                LoadedTypeCount = m_LoadedCodeReloadTypes.Count,
                ReloadableMethodIds = m_ReloadableMethods.Keys.ToList(),
                ActiveOverrideIds = m_MethodOverrides.Keys.ToList()
            };
        }

        /// <summary>
        /// Generate method ID from MethodInfo and optional custom ID.
        /// Format: TypeName.MethodName or custom ID if provided.
        /// </summary>
        private static string GetMethodId(MethodInfo method, string customId = null)
        {
            if (!string.IsNullOrEmpty(customId))
            {
                return customId;
            }

            return $"{method.DeclaringType?.Name}.{method.Name}";
        }

        /// <summary>
        /// Validate that code reload method signature is compatible with original method.
        /// Code reload method must have instance parameter as first argument.
        /// </summary>
        private static bool ValidateSignatureCompatibility(MethodInfo originalMethod, MethodInfo overrideMethod)
        {
            var originalParams = originalMethod.GetParameters();
            var overrideParams = overrideMethod.GetParameters();

            // Code reload method must have at least one parameter (the instance)
            if (overrideParams.Length == 0)
            {
                return false;
            }

            // First parameter must be compatible with declaring type of original method
            var firstParam = overrideParams[0];
            if (!originalMethod.DeclaringType.IsAssignableFrom(firstParam.ParameterType))
            {
                return false;
            }

            // Remaining parameters must match original method parameters
            if (overrideParams.Length - 1 != originalParams.Length)
            {
                return false;
            }

            for (int i = 0; i < originalParams.Length; i++)
            {
                if (overrideParams[i + 1].ParameterType != originalParams[i].ParameterType)
                {
                    return false;
                }
            }

            // Return types must match
            return originalMethod.ReturnType == overrideMethod.ReturnType;
        }

        /// <summary>
        /// Determine if original method requires main thread execution.
        /// </summary>
        private static bool GetMainThreadRequirement(MethodInfo originalMethod)
        {
            // Honour the [CodeReload] attribute's main thread requirement
            var codeReloadAttr = originalMethod.GetCustomAttribute<CodeReloadAttribute>();
            if (codeReloadAttr != null)
            {
                return codeReloadAttr.RequireMainThread;
            }

            // Default to main thread requirement for safety
            return true;
        }

        /// <summary>
        /// Information about a registered method override.
        /// </summary>
        private class MethodOverride
        {
            public string TargetMethodId { get; set; }
            public MethodInfo OverrideMethod { get; set; }

            /// <summary>
            /// IL2CPP-safe dispatch: when set, the override runs through this delegate (a IlInterpreter
            /// interpreter invoke) instead of <see cref="OverrideMethod"/> reflection. Receives the
            /// target instance and the original call arguments; returns the method's (boxed) result,
            /// or null for a void method.
            /// </summary>
            public Func<object, object[], object> InterpreterInvoke { get; set; }

            public Type SourceType { get; set; }
            public bool RequireMainThread { get; set; }
            public string Description { get; set; }
        }
    }

    /// <summary>
    /// Statistics about current code reload registry state.
    /// </summary>
    public class CodeReloadStats
    {
        /// <summary>Number of methods registered as reloadable.</summary>
        public int ReloadableMethodCount { get; set; }
        /// <summary>Number of active method overrides.</summary>
        public int ActiveOverrideCount { get; set; }
        /// <summary>Number of code-reload assembly types currently loaded.</summary>
        public int LoadedTypeCount { get; set; }
        /// <summary>Method ids registered as reloadable.</summary>
        public List<string> ReloadableMethodIds { get; set; } = new();
        /// <summary>Method ids with an active override.</summary>
        public List<string> ActiveOverrideIds { get; set; } = new();
    }
}