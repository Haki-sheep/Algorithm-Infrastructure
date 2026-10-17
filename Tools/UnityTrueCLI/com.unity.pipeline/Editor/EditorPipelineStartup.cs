using UnityEditor;
using UnityEngine;
using Unity.Pipeline.Models;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Telemetry;
using Unity.Pipeline.Threading;
using UnityEditor.MPE;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace Unity.Pipeline.Editor
{
    /// <summary>
    /// Automatically starts Pipeline HTTP server when Unity Editor loads.
    /// Handles startup, domain reload persistence, and cleanup.
    ///
    /// Static owner of the live editor server: a static owner survives domain reloads cleanly
    /// (re-created by [InitializeOnLoad]), whereas a ScriptableObject's lifetime does not track the
    /// server across editor events. <see cref="EditorPipelineConfig"/> holds the optional authored
    /// settings (ProjectSettings/, never an asset), read here at start.
    /// </summary>
    [InitializeOnLoad]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    static class PipelineServerStartup
    {
        private static EditorPipelineServer m_Server;

        /// <summary>
        /// The live editor pipeline server instance (null when stopped). Exposed so the test guard
        /// can disable its watchdog for a test run and the EditorPipelineConfig inspector can read
        /// live status.
        /// </summary>
        public static EditorPipelineServer Server => m_Server;

        static PipelineServerStartup()
        {
            // Seed the compile service's define snapshot while we're guaranteed on the main
            // thread — a background compile fired before any main-thread parse would otherwise
            // run with an empty define set (wrong #if branches). Before the worker-process
            // early-out: costs one editor API call and holds everywhere.
            Compilation.RoslynCompilationService.SnapshotProjectDefines();

            // Don't start server in AssetImportWorker processes
            if (!IsMainProcess())
                return;
            // Setup command discovery using TypeCache for fast Editor performance
            CommandRegistry.SetDiscovery(new TypeCacheCommandDiscovery());

            // Clean up any stale instance descriptor files from previous sessions
            CleanupStaleDescriptors();

            // Tell a project still carrying the pre-AUTHAPI-66 settings asset, once, that it is inert.
            EditorPipelineLegacyAssetNotice.WarnOncePerSession();

            var cfg = EditorPipelineConfig.Load();
            bool autoStart;
            try
            {
                // Apply the local eval-usage telemetry config on EVERY domain reload, not only when
                // the server starts (AUTHAPI-29): recording hooks eval itself, not the server
                // lifecycle, so an AutoStart=false session must still honor the configured opt-outs
                // instead of falling back to the hardcoded defaults.
                ApplyEvalTelemetrySettings(cfg);
                autoStart = cfg?.AutoStart ?? EditorPipelineConfig.DefaultAutoStart;
            }
            finally
            {
                if (cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }

            // Start the pipeline server (respecting the authored autoStart if settings exist)
            if (autoStart)
                StartServer();

            // Handle domain reloads and editor shutdown
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += OnEditorQuitting;
            EditorApplication.wantsToQuit += OnEditorWantsToQuit;

            // Handle domain reload detection
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        /// <summary>Start the server if it isn't already running.</summary>
        public static void EnsureServerStarted()
        {
            StartServer();
        }

        /// <summary>
        /// Force a clean restart of the editor pipeline server. Unlike EnsureServerStarted, this
        /// works even when the current server's listener has died but still reports IsRunning
        /// (e.g. after a test disrupted it). Used by tests to revive the live server they disrupted.
        /// </summary>
        public static void RestartServer()
        {
            StopServer();
            StartServer();
        }

        [MenuItem("Window/Pipeline/Start Server")]
        private static void MenuStartServer()
        {
            StartServer();
            if (m_Server != null && m_Server.IsRunning)
                Debug.Log($"Pipeline Server started on port {m_Server.Port}");
            else
                Debug.LogWarning("Pipeline Server failed to start");
        }

        [MenuItem("Window/Pipeline/Start Server", true)]
        private static bool MenuStartServerValidate() => m_Server == null || !m_Server.IsRunning;

        [MenuItem("Window/Pipeline/Stop Server")]
        private static void MenuStopServer()
        {
            StopServer();
        }

        [MenuItem("Window/Pipeline/Stop Server", true)]
        private static bool MenuStopServerValidate() => m_Server != null && m_Server.IsRunning;

        internal static bool IsMainProcess()
        {
            // Check command line arguments for asset import worker indicators
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-readonly" || args[i] == "--virtual-project-clone")
                    return true;
            }

            if (AssetDatabase.IsAssetImportWorkerProcess())
                return false;

            if (ProcessService.level != ProcessLevel.Main)
                return false;

            return true;
        }

        /// <summary>
        /// Apply the local eval-usage telemetry config (AUTHAPI-29). Local-only, no transmission;
        /// disabling it stops even local recording. StoreSource is the explicit opt-in for
        /// persisting raw eval source (off by default). Resolving the directory primes the
        /// dataPath-derived default on the main thread, so off-main-thread readers (report_evals)
        /// and the background record task never touch Unity APIs.
        ///
        /// Internal (not private): this is the ONE place that knows how to turn the authored settings
        /// into the static telemetry config, so every push site — the domain-reload static ctor,
        /// server start, and <see cref="EditorPipelineConfigEditor"/>'s live inspector push — calls
        /// through here rather than re-deriving the two-field mapping independently, which would
        /// silently drift the moment a fourth push site (or a third telemetry field) showed up.
        /// </summary>
        internal static void ApplyEvalTelemetrySettings(EditorPipelineConfig cfg)
        {
            EvalUsageTelemetry.Enabled = cfg?.EvalTelemetryEnabled ?? EditorPipelineConfig.DefaultEvalTelemetryEnabled;
            EvalUsageTelemetry.StoreSource = cfg?.StoreEvalSource ?? EditorPipelineConfig.DefaultStoreEvalSource;
            EvalUsageTelemetry.ResolveDirectory();
        }

        /// <summary>
        /// Push every setting that takes effect without a restart — the watchdog, request logging
        /// and the eval-telemetry statics — onto the live server. Port and AutoStart are absent on
        /// purpose: they only apply at the next start.
        ///
        /// Both surfaces that can change these values go through here: the settings page's
        /// interactive edits, and its reload when the file changed underneath it (a hand edit, a
        /// second editor, or the leftover-asset notice's copy button). Reloading used to refresh
        /// only what was displayed, leaving the running server on the old values until the next GUI
        /// edit or restart.
        /// </summary>
        internal static void ApplyLiveSettings(EditorPipelineConfig cfg)
        {
            var server = m_Server;
            if (server != null)
            {
                server.WatchdogEnabled = cfg?.WatchdogEnabled ?? EditorPipelineConfig.DefaultWatchdogEnabled;
                server.WatchdogIntervalSeconds = cfg?.WatchdogIntervalSeconds ?? EditorPipelineConfig.DefaultWatchdogIntervalSeconds;
                server.LogRequestsResponses = cfg?.LogRequestsResponses ?? EditorPipelineConfig.DefaultLogRequestsResponses;
            }

            ApplyEvalTelemetrySettings(cfg);
        }

        /// <summary>
        /// Start the Pipeline HTTP server, reading the authored EditorPipelineConfig settings if any
        /// exist (otherwise using defaults).
        /// </summary>
        private static void StartServer()
        {
            if (m_Server != null && m_Server.IsRunning)
                return;

            var cfg = EditorPipelineConfig.Load();
            try
            {
                m_Server = new EditorPipelineServer
                {
                    // Only the settings that cannot change without a restart. Everything that applies
                    // live comes from ApplyLiveSettings below, so the fallbacks for those fields exist
                    // in exactly one place.
                    AllowSandboxedBrowserClients = cfg?.AllowBrowserClients ?? EditorPipelineConfig.DefaultAllowBrowserClients,
                    MaxRequestBodyBytes = 32L * 1024 * 1024
                };

                // Self-healing watchdog: if the listener dies without a Stop() (an unexpected fault
                // outside a domain reload), the watchdog re-opens it so the dogfood loop doesn't
                // wedge. Must run before Start(), which arms the watchdog. Also re-applies the
                // eval-telemetry statics on every server start, so edits made this session stick
                // even without a domain reload (the static ctor covers the reload path).
                ApplyLiveSettings(cfg);

                // Rotate the transaction log once per Unity session (main thread; SessionState-gated).
                // The append path runs off-thread and can't touch SessionState.
                PipelineTransactionLog.RotateForNewSession();

                m_Server.Start(cfg?.Port ?? EditorPipelineConfig.DefaultPort); // 0 auto-assigns from the 7800-7849 range.

                // Restore whatever auto-tick state the user last set this session (survives the
                // domain reload that just wiped AutoTickCommand's statics). Only a session with no
                // prior explicit set_autotick call falls back to a default, and that default is "on"
                // when the watchdog is enabled: the watchdog rides EditorApplication.update, but a
                // backgrounded/idle editor stops ticking once the listener dies (no requests left to
                // wake it) — the exact moment the watchdog must run. Keeping auto-tick on by default
                // keeps the update loop spinning regardless of focus, which keeps both the watchdog
                // AND the dispatcher message pump alive.
                Commands.AutoTickCommand.RestoreFromSession(defaultEnabled: m_Server.WatchdogEnabled);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to start Pipeline Server: {ex.Message}");
            }
            finally
            {
                if (cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
        }

        /// <summary>
        /// Stop the Pipeline HTTP server.
        /// </summary>
        public static void StopServer()
        {
            if (m_Server != null)
            {
                try
                {
                    m_Server.Stop();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Error stopping Pipeline Server: {ex.Message}");
                }
                finally
                {
                    m_Server = null;
                }
            }
        }

        /// <summary>
        /// Clean up stale instance descriptor files from previous Editor sessions.
        /// </summary>
        private static void CleanupStaleDescriptors()
        {
            var projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);

            // Try to read existing descriptor
            var existing = InstanceDescriptor.ReadFromProjectRoot(projectPath);
            if (existing != null)
            {
                // Check if process is still running
                try
                {
                    var process = System.Diagnostics.Process.GetProcessById(existing.Pid);
                    if (process.HasExited)
                    {
                        // Process is dead, remove stale file
                        InstanceDescriptor.RemoveFromProjectRoot(projectPath);
                    }
                }
                catch
                {
                    // Process doesn't exist or access denied, remove stale file
                    InstanceDescriptor.RemoveFromProjectRoot(projectPath);
                }
            }
        }

        /// <summary>
        /// Handle play mode state changes.
        /// </summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Server continues running through play mode changes
            // Status endpoint will reflect current play mode via EditorApplication.isPlaying

            // RuntimePipelineDriver.Awake() switches CommandRegistry to reflection-based discovery
            // on every Play Mode entry (a Player has no TypeCache). With Fast Enter Play Mode
            // (domain reload disabled), nothing else runs this static constructor to switch it back
            // on the way out — pre-FEPM, re-entering Edit mode via a domain reload did this for
            // free. Restore TypeCache discovery explicitly here instead of relying on that reload.
            if (state == PlayModeStateChange.EnteredEditMode)
                CommandRegistry.SetDiscovery(new TypeCacheCommandDiscovery());

            // Dynamic command registrations (CommandRegistry.RegisterCommand) are static state with
            // domain-reload lifetime, and a script registering from Awake/OnEnable re-registers on
            // every Play Mode entry. With domain reload disabled nothing resets the registry between
            // sessions, so the second entry would throw "already registered dynamically" and a
            // surviving entry would hold a delegate bound to the previous session's destroyed
            // object. Bracket the session here to clear what it registered; registrations made in
            // Edit Mode are kept, since nothing re-runs to restore them.
            if (state == PlayModeStateChange.ExitingEditMode)
                CommandRegistry.PlayModeSessionStarted();
            else if (state == PlayModeStateChange.EnteredEditMode)
                CommandRegistry.PlayModeSessionEnded();
        }

        /// <summary>
        /// Handle Editor shutdown.
        /// </summary>
        private static void OnEditorQuitting()
        {
            StopServer(); // Stop() shuts down the server's own dispatcher.
        }

        private static bool OnEditorWantsToQuit()
        {
            // WARNING: we need to fire SessionStop in WantsToQuit. Firing in Quits won't actually send the event.
            PipelineAnalytics.SendSessionStoppedIfStarted();
            return true;
        }

        /// <summary>
        /// Handle before assembly reload (domain reload).
        /// </summary>
        private static void OnBeforeAssemblyReload()
        {
            // Server will be automatically recreated after reload due to [InitializeOnLoad]
            // Instance descriptor file will be cleaned up and recreated

        }

        /// <summary>
        /// Handle after assembly reload (domain reload).
        /// </summary>
        private static void OnAfterAssemblyReload()
        {
            // Server should already be restarted via [InitializeOnLoad]
            // This is mainly for logging/verification
        }
    }
}
