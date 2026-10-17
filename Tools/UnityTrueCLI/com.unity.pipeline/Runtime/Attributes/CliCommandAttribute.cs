using System;

namespace Unity.Pipeline.Commands
{
    /// <summary>
    /// Attribute to mark static methods as CLI-accessible commands.
    /// Commands with this attribute can be executed remotely via Pipeline Server.
    /// Adapted from unity-tools [Tool] attribute for Pipeline organization requirements.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class CliCommandAttribute : Attribute
    {
        /// <summary>
        /// Unique name of the command for CLI execution.
        /// Used in "unity request command_name" syntax.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Human-readable description of what the command does.
        /// Used in CLI help text and command discovery.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Whether this command requires Unity main thread execution.
        /// Default: true (most Unity APIs require main thread).
        /// </summary>
        public bool MainThreadRequired { get; set; } = true;

        /// <summary>
        /// Whether this command belongs to the runtime (Player) command surface.
        /// Setting it adds the "runtime" tag to <see cref="Tags"/>, which is what the rest of
        /// the pipeline reads; declaring that tag directly does the same thing.
        /// Default: false.
        /// </summary>
        public bool RuntimeOnly { get; set; } = false;

        /// <summary>
        /// Optional hierarchical tags used to group and browse commands.
        /// Path-style: a '/' separates tag from subtag (e.g. "assets", "assets/import").
        /// Default: empty (untagged).
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Create a new CLI command attribute.
        /// </summary>
        /// <param name="name">Unique command name for CLI execution</param>
        /// <param name="description">Human-readable description</param>
        public CliCommandAttribute(string name, string description)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }
    }
}