using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace UnityTrueCli
{
    [InitializeOnLoad]
    public static class PackageBootstrap
    {
        [Serializable]
        private sealed class InstallRequest
        {
            public string RequestId;
            public string PackagePath;
            public string ResultPath;
            public bool ExitWhenReady;
            public string ExpiresAt;
        }

        [Serializable]
        private sealed class InstallResult
        {
            public string RequestId;
            public bool Success;
            public string UnityVersion;
            public string PackagePath;
            public string Error;
        }

        private static readonly string RequestPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/UnityTrueCLI/bootstrap-request.json");
        private static InstallRequest Request;
        private static AddRequest AddOperation;
        private static bool Finished;
        private static double NextRequestPoll;

        static PackageBootstrap()
        {
            EditorApplication.delayCall += Start;
            EditorApplication.update += WatchRequests;
        }

        private static void WatchRequests()
        {
            if (EditorApplication.timeSinceStartup < NextRequestPoll)
                return;
            NextRequestPoll = EditorApplication.timeSinceStartup + 0.5;
            Start();
        }

        public static void Start()
        {
            if ((Request != null && !Finished) || !File.Exists(RequestPath) || AssetDatabase.IsAssetImportWorkerProcess())
                return;
            Request = JsonUtility.FromJson<InstallRequest>(File.ReadAllText(RequestPath));
            if (File.Exists(Request.ResultPath))
            {
                Finished = true;
                return;
            }
            Finished = false;
            if (DateTime.UtcNow >= DateTime.Parse(Request.ExpiresAt).ToUniversalTime())
            {
                Complete(false, "Bootstrap request expired before installation");
                return;
            }
            UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            EditorApplication.update += Poll;
            var CurrentPackage = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.pipeline");
            bool Matches = CurrentPackage != null && string.Equals(Path.GetFullPath(CurrentPackage.resolvedPath), Path.GetFullPath(Request.PackagePath), StringComparison.OrdinalIgnoreCase);
            if (Matches)
                return;
            string StartedKey = "UnityTrueCLI.Install." + Request.RequestId;
            if (SessionState.GetBool(StartedKey, false))
                return;
            SessionState.SetBool(StartedKey, true);
            AddOperation = Client.Add("file:" + Request.PackagePath.Replace('\\', '/'));
        }

        private static void Poll()
        {
            if (Finished)
                return;
            if (DateTime.UtcNow >= DateTime.Parse(Request.ExpiresAt).ToUniversalTime())
            {
                Complete(false, "Package installation or script compilation timed out");
                return;
            }
            if (AddOperation != null)
            {
                if (!AddOperation.IsCompleted)
                    return;
                if (AddOperation.Status == StatusCode.Failure)
                {
                    Complete(false, AddOperation.Error.message);
                    return;
                }
                AddOperation = null;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (EditorUtility.scriptCompilationFailed)
            {
                Complete(false, "Unity script compilation failed; inspect the Editor log");
                return;
            }
            var CurrentPackage = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.pipeline");
            if (CurrentPackage == null || !string.Equals(Path.GetFullPath(CurrentPackage.resolvedPath), Path.GetFullPath(Request.PackagePath), StringComparison.OrdinalIgnoreCase))
                return;
            var StartupType = Type.GetType("Unity.Pipeline.Editor.PipelineServerStartup, Unity.Pipeline.Editor");
            if (StartupType == null)
                return;
            try
            {
                StartupType.GetMethod("EnsureServerStarted", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
                var Server = StartupType.GetProperty("Server", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                bool IsRunning = Server != null && (bool)Server.GetType().GetProperty("IsRunning").GetValue(Server);
                if (IsRunning)
                    Complete(true, null);
            }
            catch (Exception Error)
            {
                Complete(false, Error.ToString());
            }
        }

        private static void OnAssemblyCompiled(string AssemblyPath, CompilerMessage[] MessageList)
        {
            foreach (var Message in MessageList)
            {
                if (Message.type == CompilerMessageType.Error)
                {
                    Complete(false, AssemblyPath + " " + Message.message);
                    return;
                }
            }
        }

        private static void Complete(bool Success, string Error)
        {
            if (Finished)
                return;
            Finished = true;
            EditorApplication.update -= Poll;
            UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompiled;
            var Result = new InstallResult
            {
                RequestId = Request.RequestId,
                Success = Success,
                UnityVersion = Application.unityVersion,
                PackagePath = Request.PackagePath,
                Error = Error
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Request.ResultPath));
            string TemporaryPath = Request.ResultPath + ".tmp";
            File.WriteAllText(TemporaryPath, JsonUtility.ToJson(Result, true));
            File.Move(TemporaryPath, Request.ResultPath);
            if (Success)
                File.Delete(RequestPath);
            if (Request.ExitWhenReady)
                EditorApplication.Exit(Success ? 0 : 1);
        }
    }
}
