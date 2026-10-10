using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UZS.Core;
using UZS.Gameplay;

namespace UZS.EditorTools
{
    /// <summary>
    /// Produces the downloadable Windows release: Builds/Windows/&lt;exe + data&gt; zipped into one archive with a
    /// SHA-256 checksum. Menu: Tools > UZS > Build Windows Release.
    /// Command line: Unity -batchmode -quit -projectPath Game -executeMethod UZS.EditorTools.BuildScript.BuildWindowsFromCommandLine
    /// </summary>
    public static class BuildScript
    {
        public const string OutputRoot = "Builds";
        public const string WindowsFolder = OutputRoot + "/Windows";

        public static string ZipName => $"{GameInfo.ExecutableName}-Windows-x64-v{GameInfo.ProtocolVersion}.zip";

        [MenuItem("Tools/UZS/Build Windows Release", priority = 20)]
        public static void BuildWindowsMenu()
        {
            if (BuildWindows(out var message)) EditorUtility.DisplayDialog("Build succeeded", message, "OK");
            else EditorUtility.DisplayDialog("Build failed", message, "OK");
        }

        /// <summary>Batch-mode entry point (use with -quit). Throws on failure so Unity exits with a non-zero code.</summary>
        public static void BuildWindowsFromCommandLine()
        {
            bool ok = BuildWindows(out var message);
            Debug.Log("[UZS-BUILD] " + message);
            if (!ok) throw new Exception(message);
        }

        public static bool BuildWindows(out string message)
        {
            if (AssetDatabase.LoadAssetAtPath<GameContent>(ProjectSetup.ContentPath) == null) ProjectSetup.Run();

            if (Directory.Exists(WindowsFolder)) Directory.Delete(WindowsFolder, true);
            Directory.CreateDirectory(WindowsFolder);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = $"{WindowsFolder}/{GameInfo.ExecutableName}.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                message = $"Build {report.summary.result} with {report.summary.totalErrors} error(s). Is 'Windows Build Support' installed for this editor?";
                return false;
            }

            // Strip folders Unity marks as not-for-shipping (only produced by some backends).
            foreach (var dir in Directory.GetDirectories(WindowsFolder))
            {
                string name = Path.GetFileName(dir);
                if (name.EndsWith("_DoNotShip") || name.EndsWith("_ButDontShipItWithYourGame")) Directory.Delete(dir, true);
            }
            File.WriteAllText(Path.Combine(WindowsFolder, "README.txt"),
                $"{GameInfo.DisplayTitle} v{GameInfo.ProtocolVersion}\r\n\r\n" +
                "1. Extract this whole folder (keep the _Data folder next to the .exe).\r\n" +
                $"2. Run {GameInfo.ExecutableName}.exe.\r\n" +
                "3. Choose Single Player or Play Online.\r\n\r\n" +
                "Windows may show a SmartScreen warning for unsigned games: More info > Run anyway.\r\n" +
                "Online play needs an internet connection; single player works offline.\r\n");

            string zipPath = Path.Combine(OutputRoot, ZipName);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(WindowsFolder, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);
            string hash = Sha256(zipPath);
            File.WriteAllText(zipPath + ".sha256", $"{hash}  {ZipName}\n");

            long mb = new FileInfo(zipPath).Length / (1024 * 1024);
            message = $"Built {options.locationPathName} and packaged {zipPath} ({mb} MB, sha256 {hash}).";
            return true;
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var bytes = sha.ComputeHash(stream);
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
