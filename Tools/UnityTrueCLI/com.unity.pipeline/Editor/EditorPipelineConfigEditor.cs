using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Pipeline.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="EditorPipelineConfig"/>: shows live server status (accurate
    /// running check + actual port, read from the static owner) and offers Start/Stop/Restart
    /// buttons, alongside the editable configuration. Watchdog edits are pushed to a running server
    /// immediately; port/autoStart apply on the next start.
    ///
    /// Never reached by clicking an asset in the Project window (the config is never a saved asset)
    /// — hosted directly by <see cref="EditorPipelineSettingsProvider"/>'s Project Settings page
    /// instead, via Editor.CreateEditor, which works fine on an in-memory instance. That page owns
    /// persisting the edits; this inspector only pushes them to the live server.
    /// </summary>
    [CustomEditor(typeof(EditorPipelineConfig))]
    class EditorPipelineConfigEditor : UnityEditor.Editor
    {
        // Repaint() from inside OnInspectorGUI re-triggers OnInspectorGUI on the next editor tick, so
        // a running server redrew this IMGUI-heavy inspector every frame (several ms) while merely
        // visible. Repaint from EditorApplication.update at a low rate instead.
        private const double StatusRepaintInterval = 0.25;
        private double m_NextStatusRepaint;

        /// <summary>
        /// True when the inspected object is a saved asset rather than the in-memory instance the
        /// settings page hosts — i.e. a leftover pre-AUTHAPI-66 EditorPipelineManager.asset, which
        /// still deserializes into this class because the rename kept the script's GUID, and would
        /// otherwise get the full settings UI while configuring nothing.
        /// </summary>
        private bool IsLeftoverAsset => EditorUtility.IsPersistent(target);

        private void OnEnable()
        {
            // The legacy-asset notice is static; only the live settings UI has status to keep current.
            if (!IsLeftoverAsset)
                EditorApplication.update += ThrottledStatusRepaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= ThrottledStatusRepaint;
        }

        private void ThrottledStatusRepaint()
        {
            var config = target as EditorPipelineConfig;
            if (config == null || !(config.IsServerRunning || EditorCodeReloadWatcher.IsWatching))
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now < m_NextStatusRepaint)
                return;
            m_NextStatusRepaint = now + StatusRepaintInterval;
            Repaint();
        }

        /// <summary>Draw the default inspector plus live server status and Start/Stop/Restart controls.</summary>
        public override void OnInspectorGUI()
        {
            var config = (EditorPipelineConfig)target;

            if (IsLeftoverAsset)
            {
                DrawLegacyAssetNotice(config);
                return;
            }

            EditorGUI.BeginChangeCheck();
            DrawConfigFields();
            if (EditorGUI.EndChangeCheck())
            {
                // Watchdog, request logging and the eval-telemetry statics all apply without a
                // restart. One shared helper owns that mapping, so this path and the settings page's
                // external-change reload cannot drift apart.
                PipelineServerStartup.ApplyLiveSettings(config);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Running", config.IsServerRunning);
                EditorGUILayout.IntField("Actual Port", config.ActualPort);
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(config.IsServerRunning))
                {
                    if (GUILayout.Button("Start"))
                        config.StartServer();
                }
                using (new EditorGUI.DisabledScope(!config.IsServerRunning))
                {
                    if (GUILayout.Button("Stop"))
                        config.StopServer();
                }
                if (GUILayout.Button("Restart"))
                    config.RestartServer();
            }

            DrawCodeReloadWatchSection();
            // Live status is kept current by ThrottledStatusRepaint — never Repaint() from here.
        }

        /// <summary>
        /// Everything a leftover pre-AUTHAPI-66 settings asset gets. Deliberately draws none of the
        /// fields, status rows or Start/Stop controls: nothing reads this asset's values, so showing
        /// them editable is the problem being fixed. Deletion stays behind the button rather than
        /// happening on load — it is a file the user committed, so removing it unasked would read as
        /// an unexplained VCS deletion and would fail on a read-only Perforce checkout.
        /// </summary>
        private void DrawLegacyAssetNotice(EditorPipelineConfig config)
        {
            EditorGUILayout.HelpBox(
                "This asset no longer configures anything.\n\n" +
                "The Pipeline editor server now reads its settings from Project Settings > " +
                "Pipeline > Editor, stored under ProjectSettings/ so they no longer ship inside " +
                "your build. Nothing reads this asset, and editing it would have no effect — it is " +
                "safe to delete.",
                MessageType.Warning);

            EditorGUILayout.Space();

            if (GUILayout.Button("Open Project Settings"))
                SettingsService.OpenProjectSettings(EditorPipelineSettingsProvider.SettingsPath);

            // The asset deserializes into this very class, so "migrating" is just Save().
            if (GUILayout.Button("Move These Settings To Project Settings"))
            {
                var existing = EditorPipelineConfig.Load();
                bool overwriting = existing != null;
                if (existing != null)
                    DestroyImmediate(existing);

                if (!overwriting || EditorUtility.DisplayDialog(
                        "Overwrite Pipeline editor settings?",
                        "Project Settings > Pipeline > Editor already has settings. Replace them " +
                        "with the values from this asset?",
                        "Replace", "Cancel"))
                {
                    config.Save();
                    Debug.Log($"Pipeline: copied this asset's editor settings into ProjectSettings/. " +
                        $"'{AssetDatabase.GetAssetPath(config)}' can now be deleted.");
                    SettingsService.OpenProjectSettings(EditorPipelineSettingsProvider.SettingsPath);
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Delete This Asset"))
            {
                var path = AssetDatabase.GetAssetPath(config);
                if (EditorUtility.DisplayDialog(
                        "Delete leftover Pipeline settings asset?",
                        $"Delete '{path}'?\n\nIts values are not used by anything. If you want to " +
                        "keep them, use \"Move These Settings To Project Settings\" first.",
                        "Delete", "Cancel"))
                {
                    AssetDatabase.DeleteAsset(path);
                    // Deleting the inspected object destroys `target` underneath a half-drawn IMGUI
                    // layout, which surfaces as a mismatched-LayoutGroup error. ExitGUI abandons
                    // this pass cleanly; the window redraws with the asset gone.
                    GUIUtility.ExitGUI();
                }
            }
        }

        /// <summary>
        /// Draw every visible serialized field, the way DrawDefaultInspector would — so a field
        /// added later shows up without touching this method — minus the disabled "Script" row,
        /// which suits an asset inspector but is noise on a Project Settings page.
        /// </summary>
        private void DrawConfigFields()
        {
            serializedObject.Update();

            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script")
                    continue;
                EditorGUILayout.PropertyField(property, true);
            }

            // WithoutUndo on purpose: only the settings page's in-memory instance reaches here, and
            // Unity files an undo record for a non-persistent object under the active scene.
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Watch the project's Assets folder and re-apply affected [CodeReload] methods on every save,
        /// driving <see cref="EditorCodeReloadWatcher"/>. Neither scope, target, nor backend is a
        /// choice: the whole Assets tree is watched, each save goes to the connected player(s) if any,
        /// else applies in-editor, and both paths run on the interpreter; a read-only row shows the
        /// current target resolution. Config is locked while a watch is active.
        /// </summary>
        private void DrawCodeReloadWatchSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Code Reload Interpreter Watch", EditorStyles.boldLabel);

            bool watching = EditorCodeReloadWatcher.IsWatching;

            serializedObject.Update();

            // Not a picker: each save is pushed to the connected player(s) if any, else applied
            // in-editor — resolved per save, so plugging in a device mid-watch reroutes the next save.
            int connected = EditorCodeReloadWatcher.ConnectedPlayerCount;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.LabelField(
                    new GUIContent("Target", "Resolved automatically at each file change: development player(s) if connected, otherwise the reload applies in this editor process. Both run on the interpreter."),
                    new GUIContent(connected > 0
                        ? $"Player — {connected} connected"
                        : "In Editor — no player connected"));

            // Outside the watching-locked scope: read at each player-connect event, so toggling it
            // takes effect for the next connection even while a watch is active.
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("m_CodeReloadRepushOnConnect"),
                new GUIContent("Re-push On Connect",
                    "Re-push the watched code-reload state to any player that connects while the watch " +
                    "is active. A restarted player boots the original build without previously pushed " +
                    "overrides; this catches it up immediately instead of waiting for the next save."));

            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(watching))
                {
                    // The UI always watches the whole Assets tree; StartWatch itself accepts any
                    // file or folder for CLI/API callers.
                    if (GUILayout.Button("Start Watching"))
                        EditorCodeReloadWatcher.StartWatch(Path.GetFullPath(Application.dataPath),
                            isFolder: true);
                }
                using (new EditorGUI.DisabledScope(!watching))
                {
                    if (GUILayout.Button("Stop Watching"))
                        EditorCodeReloadWatcher.StopWatch();
                }
            }

            if (watching)
                EditorGUILayout.HelpBox(
                    "Asset auto-refresh is disabled while watching, so saved scripts code-reload instead of " +
                    "triggering a domain reload. New or changed assets won't import until you refresh manually " +
                    "(Cmd/Ctrl+R); Stop Watching restores your auto-refresh setting.",
                    MessageType.Warning);

            using (new EditorGUI.DisabledScope(true))
            {
                // Read WatchPath once: it goes null the instant the watch stops (domain reload / StopWatch),
                // which can race the `watching` captured at the top of this method — guard against null here.
                var watchPath = EditorCodeReloadWatcher.WatchPath;
                // The live Target row above shows where saves currently go; this line covers what is watched.
                string status = watching && !string.IsNullOrEmpty(watchPath)
                    ? $"{(EditorCodeReloadWatcher.IsFolder ? "folder" : "file")} · {Path.GetFileName(watchPath.TrimEnd('/', '\\'))}"
                    : "idle";
                EditorGUILayout.LabelField("Watching", status);

                if (watching)
                {
                    // Liveness: saves applying while the event count stays flat means the OS file
                    // watcher is dead and the reconcile poll is carrying the watch.
                    EditorGUILayout.LabelField("Watcher Events",
                        $"{EditorCodeReloadWatcher.FsEventCount}{Ago(EditorCodeReloadWatcher.LastFsEventUtc)}");
                    EditorGUILayout.LabelField("Last Apply",
                        EditorCodeReloadWatcher.LastApplyFile is string f
                            ? $"{f}{Ago(EditorCodeReloadWatcher.LastApplyUtc)}"
                            : "none yet");
                }
            }
        }

        private static string Ago(System.DateTime? utc)
        {
            if (utc == null) return "";
            var s = (System.DateTime.UtcNow - utc.Value).TotalSeconds;
            return s < 1 ? " · just now"
                : s < 120 ? $" · {s:0}s ago"
                : s < 7200 ? $" · {s / 60:0}m ago"
                : $" · {s / 3600:0}h ago";
        }
    }
}
