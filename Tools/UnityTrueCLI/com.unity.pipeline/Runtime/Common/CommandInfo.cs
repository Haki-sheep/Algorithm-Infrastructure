using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Unity.Pipeline.Commands
{
    /// <summary>
    /// Information about a discovered CLI command.
    /// Contains metadata needed for command execution and CLI help generation.
    /// </summary>
    public class CommandInfo
    {
        /// <summary>
        /// Unique name of the command for CLI execution.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Human-readable description of the command.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Whether this command requires Unity main thread execution.
        /// </summary>
        public bool MainThreadRequired { get; }

        /// <summary>
        /// Hierarchical tags used to group and browse commands (path-style, '/'-separated).
        /// Empty for untagged commands; never null.
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// Name of the assembly this command originates from, used to group and filter
        /// commands by contributing package/source.
        /// </summary>
        public string Package { get; }

        /// <summary>
        /// Method that implements this command.
        /// </summary>
        public MethodInfo Method { get; } // TODO Maybe look at generating a dynamic Delegate to increase performance of the command call.

        /// <summary>
        /// Parameters that this command accepts.
        /// </summary>
        public IReadOnlyList<CommandParameterInfo> Parameters { get; }

        /// <summary>
        /// Instance to invoke <see cref="Method"/> on, or null when <see cref="Method"/> is static.
        /// Commands declared with a [CliCommand] attribute are always static and leave this null;
        /// a dynamically registered command bound to an instance method carries its receiver here.
        /// </summary>
        public object Target { get; }

        /// <summary>
        /// Create command information from discovery.
        /// </summary>
        /// <param name="name">Unique name of the command for CLI execution.</param>
        /// <param name="description">Human-readable description of the command.</param>
        /// <param name="mainThreadRequired">Whether this command requires Unity main thread execution.</param>
        /// <param name="method">Method that implements this command.</param>
        /// <param name="parameters">Parameters that this command accepts.</param>
        /// <param name="runtimeOnly">
        /// Whether the command belongs to the runtime (Player) command surface. True adds the
        /// <see cref="RuntimeTag"/> tag; tagging it directly does the same thing.
        /// </param>
        /// <param name="tags">Hierarchical tags used to group and browse commands.</param>
        /// <param name="package">Name of the assembly this command originates from.</param>
        /// <param name="target">
        /// Instance to invoke <paramref name="method"/> on, or null when it is static. Only a
        /// dynamically registered command bound to an instance method supplies one.
        /// </param>
        public CommandInfo(string name, string description, bool mainThreadRequired,
            MethodInfo method, IReadOnlyList<CommandParameterInfo> parameters, bool runtimeOnly = false,
            IReadOnlyList<string> tags = null, string package = null, object target = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Method = method ?? throw new ArgumentNullException(nameof(method));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            MainThreadRequired = mainThreadRequired;
            Tags = WithRuntimeTag(tags, runtimeOnly);
            Package = package;
            Target = target;
        }

        /// <summary>
        /// The tag marking the runtime (Player) command surface.
        /// </summary>
        public const string RuntimeTag = "runtime";

        /// <summary>
        /// Whether this command belongs to the runtime (Player) command surface — that is,
        /// whether it carries the <see cref="RuntimeTag"/> tag or one beneath it. Derived from
        /// <see cref="Tags"/>, so the tag is the single source of truth.
        /// </summary>
        public bool RuntimeOnly => HasTagInSubtree(RuntimeTag);

        /// <summary>
        /// Fold a runtimeOnly declaration into the tags. The flag is shorthand for
        /// <see cref="RuntimeTag"/>, so a command already in that subtree is returned unchanged.
        /// </summary>
        private static IReadOnlyList<string> WithRuntimeTag(IReadOnlyList<string> tags, bool runtimeOnly)
        {
            var declared = tags ?? Array.Empty<string>();
            if (!runtimeOnly)
                return declared;

            foreach (var tag in declared)
            {
                if (tag != null
                    && (tag.Equals(RuntimeTag, StringComparison.OrdinalIgnoreCase)
                        || tag.StartsWith(RuntimeTag + "/", StringComparison.OrdinalIgnoreCase)))
                    return declared;
            }

            var combined = new string[declared.Count + 1];
            for (var i = 0; i < declared.Count; i++)
                combined[i] = declared[i];
            combined[declared.Count] = RuntimeTag;
            return combined;
        }

        /// <summary>
        /// Whether the command carries <paramref name="tag"/> or a tag beneath it. The match is
        /// segment-aware: 'assets' matches 'assets' and 'assets/import' but not 'assetsx'. A
        /// null or empty tag matches every command.
        /// </summary>
        /// <param name="tag">Path-style tag to test, e.g. "assets" or "assets/import".</param>
        /// <returns>True when the command sits in that tag's subtree.</returns>
        public bool HasTagInSubtree(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return true;
            foreach (var candidate in Tags)
            {
                if (candidate != null
                    && (candidate.Equals(tag, StringComparison.OrdinalIgnoreCase)
                        || candidate.StartsWith(tag + "/", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        /// <summary>A short diagnostic summary of the command.</summary>
        /// <returns>The summary string.</returns>
        public override string ToString()
        {
            return $"{Name} MainThreadRequired:{MainThreadRequired} Parameters:{Parameters.Count}";
        }
    }
}