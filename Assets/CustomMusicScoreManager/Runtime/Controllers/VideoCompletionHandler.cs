using System;
using System.IO;
using UnityEngine;
using Sekai.UI;
using Process = System.Diagnostics.Process; // Alias to avoid Debug conflict
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo; // Alias to avoid Debug conflict

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>
	/// 视频完成处理协调器
	/// 协调视频后处理、保存、分享的完整流程
	/// Task 9: 实现生成完成提示和分享
	/// </summary>
	public class VideoCompletionHandler : MonoBehaviour
	{
		#region Singleton Pattern

		private static VideoCompletionHandler _instance;

		public static VideoCompletionHandler Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject("[VideoCompletionHandler]");
					_instance = go.AddComponent<VideoCompletionHandler>();
					DontDestroyOnLoad(go);
				}
				return _instance;
			}
		}

		#endregion

		#region Private Fields

		private bool _isProcessing;
		private string _tempVideoPath;
		private string _finalVideoPath;
		private string _scoreTitle;
		private DateTime _startTime;

		// 进度对话框相关
		private VideoProcessingProgressDialog _progressDialog;
		private const string PROGRESS_DIALOG_PREFAB_PATH = "CustomMusicScoreManager/UI/VideoProcessingProgressDialog";

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

			// 清理进度对话框
			CloseProgressDialog();
		}

		#endregion

		#region Public API

		/// <summary>
		/// 开始视频完成流程
		/// 从VideoGenerationController获取录制数据，依次执行：
		/// 1. VideoPostProcessor完成 → 保存视频（VideoSaveHelper）
		/// 2. 保存成功 → 显示完成对话框
		/// 3. 用户确认 → 执行分享
		/// 4. 用户取消 → 返回首页
		/// </summary>
		public void StartCompletionFlow()
		{
			if (_isProcessing)
			{
				Debug.LogWarning("[VideoCompletionHandler] 正在处理中，请等待");
				return;
			}

			if (!VideoGenerationController.Instance.HasCompleteRecordingData())
			{
				Debug.LogError("[VideoCompletionHandler] 没有完整的录制数据可供处理");
				HandleCompletionError("没有完整的录制数据");
				return;
			}

			_isProcessing = true;
			_scoreTitle = VideoGenerationController.Instance.ScoreTitle;
			_startTime = VideoGenerationController.Instance.StartTime;

			Debug.Log("[VideoCompletionHandler] 开始视频完成流程");

			// 显示进度对话框
			ShowProgressDialog();

			// Step 1: 视频后处理
			StartVideoPostProcessing();
		}

		#endregion

		#region Private Implementation

		/// <summary>
		/// 显示进度对话框
		/// </summary>
		private void ShowProgressDialog()
		{
			try
			{
				// 先关闭已有的对话框
				CloseProgressDialog();

				// 从Resources加载预制体
				GameObject prefab = Resources.Load<GameObject>(PROGRESS_DIALOG_PREFAB_PATH);
				if (prefab == null)
				{
					Debug.LogWarning($"[VideoCompletionHandler] 无法加载进度对话框预制体: {PROGRESS_DIALOG_PREFAB_PATH}");
					return;
				}

				// 查找对话框层
				Transform dialogLayer = null;
				if (ScreenManager.Instance != null)
				{
					dialogLayer = ScreenManager.Instance.GetLayerObject(DisplayLayerType.Layer_Dialog)?.transform;
				}

				// 如果找不到对话框层，使用当前对象的transform
				if (dialogLayer == null)
				{
					dialogLayer = transform;
				}

				// 实例化对话框
				GameObject dialogGO = Instantiate(prefab, dialogLayer);
				dialogGO.name = "VideoProcessingProgressDialog";

				_progressDialog = dialogGO.GetComponent<VideoProcessingProgressDialog>();
				if (_progressDialog != null)
				{
					// 初始化对话框
					_progressDialog.Initialize(DialogSize.Manual, false);

					// 设置标题
					if (_progressDialog.Header != null)
					{
						_progressDialog.Header.TitleText = "视频生成中";
					}

					// 打开对话框
					_progressDialog.Open();

					Debug.Log("[VideoCompletionHandler] 进度对话框已显示");

					// 订阅VideoPostProcessor的进度事件
					VideoPostProcessor.Instance.OnProgressUpdated += OnProgressUpdated;
				}
				else
				{
					Debug.LogError("[VideoCompletionHandler] 预制体缺少VideoProcessingProgressDialog组件");
					Destroy(dialogGO);
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoCompletionHandler] 显示进度对话框异常: {ex.Message}\n{ex.StackTrace}");
			}
		}

		/// <summary>
		/// 关闭进度对话框
		/// </summary>
		private void CloseProgressDialog()
		{
			// 取消订阅进度事件
			if (VideoPostProcessor.Instance != null)
			{
				VideoPostProcessor.Instance.OnProgressUpdated -= OnProgressUpdated;
			}

			if (_progressDialog != null)
			{
				try
				{
					_progressDialog.Close();
					Debug.Log("[VideoCompletionHandler] 进度对话框已关闭");
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"[VideoCompletionHandler] 关闭进度对话框异常: {ex.Message}");
					Destroy(_progressDialog.gameObject);
				}
				_progressDialog = null;
			}
		}

		/// <summary>
		/// 进度更新事件处理
		/// </summary>
		/// <param name="progress">进度值 (0.0 - 1.0)</param>
		/// <param name="status">当前步骤描述</param>
		private void OnProgressUpdated(float progress, string status)
		{
			if (_progressDialog != null)
			{
				_progressDialog.UpdateProgress(progress, status);
			}
		}

		/// <summary>
		/// Step 1: 启动视频后处理
		/// </summary>
		private void StartVideoPostProcessing()
		{
			Debug.Log("[VideoCompletionHandler] 启动视频后处理");

			VideoPostProcessor.Instance.StartProcessing(
				onComplete: OnPostProcessingComplete,
				onError: OnPostProcessingError,
				onProgress: OnPostProcessingProgress
			);
		}

		/// <summary>
		/// Step 2: 视频后处理完成回调
		/// </summary>
		private void OnPostProcessingComplete(string outputPath)
		{
			Debug.Log($"[VideoCompletionHandler] 视频后处理完成: {outputPath}");

			_tempVideoPath = outputPath;

			// 更新进度对话框为保存状态
			if (_progressDialog != null)
			{
				_progressDialog.UpdateProgress(0.9f, "正在保存视频...");
			}

			// Step 3: 保存视频
			SaveVideoToFinalLocation();
		}

		/// <summary>
		/// 视频后处理错误回调
		/// </summary>
		private void OnPostProcessingError(string error)
		{
			Debug.LogError($"[VideoCompletionHandler] 视频后处理失败: {error}");

			// 关闭进度对话框
			CloseProgressDialog();

			HandleCompletionError($"视频后处理失败: {error}");
		}

		/// <summary>
		/// 视频后处理进度回调（保留兼容性）
		/// </summary>
		private void OnPostProcessingProgress(float progress, string status)
		{
			Debug.Log($"[VideoCompletionHandler] 后处理进度: {progress * 100:F1}% - {status}");
			// 进度更新现在通过OnProgressUpdated事件处理
		}

		/// <summary>
		/// Step 3: 保存视频到最终位置
		/// </summary>
		private void SaveVideoToFinalLocation()
		{
			Debug.Log("[VideoCompletionHandler] 开始保存视频到最终位置");

			VideoSaveHelper.Instance.SaveVideo(
				_tempVideoPath,
				_scoreTitle,
				_startTime,
				onComplete: OnVideoSaveComplete,
				onError: OnVideoSaveError
			);
		}

		/// <summary>
		/// Step 4: 视频保存完成回调
		/// SubTask 9.1: 视频保存完成后显示二选一对话框
		/// SubTask 9.2: 对话框内容"已生成视频，是否立即分享？"，显示文件路径
		/// </summary>
		private void OnVideoSaveComplete(string finalPath)
		{
			Debug.Log($"[VideoCompletionHandler] 视频保存完成: {finalPath}");

			_finalVideoPath = finalPath;

			// 关闭进度对话框
			CloseProgressDialog();

			// 清理临时资源
			CleanupTempResources();

			// SubTask 9.1 & 9.2: 显示二选一对话框
			ShowCompletionDialog();
		}

		/// <summary>
		/// 视频保存错误回调
		/// </summary>
		private void OnVideoSaveError(string error)
		{
			Debug.LogError($"[VideoCompletionHandler] 视频保存失败: {error}");
			HandleCompletionError($"视频保存失败: {error}");
		}

		/// <summary>
		/// SubTask 9.1: 显示二选一对话框
		/// SubTask 9.2: 对话框内容"已生成视频，是否立即分享？"，显示文件路径
		/// </summary>
		private void ShowCompletionDialog()
		{
			try
			{
				if (ScreenManager.Instance == null)
				{
					Debug.LogError("[VideoCompletionHandler] ScreenManager不存在，无法显示对话框");
					ReturnToHomePage();
					return;
				}

				Debug.Log("[VideoCompletionHandler] 显示视频完成对话框");

				// SubTask 9.1: 使用ScreenManager.Instance?.Show2ButtonDialog<Common2ButtonDialog>
				// SubTask 9.2: 使用明确的按钮标签
				// 确认按钮使用"WORD_DECIDE"，取消按钮使用"WORD_CANCEL"
				Common2ButtonDialog dialog = ScreenManager.Instance.Show2ButtonDialog<Common2ButtonDialog>(
					DialogType.Common2ButtonDialog,
					null,                                           // titleKey (无标题)
					null,                                           // messageBodyKey (通过SetMessageBodyText设置)
					"WORD_DECIDE",                                  // okButtonLabelKey (确认按钮)
					"WORD_CANCEL",                                  // cancelButtonLabelKey (取消按钮)
					() => OnShareConfirmed(_finalVideoPath),        // 确认按钮回调：分享
					() => OnShareCancelled(),                       // 取消按钮回调：返回首页
					DisplayLayerType.Layer_Dialog,
					DialogSize.Manual,
					true
				);

				if (dialog != null)
				{
					// SubTask 9.2: 使用SetMessageBodyText设置消息内容
					// 主文本内容为"已生成视频，是否立即分享？"
					// 显示视频保存的文件路径
					string messageBody = $"已生成视频，是否立即分享？\n\n保存路径：{_finalVideoPath}";
					dialog.SetMessageBodyText(messageBody);
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoCompletionHandler] 显示对话框异常: {ex.Message}\n{ex.StackTrace}");
				ReturnToHomePage();
			}
		}

		/// <summary>
		/// SubTask 9.3: 用户确认后调用分享
		/// SubTask 9.4: Windows端：调用OpenInExplorer打开文件位置
		/// SubTask 9.5: 安卓端：调用ShareExportHelper.ShareFile分享视频
		/// </summary>
		private void OnShareConfirmed(string path)
		{
			Debug.Log($"[VideoCompletionHandler] 用户确认分享，路径: {path}");

			try
			{
#if UNITY_ANDROID && !UNITY_EDITOR
				// SubTask 9.5: 安卓端：调用ShareExportHelper.ShareFile分享视频
				ShareVideoOnAndroid(path);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
				// SubTask 9.4: Windows端：调用OpenInExplorer打开文件位置
				OpenVideoInExplorer(path);
#else
				// 其他平台：显示路径信息
				Debug.LogWarning($"[VideoCompletionHandler] 当前平台不支持分享，视频路径: {path}");
				ShowUnsupportedPlatformDialog(path);
#endif

				// 完成流程
				_isProcessing = false;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoCompletionHandler] 分享异常: {ex.Message}\n{ex.StackTrace}");
				HandleCompletionError($"分享失败: {ex.Message}");
			}
		}

		/// <summary>
		/// 用户取消分享，返回首页
		/// </summary>
		private void OnShareCancelled()
		{
			Debug.Log("[VideoCompletionHandler] 用户取消分享，返回首页");

			ReturnToHomePage();
			_isProcessing = false;
		}

		/// <summary>
		/// SubTask 9.5: 安卓端分享视频
		/// </summary>
		private void ShareVideoOnAndroid(string path)
		{
			Debug.Log($"[VideoCompletionHandler] 安卓端分享视频: {path}");

			try
			{
				// 调用ShareExportHelper.ShareFile
				using (AndroidJavaClass helper = new AndroidJavaClass("com.opensekai.ShareExportHelper"))
				{
					// ShareFile方法接受文件路径，会使用Android原生分享Intent
					helper.CallStatic("ShareFile", path);
					Debug.Log("[VideoCompletionHandler] 已调用ShareExportHelper.ShareFile");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoCompletionHandler] 安卓分享失败: {ex.Message}\n{ex.StackTrace}");
				HandleCompletionError($"安卓分享失败: {ex.Message}");
			}
		}

		/// <summary>
		/// SubTask 9.4: Windows端打开文件位置
		/// </summary>
		private void OpenVideoInExplorer(string path)
		{
			Debug.Log($"[VideoCompletionHandler] Windows端打开文件位置: {path}");

			try
			{
				// 使用explorer.exe /select,path命令直接选中文件
				string directory = Path.GetDirectoryName(path);

				if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
				{
					// explorer.exe /select, requires backslashes
					string selectPath = path.Replace("/", "\\");
					string args = $"/select,\"{selectPath}\"";

					ProcessStartInfo startInfo = new ProcessStartInfo
					{
						FileName = "explorer.exe",
						Arguments = args,
						UseShellExecute = true
					};

					Process.Start(startInfo);
					Debug.Log($"[VideoCompletionHandler] 已打开文件位置: {selectPath}");
				}
				else
				{
					Debug.LogWarning($"[VideoCompletionHandler] 目录不存在: {directory}");
					// 备用方案：只打开目录
					if (!string.IsNullOrEmpty(directory))
					{
						Process.Start(directory);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoCompletionHandler] 打开文件位置失败: {ex.Message}\n{ex.StackTrace}");
				HandleCompletionError($"打开文件位置失败: {ex.Message}");
			}
		}

		/// <summary>
		/// 显示不支持平台对话框
		/// </summary>
		private void ShowUnsupportedPlatformDialog(string path)
		{
			if (ScreenManager.Instance != null)
			{
				ScreenManager.Instance.Show1ButtonDialog<Common1ButtonDialog>(
					DialogType.Common1ButtonDialog,
					null,
					"WORD_DECIDE",
					null,
					DisplayLayerType.Layer_Dialog,
					DialogSize.Manual,
					true
				)?.SetMessageBodyText($"视频已保存到：\n{path}");
			}
		}

		/// <summary>
		/// 返回首页
		/// </summary>
		private void ReturnToHomePage()
		{
			try
			{
				Debug.Log("[VideoCompletionHandler] 返回谱面管理首页");

				// 使用MusicScoreMakerUtility返回MusicScoreMakerTop页面
				MusicScoreMaker.Ingame.Utilities.MusicScoreMakerUtility.RequestTransitionToOutGame(MenuScreenType.MusicScoreMakerTop);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoCompletionHandler] 返回首页异常: {ex.Message}\n{ex.StackTrace}");

				// 尝试使用ScreenManager直接切换
				if (ScreenManager.Instance != null)
				{
					ScreenManager.Instance.ChangeUIScreen(MenuScreenType.MusicScoreMakerTop, false, true);
				}
			}
		}

		/// <summary>
		/// 处理完成流程错误
		/// </summary>
		private void HandleCompletionError(string error)
		{
			Debug.LogError($"[VideoCompletionHandler] 完成流程错误: {error}");

			_isProcessing = false;

			// 关闭进度对话框
			CloseProgressDialog();

			// 清理临时资源
			CleanupTempResources();

			// 显示错误对话框
			ShowErrorDialog(error);

			// 返回首页
			ReturnToHomePage();
		}

		/// <summary>
		/// 显示错误对话框
		/// </summary>
		private void ShowErrorDialog(string error)
		{
			try
			{
				if (ScreenManager.Instance != null)
				{
					ScreenManager.Instance.Show1ButtonDialog<Common1ButtonDialog>(
						DialogType.Common1ButtonDialog,
						null,
						"WORD_DECIDE",
						null,
						DisplayLayerType.Layer_Dialog,
						DialogSize.Manual,
						true
					)?.SetMessageBodyText($"视频生成失败：\n{error}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoCompletionHandler] 显示错误对话框异常: {ex.Message}");
			}
		}

		/// <summary>
		/// 清理临时资源
		/// </summary>
		private void CleanupTempResources()
		{
			try
			{
				Debug.Log("[VideoCompletionHandler] 清理临时资源");

				// 清理临时视频文件
				if (!string.IsNullOrEmpty(_tempVideoPath) && File.Exists(_tempVideoPath))
				{
					string tempDirectory = Path.GetDirectoryName(_tempVideoPath);
					if (tempDirectory.Contains(Application.temporaryCachePath))
					{
						File.Delete(_tempVideoPath);
						Debug.Log($"[VideoCompletionHandler] 已删除临时视频文件: {_tempVideoPath}");
					}
				}

				// 清理VideoGenerationController的录制数据
				VideoGenerationController.ClearRecordingData();

				Debug.Log("[VideoCompletionHandler] 临时资源清理完成");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoCompletionHandler] 清理临时资源异常: {ex.Message}\n{ex.StackTrace}");
			}
		}

		#endregion
	}
}