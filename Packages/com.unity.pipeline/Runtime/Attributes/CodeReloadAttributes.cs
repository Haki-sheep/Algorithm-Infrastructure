using System;

namespace Unity.Pipeline.CodeReload
{
    /// <summary>
    /// Marks a method as reloadable: edit its body directly in the original source file and
    /// apply the file with the <c>reload_file</c> command (or the interpreter-backed variants). The
    /// running instance picks up the new body without a domain reload. Bodies may touch members of
    /// any accessibility, including private/protected/internal ones of the target type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class CodeReloadAttribute : Attribute
    {
        /// <summary>
        /// Optional identifier for this reloadable method.
        /// If not specified, uses TypeName.MethodName format.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Whether this method requires main thread execution.
        /// Defaults to true for Unity API safety.
        /// </summary>
        public bool RequireMainThread { get; set; } = true;
    }

    /// <summary>
    /// Marks a parameterless instance method to be invoked after a code reload is applied to its
    /// declaring component. Fires once per <c>reload_file</c> that binds at least one override on
    /// the type, on every live instance — a place to re-initialize or refresh state when the code is
    /// swapped (analogous to a domain-reload-free OnEnable). The method runs on the main thread;
    /// exceptions are logged, not propagated. Only UnityEngine.Object-derived types are supported
    /// (instances are discovered via FindObjectsByType).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [JetBrains.Annotations.MeansImplicitUse]
    public class OnCodeReloadAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a compiled static method as the code reload override for a specific <c>[CodeReload]</c>
    /// method. Emitted by the reload pipeline on the code it generates from an edited source file
    /// (see <c>SourceCodeTransformer</c>); user code does not write it by hand.
    ///
    /// Override signature: the original method's signature plus the target instance as the first
    /// parameter. Example: <c>void Update()</c> becomes <c>static void Update(TargetType instance)</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class CodeReloadOverrideMethodAttribute : Attribute
    {
        /// <summary>
        /// Target method identifier in format "TypeName.MethodName"
        /// Must match a method marked with [CodeReload].
        /// </summary>
        public string TargetMethodId { get; }

        /// <summary>
        /// Optional description of what this code reload does.
        /// Useful for debugging and code reload management.
        /// </summary>
        public string Description { get; set; }

        /// <summary>Mark a method as the code-reload override for the given target method.</summary>
        /// <param name="targetMethodId">Target method identifier, "TypeName.MethodName".</param>
        public CodeReloadOverrideMethodAttribute(string targetMethodId)
        {
            TargetMethodId = targetMethodId ?? throw new ArgumentNullException(nameof(targetMethodId));
        }
    }
}
