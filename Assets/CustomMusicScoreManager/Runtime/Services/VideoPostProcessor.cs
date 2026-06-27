using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>
	/// 视频后处理器
	/// 负责将录制的帧序列转换为最终的视频文件
	/// 使用跨平台原生编码器，无需外部FFmpeg依赖
	/// </summary>
	public class VideoPostProcessor : MonoBehaviour
	{
		#region Singleton Pattern

		private static VideoPostProcessor _instance;

		public static VideoPostProcessor Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject("[VideoPostProcessor]");
					_instance = go.AddComponent<VideoPostProcessor>();
					DontDestroyOnLoad(go);
				}
				return _instance;
			}
		}

		#endregion

		#region Configuration

		[Header("Encoding Settings")]
		[SerializeField]
		private int outputFrameRate = 30;

		[SerializeField]
		private int videoBitrate = 8000000; // 8 Mbps

		[SerializeField]
		private int audioBitrate = 192000; // 192 kbps

		[Header("Processing Settings")]
		[SerializeField]
		private bool deleteIntermediateFiles = true;

		#endregion

		#region Events

		/// <summary>
		/// 进度更新事件
		/// 参数1: 进度值 (0.0 - 1.0)
		/// 参数2: 当前步骤描述
		/// </summary>
		public event Action<float, string> OnProgressUpdated;

		#endregion

		#region Public Properties

		public bool IsProcessing { get; private set; }
		public float Progress { get; private set; }
		public string CurrentStatus { get; private set; }
		public string CurrentEncoderName { get; private set; }

		#endregion

		#region Private Fields

		private NativeVideoEncoder currentEncoder;
		private Coroutine _processingCoroutine;
		private string _currentTempDirectory;
		private Action<string> _onCompleteCallback;
		private Action<string> _onErrorCallback;
		private Action<float, string> _onProgressCallback;

		#endregion

		#region Data Structures

		/// <summary>
		/// 视频处理结果
		/// </summary>
		public class ProcessingResult
		{
			public bool Success { get; set; }
			public string OutputPath { get; set; }
			public string ErrorMessage { get; set; }
			public float Duration { get; set; }
			public int FrameCount { get; set; }
			public long FileSize { get; set; }
		}

		/// <summary>
		/// 帧序列信息
		/// </summary>
		private class FrameSequenceInfo
		{
			public string DirectoryPath { get; set; }
			public int Width { get; set; }
			public int Height { get; set; }
			public int OriginalFrameRate { get; set; }
			public int SpeedMultiplier { get; set; }
			public int FrameCount { get; set; }
			public float OriginalDuration { get; set; }
			public float AdjustedDuration { get; set; }
			public List<string> FrameFiles { get; set; } = new List<string>();
		}

		#endregion

		#region Unity Lifecycle

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Destroy(gameObject);
				return;
			}

			_instance = this;
			DontDestroyOnLoad(gameObject);

			// 创建平台对应的编码器
			InitializeEncoder();
		}

		private void OnDestroy()
		{
			if (_processingCoroutine != null)
			{
				StopCoroutine(_processingCoroutine);
				_processingCoroutine = null;
			}

			if (currentEncoder != null)
			{
				currentEncoder.CancelEncoding();
				Destroy(currentEncoder);
			}

			CleanupTempFiles();

			if (_instance == this)
			{
				_instance = null;
			}
		}

		#endregion

		#region Encoder Initialization

		/// <summary>
		/// 初始化平台对应的编码器
		/// </summary>
		private void InitializeEncoder()
		{
			// 根据平台创建对应的编码器
			if (Application.platform == RuntimePlatform.WindowsEditor ||
				Application.platform == RuntimePlatform.WindowsPlayer)
			{
				GameObject encoderGO = new GameObject("[WindowsVideoEncoder]");
				encoderGO.transform.SetParent(transform);
				currentEncoder = encoderGO.AddComponent<WindowsVideoEncoder>();
				CurrentEncoderName = currentEncoder.EncoderName;
				Debug.Log($"[VideoPostProcessor] 使用Windows编码器: {CurrentEncoderName}");
			}
			else if (Application.platform == RuntimePlatform.Android)
			{
				GameObject encoderGO = new GameObject("[AndroidVideoEncoder]");
				encoderGO.transform.SetParent(transform);
				currentEncoder = encoderGO.AddComponent<AndroidVideoEncoder>();
				CurrentEncoderName = currentEncoder.EncoderName;
				Debug.Log($"[VideoPostProcessor] 使用Android编码器: {CurrentEncoderName}");
			}
			else
			{
				Debug.LogWarning($"[VideoPostProcessor] 当前平台 {Application.platform} 不支持原生视频编码");
				// 尝试使用Windows编码器作为备用（例如在编辑器中）
				GameObject encoderGO = new GameObject("[WindowsVideoEncoder_Fallback]");
				encoderGO.transform.SetParent(transform);
				currentEncoder = encoderGO.AddComponent<WindowsVideoEncoder>();
				CurrentEncoderName = currentEncoder.EncoderName + " (Fallback)";
				Debug.Log($"[VideoPostProcessor] 使用备用编码器: {CurrentEncoderName}");
			}

			// 配置编码器
			if (currentEncoder != null)
			{
				currentEncoder.Configure(outputFrameRate, videoBitrate, audioBitrate);
				Debug.Log($"[VideoPostProcessor] 编码器能力: {currentEncoder.GetCapabilities()}");
			}
		}

		#endregion

		#region Public API

		/// <summary>
		/// 开始视频后处理
		/// 从VideoGenerationController获取录制数据并处理
		/// </summary>
		/// <param name="onComplete">完成回调，参数为输出文件路径</param>
		/// <param name="onError">错误回调，参数为错误消息</param>
		/// <param name="onProgress">进度回调，参数为进度(0-1)和状态消息</param>
		public void StartProcessing(Action<string> onComplete = null, Action<string> onError = null, Action<float, string> onProgress = null)
		{
			if (IsProcessing)
			{
				Debug.LogWarning("[VideoPostProcessor] 正在处理中，请等待当前处理完成");
				onError?.Invoke("正在处理中，请等待");
				return;
			}

			if (!VideoGenerationController.Instance.HasCompleteRecordingData())
			{
				Debug.LogError("[VideoPostProcessor] 没有完整的录制数据可供处理");
				onError?.Invoke("没有完整的录制数据");
				return;
			}

			_onCompleteCallback = onComplete;
			_onErrorCallback = onError;
			_onProgressCallback = onProgress;

			_processingCoroutine = StartCoroutine(ProcessVideoCoroutine());
		}

		/// <summary>
		/// 开始视频后处理（指定输入路径）
		/// </summary>
		/// <param name="frameSequencePath">帧序列目录路径</param>
		/// <param name="audioPath">音频文件路径</param>
		/// <param name="outputPath">输出文件路径</param>
		/// <param name="speedMultiplier">速度倍数（录制时的速度，用于恢复）</param>
		/// <param name="onComplete">完成回调</param>
		/// <param name="onError">错误回调</param>
		/// <param name="onProgress">进度回调</param>
		public void StartProcessing(
			string frameSequencePath,
			string audioPath,
			string outputPath,
			int speedMultiplier = 1, // 新方案：默认正常速度
			Action<string> onComplete = null,
			Action<string> onError = null,
			Action<float, string> onProgress = null)
		{
			if (IsProcessing)
			{
				Debug.LogWarning("[VideoPostProcessor] 正在处理中，请等待当前处理完成");
				onError?.Invoke("正在处理中，请等待");
				return;
			}

			_onCompleteCallback = onComplete;
			_onErrorCallback = onError;
			_onProgressCallback = onProgress;

			_processingCoroutine = StartCoroutine(ProcessVideoCoroutine(frameSequencePath, audioPath, outputPath, speedMultiplier));
		}

		/// <summary>
		/// 取消当前处理
		/// </summary>
		public void CancelProcessing()
		{
			if (!IsProcessing)
			{
				return;
			}

			if (_processingCoroutine != null)
			{
				StopCoroutine(_processingCoroutine);
				_processingCoroutine = null;
			}

			if (currentEncoder != null)
			{
				currentEncoder.CancelEncoding();
			}

			IsProcessing = false;
			Progress = 0f;
			CurrentStatus = "已取消";

			CleanupTempFiles();

			Debug.Log("[VideoPostProcessor] 处理已取消");
		}

		/// <summary>
		/// 获取当前编码器信息
		/// </summary>
		/// <returns>编码器信息描述</returns>
		public string GetEncoderInfo()
		{
			if (currentEncoder == null)
			{
				return "编码器未初始化";
			}

			return $"当前编码器: {CurrentEncoderName}\n" +
				$"可用性: {currentEncoder.IsAvailable}\n" +
				$"能力: {currentEncoder.GetCapabilities()}";
		}

		/// <summary>
		/// 获取建议的输出文件路径
		/// </summary>
		/// <returns>输出文件路径</returns>
		public string GetSuggestedOutputPath()
		{
			string fileName = VideoGenerationController.Instance.GetSuggestedOutputFileName();
			string directory = Path.Combine(Application.temporaryCachePath, "VideoOutput");
			Directory.CreateDirectory(directory);
			return Path.Combine(directory, $"{fileName}.mp4");
		}

		#endregion

		#region Processing Coroutine

		/// <summary>
		/// 从VideoGenerationController获取数据并处理
		/// </summary>
		private IEnumerator ProcessVideoCoroutine()
		{
			IsProcessing = true;
			Progress = 0f;
			CurrentStatus = "准备处理...";

			string frameSequencePath = VideoGenerationController.Instance.RawRecordingPath;
			string audioPath = VideoGenerationController.Instance.AudioPath;
			string outputPath = GetSuggestedOutputPath();
			int speedMultiplier = VideoGenerationController.Instance.BootData?.VideoGenerationSpeedMultiplier ?? 1; // 新方案：默认正常速度

			yield return StartCoroutine(ProcessVideoCoroutine(frameSequencePath, audioPath, outputPath, speedMultiplier));
		}

		/// <summary>
		/// 处理视频的核心协程
		/// </summary>
		private IEnumerator ProcessVideoCoroutine(
			string frameSequencePath,
			string audioPath,
			string outputPath,
			int speedMultiplier)
		{
			IsProcessing = true;
			Progress = 0f;
			float startTime = Time.time;

			// Step 1: 解析帧序列信息 (10%)
			UpdateProgress(0.05f, "解析帧序列信息...");
			FrameSequenceInfo frameInfo = null;
			bool parseSuccess = false;

			yield return StartCoroutine(ParseFrameSequenceInfoCoroutine(frameSequencePath, speedMultiplier, (info) =>
			{
				frameInfo = info;
				parseSuccess = info != null;
			}));

			if (!parseSuccess || frameInfo == null)
			{
				HandleError("解析帧序列信息失败");
				yield break;
			}

			UpdateProgress(0.1f, $"帧序列解析完成: {frameInfo.FrameCount}帧, {frameInfo.AdjustedDuration:F2}秒");

			// Step 2: 验证音频文件 (15%)
			UpdateProgress(0.12f, "验证音频文件...");
			bool audioValid = false;
			float audioDuration = 0f;

			yield return StartCoroutine(ValidateAudioFileCoroutine(audioPath, (valid, duration) =>
			{
				audioValid = valid;
				audioDuration = duration;
			}));

			if (!audioValid && !string.IsNullOrEmpty(audioPath))
			{
				Debug.LogWarning($"[VideoPostProcessor] 音频文件无效或不存在: {audioPath}");
			}

			UpdateProgress(0.15f, $"音频验证完成: {(audioValid ? $"{audioDuration:F2}秒" : "无音频")}");

			// Step 3: 处理帧序列速度恢复 (20%)
			UpdateProgress(0.18f, "处理帧序列...");
			string processedFramePath = null;
			bool frameProcessSuccess = false;

			yield return StartCoroutine(ProcessFrameSequenceForSpeedCoroutine(frameInfo, (success, path) =>
			{
				frameProcessSuccess = success;
				processedFramePath = path;
			}));

			if (!frameProcessSuccess)
			{
				HandleError("处理帧序列失败");
				yield break;
			}

			UpdateProgress(0.2f, "帧序列处理完成");

			// Step 4: 使用原生编码器编码视频 (20% - 85%)
			if (currentEncoder == null)
			{
				HandleError("编码器未初始化");
				yield break;
			}

			if (!currentEncoder.IsAvailable)
			{
				Debug.LogWarning($"[VideoPostProcessor] 当前编码器 {CurrentEncoderName} 不可用");
			}

			UpdateProgress(0.25f, $"使用 {CurrentEncoderName} 编码视频...");

			yield return StartCoroutine(currentEncoder.EncodeVideo(
				processedFramePath,
				audioValid ? audioPath : null,
				outputPath,
				outputFrameRate,
				frameInfo.Width,
				frameInfo.Height,
				speedMultiplier,
				(progress, status) =>
				{
					// 映射编码器进度到总体进度 (0.2 - 0.85)
					float overallProgress = 0.2f + 0.65f * progress;
					UpdateProgress(overallProgress, status);
				}));

			// 验证编码是否成功
			bool encodeSuccess = File.Exists(outputPath);
			if (!encodeSuccess)
			{
				HandleError("视频编码失败");
				yield break;
			}

			// Step 5: 完成处理 (100%)
			UpdateProgress(0.95f, "完成处理...");

			// 清理临时文件
			if (deleteIntermediateFiles)
			{
				CleanupIntermediateFiles(processedFramePath, frameInfo.DirectoryPath);
			}

			float totalTime = Time.time - startTime;
			UpdateProgress(1f, $"处理完成，耗时: {totalTime:F1}秒");

			// 验证输出文件
			if (!File.Exists(outputPath))
			{
				HandleError($"输出文件不存在: {outputPath}");
				yield break;
			}

			long fileSize = new FileInfo(outputPath).Length;
			Debug.Log($"[VideoPostProcessor] 视频处理完成: {outputPath}, 大小: {fileSize / 1024 / 1024:F2}MB");

			IsProcessing = false;
			_onCompleteCallback?.Invoke(outputPath);
		}

		#endregion

		#region Frame Sequence Processing

		/// <summary>
		/// 解析帧序列信息
		/// </summary>
		private IEnumerator ParseFrameSequenceInfoCoroutine(
			string frameSequencePath,
			int speedMultiplier,
			Action<FrameSequenceInfo> onComplete)
		{
			yield return null; // 确保在主线程执行

			try
			{
				if (!Directory.Exists(frameSequencePath))
				{
					Debug.LogError($"[VideoPostProcessor] 帧序列目录不存在: {frameSequencePath}");
					onComplete?.Invoke(null);
					yield break;
				}

				FrameSequenceInfo info = new FrameSequenceInfo
				{
					DirectoryPath = frameSequencePath,
					SpeedMultiplier = speedMultiplier
				};

				// 读取manifest.json
				string manifestPath = Path.Combine(frameSequencePath, "manifest.json");
				if (File.Exists(manifestPath))
				{
					string manifestJson = File.ReadAllText(manifestPath);
					VideoManifest manifest = JsonUtility.FromJson<VideoManifest>(manifestJson);

					info.Width = manifest.width;
					info.Height = manifest.height;
					info.OriginalFrameRate = manifest.frameRate;
					info.FrameCount = manifest.frameCount;
					info.OriginalDuration = manifest.duration;
				}
				else
				{
					// 如果没有manifest，从帧文件推断
					Debug.LogWarning("[VideoPostProcessor] manifest.json不存在，从帧文件推断信息");
					info.OriginalFrameRate = outputFrameRate;
					info.Width = 1920;
					info.Height = 1080;
				}

				// 获取所有帧文件
				string[] frameFiles = Directory.GetFiles(frameSequencePath, "frame_*.jpg");
				Array.Sort(frameFiles);
				info.FrameFiles = new List<string>(frameFiles);

				if (info.FrameFiles.Count == 0)
				{
					Debug.LogError("[VideoPostProcessor] 没有找到帧文件");
					onComplete?.Invoke(null);
					yield break;
				}

				// 新方案：正常速度录制，无需调整时长
				// 录制时长 = 实际游戏时长（无需倍速计算）
				info.AdjustedDuration = info.OriginalDuration;

				Debug.Log($"[VideoPostProcessor] 帧序列信息: {info.FrameCount}帧, {info.OriginalFrameRate}fps, " +
					$"原始时长: {info.OriginalDuration:F2}秒, 调整后时长: {info.AdjustedDuration:F2}秒");

				onComplete?.Invoke(info);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoPostProcessor] 解析帧序列失败: {ex.Message}\n{ex.StackTrace}");
				onComplete?.Invoke(null);
			}
		}

		/// <summary>
		/// 处理帧序列以恢复速度
		/// 方案：将帧复制到新目录，并重命名为连续帧号
		/// FFmpeg会根据帧率自动调整速度
		/// </summary>
		private IEnumerator ProcessFrameSequenceForSpeedCoroutine(
			FrameSequenceInfo frameInfo,
			Action<bool, string> onComplete)
		{
			// 创建临时目录存放处理后的帧
			_currentTempDirectory = Path.Combine(Application.temporaryCachePath, "VideoProcessing", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_currentTempDirectory);

			// 对于3倍速恢复到1倍速，有两种方案：
			// 方案1：保持原始帧率，使用FFmpeg的setpts滤镜调整速度
			// 方案2：调整帧率，将30fps变为10fps
			// 我们采用方案1，保持帧率，通过FFmpeg调整速度

			// 复制manifest.json文件（如果存在）
			string sourceManifestPath = Path.Combine(frameInfo.DirectoryPath, "manifest.json");
			if (File.Exists(sourceManifestPath))
			{
				string destManifestPath = Path.Combine(_currentTempDirectory, "manifest.json");
				File.Copy(sourceManifestPath, destManifestPath, true);
				Debug.Log($"[VideoPostProcessor] 已复制manifest.json到临时目录");
			}

			// 复制帧文件到临时目录
			int totalFrames = frameInfo.FrameFiles.Count;
			int processedFrames = 0;

			foreach (string frameFile in frameInfo.FrameFiles)
			{
				string fileName = Path.GetFileName(frameFile);
				string destPath = Path.Combine(_currentTempDirectory, fileName);
				File.Copy(frameFile, destPath, true);
				processedFrames++;

				// 每10帧更新一次进度
				if (processedFrames % 10 == 0)
				{
					yield return null; // 让出主线程
				}
			}

			Debug.Log($"[VideoPostProcessor] 已复制 {processedFrames} 帧到临时目录");
			onComplete?.Invoke(true, _currentTempDirectory);
		}

		#endregion

		#region Audio Processing

		/// 验证音频文件
		private IEnumerator ValidateAudioFileCoroutine(
			string audioPath,
			Action<bool, float> onComplete)
		{
			yield return null;

			if (string.IsNullOrEmpty(audioPath))
			{
				onComplete?.Invoke(false, 0f);
				yield break;
			}

			if (!File.Exists(audioPath))
			{
				Debug.LogWarning($"[VideoPostProcessor] 音频文件不存在: {audioPath}");
				onComplete?.Invoke(false, 0f);
				yield break;
			}

			// 使用Unity的AudioClip获取音频时长
			string uri = "file:///" + audioPath.Replace("\\", "/");
			using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN))
			{
				yield return request.SendWebRequest();

				if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
				{
					AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(request);
					if (clip != null)
					{
						float duration = clip.length;
						UnityEngine.Object.Destroy(clip);
						Debug.Log($"[VideoPostProcessor] 音频文件有效: {audioPath}, 时长: {duration:F2}秒");
						onComplete?.Invoke(true, duration);
						yield break;
					}
				}
			}

			Debug.LogWarning($"[VideoPostProcessor] 无法加载音频文件: {audioPath}");
			onComplete?.Invoke(false, 0f);
		}

		#endregion

		#region Utility Methods

		/// <summary>
		/// 更新进度
		/// </summary>
		private void UpdateProgress(float progress, string status)
		{
			Progress = progress;
			CurrentStatus = status;

			// 触发进度更新事件
			OnProgressUpdated?.Invoke(progress, status);

			// 回调方式（保留兼容性）
			_onProgressCallback?.Invoke(progress, status);

			Debug.Log($"[VideoPostProcessor] 进度: {progress * 100:F1}% - {status}");
		}

		/// <summary>
		/// 处理错误
		/// </summary>
		private void HandleError(string message)
		{
			Debug.LogError($"[VideoPostProcessor] 错误: {message}");
			IsProcessing = false;
			CurrentStatus = $"错误: {message}";
			_onErrorCallback?.Invoke(message);

			CleanupTempFiles();
		}

		/// <summary>
		/// 清理临时文件
		/// </summary>
		private void CleanupTempFiles()
		{
			if (!string.IsNullOrEmpty(_currentTempDirectory) && Directory.Exists(_currentTempDirectory))
			{
				try
				{
					Directory.Delete(_currentTempDirectory, true);
					Debug.Log($"[VideoPostProcessor] 已清理临时目录: {_currentTempDirectory}");
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"[VideoPostProcessor] 清理临时目录失败: {ex.Message}");
				}
				_currentTempDirectory = null;
			}
		}

		/// <summary>
		/// 清理中间文件
		/// </summary>
		private void CleanupIntermediateFiles(params string[] paths)
		{
			foreach (string path in paths)
			{
				if (string.IsNullOrEmpty(path)) continue;

				try
				{
					if (Directory.Exists(path))
					{
						Directory.Delete(path, true);
					}
					else if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"[VideoPostProcessor] 清理中间文件失败: {path}, 错误: {ex.Message}");
				}
			}
		}

		#endregion

		#region Static Utility Methods

		/// <summary>
		/// 获取视频编码器的推荐设置
		/// </summary>
		public static (int bitrate, int audioBitrate, int frameRate) GetRecommendedEncodingSettings(int width, int height)
		{
			// 基于分辨率推荐编码设置
			int pixels = width * height;

			if (pixels >= 3840 * 2160) // 4K
			{
				return (20000000, 256000, 30); // 20 Mbps video, 256 kbps audio
			}
			else if (pixels >= 1920 * 1080) // 1080p
			{
				return (8000000, 192000, 30); // 8 Mbps video, 192 kbps audio
			}
			else if (pixels >= 1280 * 720) // 720p
			{
				return (5000000, 128000, 30); // 5 Mbps video, 128 kbps audio
			}
			else // 480p or lower
			{
				return (2500000, 96000, 30); // 2.5 Mbps video, 96 kbps audio
			}
		}

		/// <summary>
		/// 检查指定路径是否有足够的磁盘空间
		/// </summary>
		public static bool HasEnoughDiskSpace(string path, long requiredBytes)
		{
			try
			{
				string drive = Path.GetPathRoot(path);
				DriveInfo driveInfo = new DriveInfo(drive);
				long availableBytes = driveInfo.AvailableFreeSpace;
				return availableBytes >= requiredBytes;
			}
			catch
			{
				return true; // 无法检测时假设有足够空间
			}
		}

		/// <summary>
		/// 估算视频文件大小
		/// </summary>
		public static long EstimateVideoFileSize(int width, int height, int frameRate, float duration, int bitrate)
		{
			// 估算公式: (bitrate * duration) / 8 + 音频大小 + 容器开销
			long videoBytes = (long)(bitrate * duration / 8);
			long audioBytes = (long)(192000 * duration / 8); // 假设192kbps音频
			long containerOverhead = (long)(videoBytes * 0.01); // 1%容器开销
			return videoBytes + audioBytes + containerOverhead;
		}

		#endregion
	}
}