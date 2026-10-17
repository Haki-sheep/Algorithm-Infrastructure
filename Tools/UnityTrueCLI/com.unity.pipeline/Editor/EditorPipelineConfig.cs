using System;
using System.IO;
using UnityEngine;

namespace Unity.Pipeline.Editor
{
    /// <summary>
    /// Configuration and control surface for the live editor pipeline server. This is settings,
    /// NOT the server's owner — <see cref="PipelineServerStartup"/> owns the server instance (a
    /// static owner survives domain reloads cleanly, whereas a ScriptableObject's lifetime does not
    /// track the server across editor events). The owner reads this config when starting; the
    /// settings page drives Start/Stop through the owner and shows live status.
    ///
    /// Authored settings live in a JSON file under ProjectSettings/ (see <see cref="Save"/> /
    /// <see cref="Load"/>) — never as an asset under Assets/, which would ship the package's own
    /// configuration inside the user's build. Same arrangement as the runtime server's
    /// RuntimePipelineConfig, and edited the same way, from
    /// Project Settings > Pipeline > Editor (<see cref="EditorPipelineSettingsProvider"/>).
    ///
    /// The file is optional: without it the owner uses the defaults below.
    /// </summary>
    class EditorPipelineConfig : ScriptableObject
    {
        // Each of these is used twice: as the field initializer below, and as the fallback wherever
        // a caller reads a setting with no settings file authored (Load returns null then). Those two
        // have to agree — otherwise merely authoring a file, without editing anything in it, would
        // change how the server starts. Naming the value once makes that true by construction
        // instead of leaving it to a test to notice the day they drift apart.
        internal const ushort DefaultPort = 0;
        internal const bool DefaultAutoStart = true;
        internal const bool DefaultWatchdogEnabled = true;
        internal const int DefaultWatchdogIntervalSeconds = 5;
        internal const bool DefaultLogRequestsResponses = false;
        internal const bool DefaultEvalTelemetryEnabled = true;
        internal const bool DefaultStoreEvalSource = false;
        internal const bool DefaultAllowBrowserClients = false;
        internal const bool DefaultCodeReloadRepushOnConnect = true;

        [Tooltip("HTTP port for the editor server. 0 = auto-assign from the 7800-7849 range. Applies on next start.")]
        [SerializeField] private ushort m_Port = DefaultPort;

        [Tooltip("Start the server automatically when the editor loads. Applies on next editor load.")]
        [SerializeField] private bool m_AutoStart = DefaultAutoStart;

        [Tooltip("Self-heal: if the HTTP listener dies without a Stop(), re-open it on a timer. " +
                 "Keeps auto-tick on so the editor keeps ticking while unfocused (required for the watchdog).")]
        [SerializeField] private bool m_WatchdogEnabled = DefaultWatchdogEnabled;

        [Tooltip("How often the watchdog checks the listener, between 1 and 60 seconds.")]
        [SerializeField] private int m_WatchdogIntervalSeconds = DefaultWatchdogIntervalSeconds;

        [Tooltip("Log every command request/response (raw JSON) handled by the editor server to " +
                 "<project>/Logs/pipeline.log. Editor only; applies live.")]
        [SerializeField] private bool m_LogRequestsResponses = DefaultLogRequestsResponses;

        [Tooltip("Record local eval-usage telemetry (fingerprints + shape, no raw source) to " +
                 "<project>/Library/Pipeline/eval-usage.jsonl. Read it back with the 'report_evals' " +
                 "command. Local-only; no data leaves the machine. Applies live.")]
        [SerializeField] private bool m_EvalTelemetryEnabled = DefaultEvalTelemetryEnabled;

        [Tooltip("Also store the raw eval source in each eval-usage telemetry record. Off by default " +
                 "(privacy-first) — enable only for local debugging. Applies live.")]
        [SerializeField] private bool m_StoreEvalSource = DefaultStoreEvalSource;

