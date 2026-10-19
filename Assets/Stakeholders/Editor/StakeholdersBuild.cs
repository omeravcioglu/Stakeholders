using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Stakeholders.EditorTools
{
    /// <summary>Build helpers. Also usable from the command line (CI / automated testing).</summary>
    public static class StakeholdersBuild
    {
        const string DefaultPath = "Builds/Windows/Stakeholders.exe";

        [MenuItem("Stakeholders/Build Windows Player")]
        public static void BuildWindowsMenu()
        {
            string path = Path.GetFullPath(DefaultPath);
            if (Build(path, false)) EditorUtility.RevealInFinder(path);
        }

        /// <summary>Unity.exe -batchmode -quit -projectPath ... -executeMethod Stakeholders.EditorTools.StakeholdersBuild.BuildWindowsFromCommandLine -stkBuildPath C:\out\Stakeholders.exe</summary>
        public static void BuildWindowsFromCommandLine()
        {
            string path = Arg("-stkBuildPath") ?? DefaultPath;
            bool ok = Build(path, true);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(string path, bool windowedForTesting)
        {
            if (windowedForTesting)
            {
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 1600;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.resizableWindow = true;
            }
            PlayerSettings.runInBackground = true;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Debug.Log($"Stakeholders build: {report.summary.result}, {report.summary.totalErrors} errors, size {report.summary.totalSize / (1024 * 1024)} MB -> {path}");
            return report.summary.result == BuildResult.Succeeded;
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
