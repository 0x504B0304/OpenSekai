using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Sekai.CustomMusicScoreManager
{
	// MediaCodec and disk I/O run on a Java worker; Unity UI stays on the main thread.
	public sealed class AndroidVideoEncoder : NativeVideoEncoder
	{
		public override string EncoderName => "Android MediaCodec";
		public override bool IsAvailable => Application.platform == RuntimePlatform.Android;
		public bool LastEncodingSucceeded { get; private set; }
		public string LastError { get; private set; }
		private AndroidJavaObject job;
		private bool cancelled;

		public static string CheckSupport(int width, int height, int frameRate)
		{
			if (Application.platform != RuntimePlatform.Android) return null;
			try
			{
				using (var helper = new AndroidJavaClass("com.opensekai.VideoEncoderJob"))
					return helper.CallStatic<string>("checkSupport", width, height, frameRate);
			}
			catch (Exception ex) { return "无法初始化 Android 编码器：" + ex.Message; }
		}

		public override IEnumerator EncodeVideo(string frameSequencePath, string audioPath, string outputPath,
			int frameRate, int width, int height, int speedMultiplier, Action<float, string> onProgress = null)
		{
			this.onProgress = onProgress;
			LastEncodingSucceeded = false; LastError = null; cancelled = false;
			IsEncoding = true;
			try
			{
				if (!IsAvailable) LastError = "当前平台不支持 Android 编码器。";
				else
				{
					var info = ReadFrameSequenceInfo(frameSequencePath, speedMultiplier);
					if (info == null) LastError = "无法读取录制帧信息。";
					else
					{
						LastError = CheckSupport(info.Width, info.Height, info.OriginalFrameRate);
						if (string.IsNullOrEmpty(LastError))
							job = new AndroidJavaObject("com.opensekai.VideoEncoderJob", frameSequencePath, audioPath, outputPath,
								info.Width, info.Height, info.OriginalFrameRate, info.FrameCount, videoBitrate, audioBitrate);
					}
				}
			}
			catch (Exception ex) { LastError = ex.Message; }

			try
			{
				while (job != null && !cancelled)
				{
					bool finished = false;
					try
					{
						finished = job.Call<bool>("isDone");
						UpdateProgress(job.Call<float>("getProgress"), job.Call<string>("getStatus"));
						if (finished)
						{
							LastEncodingSucceeded = job.Call<bool>("isSucceeded") && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
							LastError = job.Call<string>("getError");
						}
					}
					catch (Exception ex) { LastError = ex.Message; finished = true; }
					if (finished) break;
					yield return new WaitForSecondsRealtime(0.1f);
				}
				if (cancelled) LastError = "视频生成已取消，录制素材已保留。";
				if (!LastEncodingSucceeded)
				{
					if (string.IsNullOrEmpty(LastError)) LastError = "Android 视频编码失败。";
					UpdateProgress(0, LastError);
				}
			}
			finally
			{
				ReleaseJob();
				IsEncoding = false;
			}
		}

		private void ReleaseJob()
		{
			if (job == null) return;
			try { if (!job.Call<bool>("isDone")) job.Call("cancel"); }
			catch (Exception ex) { Debug.LogWarning(ex.Message); }
			finally { job.Dispose(); job = null; }
		}

		public override string GetCapabilities() => "H.264 + AAC / MP4，原生后台线程编码，无需 FFmpeg。";
		public override void CancelEncoding()
		{
			cancelled = true;
			LastEncodingSucceeded = false;
			ReleaseJob();
			base.CancelEncoding();
		}
		private void OnDestroy() => CancelEncoding();
	}
}