        [Tooltip("Accept requests from a client running in a sandboxed browser frame (Origin: null), " +
                 "such as a plugin hosted inside a web application. An ordinary web page is refused " +
                 "either way, and a sandboxed one still needs the bearer token. Applies on next start.")]
        [SerializeField] private bool m_AllowBrowserClients = DefaultAllowBrowserClients;

        // Code Reload Watch config, drawn by a dedicated inspector section (not DrawDefaultInspector).
        [HideInInspector, SerializeField, UnityEngine.Serialization.FormerlySerializedAs("m_HotReloadRepushOnConnect")]
        private bool m_CodeReloadRepushOnConnect = DefaultCodeReloadRepushOnConnect;

        /// <summary>HTTP port for the editor server. 0 = auto-assign. Applies on next start.</summary>
        public int Port => m_Port;
        /// <summary>Start the server automatically when the editor loads.</summary>
        public bool AutoStart => m_AutoStart;
        /// <summary>Self-heal: re-open the listener on a timer if it dies without a Stop().</summary>
        public bool WatchdogEnabled => m_WatchdogEnabled;
        /// <summary>How often the watchdog checks the listener, in seconds.</summary>
        public int WatchdogIntervalSeconds => m_WatchdogIntervalSeconds;
        /// <summary>Log every command request/response to Logs/pipeline.log.</summary>
        public bool LogRequestsResponses => m_LogRequestsResponses;
        public bool EvalTelemetryEnabled => m_EvalTelemetryEnabled;
        public bool StoreEvalSource => m_StoreEvalSource;
        public bool AllowBrowserClients => m_AllowBrowserClients;

        /// <summary>Player-target watches: re-push the watched code-reload state to any player that
        /// connects mid-watch, so a restarted player catches up without waiting for the next save.</summary>
        internal bool CodeReloadRepushOnConnect => m_CodeReloadRepushOnConnect;

        /// <summary>Whether the live server (owned by PipelineServerStartup) is actually running.</summary>
        public bool IsServerRunning => PipelineServerStartup.Server != null && PipelineServerStartup.Server.IsRunning;

        /// <summary>The port the live server is actually listening on, or 0 when stopped.</summary>
        public int ActualPort => PipelineServerStartup.Server?.Port ?? 0;

        /// <summary>Start the live server (delegates to the static owner, which reads this config).</summary>
        public void StartServer() => PipelineServerStartup.EnsureServerStarted();

        /// <summary>Stop the live server.</summary>
        public void StopServer() => PipelineServerStartup.StopServer();

        /// <summary>Restart the live server.</summary>
        public void RestartServer() => PipelineServerStartup.RestartServer();

        /// <summary>Project-relative path of the authored settings file (never under Assets/).</summary>
        private const string SettingsFilePath = "ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json";

