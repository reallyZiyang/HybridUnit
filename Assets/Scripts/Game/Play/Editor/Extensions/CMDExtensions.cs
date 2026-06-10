using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Play.Editor.Extensions
{
    public static class CmdExtensions
    {
        private const string k_ConfigPath = "game-configs";
        private const string k_ProjectPath = "game-client";

        [MenuItem("CMD/SVN Update %#u", false, 102)]
        private static void SvnUpdate()
        {
            ExecSvnUpdate(new[] { $"../{k_ConfigPath}", $"../{k_ProjectPath}" });
        }

        [MenuItem("CMD/SVN Commit %#i", false, 101)]
        private static void SvnCommit()
        {
            ExecSvnCommit($"../{k_ProjectPath}");
        }

        [MenuItem("CMD/SVN Cleanup", false, 100)]
        private static void SvnCleanup()
        {
            ExecSnvCleanup($"../{k_ProjectPath}");
        }

        [MenuItem("CMD/Gen Proto %#p", false, 10)]
        private static void ImportProtos()
        {
            ExecSvnUpdate("../game-configs");

            var directoryInfo = Directory.GetParent(Application.dataPath)?.Parent;
            if (directoryInfo != null)
            {
                var basePath = directoryInfo.FullName;
                var workingPath = Path.Join(basePath, k_ConfigPath, "Tools/Sproto");
                var fileName = Path.Join(workingPath, "gen_cs.bat");
                Debug.Log(ProcessAsyncHelper.ExecuteBat(workingPath, fileName));
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("CMD/Gen Config %#o", false, 10)]
        private static void ImportConfig()
        {
            var directoryInfo = Directory.GetParent(Application.dataPath)?.Parent;
            if (directoryInfo == null) return;

            var workingDir = Path.Join(directoryInfo.FullName, k_ConfigPath);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{Path.Join(workingDir, "Gen.bat")}\"",
                WorkingDirectory = workingDir,
                CreateNoWindow = false,
                UseShellExecute = true
            };

            Process.Start(psi);
        }

        private static void ExecSvnUpdate(string path)
        {
            var arguments = $"/c TortoiseProc.exe /command:update /path:\"{path}\" /closeonend:3";
            ProcessAsyncHelper.ExecuteShellCommand("cmd.exe", arguments);
        }

        private static void ExecSvnUpdate(string[] paths)
        {
            var arguments = $"/c TortoiseProc.exe /command:update /path:\"{string.Join("*", paths)}\" /closeonend:3";
            ProcessAsyncHelper.ExecuteShellCommand("cmd.exe", arguments);
        }

        private static void ExecSvnCommit(string path)
        {
            var arguments = $"/c TortoiseProc.exe /command:commit /path:{path} /closeonend:0";
            ProcessAsyncHelper.ExecuteShellCommand("cmd.exe", arguments);
        }

        private static void ExecSnvCleanup(string path)
        {
            var arguments = $"/c TortoiseProc.exe /command:cleanup /path:{path} /closeonend:3";
            ProcessAsyncHelper.ExecuteShellCommand("cmd.exe", arguments);
        }
    }
}