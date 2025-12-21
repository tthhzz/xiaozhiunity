using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace XiaoZhi.Unity.Editor
{
    public static class AudioImportPreProcessor
    {
        private const int ConstantSampleRate = 16000;
        
        [MenuItem("Assets/Audio/PreProcess", isValidateFunction: false, priority: 1001)]
        private static void PreProcess()
        {
            var objects = Selection.objects;
            foreach (var o in objects)
                PreProcess(AssetDatabase.GetAssetPath(o));
            AssetDatabase.Refresh();
        }

        private static void PreProcess(string path)
        {
            var tempPath = Path.Combine(Path.GetDirectoryName(path)!,
                $"{Path.GetFileNameWithoutExtension(path)}_{Random.Range(0, int.MaxValue)}{Path.GetExtension(path)}");
            if (File.Exists(tempPath)) AssetDatabase.DeleteAsset(tempPath);
            var success = ResampleAudio(path, tempPath, ConstantSampleRate);
            if (!success)
            {
                Debug.LogError($"Resample failed: {path}");
                return;
            }

            File.Move(path, $"{path}.bak");
            AssetDatabase.RenameAsset(tempPath, Path.GetFileName(path));
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            importer.forceToMono = true;
            importer.SaveAndReimport();
        }


        private static bool ResampleAudio(string inputPath, string outputPath, int sampleRate)
        {
#if UNITY_EDITOR_WIN
            var ffmpegPath = Path.Combine(Application.dataPath, "Plugins/ffmpeg/win/bin/ffmpeg.exe");
#elif UNITY_EDITOR_OSX
            var ffmpegPath = Path.Combine(Application.dataPath, "Plugins/ffmpeg/mac/ffmpeg");
#endif
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                Debug.LogError("ffmpeg is missing in current platform.");
                return false;
            }

            var arguments = $"-y -i \"{inputPath}\" -ar {sampleRate} \"{outputPath}\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(startInfo);
            if (process == null) return false;
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                Debug.LogError($"ffmpeg error: {error}");
                return false;
            }

            Debug.Log($"Resample success！Output path: {outputPath}");
            AssetDatabase.Refresh();
            return true;
        }
    }
}