using System;
using System.IO;
using System.Linq;
using System.Threading;
using Sekai.MusicScoreMaker.Ingame.AudioAssist;
using UnityEditor;
using UnityEngine;

// Explicit batch validation; never enters gameplay or changes a song package.
public static class AudioAssistNativeValidation
{
    public static async void Run()
    {
        try
        {
            string fixture = Environment.GetEnvironmentVariable("OPENSEKAI_NATIVE_FIXTURE");
            if (string.IsNullOrEmpty(fixture)) throw new ArgumentException("Set OPENSEKAI_NATIVE_FIXTURE to a PCM16 stereo WAV.");
            float[] pcm; int channels = 0, rate = 0;
            using (var reader = new BinaryReader(File.OpenRead(fixture)))
            {
                reader.BaseStream.Position = 12;
                pcm = null;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string id = new string(reader.ReadChars(4)); int size = reader.ReadInt32(); long next = reader.BaseStream.Position + size + (size & 1);
                    if (id == "fmt ")
                    {
                        if (reader.ReadInt16() != 1) throw new IOException("Fixture must be PCM16");
                        channels = reader.ReadInt16(); rate = reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt16();
                        if (reader.ReadInt16() != 16) throw new IOException("Fixture must be PCM16");
                    }
                    if (id == "data") { pcm = new float[size / 2]; for (int i = 0; i < pcm.Length; i++) pcm[i] = reader.ReadInt16() / 32768f; }
                    reader.BaseStream.Position = next;
                }
            }
            string root = Path.GetFullPath("Logs/NativeSmoke"); Directory.CreateDirectory(root);
            int main = Thread.CurrentThread.ManagedThreadId;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var result = await AudioAssistLocalAnalysis.RunPcm(pcm, channels, rate, Path.ChangeExtension(fixture, ".lrc"), root, "ja", .2,
                (s, p) => { if (Thread.CurrentThread.ManagedThreadId != main) throw new Exception("Progress left Unity thread"); Debug.Log($"NATIVE {p:P0}: {s}"); }, CancellationToken.None);
            if (result.stems.Count != 5 || result.lyrics.Count == 0 || result.lyrics.Any(l => l.syllables.Count != 4))
                throw new Exception("Missing stems or repeated vocal syllables");
            if (result.lyrics.Any(l => Math.Abs(l.syllables[0].seconds - l.seconds) > 1.2))
                throw new Exception("Repeated refrain escaped its LRC anchor");
            long frames = (long)Math.Round((double)pcm.Length / channels * 44100 / rate);
            foreach (var stem in result.stems)
                if (new FileInfo(stem.path).Length != 44 + frames * 4) throw new Exception("Stem duration changed");
            foreach (var point in result.lyrics.SelectMany(l => l.syllables))
                if (point.seconds < 0 || point.end <= point.seconds || point.end > (double)pcm.Length / channels / rate || float.IsNaN(point.confidence))
                    throw new Exception("Invalid vocal timing");
            File.WriteAllText(Path.Combine(root, "verified.json"), JsonUtility.ToJson(result, true));
            Debug.Log("NATIVE REAL AUDIO PIPELINE PASSED in " + timer.Elapsed.TotalSeconds + " seconds");
            // A pre-cancelled job must neither create partial results nor lock out
            // a later job. Cancel during native inference in the Python ABI test.
            int before = Directory.GetDirectories(root, "analysis_*").Length;
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            try
            {
                await AudioAssistLocalAnalysis.RunPcm(pcm, channels, rate, null, root, "ja", 0, null, cancel.Token);
                throw new Exception("Cancelled analysis unexpectedly ran");
            }
            catch (OperationCanceledException) { }
            if (Directory.GetDirectories(root, "analysis_*").Length != before) throw new Exception("Cancelled job left output");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
}
