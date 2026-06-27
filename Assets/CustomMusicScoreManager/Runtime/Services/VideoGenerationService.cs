using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>
	/// 游戏内实时画面捕获录屏服务
	/// 使用RenderTexture方案捕获游戏画面并编码为视频文件
	/// </summary>
	public class VideoGenerationService : MonoBehaviour
	{
		#region Singleton Pattern

		private static VideoGenerationService _instance;

		public static VideoGenerationService Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject("[VideoGenerationService]");
					_instance = go.AddComponent<VideoGenerationService>();
					DontDestroyOnLoad(go);
				}
				return _instance;
			}
		}

		#endregion

		#region Configuration

		[Header("Recording Settings")]
		[SerializeField]
		private int targetWidth = 1920;

		[SerializeField]
		private int targetHeight = 1080;

		[SerializeField]
		[Range(15, 60)]
		private int targetFrameRate = 30;

		[SerializeField]
		private Camera targetCamera;

		[SerializeField]
		private bool useMainCameraByDefault = true;

		[Header("Encoding Settings")]
		[SerializeField]
		private VideoEncodingFormat encodingFormat = VideoEncodingFormat.ImageSequence;

		[SerializeField]
		[Range(1, 100)]
		private int jpegQuality = 90;

		[SerializeField]
		private bool useAsyncEncoding = false; // 禁用异步编码，避免阻塞录制循环

		[SerializeField]
		private int maxCachedFrames = 300;

		#endregion

		#region Public Properties

		public bool IsRecording { get; private set; }
		public int RecordedFrameCount { get; private set; }
		// 使用实际录制时间（从启动到停止），而不是根据帧数计算
		public float RecordingDuration => _startTime > 0 ? Time.time - _startTime : 0f;

		/// <summary>
		/// 录制的游戏音频文件路径（打击音效）
		/// </summary>
		public string RecordedAudioPath => _audioPath;

		#endregion

		#region Private Fields

		private RenderTexture _renderTexture;
		private Texture2D _frameTexture;
		private List<FrameData> _cachedFrames;
		private string _tempDirectory;
		private string _audioPath; // 录制的音频文件路径
		private Coroutine _recordingCoroutine;
		private AudioRecorder _audioRecorder; // 音频录制器
		private float _frameInterval;
		private float _lastCaptureTime;
		private int _frameSequence;
		private float _startTime; // 录制启动时间

		#endregion

		#region Data Structures

		private class FrameData
		{
			public byte[] Data;
			public int FrameIndex;
		}

		public enum VideoEncodingFormat
		{
			ImageSequence,
			MP4,
			WebM
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

			InitializeService();
		}

		private void OnDestroy()
		{
			StopRecording();
			CleanupResources();

			if (_instance == this)
			{
				_instance = null;
			}
		}

		#endregion

		#region Initialization

		private void InitializeService()
		{
			_cachedFrames = new List<FrameData>();
			_frameInterval = 1f / targetFrameRate;

			if (useMainCameraByDefault && targetCamera == null)
			{
				targetCamera = Camera.main;
			}

			// 初始化AudioRecorder（录制游戏音效）
			InitializeAudioRecorder();
		}

		/// <summary>
		/// 初始化AudioRecorder，附加到场景中的AudioListener上
		/// </summary>
		private void InitializeAudioRecorder()
		{
			// 查找场景中的AudioListener
			AudioListener audioListener = FindObjectOfType<AudioListener>();

			if (audioListener == null)
			{
				Debug.LogWarning("[VideoGenerationService] 场景中没有找到AudioListener，创建新的AudioListener");
				GameObject listenerObj = new GameObject("AudioListener_Recording");
				audioListener = listenerObj.AddComponent<AudioListener>();
				DontDestroyOnLoad(listenerObj); // 确保录制过程中AudioListener不被销毁
			}

			// 添加AudioRecorder组件
			_audioRecorder = audioListener.GetComponent<AudioRecorder>();
			if (_audioRecorder == null)
			{
				_audioRecorder = audioListener.gameObject.AddComponent<AudioRecorder>();
				Debug.Log($"[VideoGenerationService] AudioRecorder已附加到 {audioListener.gameObject.name}");
			}
		}

		private void InitializeRenderTexture()
		{
			if (_renderTexture != null)
			{
				_renderTexture.Release();
				Destroy(_renderTexture);
			}

			_renderTexture = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32)
			{
				antiAliasing = 1,
				useDynamicScale = false
			};
			_renderTexture.Create();

			if (_frameTexture != null)
			{
				Destroy(_frameTexture);
			}

			_frameTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
		}

		#endregion

		#region Public API

		/// <summary>
		/// 开始录制游戏画面
		/// </summary>
		/// <param name="camera">要录制的摄像机，为null则使用默认配置的摄像机</param>
		/// <param name="width">目标宽度</param>
		/// <param name="height">目标高度</param>
		/// <param name="frameRate">帧率</param>
		/// <returns>是否成功开始录制</returns>
		public bool StartRecording(Camera camera = null, int? width = null, int? height = null, int? frameRate = null)
		{
			if (IsRecording)
			{
				Debug.LogWarning("[VideoGenerationService] 已经在录制中，无法重复开始。");
				return false;
			}

			if (camera != null)
			{
				targetCamera = camera;
			}
			else if (useMainCameraByDefault)
			{
				targetCamera = Camera.main;
				// 即使Camera.main为null，也允许启动录制
				// CaptureFrame会在录制循环中动态获取相机
				if (targetCamera == null)
				{
					Debug.LogWarning("[VideoGenerationService] 启动录制时没有主相机，将在录制过程中动态获取。");
				}
			}

			// 不强制要求相机必须存在，允许动态获取
			// if (targetCamera == null)
			// {
			//     Debug.LogError("[VideoGenerationService] 没有可用的摄像机进行录制。");
			//     return false;
			// }

			targetWidth = width ?? targetWidth;
			targetHeight = height ?? targetHeight;
			targetFrameRate = frameRate ?? targetFrameRate;
			_frameInterval = 1f / targetFrameRate;

			if (targetWidth <= 0 || targetHeight <= 0)
			{
				Debug.LogError($"[VideoGenerationService] 无效的分辨率: {targetWidth}x{targetHeight}");
				return false;
			}

			if (targetFrameRate <= 0 || targetFrameRate > 120)
			{
				Debug.LogError($"[VideoGenerationService] 无效的帧率: {targetFrameRate}");
				return false;
			}

			try
			{
				InitializeRenderTexture();
				_tempDirectory = CreateTempDirectory();
				_cachedFrames.Clear();
				RecordedFrameCount = 0;
				_frameSequence = 1; // FFmpeg expects frame numbering to start from 1
				_lastCaptureTime = Time.time;
				_startTime = Time.time; // 记录录制启动时间

				// 开始录制音频（游戏音效）
				if (_audioRecorder != null)
				{
					string audioOutputPath = Path.Combine(_tempDirectory, "game_audio.wav");
					_audioRecorder.StartRecording(audioOutputPath);
					_audioPath = audioOutputPath;
					Debug.Log($"[VideoGenerationService] 开始录制音频到: {audioOutputPath}");
				}
				else
				{
					Debug.LogWarning("[VideoGenerationService] AudioRecorder未初始化，无法录制音频");
					_audioPath = null;
				}

				IsRecording = true;
				_recordingCoroutine = StartCoroutine(RecordingLoop());

				Debug.Log($"[VideoGenerationService] 开始录制: {targetWidth}x{targetHeight} @ {targetFrameRate}fps");
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationService] 启动录制失败: {ex.Message}");
				CleanupResources();
				return false;
			}
		}

		/// <summary>
		/// 停止录制并保存视频文件
		/// </summary>
		/// <param name="outputPath">输出文件路径，为null则保存到临时目录</param>
		/// <returns>保存的视频文件路径或目录路径</returns>
		public string StopRecording(string outputPath = null)
		{
			try
			{
				if (!IsRecording)
				{
					Debug.LogWarning("[VideoGenerationService] 当前没有在录制中。");
					return null;
				}

				IsRecording = false;

				if (_recordingCoroutine != null)
				{
					StopCoroutine(_recordingCoroutine);
					_recordingCoroutine = null;
				}

				// 停止录制音频
				if (_audioRecorder != null && _audioRecorder.IsRecording)
				{
					string recordedAudioPath = _audioRecorder.StopRecording();
					if (recordedAudioPath != null)
					{
						Debug.Log($"[VideoGenerationService] 音频录制完成: {recordedAudioPath}, 时长: {_audioRecorder.RecordingDuration:F2}秒");
						// _audioPath已经由StartRecording设置，这里不需要更新
					}
					else
					{
						Debug.LogWarning("[VideoGenerationService] 音频录制失败或没有数据");
						_audioPath = null;
					}
				}

				string result = SaveRecording(outputPath);

				Debug.Log($"[VideoGenerationService] 录制完成: {RecordedFrameCount} 帧, 时长: {RecordingDuration:F2}秒");

				CleanupResources();

				return result;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationService] StopRecording异常: {ex.Message}\n{ex.StackTrace}");

				// 触发失败流程
				if (VideoGenerationController.Instance != null)
				{
					VideoGenerationController.Instance.HandleRecordingFailure($"停止录制失败: {ex.Message}");
				}

				return null;
			}
		}

		/// <summary>
		/// 取消录制，不保存文件
		/// </summary>
		public void CancelRecording()
		{
			try
			{
				if (!IsRecording)
				{
					return;
				}

				IsRecording = false;

				if (_recordingCoroutine != null)
				{
					StopCoroutine(_recordingCoroutine);
					_recordingCoroutine = null;
				}

				CleanupResources();
				CleanupTempDirectory();

				Debug.Log("[VideoGenerationService] 录制已取消。");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationService] CancelRecording异常: {ex.Message}\n{ex.StackTrace}");
				// 确保清理资源
				IsRecording = false;
				ForceCleanup();
			}
		}

		/// <summary>
		/// 配置录制参数
		/// </summary>
		public void Configure(int width, int height, int frameRate, VideoEncodingFormat format)
		{
			if (IsRecording)
			{
				Debug.LogWarning("[VideoGenerationService] 录制进行中无法更改配置。");
				return;
			}

			targetWidth = width;
			targetHeight = height;
			targetFrameRate = frameRate;
			encodingFormat = format;
			_frameInterval = 1f / targetFrameRate;
		}

		/// <summary>
		/// 设置目标摄像机
		/// </summary>
		public void SetTargetCamera(Camera camera)
		{
			if (IsRecording)
			{
				Debug.LogWarning("[VideoGenerationService] 录制进行中无法更改摄像机。");
				return;
			}

			targetCamera = camera;
		}

		#endregion

		#region Recording Logic

		private IEnumerator RecordingLoop()
		{
			float nextCaptureTime = Time.time;

			while (IsRecording)
			{
				// 等待直到下一个捕获时间点
				while (Time.time < nextCaptureTime && IsRecording)
				{
					yield return null; // 等待下一帧Update
				}

				// 检查是否仍然在录制（可能在等待过程中停止了）
				if (!IsRecording)
					break;

				// 捕获当前帧
				CaptureFrame();

				// 设置下一个捕获时间点
				nextCaptureTime += _frameInterval;

				// 如果缓存帧数达到上限，异步保存到磁盘
				if (useAsyncEncoding && _cachedFrames.Count >= maxCachedFrames)
				{
					yield return StartCoroutine(FlushCachedFramesAsync());
				}
			}
		}

		private void CaptureFrame()
		{
			// 动态检测并更新主相机引用（应对场景切换）
			Camera currentMainCamera = Camera.main;

			// 检查targetCamera是否已被销毁（使用隐式bool转换检查）
			bool targetCameraDestroyed = targetCamera == null || !targetCamera.isActiveAndEnabled;

			// 每隔30帧记录相机状态（仅在相机有效时）
			if (RecordedFrameCount % 30 == 0 && !targetCameraDestroyed)
			{
				Debug.Log($"[VideoGenerationService] 相机状态检查: targetCamera={targetCamera?.name ?? "null"}, currentMainCamera={currentMainCamera?.name ?? "null"}, isActive={targetCamera?.isActiveAndEnabled}");
			}

			// 如果targetCamera不再是当前主相机，或者已失效/被销毁，更新引用
			if (targetCameraDestroyed || (currentMainCamera != null && targetCamera != currentMainCamera))
			{
				if (currentMainCamera != null && currentMainCamera.isActiveAndEnabled)
				{
					if (!targetCameraDestroyed && targetCamera != currentMainCamera)
					{
						Debug.Log($"[VideoGenerationService] 动态更新相机引用: {targetCamera?.name ?? "null"} -> {currentMainCamera.name}");
					}
					else if (targetCameraDestroyed)
					{
						Debug.Log($"[VideoGenerationService] 重新获取相机引用: {currentMainCamera.name} (之前相机已失效)");
					}
					targetCamera = currentMainCamera;
				}
				else
				{
					// 没有可用的相机，跳过此帧但记录警告
					Debug.LogWarning($"[VideoGenerationService] 当前没有可用的主相机，跳过帧捕获 (已录制 {RecordedFrameCount} 帧)");
					return;
				}
			}

			if (_renderTexture == null)
			{
				Debug.LogWarning($"[VideoGenerationService] RenderTexture未初始化，跳过帧捕获");
				return;
			}

			try
			{
				RenderTexture previousTarget = targetCamera.targetTexture;
				targetCamera.targetTexture = _renderTexture;
				targetCamera.Render();
				targetCamera.targetTexture = previousTarget;

				RenderTexture.active = _renderTexture;
				_frameTexture.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0, false);
				_frameTexture.Apply(false, false);
				RenderTexture.active = null;

				byte[] frameData;
				switch (encodingFormat)
				{
					case VideoEncodingFormat.ImageSequence:
						frameData = _frameTexture.EncodeToJPG(jpegQuality);
						break;
					case VideoEncodingFormat.MP4:
					case VideoEncodingFormat.WebM:
						frameData = _frameTexture.EncodeToJPG(jpegQuality);
						break;
					default:
						frameData = _frameTexture.EncodeToJPG(jpegQuality);
						break;
				}

				_cachedFrames.Add(new FrameData
				{
					Data = frameData,
					FrameIndex = _frameSequence++
				});

				RecordedFrameCount++;

				if (RecordedFrameCount % 30 == 0)
				{
					Debug.Log($"[VideoGenerationService] 已录制 {RecordedFrameCount} 帧");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationService] 捕获帧失败: {ex.Message}\n{ex.StackTrace}");

				// 捕获帧失败时触发失败流程
				IsRecording = false;
				if (VideoGenerationController.Instance != null)
				{
					VideoGenerationController.Instance.HandleRecordingFailure($"捕获帧失败: {ex.Message}");
				}
				else
				{
					// 如果VideoGenerationController不存在，直接清理
					CancelRecording();
				}
			}
		}

		private IEnumerator FlushCachedFramesAsync()
		{
			// 获取当前缓存的帧副本，避免在flush过程中被修改
			List<FrameData> framesToFlush = new List<FrameData>(_cachedFrames);
			int frameCount = framesToFlush.Count;

			if (frameCount == 0)
			{
				Debug.LogWarning("[VideoGenerationService] FlushCachedFramesAsync: 缓存中没有帧需要写入。");
				yield break;
			}

			Debug.Log($"[VideoGenerationService] 开始写入 {frameCount} 个缓存帧到磁盘。");

			int processed = 0;
			int successfullyWritten = 0;

			while (processed < frameCount)
			{
				int batchSize = Math.Min(10, frameCount - processed);
				for (int i = 0; i < batchSize; i++)
				{
					try
					{
						FrameData frame = framesToFlush[processed + i];
						string framePath = Path.Combine(_tempDirectory, $"frame_{frame.FrameIndex:D6}.jpg");

						// 确保目录存在
						if (!Directory.Exists(_tempDirectory))
						{
							Directory.CreateDirectory(_tempDirectory);
						}

						File.WriteAllBytes(framePath, frame.Data);
						successfullyWritten++;
					}
					catch (Exception ex)
					{
						Debug.LogError($"[VideoGenerationService] 写入帧失败: {ex.Message}");
					}
				}

				processed += batchSize;
				yield return null;
			}

			Debug.Log($"[VideoGenerationService] 成功写入 {successfullyWritten}/{frameCount} 个帧到磁盘。");

			// 清空原始缓存
			_cachedFrames.Clear();
		}

		#endregion

		#region Saving Logic

		private string SaveRecording(string outputPath)
		{
			try
			{
				// 如果没有传入outputPath，直接使用_tempDirectory（已包含所有flush的帧）
				if (string.IsNullOrEmpty(outputPath))
				{
					outputPath = _tempDirectory;
				}
				else
				{
					// 如果传入了outputPath，需要将_tempDirectory中的帧复制到outputPath
					if (_tempDirectory != outputPath && Directory.Exists(_tempDirectory))
					{
						Debug.Log($"[VideoGenerationService] 将临时目录的帧复制到目标目录: {_tempDirectory} -> {outputPath}");
						Directory.CreateDirectory(outputPath);

						string[] frameFiles = Directory.GetFiles(_tempDirectory, "frame_*.jpg");
						foreach (string frameFile in frameFiles)
						{
							string fileName = Path.GetFileName(frameFile);
							string destPath = Path.Combine(outputPath, fileName);
							File.Copy(frameFile, destPath, true);
						}

						// 复制manifest.json（如果存在）
						string manifestPath = Path.Combine(_tempDirectory, "manifest.json");
						if (File.Exists(manifestPath))
						{
							File.Copy(manifestPath, Path.Combine(outputPath, "manifest.json"), true);
						}
					}
				}

				// 确保outputPath目录存在
				if (!Directory.Exists(outputPath))
				{
					Directory.CreateDirectory(outputPath);
				}

				// 保存_cachedFrames中的剩余帧（录制停止时还未flush的帧）
				if (_cachedFrames.Count > 0)
				{
					Debug.Log($"[VideoGenerationService] 保存剩余{_cachedFrames.Count}个缓存帧到: {outputPath}");
					SaveCachedFramesToDisk(outputPath);
				}

				// 创建或更新manifest文件
				CreateManifestFile(outputPath);

				Debug.Log($"[VideoGenerationService] 视频已保存到: {outputPath}, 共{RecordedFrameCount}帧");
				return outputPath;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationService] 保存录制失败: {ex.Message}");
				return null;
			}
		}

		private void SaveCachedFramesToDisk(string outputDirectory)
		{
			foreach (FrameData frame in _cachedFrames)
			{
				string extension = GetFileExtension();
				string framePath = Path.Combine(outputDirectory, $"frame_{frame.FrameIndex:D6}{extension}");
				File.WriteAllBytes(framePath, frame.Data);
			}
		}

		private string CreateManifestFile(string outputDirectory)
		{
			string manifestPath = Path.Combine(outputDirectory, "manifest.json");

			var manifest = new VideoManifest
			{
				width = targetWidth,
				height = targetHeight,
				frameRate = targetFrameRate,
				frameCount = RecordedFrameCount,
				duration = RecordingDuration,
				format = encodingFormat.ToString(),
				createdTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
			};

			string json = JsonUtility.ToJson(manifest, true);
			File.WriteAllText(manifestPath, json);

			return manifestPath;
		}

		private string GetFileExtension()
		{
			switch (encodingFormat)
			{
				case VideoEncodingFormat.ImageSequence:
					return ".jpg";
				case VideoEncodingFormat.MP4:
					return ".jpg";
				case VideoEncodingFormat.WebM:
					return ".jpg";
				default:
					return ".jpg";
			}
		}

		#endregion

		#region Cleanup

		private void CleanupResources()
		{
			try
			{
				if (_renderTexture != null)
				{
					_renderTexture.Release();
					Destroy(_renderTexture);
					_renderTexture = null;
				}

				if (_frameTexture != null)
				{
					Destroy(_frameTexture);
					_frameTexture = null;
				}

				if (_cachedFrames != null)
				{
					_cachedFrames.Clear();
				}

				RecordedFrameCount = 0;
				_frameSequence = 1; // FFmpeg expects frame numbering to start from 1
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationService] CleanupResources异常: {ex.Message}\n{ex.StackTrace}");
			}
		}

		/// <summary>
		/// 清理临时录屏目录
		/// </summary>
		private void CleanupTempDirectory()
		{
			try
			{
				if (!string.IsNullOrEmpty(_tempDirectory) && Directory.Exists(_tempDirectory))
				{
					Debug.Log($"[VideoGenerationService] 清理临时目录: {_tempDirectory}");
					Directory.Delete(_tempDirectory, true);
					_tempDirectory = null;
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationService] CleanupTempDirectory异常: {ex.Message}\n{ex.StackTrace}");
			}
		}

		/// <summary>
		/// 强制清理所有资源（用于异常情况）
		/// </summary>
		private void ForceCleanup()
		{
			try
			{
				if (_renderTexture != null)
				{
					_renderTexture.Release();
					Destroy(_renderTexture);
					_renderTexture = null;
				}

				if (_frameTexture != null)
				{
					Destroy(_frameTexture);
					_frameTexture = null;
				}

				if (_cachedFrames != null)
				{
					_cachedFrames.Clear();
				}

				if (!string.IsNullOrEmpty(_tempDirectory) && Directory.Exists(_tempDirectory))
				{
					Directory.Delete(_tempDirectory, true);
					_tempDirectory = null;
				}

				RecordedFrameCount = 0;
				_frameSequence = 1; // FFmpeg expects frame numbering to start from 1
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationService] ForceCleanup异常: {ex.Message}\n{ex.StackTrace}");
			}
		}

		private string CreateTempDirectory()
		{
			string tempPath = Path.Combine(Application.temporaryCachePath, "VideoGeneration", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(tempPath);
			return tempPath;
		}

		#endregion

		#region Utility Methods

		/// <summary>
		/// 获取默认输出路径
		/// </summary>
		public static string GetDefaultOutputPath()
		{
			string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			return Path.Combine(Application.temporaryCachePath, "VideoRecordings", $"Recording_{timestamp}");
		}

		/// <summary>
		/// 获取支持的编码格式列表
		/// </summary>
		public static string[] GetSupportedFormats()
		{
			return new string[]
			{
				"ImageSequence",
				"MP4",
				"WebM"
			};
		}

		#endregion
	}

	[Serializable]
	internal class VideoManifest
	{
		public int width;
		public int height;
		public int frameRate;
		public int frameCount;
		public float duration;
		public string format;
		public string createdTime;
	}
}