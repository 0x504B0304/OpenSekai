using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

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
        [Serializable] private sealed class Progress { public string message;public float progress; }
        public static string BundleDirectory
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath,"..",".audio-assist-bundle"));
#else
                return Path.GetFullPath(Path.Combine(Application.dataPath,"..","AudioAssistEngine"));
#endif
            }
        }
        public static bool Available => (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            && File.Exists(Path.Combine(BundleDirectory,"manifest.json"))
            && File.Exists(Path.Combine(BundleDirectory,"runtime","python.exe"));
        public static async Task<AssistAnalysisOutput> Run(string audio, string lrc, string directory, string language, double offset, Action<string> progress, CancellationToken token)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            string bundle = BundleDirectory;
            string python = Path.Combine(bundle,"runtime","python.exe");
            if (!Available) throw new IOException("程序缺少内置分析引擎，请使用含 AudioAssistEngine 的完整 Windows 版本");
#if UNITY_EDITOR
            string worker=Path.Combine(Application.streamingAssetsPath,"AudioAssist","worker.py");
#else
            string worker=Path.Combine(bundle,"worker.py");
#endif
            foreach (string file in new[]{"worker.py","models/htdemucs.json","models/htdemucs.pt","models/mms_fa.pt","decoder/ffmpeg.exe"})
                if(!File.Exists(Path.Combine(bundle,file)))throw new FileNotFoundException("内置分析引擎文件缺失，请重新解压完整程序",file);
            string output=Path.Combine(directory,"analysis_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
            string status=Path.Combine(output,"progress.json");
            var args=new List<string>{"-I","-B","-X","utf8",worker,"--models",Path.Combine(bundle,"models"),"--ffmpeg",Path.Combine(bundle,"decoder","ffmpeg.exe"),"--audio",audio,"--output",output,"--language",language,"--offset",offset.ToString(System.Globalization.CultureInfo.InvariantCulture)};
            if(!string.IsNullOrEmpty(lrc)){args.Add("--lrc");args.Add(lrc);}
            var command=new StringBuilder();foreach(string arg in args){if(command.Length>0)command.Append(' ');command.Append(Quote(arg));}
            using var process=new Process{StartInfo=new ProcessStartInfo(python,command.ToString()){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=output}};
            process.StartInfo.EnvironmentVariables["PYTHONUTF8"]="1";
            process.StartInfo.EnvironmentVariables["PYTHONIOENCODING"]="utf-8";
            process.StartInfo.EnvironmentVariables["HF_HUB_OFFLINE"]="1";
            process.Start();Task<string> stdout=process.StandardOutput.ReadToEndAsync(),stderr=process.StandardError.ReadToEndAsync();
            try
            {
                while(!process.HasExited)
                {
                    token.ThrowIfCancellationRequested();
                    if(File.Exists(status))try{var value=JsonUtility.FromJson<Progress>(File.ReadAllText(status));progress(value.message+" "+Mathf.RoundToInt(value.progress*100)+"%");}catch(IOException){}
                    await Task.Delay(250,token);
                }
                token.ThrowIfCancellationRequested();
                string log=await stderr;await stdout;
                if(process.ExitCode!=0)throw new IOException("本机分析失败："+log.Substring(Math.Max(0,log.Length-700)));
                string result=Path.Combine(output,"result.json");if(!File.Exists(result))throw new IOException("分析没有生成结果");
                return JsonUtility.FromJson<AssistAnalysisOutput>(File.ReadAllText(result))??throw new IOException("分析结果格式无效");
            }
            finally
            {
                if (!process.HasExited)
                {
                    // taskkill also terminates a decoding child; Process.Kill alone does not.
                    try
                    {
                        using var kill = Process.Start(new ProcessStartInfo("taskkill.exe", "/PID " + process.Id + " /T /F")
                        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
                        if (kill != null) { await kill.StandardOutput.ReadToEndAsync();await kill.StandardError.ReadToEndAsync(); }
                    }
                    catch (Exception) { try { process.Kill(); } catch (InvalidOperationException) { } }
                }
            }
#else
            await Task.CompletedTask;
            throw new PlatformNotSupportedException("本机模型分析目前支持 Windows；移动端可导入分轨与增强 LRC");
#endif
        }
        internal static string Quote(string value)
        {
            var b=new StringBuilder("\"");int slashes=0;
            foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"'){b.Append('\\',slashes*2+1);b.Append(c);slashes=0;continue;}b.Append('\\',slashes);slashes=0;b.Append(c);}
            b.Append('\\',slashes*2);b.Append('"');return b.ToString();
        }
    }
}
