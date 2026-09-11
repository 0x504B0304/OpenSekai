using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    [Serializable] public sealed class AssistAnalysisOutput
    {
        public List<AssistStem> stems = new List<AssistStem>();
        public List<AssistLyric> lyrics = new List<AssistLyric>();
        public List<string> warnings = new List<string>();
    }

    public static class AudioAssistLocalAnalysis
    {
        [Serializable] public sealed class ModelFile { public string path, sha256; public long bytes; }
        [Serializable] public sealed class ModelManifest { public int schema; public string version; public List<ModelFile> files; }
        public static readonly string[] RequiredFiles = { "htdemucs.onnx", "mms_fa.int8.onnx", "tokens.json", "ja-reading.tsv", "ja-roman.tsv", "zh-reading.tsv" };
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        public static string BundleDirectory => Path.Combine(Application.streamingAssetsPath, "AudioAssistNative");
        public static bool Available => AudioAssistNative.Available;

        // Called on Unity's main thread. The worker owns only PCM and filesystem
        // paths; Unity objects and progress callbacks stay on the main thread.
        public static async Task<AssistAnalysisOutput> RunPcm(float[] pcm, int channels, int sampleRate,
            string lrcPath, string directory, string language, double offset,
            Action<string, float> progress, CancellationToken token)
        {
            if (!Available) throw new PlatformNotSupportedException("此版本缺少本机分析插件，请使用完整的 Windows x64 或 Android ARM64 版本");
            if (language != "ja" && language != "zh" && language != "en") throw new ArgumentException("不支持的歌词语言");
            string bundled = BundleDirectory;
            string cache = Path.Combine(Application.persistentDataPath, "AudioAssistModels");
            int threads = Math.Max(1, Math.Min(Application.isMobilePlatform ? 2 : 4, SystemInfo.processorCount / 2));
            bool completed = false;
            IProgress<(string message, float value)> posted = new Progress<(string message, float value)>(p =>
            { if (!completed && !token.IsCancellationRequested) progress?.Invoke(p.message, p.value); });
            progress?.Invoke("准备内置分析引擎", 0);
            await Gate.WaitAsync(token);
            string output = Path.Combine(directory, "analysis_" + Guid.NewGuid().ToString("N"));
            try
            {
                string models = await PrepareModels(bundled, cache, (s, p) => progress?.Invoke(s, p * .08f), token);
                string lrc = string.IsNullOrEmpty(lrcPath) ? null : File.ReadAllText(lrcPath);
                var result = await Task.Run(() => AudioAssistNativePipeline.Run(pcm, channels, sampleRate, lrc,
                    output, models, language, offset, threads, (s, p) => posted.Report((s, .08f + p * .92f)), token), token);
                token.ThrowIfCancellationRequested();
                // Keep exact end times for cache recovery and manually editable tags.
                File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(result, true));
                completed = true;
                progress?.Invoke("分析完成", 1);
                return result;
            }
            catch
            {
                // Only the fresh GUID directory owned by this job is removed.
                if (Directory.Exists(output)) Directory.Delete(output, true);
                throw;
            }
            finally { completed = true; Gate.Release(); }
        }
        public static async Task<AssistAnalysisOutput> AlignPcm(float[] pcm, int channels, int sampleRate,
            string lrcPath, string directory, string language, double offset,
            Action<string, float> progress, CancellationToken token)
        {
            if (!Available) throw new PlatformNotSupportedException("此版本缺少本机分析插件，请使用完整的 Windows x64 或 Android ARM64 版本");
            if (string.IsNullOrEmpty(lrcPath) || !File.Exists(lrcPath)) throw new IOException("请先导入 LRC 歌词");
            string bundled = BundleDirectory;
            string cache = Path.Combine(Application.persistentDataPath, "AudioAssistModels");
            int threads = Math.Max(1, Math.Min(Application.isMobilePlatform ? 2 : 4, SystemInfo.processorCount / 2));
            bool completed = false;
            IProgress<(string message, float value)> posted = new Progress<(string message, float value)>(p =>
            { if (!completed && !token.IsCancellationRequested) progress?.Invoke(p.message, p.value); });
            await Gate.WaitAsync(token);
            try
            {
                string models = await PrepareModels(bundled, cache, (s, p) => progress?.Invoke(s, p * .08f), token);
                string lrc = File.ReadAllText(lrcPath);
                var result = await Task.Run(() => AudioAssistNativePipeline.AlignOnly(pcm, channels, sampleRate, lrc,
                    models, language, offset, threads, (s, p) => posted.Report((s, .08f + p * .92f)), token), token);
                token.ThrowIfCancellationRequested(); completed = true; progress?.Invoke("对齐完成", 1); return result;
            }
            finally { completed = true; Gate.Release(); }
        }

        public static ModelManifest ParseManifest(string json)
        {
            var manifest = JsonUtility.FromJson<ModelManifest>(json);
            if (manifest == null || manifest.schema != 1 || manifest.files == null ||
                string.IsNullOrEmpty(manifest.version) || !System.Text.RegularExpressions.Regex.IsMatch(manifest.version, "^[a-zA-Z0-9_-]+$"))
                throw new IOException("内置模型清单无效");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in manifest.files)
                if (file == null || Array.IndexOf(RequiredFiles, file.path) < 0 || !seen.Add(file.path) || file.bytes <= 0 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(file.sha256 ?? "", "^[a-f0-9]{64}$"))
                    throw new IOException("内置模型清单包含无效文件");
            foreach (string required in RequiredFiles) if (!seen.Contains(required)) throw new IOException("模型清单缺少 " + required);
            return manifest;
        }

        private static async Task<string> PrepareModels(string bundle, string cache, Action<string, float> progress, CancellationToken token)
        {
            bool packed = bundle.Contains("://");
            string json;
            if (packed)
            {
                using var request = UnityWebRequest.Get(bundle + "/manifest.json");
                await Send(request, token); json = request.downloadHandler.text;
            }
            else json = File.ReadAllText(Path.Combine(bundle, "manifest.json"));
            var manifest = ParseManifest(json);
            string root = packed ? Path.Combine(cache, manifest.version) : bundle;
            Directory.CreateDirectory(root);
            for (int i = 0; i < manifest.files.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var file = manifest.files[i]; string target = Path.Combine(root, file.path);
                progress("校验内置模型 " + (i + 1) + "/" + manifest.files.Count, (float)i / manifest.files.Count);
                if (await Task.Run(() => VerifyFile(target, file, token), token)) continue;
                if (!packed) throw new IOException("内置模型缺失或损坏：" + file.path + "，请重新安装完整版本");
                string temp = target + ".partial";
                try
                {
                    progress("首次准备离线模型（仅需一次）：" + file.path, (float)i / manifest.files.Count);
                    using (var request = UnityWebRequest.Get(bundle + "/" + file.path))
                    {
                        request.downloadHandler = new DownloadHandlerFile(temp) { removeFileOnAbort = true };
                        await Send(request, token);
                    }
                    if (!await Task.Run(() => VerifyFile(temp, file, token), token)) throw new IOException("内置模型校验失败：" + file.path);
                    token.ThrowIfCancellationRequested();
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(temp, target);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            return root;
        }
        private static async Task Send(UnityWebRequest request, CancellationToken token)
        {
            var operation = request.SendWebRequest();
            try
            {
                while (!operation.isDone) { token.ThrowIfCancellationRequested(); await Task.Delay(40, token); }
                token.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success) throw new IOException("读取内置模型失败：" + request.error);
            }
            catch { request.Abort(); throw; }
        }
        public static bool VerifyFile(string path, ModelFile entry, CancellationToken token)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != entry.bytes) return false;
            using var sha = SHA256.Create(); using var stream = File.OpenRead(path);
            var buffer = new byte[1024 * 1024]; int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            { token.ThrowIfCancellationRequested(); sha.TransformBlock(buffer, 0, count, buffer, 0); }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant() == entry.sha256;
        }
    }
}
