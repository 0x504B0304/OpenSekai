using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sekai.EditorTools
{
    // Outside StreamingAssets: do not import 2 GB of native DLLs into Unity or Android.
    public sealed class AudioAssistBundleBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        [Serializable] private sealed class Entry { public string path, sha256;public long bytes; }
        [Serializable] private sealed class Manifest { public int schema;public List<Entry> files; }
        public int callbackOrder => 1000;
        private static string Source => Path.GetFullPath(Path.Combine(Application.dataPath,"..",".audio-assist-bundle"));
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            Verify(Source);
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            string destination=Path.Combine(Path.GetDirectoryName(report.summary.outputPath),"AudioAssistEngine");
            var manifest=ReadManifest(Source);
            foreach(var entry in manifest.files)
            {
                string target=SafePath(destination,entry.path);Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(SafePath(Source,entry.path),target,true);
            }
            File.Copy(Path.Combine(Source,"manifest.json"),Path.Combine(destination,"manifest.json"),true);
            Verify(destination);
            Debug.Log("Bundled offline audio analysis verified: "+destination);
        }
        private static Manifest ReadManifest(string root)
        {
            string path=Path.Combine(root,"manifest.json");
            if(!File.Exists(path))throw new BuildFailedException("Missing bundled audio engine. Run Tools/AudioAssist/package_runtime.py in the development analysis environment before building Windows.");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            if(manifest==null||manifest.schema!=1||manifest.files==null||manifest.files.Count==0)throw new BuildFailedException("Invalid audio engine manifest");
            return manifest;
        }
        private static string SafePath(string root,string relative)
        {
            string full=Path.GetFullPath(Path.Combine(root,relative));
            if(!full.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new BuildFailedException("Invalid audio engine manifest path");
            return full;
        }
        public static void Verify(string root)
        {
            var manifest=ReadManifest(root);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var sha=SHA256.Create();
            foreach(var entry in manifest.files)
            {
                string path=SafePath(root,entry.path);seen.Add(entry.path);
                if(!File.Exists(path)||new FileInfo(path).Length!=entry.bytes)throw new BuildFailedException("Missing/truncated audio engine file: "+entry.path);
                using var stream=File.OpenRead(path);
                if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=entry.sha256)throw new BuildFailedException("Audio engine checksum mismatch: "+entry.path);
            }
            foreach(string required in new[]{"runtime/python.exe","models/htdemucs.json","models/htdemucs.pt","models/mms_fa.pt","decoder/ffmpeg.exe","worker.py","THIRD_PARTY_NOTICES.md"})
                if(!seen.Contains(required))throw new BuildFailedException("Audio engine manifest lacks "+required);
            // Never build a stale worker beside newer Unity integration.
            string worker=Path.Combine(Application.streamingAssetsPath,"AudioAssist","worker.py");
            if(File.ReadAllText(worker)!=File.ReadAllText(Path.Combine(root,"worker.py")))throw new BuildFailedException("Repackage audio engine: worker.py changed.");
        }
    }
}
