using System;
using System.IO;
using System.Threading;
using Sekai.MusicScoreMaker.Ingame.AudioAssist;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sekai.EditorTools
{
    public sealed class AudioAssistBundleBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 1000;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64 && report.summary.platform != BuildTarget.Android) return;
            Verify(AudioAssistLocalAnalysis.BundleDirectory);
            string plugins = Path.Combine(Application.dataPath, "Plugins/AudioAssistNative");
            string[] required = report.summary.platform == BuildTarget.Android
                ? new[] { "Android/arm64-v8a/libopensekai_audio.so", "Android/arm64-v8a/libonnxruntime.so" }
                : new[] { "Windows/x86_64/opensekai_audio.dll", "Windows/x86_64/onnxruntime.dll", "Windows/x86_64/msvcp140.dll", "Windows/x86_64/vcruntime140.dll" };
            foreach (string file in required)
            {
                string path = Path.Combine(plugins, file);
                var importer = AssetImporter.GetAtPath("Assets/Plugins/AudioAssistNative/" + file) as PluginImporter;
                if (!File.Exists(path) || importer == null || !importer.GetCompatibleWithPlatform(report.summary.platform))
                    throw new BuildFailedException("Missing/incompatible native analysis plugin: " + file + ". Run Tools/AudioAssist/prepare_native.py.");
            }
            if (report.summary.platform == BuildTarget.Android && PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                throw new BuildFailedException("Bundled audio analysis currently requires an ARM64-only Android build.");
            if (Directory.Exists(Path.Combine(Application.streamingAssetsPath, "AudioAssist")))
                throw new BuildFailedException("Legacy Python worker must not be included in StreamingAssets.");
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.StandaloneWindows64)
            {
                string data = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), Path.GetFileNameWithoutExtension(report.summary.outputPath) + "_Data");
                Verify(Path.Combine(data, "StreamingAssets/AudioAssistNative"));
                foreach (string file in new[] { "opensekai_audio.dll", "onnxruntime.dll" })
                    if (Directory.GetFiles(data, file, SearchOption.AllDirectories).Length == 0)
                        throw new BuildFailedException("Native analysis DLL missing from player: " + file);
                Debug.Log("Native offline audio models and Windows inference plugins verified.");
            }
        }
        public static void Verify(string root)
        {
            string manifestPath = Path.Combine(root, "manifest.json");
            if (!File.Exists(manifestPath)) throw new BuildFailedException("Run Tools/AudioAssist/prepare_native.py before building: native models missing.");
            var manifest = AudioAssistLocalAnalysis.ParseManifest(File.ReadAllText(manifestPath));
            foreach (var entry in manifest.files)
                if (!AudioAssistLocalAnalysis.VerifyFile(Path.Combine(root, entry.path), entry, CancellationToken.None))
                    throw new BuildFailedException("Missing/corrupt native model: " + entry.path);
            if (!File.Exists(Path.Combine(root, "THIRD_PARTY_NOTICES.md")) || !Directory.Exists(Path.Combine(root, "licenses")))
                throw new BuildFailedException("Native model licenses missing.");
        }
    }
}
