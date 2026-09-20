using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if UNITY_6000_3_OR_NEWER
using UnityEngine.Assemblies;
#endif
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace Unity.Pipeline.Commands
{
    /// <summary>
    /// Registry for discovering and managing CLI commands via pluggable discovery mechanism.
    /// Scans for methods marked with [CliCommand] attribute across all assemblies.
    /// Based on unity-tools ToolRegistry patterns adapted for Pipeline requirements.
    /// </summary>
    /// <remarks>
    /// [NoAutoStaticsCleanup] on purpose: m_DiscoveryLock and the injected discovery mechanism are
    /// set up once per domain and must not be reset under the running server. The dynamic-command
    /// table is the one static here with a shorter useful life, and PlayModeSessionEnded resets the
    /// part of it that a Play session owns.
    /// </remarks>
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    static class CommandRegistry
    {
        private static IReadOnlyList<CommandInfo> m_CachedCommands;
        private static IReadOnlyList<CommandInfo> m_CachedAllCommands;
        private static readonly Dictionary<string, CommandInfo> m_DynamicCommands =
            new Dictionary<string, CommandInfo>(StringComparer.Ordinal);
        // Names in m_DynamicCommands that predate the Play session currently running; null outside
        // one. Lets PlayModeSessionEnded tell a registration made during the session apart from one
        // that was already there. See PlayModeSessionStarted.
        private static HashSet<string> m_PrePlayModeCommands;
        private static ICommandDiscovery m_Discovery;
        private static readonly object m_DiscoveryLock = new object();

        /// <summary>
        /// Set the command discovery mechanism.
        /// Editor assembly provides TypeCache-based discovery, Runtime uses reflection fallback.
        /// </summary>
        /// <param name="discovery">The discovery mechanism to use.</param>
        public static void SetDiscovery(ICommandDiscovery discovery)
        {
            m_Discovery = discovery;
            ClearCache(); // Re-discover with new mechanism
        }

        /// <summary>
        /// Discover every available command: the methods marked with [CliCommand], plus any
        /// registered through <see cref="RegisterCommand"/>.
        /// Attribute discovery uses the injected mechanism (TypeCache in Editor, reflection in
        /// Runtime) and is cached until domain reload; the combined result is re-cached whenever
        /// a dynamic command is registered or unregistered.
        /// </summary>
        /// <returns>All discovered commands.</returns>
        public static IEnumerable<CommandInfo> DiscoverCommands()
        {
            var cached = m_CachedAllCommands;
            if (cached != null)
            {
                return cached;
            }

            lock (m_DiscoveryLock)
            {
                if (m_CachedCommands == null)
                {
                    m_CachedCommands = DiscoverCommandsInternal().ToList();
                }

                if (m_CachedAllCommands == null)
                {
                    m_CachedAllCommands = m_DynamicCommands.Count == 0
                        ? m_CachedCommands
                        : m_CachedCommands.Concat(m_DynamicCommands.Values).ToList();
                }
            }

            return m_CachedAllCommands;
        }

        /// <summary>
        /// Register a command at runtime, without a [CliCommand] attribute. The resulting command
        /// is indistinguishable from an attribute-declared one: it is listed by <c>/api/commands</c>
        /// and executable through <c>/api/exec</c> under <paramref name="name"/>.
        ///
        /// Parameter metadata is read from the handler's own parameters exactly as it is for an
        /// attribute-declared command — a [CliArg] on a parameter supplies its CLI name,
        /// description, and required flag; a parameter without one falls back to its declared name
        /// and default value. Note that a lambda cannot carry [CliArg], so registering a named
        /// method is what yields full metadata.
        ///
        /// Registrations live until they are unregistered or the domain reloads; they survive
        /// <see cref="ClearCache"/> and <see cref="SetDiscovery"/>. One made while the Editor is in
        /// Play Mode is additionally dropped when Play Mode exits — see
        /// <see cref="PlayModeSessionEnded"/>.
        /// </summary>
        /// <param name="name">Unique command name for CLI execution.</param>
        /// <param name="description">Human-readable description, shown in command listings.</param>
        /// <param name="handler">
        /// Method to invoke. An instance-method delegate keeps its receiver, so the command runs
        /// against that instance; a static one runs with no receiver.
        /// </param>
        /// <param name="mainThreadRequired">Whether the command must run on the Unity main thread.</param>
        /// <param name="runtimeOnly">Whether the command belongs to the Player command surface only.</param>
        /// <param name="tags">Optional path-style tags used to group and browse the command.</param>
        /// <exception cref="InvalidOperationException">
        /// A command called <paramref name="name"/> is already registered. Attribute-declared
        /// commands can never be replaced; a dynamic one must be unregistered first.
        /// </exception>
        public static void RegisterCommand(string name, string description, Delegate handler,
            bool mainThreadRequired = true, bool runtimeOnly = false, string[] tags = null)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (description == null)
                throw new ArgumentNullException(nameof(description));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Command name must not be empty or whitespace.", nameof(name));

            var method = handler.Method;
            var command = new CommandInfo(
                name,
                description,
                mainThreadRequired,
                method,
                DiscoverParameters(method).ToList(),
                runtimeOnly,
                tags,
                method.DeclaringType?.Assembly.GetName().Name,
                handler.Target);

            lock (m_DiscoveryLock)
            {
                if (m_DynamicCommands.ContainsKey(name))
                {
                    throw new InvalidOperationException(
                        $"A command named '{name}' is already registered dynamically. " +
                        "Unregister it before registering it again.");
                }

                // Anything left matching here is attribute-declared: the dynamic table was just checked.
                if (DiscoverCommands().Any(c => c.Name == name))
                {
                    throw new InvalidOperationException(
                        $"A command named '{name}' is already declared with a [CliCommand] attribute " +
                        "and cannot be replaced.");
                }

                m_DynamicCommands[name] = command;
                m_CachedAllCommands = null;
            }
        }

        /// <summary>
        /// Remove a command previously registered with <see cref="RegisterCommand"/>.
        /// Attribute-declared commands are part of the code and are never removable.
        /// </summary>
        /// <param name="name">Name the command was registered under.</param>
        /// <returns>
        /// True if a dynamic command was removed; false if no command is registered under that
        /// name dynamically, including when the name belongs to an attribute-declared command.
        /// </returns>
        public static bool UnregisterCommand(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            lock (m_DiscoveryLock)
            {
                if (!m_DynamicCommands.Remove(name))
                    return false;

                m_CachedAllCommands = null;
                return true;
            }
        }

        /// <summary>
        /// Note that a Play session is starting, so registrations made from here on can be told
        /// apart from the ones that predate it. Called by the Editor on Play Mode entry, before any
        /// user code runs.
        /// </summary>
        internal static void PlayModeSessionStarted()
        {
            lock (m_DiscoveryLock)
            {
                m_PrePlayModeCommands = new HashSet<string>(m_DynamicCommands.Keys, StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// Drop every dynamic registration made during the Play session that just ended, the way a
        /// domain reload would have. Registrations that predate the session are left alone: nothing
        /// re-runs to restore them (an [InitializeOnLoadMethod] runs once per domain).
        ///
        /// A script registering from Awake or OnEnable re-registers on every Play Mode entry, so
        /// without this the second entry would throw "already registered dynamically" whenever no
        /// domain reload happened in between — and any surviving entry would still hold a delegate
        /// bound to the previous session's destroyed object. That covers Enter Play Mode with domain
        /// reload disabled on any Editor version, and on 6000.5+ the class-wide
        /// [NoAutoStaticsCleanup] means AutoStaticsCleanup does not step in either.
        /// </summary>
        internal static void PlayModeSessionEnded()
        {
            lock (m_DiscoveryLock)
            {
                // No recorded session start (e.g. a domain reload wiped it on the way in, having
                // already cleared the registry): nothing is known to belong to the session, so
                // removing anything would be guesswork.
                if (m_PrePlayModeCommands == null)
                    return;

                foreach (var name in m_DynamicCommands.Keys.Where(n => !m_PrePlayModeCommands.Contains(n)).ToList())
                    m_DynamicCommands.Remove(name);

                m_PrePlayModeCommands = null;
                m_CachedAllCommands = null;
            }
        }

        /// <summary>
        /// Clear the discovered-command cache, so the next discovery re-scans for [CliCommand]
        /// methods. Called automatically on domain reload, and whenever the discovery mechanism
        /// changes. Commands registered through <see cref="RegisterCommand"/> are not affected.
        /// </summary>
        public static void ClearCache()
        {
            lock (m_DiscoveryLock)
            {
                m_CachedCommands = null;
                m_CachedAllCommands = null;
            }
        }

        /// <summary>
        /// Internal implementation of command discovery using injected discovery mechanism.
        /// </summary>
        private static IEnumerable<CommandInfo> DiscoverCommandsInternal()
        {
            var commands = new List<CommandInfo>();

            try
            {
                // Use injected discovery mechanism if available, otherwise fallback to reflection
                IEnumerable<MethodInfo> methods;

                if (m_Discovery != null)
                {
                    methods = m_Discovery.GetMethodsWithAttribute<CliCommandAttribute>();
                }
                else
                {
                    // Fallback: Use reflection to scan loaded assemblies
                    methods = GetMethodsWithAttributeViaReflection<CliCommandAttribute>();
                }

                foreach (var method in methods)
                {
                    try
                    {
                        var commandInfo = CreateCommandInfo(method);
                        if (commandInfo != null)
                        {
                            commands.Add(commandInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to register command from method {method.DeclaringType?.Name}.{method.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to discover commands: {ex.Message}");
            }

            return commands;
        }

        /// <summary>
        /// Fallback command discovery using reflection when TypeCache is not available.
        /// </summary>
        private static IEnumerable<MethodInfo> GetMethodsWithAttributeViaReflection<T>() where T : Attribute
        {
            var methods = new List<MethodInfo>();

            try
            {
                var assemblies = PipelineUtils.GetLoadedAssemblies();

                foreach (var assembly in assemblies)
                {
                    // Skip system assemblies for performance
                    if (IsSystemAssembly(assembly))
                        continue;

                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                            {
                                if (method.GetCustomAttribute<T>() != null)
                                {
                                    methods.Add(method);
                                }
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        // Skip assemblies that can't be loaded
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Reflection-based command discovery failed: {ex.Message}");
            }

            return methods;
        }

        /// <summary>
        /// Check if assembly is a system assembly that should be skipped during discovery.
        /// </summary>
        private static bool IsSystemAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            return name.StartsWith("System.") ||
                   name.StartsWith("Microsoft.") ||
                   name.StartsWith("mscorlib") ||
                   name.StartsWith("netstandard") ||
                   name.Equals("UnityEngine") ||
                   name.Equals("UnityEditor");
        }

        /// <summary>
        /// Create CommandInfo from a method with CliCommand attribute.
        /// </summary>
        private static CommandInfo CreateCommandInfo(MethodInfo method)
        {
            var commandAttr = method.GetCustomAttribute<CliCommandAttribute>();
            if (commandAttr == null)
                return null;

            // Validate method is static (required for CLI commands).
            // Any static method may be registered regardless of accessibility
            // (public, internal, or private) — invocation goes through MethodInfo.Invoke.
            if (!method.IsStatic)
            {
                Debug.LogWarning($"Command method {method.DeclaringType?.Name}.{method.Name} must be static");
                return null;
            }

            // Discover parameters
            var parameters = DiscoverParameters(method).ToList();

            return new CommandInfo(
                commandAttr.Name,
                commandAttr.Description,
                commandAttr.MainThreadRequired,
                method,
                parameters,
                commandAttr.RuntimeOnly,
                commandAttr.Tags,
                method.DeclaringType?.Assembly.GetName().Name
            );
        }

        /// <summary>
        /// Discover parameter information from method parameters.
        /// </summary>
        private static IEnumerable<CommandParameterInfo> DiscoverParameters(MethodInfo method)
        {
            foreach (var param in method.GetParameters())
            {
                var argAttr = param.GetCustomAttribute<CliArgAttribute>();

                // Parameters without CliArg attribute get default metadata
                var name = argAttr?.Name ?? param.Name;
                var description = argAttr?.Description ?? $"Parameter: {param.Name}";
                var required = argAttr?.Required ?? !param.HasDefaultValue;
                var defaultValue = param.HasDefaultValue ? param.DefaultValue : argAttr?.DefaultValue;

                yield return new CommandParameterInfo(name, description, required, param.ParameterType, defaultValue);
            }
        }
    }
}