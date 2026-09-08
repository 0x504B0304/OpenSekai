using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Process = System.Diagnostics.Process; // Alias to avoid Debug conflict
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo; // Alias to avoid Debug conflict

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>
	/// Windows平台视频编码器
	/// 使用FFmpeg进行视频编码（支持系统安装的FFmpeg或嵌入式FFmpeg）
	/// </summary>
	public class WindowsVideoEncoder : NativeVideoEncoder
	{
		#region Properties

		public override string EncoderName => "Windows Video Encoder";
		public override bool IsAvailable => CheckFFmpegAvailable();
		public bool LastEncodingSucceeded { get; private set; }

		#endregion

		#region Fields

		private string ffmpegPath = "ffmpeg";
		private Process encodingProcess;
		private bool isCancelled = false;

		#endregion

		#region Public API

		public override IEnumerator EncodeVideo(
			string frameSequencePath,
			string audioPath,
			string outputPath,
			int frameRate,
			int width,
			int height,
			int speedMultiplier,
			Action<float, string> onProgress = null)
		{
			this.onProgress = onProgress;
			LastEncodingSucceeded = false;
			IsEncoding = true;
			isCancelled = false;

			Debug.Log($"[{EncoderName}] 开始编码视频: {frameSequencePath}");

			// 读取帧序列信息
			FrameSequenceInfo frameInfo = ReadFrameSequenceInfo(frameSequencePath, speedMultiplier);
			if (frameInfo == null)
			{
				UpdateProgress(0f, "读取帧序列信息失败");
				IsEncoding = false;
				yield break;
			}

			UpdateProgress(0.1f, $"读取帧序列: {frameInfo.FrameCount}帧");

			// 检查FFmpeg是否可用
			if (!CheckFFmpegAvailable())
			{
				Debug.LogWarning($"[{EncoderName}] FFmpeg不可用，生成图片序列描述文件");
				yield return StartCoroutine(GenerateFrameSequenceDescription(
					frameSequencePath,
					audioPath,
					outputPath,
					frameInfo,
					onProgress));
				IsEncoding = false;
				yield break;
			}

			// 使用FFmpeg编码
			yield return StartCoroutine(EncodeWithFFmpeg(
				frameSequencePath,
				audioPath,
				outputPath,
				frameInfo,
				onProgress));

			IsEncoding = false;
		}

		public override string GetCapabilities()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("Windows视频编码器能力:");
			sb.AppendLine("- 支持H.264视频编码");
			sb.AppendLine("- 支持AAC音频编码");
			sb.AppendLine("- 支持MP4容器格式");
			sb.AppendLine($"- FFmpeg可用: {CheckFFmpegAvailable()}");
			return sb.ToString();
		}

		#endregion

		#region Private Methods

		private bool CheckFFmpegAvailable()
		{
			ffmpegPath = FFmpegLocator.FindAvailable(GetExecutableDirectory(), Environment.GetEnvironmentVariable("PATH"));
			return !string.IsNullOrEmpty(ffmpegPath);
		}

		/// <summary>
		/// 检测FFmpeg是否支持某个编码器
		/// </summary>
		/// <param name="encoderName">编码器名称（如libx264, mpeg4, aac）</param>
		/// <returns>是否支持该编码器</returns>
		private bool CheckEncoderAvailability(string encoderName)
		{
			try
			{
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					FileName = ffmpegPath,
					Arguments = $"-hide_banner -encoders",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				};

				using (Process process = Process.Start(startInfo))
				{
					if (process != null)
					{
						string output = process.StandardOutput.ReadToEnd();
						process.WaitForExit(5000);
						// 检查输出中是否包含该编码器
						return output.Contains(encoderName);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[{EncoderName}] 检测编码器失败: {ex.Message}");
			}

			return false;
		}

		/// <summary>
		/// 获取可执行文件目录
		/// </summary>
		/// <returns>可执行文件所在目录路径</returns>
		private string GetExecutableDirectory()
		{
			try
			{
				// 对于Windows独立应用，Application.dataPath指向游戏数据目录
				// 可执行文件在同级目录
				if (Application.platform == RuntimePlatform.WindowsPlayer)
				{
					string dataPath = Application.dataPath;
					// 例如：C:/Game/GameName_Data -> C:/Game
					string parentDir = Path.GetDirectoryName(dataPath);
					return parentDir;
				}
				// 对于编辑器模式，返回项目目录
				else if (Application.platform == RuntimePlatform.WindowsEditor)
				{
					// 在编辑器模式下，检查项目根目录
					string projectDir = Path.GetDirectoryName(Application.dataPath);
					return projectDir;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[{EncoderName}] 获取可执行文件目录失败: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// 使用FFmpeg编码视频
		/// </summary>
		private IEnumerator EncodeWithFFmpeg(
			string frameSequencePath,
			string audioPath,
			string outputPath,
			FrameSequenceInfo frameInfo,
			Action<float, string> onProgress)
		{
			// 确保输出目录存在
			string outputDirectory = Path.GetDirectoryName(outputPath);
			if (!string.IsNullOrEmpty(outputDirectory))
			{
				Directory.CreateDirectory(outputDirectory);
			}

			// 检查帧序列目录是否存在且有帧文件
			if (!Directory.Exists(frameSequencePath))
			{
				Debug.LogError($"[{EncoderName}] 帧序列目录不存在: {frameSequencePath}");
				UpdateProgress(0f, $"帧序列目录不存在: {frameSequencePath}");
				IsEncoding = false;
				yield break;
			}

			string[] frameFiles = Directory.GetFiles(frameSequencePath, "frame_*.jpg");
			if (frameFiles.Length == 0)
			{
				Debug.LogError($"[{EncoderName}] 帧序列目录中没有帧文件: {frameSequencePath}");
				UpdateProgress(0f, "帧序列目录中没有帧文件");
				IsEncoding = false;
				yield break;
			}

			// 对帧文件排序以确保顺序正确
			Array.Sort(frameFiles);

			Debug.Log($"[{EncoderName}] 找到 {frameFiles.Length} 个帧文件在目录: {frameSequencePath}");
			if (frameFiles.Length > 0)
			{
				Debug.Log($"[{EncoderName}] 第一帧文件: {frameFiles[0]}");
				Debug.Log($"[{EncoderName}] 最后一帧文件: {frameFiles[frameFiles.Length - 1]}");

				// 检查帧文件命名是否正确（FFmpeg期望从1开始编号）
				string firstFrameFileName = Path.GetFileName(frameFiles[0]);
				if (firstFrameFileName == "frame_000000.jpg")
				{
					Debug.LogError($"[{EncoderName}] 帧文件命名错误：第一帧是frame_000000.jpg，FFmpeg期望从frame_000001.jpg开始");
					UpdateProgress(0f, "帧文件命名错误，无法编码");
					IsEncoding = false;
					yield break;
				}
				else if (!firstFrameFileName.StartsWith("frame_000001"))
				{
					Debug.LogWarning($"[{EncoderName}] 帧文件命名可能不连续：第一帧是{firstFrameFileName}");
				}
			}

			// 检测可用的编码器
			bool hasLibx264 = CheckEncoderAvailability("libx264");
			bool hasMpeg4 = CheckEncoderAvailability("mpeg4");
			bool hasAac = CheckEncoderAvailability("aac");
			Debug.Log($"[{EncoderName}] 编码器检测结果: libx264={hasLibx264}, mpeg4={hasMpeg4}, aac={hasAac}");

			// 如果没有合适的视频编码器，使用备用方案
			if (!hasLibx264 && !hasMpeg4)
			{
				Debug.LogWarning($"[{EncoderName}] FFmpeg不支持libx264或mpeg4编码器，生成帧序列描述文件");
				yield return StartCoroutine(GenerateFrameSequenceDescription(
					frameSequencePath,
					audioPath,
					outputPath,
					frameInfo,
					onProgress));
				yield break;
			}

			// 构建FFmpeg命令
			// 保持录制帧率，与 CRI 采集的音乐和音效合成。

			StringBuilder argsBuilder = new StringBuilder();

			// 输入帧序列
			// 注意：FFmpeg的%06d模式期望文件从1开始编号（frame_000001.jpg）
			// FFmpeg需要使用正斜杠路径，即使在Windows上
			string normalizedPath = frameSequencePath.Replace('\\', '/');
			string framePattern = $"{normalizedPath}/frame_%06d.jpg";
			Debug.Log($"[{EncoderName}] 帧输入模式: {framePattern}");
			Debug.Log($"[{EncoderName}] 帧率: {frameInfo.OriginalFrameRate}, 速度倍数: {frameInfo.SpeedMultiplier}");
			argsBuilder.Append($"-nostdin -nostats -progress pipe:1 -framerate {frameInfo.OriginalFrameRate} -start_number 1 -i \"{framePattern}\"");

			// 输入音频（如果有）
			bool hasAudio = !string.IsNullOrEmpty(audioPath) && File.Exists(audioPath);
			if (hasAudio)
			{
				// FFmpeg需要使用正斜杠路径
				string normalizedAudioPath = audioPath.Replace('\\', '/');
				argsBuilder.Append($" -i \"{normalizedAudioPath}\"");
			}

			// 新方案：不需要视频滤镜调整速度

			// 视频编码设置 - 根据可用编码器选择
			if (hasLibx264)
			{
				argsBuilder.Append($" -c:v libx264 -preset medium -b:v {videoBitrate}");
			}
			else if (hasMpeg4)
			{
				// 使用mpeg4编码器（较老的编码器，但兼容性好）
				argsBuilder.Append($" -c:v mpeg4 -b:v {videoBitrate}");
				Debug.LogWarning($"[{EncoderName}] 使用mpeg4编码器（libx264不可用）");
			}

			// 音频编码设置（如果有音频）
			if (hasAudio)
			{
				if (hasAac)
				{
					argsBuilder.Append($" -c:a aac -b:a {audioBitrate}");
				}
				else
				{
					// 使用mp3作为备用音频编码器
					argsBuilder.Append($" -c:a libmp3lame -b:a {audioBitrate}");
					Debug.LogWarning($"[{EncoderName}] 使用libmp3lame编码器（aac不可用）");
				}
				argsBuilder.Append(" -map 0:v:0 -map 1:a:0 -af apad -shortest");
			}

			// 输出文件
			// FFmpeg需要使用正斜杠路径
			string normalizedOutputPath = outputPath.Replace('\\', '/');
			argsBuilder.Append($" -pix_fmt yuv420p -movflags +faststart -map_metadata -1 -y \"{normalizedOutputPath}\"");

			string args = argsBuilder.ToString();
			Debug.Log($"[{EncoderName}] FFmpeg命令: ffmpeg {args}");

			UpdateProgress(0.2f, "正在编码视频...");

			// 执行FFmpeg命令
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = ffmpegPath,
				Arguments = args,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};

			bool success = false;
			float progressStart = 0.2f;
			float progressEnd = 0.9f;
			StringBuilder errorBuilder = new StringBuilder();
			var progressLines = new ConcurrentQueue<string>();

			try
			{
				encodingProcess = new Process();
				encodingProcess.StartInfo = startInfo;
				encodingProcess.EnableRaisingEvents = true;
				encodingProcess.OutputDataReceived += (sender, e) =>
				{
					if (e.Data != null) progressLines.Enqueue(e.Data);
				};

				encodingProcess.ErrorDataReceived += (sender, e) =>
				{
					if (!string.IsNullOrEmpty(e.Data))
					{
						lock (errorBuilder) errorBuilder.AppendLine(e.Data);
					}
				};

				encodingProcess.Start();
				encodingProcess.BeginOutputReadLine();
				encodingProcess.BeginErrorReadLine();
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{EncoderName}] FFmpeg进程启动异常: {ex.Message}");
				encodingProcess?.Dispose();
				encodingProcess = null;
				UpdateProgress(0f, "编码失败");
				yield break;
			}

			// 等待进程完成（在 try-catch 外使用 yield）
			while (!encodingProcess.HasExited && !isCancelled)
			{
				// Process callbacks run on worker threads. Only this coroutine may update Unity UI.
				while (progressLines.TryDequeue(out string line))
					ParseFFmpegProgress(line, frameInfo.AdjustedDuration, progressStart, progressEnd);
				yield return new WaitForSecondsRealtime(0.1f);
			}

			if (isCancelled)
			{
				try
				{
					encodingProcess.Kill();
				}
				catch { }
				encodingProcess.Dispose();
				encodingProcess = null;
				UpdateProgress(0f, "编码已取消");
				yield break;
			}

			try
			{
				encodingProcess.WaitForExit();
				while (progressLines.TryDequeue(out string line))
					ParseFFmpegProgress(line, frameInfo.AdjustedDuration, progressStart, progressEnd);
				success = encodingProcess.ExitCode == 0;

				if (!success)
				{
					string errorOutput = errorBuilder.ToString();
					Debug.LogError($"[{EncoderName}] FFmpeg编码失败:\n{errorOutput}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{EncoderName}] FFmpeg进程结束异常: {ex.Message}");
				success = false;
			}
			finally
			{
				encodingProcess.Dispose();
			}

			encodingProcess = null;

			if (success && File.Exists(outputPath))
			{
				LastEncodingSucceeded = new FileInfo(outputPath).Length > 0;
				long fileSize = new FileInfo(outputPath).Length;
				UpdateProgress(1f, $"编码完成: {fileSize / 1024 / 1024:F2}MB");
				Debug.Log($"[{EncoderName}] 视频编码成功: {outputPath}");
			}
			else
			{
				UpdateProgress(0f, "编码失败");
				Debug.LogError($"[{EncoderName}] 视频编码失败");
			}
		}

		/// <summary>
		/// 解析FFmpeg进度信息
		/// </summary>
		private void ParseFFmpegProgress(string output, float totalDuration, float progressStart, float progressEnd)
		{
			if (!TryReadProgressSeconds(output, out double seconds)) return;
			float fraction = totalDuration > 0 ? Mathf.Clamp01((float)seconds / totalDuration) : 0;
			UpdateProgress(progressStart + (progressEnd - progressStart) * fraction,
				$"编码中… {seconds:F1}秒 / {totalDuration:F1}秒");
		}

		public static bool TryReadProgressSeconds(string line, out double seconds)
		{
			seconds = 0;
			const string prefix = "out_time_us=";
			if (line == null || !line.StartsWith(prefix, StringComparison.Ordinal)) return false;
			if (!long.TryParse(line.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out long microseconds) || microseconds < 0) return false;
			seconds = microseconds / 1000000d;
			return true;
		}

		/// <summary>
		/// 生成帧序列描述文件（当FFmpeg不可用时）
		/// </summary>
		private IEnumerator GenerateFrameSequenceDescription(
			string frameSequencePath,
			string audioPath,
			string outputPath,
			FrameSequenceInfo frameInfo,
			Action<float, string> onProgress)
		{
			UpdateProgress(0.5f, "生成帧序列描述文件...");

			try
			{
				// 生成一个描述文件，包含帧序列信息
				string descPath = Path.ChangeExtension(outputPath, ".frameseq");
				StringBuilder descBuilder = new StringBuilder();

				// FFmpeg需要使用正斜杠路径
				string normalizedFrameSequencePath = frameSequencePath.Replace('\\', '/');
				string normalizedAudioPath = !string.IsNullOrEmpty(audioPath) ? audioPath.Replace('\\', '/') : "";
				string normalizedOutputPath = outputPath.Replace('\\', '/');

				descBuilder.AppendLine($"# Video Frame Sequence Description");
				descBuilder.AppendLine($"# Generated by WindowsVideoEncoder");
				descBuilder.AppendLine($"frame_directory={normalizedFrameSequencePath}");
				descBuilder.AppendLine($"frame_count={frameInfo.FrameCount}");
				descBuilder.AppendLine($"original_frame_rate={frameInfo.OriginalFrameRate}");
				descBuilder.AppendLine($"output_frame_rate={outputFrameRate}");
				descBuilder.AppendLine($"width={frameInfo.Width}");
				descBuilder.AppendLine($"height={frameInfo.Height}");
				descBuilder.AppendLine($"original_duration={frameInfo.OriginalDuration}");
				descBuilder.AppendLine($"adjusted_duration={frameInfo.AdjustedDuration}");
				descBuilder.AppendLine($"speed_multiplier={frameInfo.SpeedMultiplier}");
				descBuilder.AppendLine($"audio_path={normalizedAudioPath}");
				descBuilder.AppendLine();
				descBuilder.AppendLine($"# To encode this video, use FFmpeg with the following command:");
				// 新方案：正常速度录制，无需setpts滤镜
				string ffmpegCmd = $"ffmpeg -framerate {frameInfo.OriginalFrameRate} -i \"{normalizedFrameSequencePath}/frame_%06d.jpg\" -c:v libx264 -preset medium -b:v {videoBitrate} \"{normalizedOutputPath}\"";
				descBuilder.AppendLine(ffmpegCmd);

				File.WriteAllText(descPath, descBuilder.ToString());

				UpdateProgress(1f, "已生成帧序列描述文件");

				Debug.Log($"[{EncoderName}] 已生成帧序列描述文件: {descPath}");
				Debug.Log($"[{EncoderName}] 请手动使用FFmpeg编码视频");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{EncoderName}] 生成帧序列描述文件失败: {ex.Message}");
				UpdateProgress(0f, "生成失败");
			}

			yield return null;
		}

		#endregion

		#region Cleanup

		private void OnDestroy() => CancelEncoding();

		public override void CancelEncoding()
		{
			base.CancelEncoding();
			isCancelled = true;

			if (encodingProcess != null && !encodingProcess.HasExited)
			{
				try
				{
					encodingProcess.Kill();
					Debug.Log($"[{EncoderName}] 已终止编码进程");
				}
				catch (Exception ex)
				{
					Debug.LogError($"[{EncoderName}] 终止编码进程失败: {ex.Message}");
				}
			}
		}

		#endregion
	}
}
