using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.Pipeline.Config;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Unity.Pipeline.Editor.BuildProcessors
{
    /// <summary>
    /// Build processor for runtime Pipeline support. Three responsibilities:
    ///  - Validates the RuntimePipelineConfig settings (JSON-authored, read via
    ///    RuntimePipelineConfig.Load()) before allowing builds with runtime Pipeline enabled.
    ///  - Bakes the current settings into a transient RuntimePipelineConfig Resources asset so a
    ///    Player build can find them (the authored copy stays in ProjectSettings/, never here).
    ///  - Bakes the project's code reload scope (Assets + loaded package locations) into a
    ///    generated RuntimePipelineBuildInfo asset. A running Player cannot resolve the project
    ///    layout, so the absolute roots it is allowed to code reload from must be captured at build
    ///    time.
    ///  Both generated assets are purged at the very start of every build (before either is
    ///  possibly rewritten) and deleted again after a successful build, so machine-specific/
    ///  duplicated data never lingers in the project — including a stale, possibly-enabled asset
    ///  left behind by a build that was interrupted before OnPostprocessBuild could run (Unity
    ///  never invokes it for a failed or cancelled build).
    /// </summary>
#if UNITY_6000_3_OR_NEWER
    class PipelineRuntimeBuildProcessor : IPreprocessBuildWithContext, IPostprocessBuildWithReport
#else
    class PipelineRuntimeBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
#endif
    {
        /// <summary>Callback ordering relative to other build processors (0 = default).</summary>
        public int callbackOrder => 0;

        private const string ConfigAssetPath = "Assets/Settings/Pipeline/Resources/RuntimePipelineConfig.asset";
        private const string BuildInfoAssetPath = "Assets/Settings/Pipeline/Resources/RuntimePipelineBuildInfo.asset";

        // Opt-in symbol that puts the Pipeline into a build that is not a Development Build. Kept in
        // step with the defineConstraints on Unity.Pipeline / Unity.Pipeline.IlInterpreter and on the
        // bundled DLLs under Runtime/Plugins/CodeAnalysis: if this file and those disagree, a player
        // gets settings assets no code can read, or Pipeline code with no settings to start from.
        private const string EnableSymbol = "ENABLE_RUNTIME_PIPELINE";

        /// <summary>Verify bundled DLL integrity and validate runtime pipeline configuration before a build.</summary>
#if UNITY_6000_3_OR_NEWER
        /// <param name="ctx">The build callback context.</param>
        public void OnPreprocessBuild(BuildCallbackContext ctx)
#else
        /// <param name="report">The build report.</param>
        public void OnPreprocessBuild(BuildReport report)
#endif
        {
            // Purge any leftovers from a previous, interrupted build before anything else in this
            // method — every exit path below (no config, disabled, not a development build,
            // validation failure) must start from a clean slate. OnPostprocessBuild is not invoked
            // for a failed/cancelled build, so it can never be relied on for this;
            // this purge is the only mechanism that actually guarantees it. Without it, a stale
            // *enabled* asset left by an earlier interrupted build would still get packaged into a
            // build that is supposed to have Pipeline disabled.
            DeleteGeneratedAssetsIfPresent();

            // Integrity gate: fail the build if a bundled Roslyn DLL was swapped or modified.
            VerifyBundledChecksums();

            var config = RuntimePipelineConfig.Load();
            if (config == null)
            {
                Debug.LogWarning("Pipeline: No RuntimePipelineConfig asset found (Project Settings > Pipeline > Runtime). Pipeline will be disabled in Player builds.");
                return;
            }

            try
            {
                if (!config.enableInBuilds)
                {
                    Debug.LogWarning("Pipeline: RuntimePipelineConfig found, but enableInBuilds = false. Pipeline will be disabled in Player builds.");
                    return;
                }

                // A build that will not contain the Pipeline assemblies cannot read the generated
                // Resources assets, so baking them would ship dead, machine-specific data.
                if (!PipelineIsInBuild())
                {
                    Debug.Log("Pipeline: enableInBuilds is on, but this build is neither a Development " +
                        $"Build nor defines {EnableSymbol}, so the Pipeline assemblies and settings are " +
                        "excluded from it.");
                    return;
                }

                var validationResult = config.Validate();
                if (!validationResult.IsValid)
                {
                    throw new BuildFailedException($"Pipeline: Runtime configuration validation failed: {validationResult.Message}");
                }

                if (validationResult.Level == "warning")
                {
                    Debug.LogWarning($"Pipeline: Runtime configuration warning: {validationResult.Message}");
                }

                // Push signing: ensure the project key exists and bake its public half into the
                // build info, so the player rejects any code-reload push not signed by the matching
                // private key. Log the fingerprint so a later key mismatch is diagnosable from the
                // build log alone.
                var privateKey = PushSigningKey.LoadOrCreate(PushSigningKey.DefaultDirectory, out var keyCreated);
                var pushPublicKey = Unity.Pipeline.CodeReload.PushEnvelope.DerivePublicKey(privateKey);
                Debug.Log($"Pipeline: push-signing key {(keyCreated ? "GENERATED" : "loaded")} " +
                          $"(fingerprint {Unity.Pipeline.CodeReload.PushEnvelope.Fingerprint(pushPublicKey)}, " +
                          $"Library/Pipeline/{PushSigningKey.KeyFileName}). This build only accepts pushes signed with it.");

                WriteConfigAsset(config);
                WriteBuildInfoAsset(pushPublicKey);

                Debug.Log("Pipeline: Runtime server ENABLED in build.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        /// <summary>
        /// Whether this build will contain the Pipeline runtime assemblies, i.e. whether it satisfies
        /// their <c>UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_RUNTIME_PIPELINE</c> define
        /// constraint. A scripted build (the <c>build</c> command, which calls BuildPlayer with
        /// <see cref="BuildOptions.Development"/>) is a development build even when the Build
        /// Settings toggle is off, so the command's active options count too.
        /// </summary>
        private static bool PipelineIsInBuild()
        {
            if (EditorUserBuildSettings.development)
                return true;

            if (Commands.Build.BuildCommand.ActiveScriptedBuildOptions is { } scriptedOptions
                && (scriptedOptions & BuildOptions.Development) != 0)
                return true;

            var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group));
            return defines != null && defines.Split(';').Any(d => d.Trim() == EnableSymbol);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            DeleteGeneratedAssetsIfPresent();
        }

        /// <summary>
        /// Absolute roots considered in-scope for runtime code reload: the project's Assets folder
        /// plus the resolved location of every package loaded into the project (a local package may
        /// live anywhere on disk, not only under Packages).
        /// </summary>
        /// <returns>Absolute paths of the project's Assets folder and every loaded package.</returns>
        public static List<string> CollectProjectRoots()
        {
            var roots = new List<string> { Path.GetFullPath(Application.dataPath) };

            foreach (var package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                if (!string.IsNullOrEmpty(package.resolvedPath))
                {
                    roots.Add(Path.GetFullPath(package.resolvedPath));
                }
            }

            return roots;
        }

        private static void WriteConfigAsset(RuntimePipelineConfig sourceConfig)
        {
            var folder = Path.GetDirectoryName(ConfigAssetPath).Replace('\\', '/');
            CreateFolderRecursive(folder);

            // No DeleteGeneratedAssetIfPresent() call here: OnPreprocessBuild already purged both
            // paths before any of this method's caller ran, so this path is guaranteed clear.

            var baked = ScriptableObject.CreateInstance<RuntimePipelineConfig>();
            baked.enableInBuilds = sourceConfig.enableInBuilds;
            baked.port = sourceConfig.port;
            baked.requestTimeoutMs = sourceConfig.requestTimeoutMs;
            baked.enableAuditLogging = sourceConfig.enableAuditLogging;
            baked.autoStart = sourceConfig.autoStart;
            baked.maxWorkItemsPerFrame = sourceConfig.maxWorkItemsPerFrame;
            AssetDatabase.CreateAsset(baked, ConfigAssetPath);
            AssetDatabase.SaveAssets();
        }

        private static void WriteBuildInfoAsset(string pushPublicKey)
        {
            var folder = Path.GetDirectoryName(BuildInfoAssetPath).Replace('\\', '/');
            CreateFolderRecursive(folder);

            // No DeleteGeneratedAssetIfPresent() call here: OnPreprocessBuild already purged both
            // paths before any of this method's caller ran, so this path is guaranteed clear.

            var buildInfo = ScriptableObject.CreateInstance<RuntimePipelineBuildInfo>();
            buildInfo.allowedReloadRoots = CollectProjectRoots();
            buildInfo.pushPublicKey = pushPublicKey ?? "";
            AssetDatabase.CreateAsset(buildInfo, BuildInfoAssetPath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Delete both transient build-only assets if either is present, however they got there —
        /// baked by a previous build (normal cleanup) or left behind by one that never reached
        /// OnPostprocessBuild (crash, force-quit, cancelled build).
        /// </summary>
        private static void DeleteGeneratedAssetsIfPresent()
        {
            DeleteGeneratedAssetIfPresent(ConfigAssetPath);
            DeleteGeneratedAssetIfPresent(BuildInfoAssetPath);
        }

        /// <summary>
        /// Untyped load + a raw file-existence fallback, deliberately not a typed
        /// AssetDatabase.LoadAssetAtPath&lt;T&gt;: a typed load returns null — leaving a stale file
        /// untouched on disk, inside Resources, where a Player build would still package it — for
        /// any asset whose script reference broke after a package upgrade, or that was only
        /// partially written by the very crash that left it behind. Both are exactly the kind of
        /// leftover this cleanup exists to catch.
        /// </summary>
        private static void DeleteGeneratedAssetIfPresent(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null || File.Exists(assetPath))
                AssetDatabase.DeleteAsset(assetPath);
        }

        private static void CreateFolderRecursive(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // Relative location of the bundled Roslyn DLLs + their integrity manifest within the package.
        private const string CodeAnalysisRelDir = "Runtime/Plugins/CodeAnalysis";
        private const string ChecksumsFileName = "CHECKSUMS";

        /// <summary>
        /// Verify the bundled Roslyn DLLs against the committed CHECKSUMS manifest, locating them
        /// relative to this package on disk. Throws <see cref="BuildFailedException"/> on any
        /// mismatch so a tampered/swapped DLL cannot be built into a player.
        /// </summary>
        public static void VerifyBundledChecksums()
        {
            if (!TryGetBundledCodeAnalysisPaths(out var codeAnalysisDir, out var checksumsPath))
            {
                throw new BuildFailedException(
                    "Pipeline: could not locate the com.unity.pipeline package on disk to verify " +
                    "bundled Roslyn DLL integrity. Aborting build.");
            }

            var error = VerifyChecksums(codeAnalysisDir, checksumsPath);
            if (error != null)
            {
                throw new BuildFailedException($"Pipeline: bundled Roslyn DLL integrity check failed. {error}");
            }
        }

        /// <summary>
        /// Resolve the bundled Roslyn DLL folder and its CHECKSUMS manifest from this package's
        /// location on disk. Returns false when the package cannot be located, in which case both
        /// out parameters are null.
        /// </summary>
        /// <param name="codeAnalysisDir">Directory holding the bundled Roslyn DLLs.</param>
        /// <param name="checksumsPath">Path to the CHECKSUMS manifest inside that directory.</param>
        /// <returns>True when the package was located and both paths were resolved.</returns>
        public static bool TryGetBundledCodeAnalysisPaths(out string codeAnalysisDir, out string checksumsPath)
        {
            codeAnalysisDir = null;
            checksumsPath = null;

            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(PipelineRuntimeBuildProcessor).Assembly);

            if (packageInfo == null || string.IsNullOrEmpty(packageInfo.resolvedPath))
                return false;

            codeAnalysisDir = Path.Combine(packageInfo.resolvedPath, CodeAnalysisRelDir);
            checksumsPath = Path.Combine(codeAnalysisDir, ChecksumsFileName);
            return true;
        }

        /// <summary>
        /// Core, side-effect-free integrity check (so it is directly unit-testable). Returns null
        /// when every DLL listed in <paramref name="checksumsPath"/> exists under
        /// <paramref name="codeAnalysisDir"/> with a matching SHA-256 and no unlisted DLL is present;
        /// otherwise returns a human-readable error describing the first problem found.
        /// </summary>
        /// <param name="codeAnalysisDir">Directory containing the bundled Roslyn DLLs.</param>
        /// <param name="checksumsPath">Path to the CHECKSUMS manifest.</param>
        /// <returns>Null if every DLL matches; otherwise a human-readable description of the first problem found.</returns>
        public static string VerifyChecksums(string codeAnalysisDir, string checksumsPath)
        {
            if (!Directory.Exists(codeAnalysisDir))
                return $"DLL directory not found: {codeAnalysisDir}";
            if (!File.Exists(checksumsPath))
                return $"CHECKSUMS manifest not found: {checksumsPath}";

            var expected = ParseChecksums(checksumsPath);
            if (expected.Count == 0)
                return $"CHECKSUMS manifest has no entries: {checksumsPath}";

            // Every listed DLL must exist and match.
            foreach (var entry in expected)
            {
                var dllPath = Path.Combine(codeAnalysisDir, entry.Key);
                if (!File.Exists(dllPath))
                    return $"listed DLL is missing: {entry.Key}";

                var actual = ComputeSha256(dllPath);
                if (!string.Equals(actual, entry.Value, StringComparison.OrdinalIgnoreCase))
                    return $"hash mismatch for {entry.Key} (expected {entry.Value}, got {actual})";
            }

            // No unlisted DLL may sit alongside them (guards against an injected extra assembly).
            foreach (var dllPath in Directory.GetFiles(codeAnalysisDir, "*.dll"))
            {
                var name = Path.GetFileName(dllPath);
                if (!expected.ContainsKey(name))
                    return $"unexpected DLL not listed in CHECKSUMS: {name}";
            }

            return null;
        }

        /// <summary>SHA-256 of a file as a lowercase hex string.</summary>
        /// <param name="filePath">The file to hash.</param>
        /// <returns>The lowercase hex-encoded SHA-256 hash.</returns>
        public static string ComputeSha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // Parse "<sha256>  <filename>  # comment" lines, skipping blanks and '#' comment lines.
        private static Dictionary<string, string> ParseChecksums(string checksumsPath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in File.ReadAllLines(checksumsPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2)
                    continue;

                result[tokens[1]] = tokens[0];
            }

            return result;
        }
    }
}