        /// <summary>
        /// Load the authored settings, or null if none have been written yet (callers fall back to
        /// the defaults above). No caching — this is only read at start, at inspect, and on the
        /// settings page, never per frame.
        ///
        /// Returns a fresh instance each call, not a shared asset, so <b>the caller owns it and must
        /// DestroyImmediate it</b>. Same contract as RuntimePipelineConfig.Load.
        /// </summary>
        /// <returns>The authored settings, or null if none exist.</returns>
        public static EditorPipelineConfig Load()
        {
            var path = GetAbsoluteSettingsPath();
            if (!File.Exists(path))
                return null;

            var config = CreateInstance<EditorPipelineConfig>();
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), config);
                config.Sanitize();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Pipeline: could not read editor settings at '{path}' ({ex.Message}); using defaults. Delete the file to reset.");
                DestroyImmediate(config);
                return null;
            }
            return config;
        }

        /// <summary>
        /// Persist this instance's current field values to the ProjectSettings/ JSON file, leaving a
        /// write time strictly later than the one already there.
        ///
        /// That guarantee is load-bearing: <see cref="EditorPipelineSettingsProvider"/> detects an
        /// out-of-band write purely by comparing <see cref="GetSettingsFileWriteTimeUtc"/>, and a
        /// rewrite within the same filesystem timestamp tick would otherwise leave the stamp
        /// unchanged — Windows' clock ticks about every 15ms, and some filesystems record mtime at
        /// whole-second resolution. An open settings page would then keep a stale instance whose
        /// next edit overwrites what was just written (reachable through the leftover-asset notice's
        /// copy button, or a second editor on the same project). Same nudge as
        /// RuntimePipelineConfig.Save.
        /// </summary>
        public void Save()
        {
            var path = GetAbsoluteSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var previousWrite = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            File.WriteAllText(path, JsonUtility.ToJson(this, true));

            if (File.GetLastWriteTimeUtc(path) <= previousWrite)
                File.SetLastWriteTimeUtc(path, previousWrite.AddMilliseconds(1));
        }

        /// <summary>
        /// Last-write-time of the authored settings file (UTC), or DateTime.MinValue if it doesn't
        /// exist yet. Lets the settings page detect that the file changed underneath it — someone
        /// editing it by hand, or a second editor instance sharing the project — without reloading
        /// from disk on every repaint.
        /// </summary>
        public static DateTime GetSettingsFileWriteTimeUtc()
        {
            var path = GetAbsoluteSettingsPath();
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }

        /// <summary>Delete the authored settings file, if any. For test cleanup only.</summary>
        public static void DeleteSettingsFileForTesting()
        {
            var path = GetAbsoluteSettingsPath();
            if (File.Exists(path))
                File.Delete(path);
        }

        /// <summary>
        /// Move the authored settings file aside, if one exists, so a test run can start from a
        /// clean slate without destroying it. Returns the backup path to pass to
        /// <see cref="RestoreSettingsFileForTesting"/>, or null if there was no file to back up.
        /// For test fixture setup only.
        /// </summary>
        public static string BackUpSettingsFileForTesting()
        {
            var path = GetAbsoluteSettingsPath();
            var backupPath = path + ".test-backup";

            // An interrupted run leaves the developer's real file parked at the backup path.
            // Recover it rather than delete it; see RuntimePipelineConfig for the full reasoning.
            if (File.Exists(backupPath))
            {
                if (!File.Exists(path))
                {
                    File.Move(backupPath, path);
                }
                else
                {
                    var rescued = $"{backupPath}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
                    File.Move(backupPath, rescued);
                    Debug.LogWarning(
                        $"Editor Pipeline: an interrupted test run had left settings at " +
                        $"'{backupPath}'. Preserved as '{rescued}' rather than overwritten.");
                }
            }

            if (!File.Exists(path))
                return null;

            File.Move(path, backupPath);
            return backupPath;
        }

        /// <summary>
        /// Restore a settings file backed up by <see cref="BackUpSettingsFileForTesting"/>,
        /// discarding whatever the test run left in its place. Pass null (no prior backup) to just
        /// delete a test-created file, leaving no settings file behind. For test fixture teardown only.
        /// </summary>
        public static void RestoreSettingsFileForTesting(string backupPath)
        {
            var path = GetAbsoluteSettingsPath();
            if (File.Exists(path))
                File.Delete(path);

            if (backupPath != null)
                File.Move(backupPath, path);
        }

        private static string GetAbsoluteSettingsPath()
        {
            // Application.dataPath is ".../Assets"; ProjectSettings is a sibling directory.
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, SettingsFilePath);
        }

        private void OnValidate() => Sanitize();

        /// <summary>
        /// Clamp fields to their valid ranges. Needs calling from both <see cref="OnValidate"/>
        /// (which covers settings-page edits, raised by the inspector's
        /// ApplyModifiedPropertiesWithoutUndo) and <see cref="Load"/>, because
        /// JsonUtility.FromJsonOverwrite raises no Unity callbacks and the settings file is
        /// hand-editable text. A 0 watchdog interval is the one that bites: WatchdogTick throttles
        /// on `elapsed &lt; WatchdogIntervalSeconds`, so it would stop throttling and re-open a
        /// dead listener every editor update.
        /// </summary>
        private void Sanitize()
        {
            m_WatchdogIntervalSeconds = Mathf.Clamp(m_WatchdogIntervalSeconds, 1, 60);
        }
    }
}
