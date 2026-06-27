using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Sekai;

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>
	/// 视频生成流程控制器
	/// 协调录屏启动、停止以及存储后处理所需数据
	/// 包含错误处理和后台切换检测
	/// </summary>
	public class VideoGenerationController : MonoBehaviour
	{
		#region Singleton Pattern

		private static VideoGenerationController _instance;

		public static VideoGenerationController Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject("[VideoGenerationController]");
					_instance = go.AddComponent<VideoGenerationController>();
					DontDestroyOnLoad(go);
				}
				return _instance;
			}
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// 当前是否在录制视频
		/// </summary>
		public static bool IsVideoGenerationRecording => Instance._isRecording;

		/// <summary>
		/// 视频生成启动数据
		/// </summary>
		public VideoGenerationBootData BootData => _bootData;

		/// <summary>
		/// 原始录屏文件路径（帧序列目录）
		/// </summary>
		public string RawRecordingPath => _rawRecordingPath;

		/// <summary>
		/// 原始音乐文件路径（谱面音乐，用于后处理合成）
		/// </summary>
		public string MusicPath => _audioPath;

		/// <summary>
		/// 录制的游戏音频文件路径（打击音效）
		/// </summary>
		public string RecordedAudioPath => VideoGenerationService.Instance?.RecordedAudioPath;

		/// <summary>
		/// 音频文件路径（用于合成）
		/// 如果没有设置原始音频路径，则使用录制的游戏音频
		/// </summary>
		public string AudioPath
		{
			get
			{
				// 如果有原始音频路径，优先使用
				if (!string.IsNullOrEmpty(_audioPath))
				{
					return _audioPath;
				}
				// 否则使用录制的游戏音频（打击音效 + 谱面音乐）
				return RecordedAudioPath;
			}
		}

		/// <summary>
		/// 谱面标题（用于生成文件名）
		/// </summary>
		public string ScoreTitle => _scoreTitle;

		/// <summary>
		/// 开始录制时间戳
		/// </summary>
		public DateTime StartTime => _startTime;

		/// <summary>
		/// 录制帧率
		/// </summary>
		public int FrameRate => _frameRate;

		/// <summary>
		/// 录制分辨率
		/// </summary>
		public (int width, int height) Resolution => (_width, _height);

		#endregion

		#region Private Fields

		private bool _isRecording;
		private VideoGenerationBootData _bootData;
		private string _rawRecordingPath;
		private string _audioPath;
		private string _scoreTitle;
		private DateTime _startTime;
		private int _frameRate = 30;
		private int _width = 1920;
		private int _height = 1080;
		private bool _isFailureHandled = false; // 防止重复处理失败

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
		}

		private void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
			}
		}

		/// <summary>
		/// 监听应用暂停事件（切后台）
		/// SubTask 10.1: 在VideoGenerationController中添加OnApplicationPause监听
		/// </summary>
		private void OnApplicationPause(bool pauseStatus)
		{
			try
			{
				// SubTask 10.1: 检测pauseStatus为true时（游戏切到后台）
				if (pauseStatus && _isRecording)
				{
					Debug.LogWarning("[VideoGenerationController] 应用切到后台，触发录屏失败流程");

					// 如果当前正在录屏，触发失败流程
					if (VideoGenerationService.Instance != null && VideoGenerationService.Instance.IsRecording)
					{
						HandleRecordingFailure("应用切到后台，录屏被中断");
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationController] OnApplicationPause处理异常: {ex.Message}\n{ex.StackTrace}");
				// 即使异常也要执行失败流程
				ExecuteFailureFlow("应用切换处理异常");
			}
		}

		#endregion

		#region Public API

		/// <summary>
		/// 启动视频生成录屏
		/// 在LiveLoading场景开始时调用
		/// </summary>
		/// <param name="bootData">视频生成启动数据</param>
		/// <param name="scoreTitle">谱面标题</param>
		/// <param name="width">录制宽度</param>
		/// <param name="height">录制高度</param>
		/// <param name="frameRate">录制帧率</param>
		/// <returns>是否成功启动</returns>
		public static bool StartVideoGenerationRecording(
			VideoGenerationBootData bootData,
			string scoreTitle,
			int width = 1920,
			int height = 1080,
			int frameRate = 30,
			Camera targetCamera = null)
		{
			return Instance.StartRecordingInternal(bootData, scoreTitle, width, height, frameRate, targetCamera);
		}

		/// <summary>
		/// 停止视频生成录屏
		/// 在Live结束过渡到过渡场景时调用
		/// </summary>
		/// <returns>录屏文件路径</returns>
		public static string StopVideoGenerationRecording()
		{
			return Instance.StopRecordingInternal();
		}

		/// <summary>
		/// 设置谱面标题（如果未在启动时设置）
		/// </summary>
		/// <param name="title">谱面标题</param>
		public static void SetScoreTitle(string title)
		{
			if (Instance._scoreTitle == null || Instance._scoreTitle == "")
			{
				Instance._scoreTitle = title;
			}
		}

		/// <summary>
		/// 清除所有录制数据（用于完成后清理）
		/// </summary>
		public static void ClearRecordingData()
		{
			Instance._bootData = null;
			Instance._rawRecordingPath = null;
			Instance._audioPath = null;
			Instance._scoreTitle = null;
			Instance._isRecording = false;
			Instance._isFailureHandled = false; // 重置失败标记
		}

		/// <summary>
		/// 获取录制时长（秒）
		/// </summary>
		/// <returns>录制时长</returns>
		public static float GetRecordingDuration()
		{
			if (!Instance._isRecording)
			{
				return 0f;
			}

			float duration = (float)(DateTime.Now - Instance._startTime).TotalSeconds;
			return duration;
		}

		/// <summary>
		/// 处理录屏失败
		/// SubTask 10.6: 添加全局try-catch，异常时执行相同失败流程
		/// </summary>
		public void HandleRecordingFailure(string reason)
		{
			try
			{
				Debug.LogError($"[VideoGenerationController] 录屏失败: {reason}");

				// 防止重复处理
				if (_isFailureHandled)
				{
					Debug.LogWarning("[VideoGenerationController] 失败流程已处理，避免重复执行");
					return;
				}

				_isFailureHandled = true;
				ExecuteFailureFlow(reason);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationController] HandleRecordingFailure异常: {ex.Message}\n{ex.StackTrace}");
				// 即使异常也要尝试清理
				ForceCleanupResources();
			}
		}

		#endregion

		#region Private Implementation

		private bool StartRecordingInternal(
			VideoGenerationBootData bootData,
			string scoreTitle,
			int width,
			int height,
			int frameRate,
			Camera targetCamera = null)
		{
			try
			{
				// 检查是否已经处于录制状态
				if (_isRecording)
				{
					Debug.LogWarning("[VideoGenerationController] 已经处于录制状态，无法重复启动。");
					return false;
				}

				// 检查是否处于视频生成模式
				if (!bootData.IsVideoGenerationMode)
				{
					Debug.LogWarning("[VideoGenerationController] 不处于视频生成模式，不启动录屏。");
					return false;
				}

				// 重置失败标记
				_isFailureHandled = false;

				// 存储启动数据
				_bootData = bootData;
				_scoreTitle = scoreTitle ?? $"Score_{bootData.MusicData?.Music?.id}_{bootData.MusicData?.DifficultyString}";
				_audioPath = bootData.VideoGenerationAudioPath;
				_width = width;
				_height = height;
				_frameRate = frameRate;

				// 如果没有传入相机，尝试获取Camera.main
				if (targetCamera == null)
				{
					targetCamera = Camera.main;
					if (targetCamera == null)
					{
						Debug.LogWarning("[VideoGenerationController] 当前没有主摄像机，VideoGenerationService将动态获取相机。");
						// 不中断录制，让VideoGenerationService在录制循环中动态获取相机
					}
				}
				else
				{
					Debug.Log($"[VideoGenerationController] 使用传入的相机: {targetCamera.name}");
				}

				// 启动录屏（传入相机，VideoGenerationService会处理）
				bool success = VideoGenerationService.Instance.StartRecording(
					targetCamera,  // 可能为null，VideoGenerationService会处理
					width,
					height,
					frameRate);

				if (success)
				{
					_isRecording = true;
					_startTime = DateTime.Now;

					Debug.Log($"[VideoGenerationController] 录屏已启动: {width}x{height}@{frameRate}fps, 谱面: {_scoreTitle}");
					Debug.Log($"[VideoGenerationController] 音频路径: {_audioPath}");
				}
				else
				{
					Debug.LogError("[VideoGenerationController] VideoGenerationService启动录屏失败。");
				}

				return success;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationController] StartRecordingInternal异常: {ex.Message}\n{ex.StackTrace}");
				HandleRecordingFailure($"启动录屏失败: {ex.Message}");
				return false;
			}
		}

		private string StopRecordingInternal()
		{
			try
			{
				if (!_isRecording)
				{
					Debug.LogWarning("[VideoGenerationController] 当前没有在录制中。");
					return null;
				}

				// 停止录屏
				string outputPath = VideoGenerationService.Instance.StopRecording();

				if (outputPath != null)
				{
					_rawRecordingPath = outputPath;

					float duration = GetRecordingDuration();
					Debug.Log($"[VideoGenerationController] 录屏已停止: {_rawRecordingPath}, 时长: {duration:F2}秒");
				}
				else
				{
					Debug.LogError("[VideoGenerationController] VideoGenerationService停止录屏失败。");
				}

				_isRecording = false;

				return outputPath;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationController] StopRecordingInternal异常: {ex.Message}\n{ex.StackTrace}");
				HandleRecordingFailure($"停止录屏失败: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// 执行失败流程
		/// 按顺序执行：结束Live -> 返回首页 -> 显示对话框 -> 清理资源
		/// 使用协程延迟显示对话框，等待ScreenManager初始化
		/// </summary>
		private void ExecuteFailureFlow(string reason)
		{
			Debug.Log("[VideoGenerationController] 开始执行失败流程");

			// SubTask 10.2: 检测到暂停（切后台）时立即调用LiveTransitioner.SafeForceFinish
			ForceFinishLive();

			// SubTask 10.3: 使用ScreenManager返回主页MenuScreenType.MusicScoreMakerTop
			ReturnToHomePage();

			// 使用协程延迟显示失败对话框，等待ScreenManager准备好
			StartCoroutine(ShowFailureDialogDelayed());

			// SubTask 10.5: 清理临时录屏文件和缓存
			CleanupRecordingResources();

			// 清除录制状态
			_isRecording = false;
			_isFailureHandled = true;

			Debug.Log("[VideoGenerationController] 失败流程执行完成");
		}

		/// <summary>
		/// 延迟显示失败对话框，等待ScreenManager初始化
		/// </summary>
		private IEnumerator ShowFailureDialogDelayed()
		{
			// 等待ScreenManager准备好（最多等待5秒）
			float maxWaitTime = 5f;
			float elapsedTime = 0f;

			while (ScreenManager.Instance == null && elapsedTime < maxWaitTime)
			{
				yield return new WaitForSeconds(0.5f);
				elapsedTime += 0.5f;
				Debug.Log($"[VideoGenerationController] 等待ScreenManager初始化... ({elapsedTime}s)");
			}

			// 显示失败对话框
			ShowFailureDialog();
		}

		/// <summary>
		/// SubTask 10.2: 立即结束Live
		/// </summary>
		private void ForceFinishLive()
		{
			try
			{
				if (LiveTransitioner.Exists)
				{
					Debug.Log("[VideoGenerationController] 强制结束Live");
					LiveTransitioner.SafeForceFinish(() =>
					{
						Debug.Log("[VideoGenerationController] Live已强制结束");
					});
				}
				else
				{
					Debug.LogWarning("[VideoGenerationController] LiveTransitioner不存在，无需结束");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationController] ForceFinishLive异常: {ex.Message}\n{ex.StackTrace}");
			}
		}

		/// <summary>
		/// SubTask 10.3: 返回主页MenuScreenType.MusicScoreMakerTop
		/// </summary>
		private void ReturnToHomePage()
		{
			try
			{
				Debug.Log("[VideoGenerationController] 返回谱面管理首页");

				// 使用MusicScoreMakerUtility返回MusicScoreMakerTop页面
				MusicScoreMaker.Ingame.Utilities.MusicScoreMakerUtility.RequestTransitionToOutGame(MenuScreenType.MusicScoreMakerTop);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationController] ReturnToHomePage异常: {ex.Message}\n{ex.StackTrace}");

				// 尝试使用ScreenManager直接切换
				if (ScreenManager.Instance != null)
				{
					ScreenManager.Instance.ChangeUIScreen(MenuScreenType.MusicScoreMakerTop, false, true);
				}
			}
		}

		/// <summary>
		/// SubTask 10.4: 显示失败对话框"录屏失败"
		/// </summary>
		private void ShowFailureDialog()
		{
			try
			{
				if (ScreenManager.Instance != null)
				{
					Debug.Log("[VideoGenerationController] 显示录屏失败对话框");

					// 使用ScreenManager显示单按钮对话框
					ScreenManager.Instance.Show1ButtonDialog<Common1ButtonDialog>(
						DialogType.Common1ButtonDialog,
						null,           // titleKey (无标题)
						"录屏失败",      // messageBodyKey (显示内容)
						"确认",          // okButtonLabelKey (按钮文本)
						() =>
						{
							Debug.Log("[VideoGenerationController] 用户确认录屏失败对话框");
						},
						DisplayLayerType.Layer_Dialog,
						DialogSize.Manual,
						true
					);
				}
				else
				{
					// ScreenManager超时仍未初始化，使用Toast提示或日志记录
					Debug.LogWarning("[VideoGenerationController] ScreenManager超时未初始化，录屏失败信息已记录到日志");

					// 尝试使用Toast提示（如果Toast系统可用）
					// 注意：这里暂时只记录日志，因为Toast系统可能同样依赖ScreenManager
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationController] ShowFailureDialog异常: {ex.Message}\n{ex.StackTrace}");
			}
		}

		/// <summary>
		/// SubTask 10.5: 清理临时录屏文件和缓存
		/// </summary>
		private void CleanupRecordingResources()
		{
			try
			{
				Debug.Log("[VideoGenerationController] 清理临时录屏文件和缓存");

				// 调用VideoGenerationService取消录制
				if (VideoGenerationService.Instance != null)
				{
					VideoGenerationService.Instance.CancelRecording();
				}

				// 清理临时目录
				CleanupTempDirectories();

				// 清除录制数据
				ClearRecordingData();
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationController] CleanupRecordingResources异常: {ex.Message}\n{ex.StackTrace}");
			}
		}

		/// <summary>
		/// 清理所有临时录屏目录
		/// </summary>
		private void CleanupTempDirectories()
		{
			try
			{
				string tempCachePath = Application.temporaryCachePath;

				// 清理VideoGeneration临时目录
				string videoGenerationPath = Path.Combine(tempCachePath, "VideoGeneration");
				if (Directory.Exists(videoGenerationPath))
				{
					Debug.Log($"[VideoGenerationController] 清理临时目录: {videoGenerationPath}");
					Directory.Delete(videoGenerationPath, true);
				}

				// 清理VideoRecordings临时目录
				string videoRecordingsPath = Path.Combine(tempCachePath, "VideoRecordings");
				if (Directory.Exists(videoRecordingsPath))
				{
					Debug.Log($"[VideoGenerationController] 清理临时目录: {videoRecordingsPath}");
					Directory.Delete(videoRecordingsPath, true);
				}

				Debug.Log("[VideoGenerationController] 临时目录清理完成");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationController] CleanupTempDirectories异常: {ex.Message}\n{ex.StackTrace}");
			}
		}

		/// <summary>
		/// 强制清理资源（用于异常情况）
		/// </summary>
		private void ForceCleanupResources()
		{
			try
			{
				if (VideoGenerationService.Instance != null)
				{
					VideoGenerationService.Instance.CancelRecording();
				}

				CleanupTempDirectories();
				ClearRecordingData();
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoGenerationController] ForceCleanupResources异常: {ex.Message}\n{ex.StackTrace}");
			}
		}

		#endregion

		#region Utility Methods

		/// <summary>
		/// 获取建议的输出文件名
		/// </summary>
		/// <returns>建议的文件名（不含扩展名）</returns>
		public string GetSuggestedOutputFileName()
		{
			if (_scoreTitle == null || _scoreTitle == "")
			{
				return $"Video_{_startTime:yyyyMMdd_HHmmss}";
			}

			// 清理标题中的非法字符
			string cleanTitle = _scoreTitle;
			char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
			foreach (char c in invalidChars)
			{
				cleanTitle = cleanTitle.Replace(c, '_');
			}

			return $"{cleanTitle}_{_startTime:yyyyMMdd_HHmmss}";
		}

		/// <summary>
		/// 检查是否有完整的录制数据可供后处理
		/// </summary>
		/// <returns>是否可以开始后处理</returns>
		public bool HasCompleteRecordingData()
		{
			// 使用AudioPath属性，它会自动选择原始音频或录制的音频
			return _rawRecordingPath != null &&
				   AudioPath != null && // 使用属性而不是字段
				   _bootData != null;
		}

		#endregion
	}
}