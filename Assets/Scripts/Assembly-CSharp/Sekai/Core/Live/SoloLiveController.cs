using System.Collections;
using Sekai.CustomMusicScoreManager;
using Sekai.Live;
using Sekai.MusicScoreMaker.Common;
using Sekai.MusicScoreMaker.Ingame.Presenters;
using UnityEngine;

namespace Sekai.Core.Live
{
	public class SoloLiveController : BaseLiveController
	{
		private bool isTestPlayFinishedCalled;
		private Coroutine finishCoroutine;
		private Coroutine resumeCoroutine;
		private bool playHistoryRecorded;
		private LiveViewBase[] liveViews;
		private LiveLogic liveLogic;

		// Video Generation Mode state
		private bool isVideoGenerationMode;
		private int videoGenerationSpeedMultiplier = 1;
		private bool videoGenerationMuteAudio;
		private bool videoGenerationDisablePause;
		private float originalBgmVolume;

		protected override void OnAwake()
		{
			base.OnAwake();
			BootData = UserDataManager.Instance.FreeLiveBootData;
			if (BootData == null)
			{
				Debug.LogWarning("FreeLiveBootData is null. Test play cannot start.");
				return;
			}

			// Detect Video Generation Mode
			DetectVideoGenerationMode();

			currentMusicTimeMs = System.Math.Max(0L, BootData.MusicData?.StartMusicTimeMs ?? 0L);
			Setup();
			LoadSound();
			liveViews = LiveViewFactory.Create(BootData, transform);
			LiveViewExt.Setup(liveViews, this);
			RetryProcess();
			liveOutUIController?.Initialize(BootData.LivePlayMode, BaseCamera);
			LiveViewExt.OnLoad(liveViews);
			StartCoroutine(LiveStart(6f));
		}

		protected override void OnMusicStart()
		{
			LiveTransitioner.SafeFinish(null, null);
			base.OnMusicStart();
			LiveViewExt.MusicStart(liveViews, currentAudioLatencyMusicTimeMs);

			// Apply Video Generation Mode settings
			ApplyVideoGenerationModeSettings();

			// 在游戏实际开始时启动录制，延迟几帧等待相机初始化
			StartCoroutine(DelayedStartVideoGenerationRecording());
		}

		private IEnumerator DelayedStartVideoGenerationRecording()
		{
			// 等待BaseCamera初始化（最多等待1秒）
			Camera targetCamera = BaseCamera;
			int waitFrames = 0;
			while (targetCamera == null && waitFrames < 60) // 最多等待60帧（约1秒）
			{
				yield return null;
				targetCamera = BaseCamera;
				waitFrames++;
			}

			if (targetCamera == null)
			{
				Debug.LogError("[SoloLiveController] BaseCamera仍未初始化，无法启动录制。");
				yield break;
			}

			Debug.Log($"[SoloLiveController] BaseCamera已就绪: {targetCamera.name}, 等待帧数: {waitFrames}");
			TryStartVideoGenerationRecording(targetCamera);
		}

		protected override void OnRhythmGameStart()
		{
			LiveViewExt.RhythmGameStart(liveViews);
			base.OnRhythmGameStart();
			liveLogic?.RefreshInput();
		}

		protected override void OnUpdate()
		{
			base.OnUpdate();
			if (state != LiveControllerState.Playing)
			{
				return;
			}

			UpdateMusicTime();
			float fillerSec = BootData?.MusicData?.Music?.fillerSec ?? 0f;
			liveLogic?.OnUpdate(currentAudioLatencyMusicTimeMs - fillerSec, currentGameTime);
			if (liveLogic?.result == 2 && liveLogic.IsNotesAllFinished)
			{
				OnFinished();
			}
			if (BootData?.IsAuto == true)
			{
				liveLogic?.OnAutoInput();
			}
			else
			{
				liveLogic?.OnInput();
			}
			LiveViewExt.OnUpdate(liveViews, currentAudioLatencyMusicTimeMs);
		}

		private new void OnApplicationPause(bool pauseStatus)
		{
			if (pauseStatus)
			{
				OnPause();
			}
		}

		protected override void OnPause()
		{
			// Video Generation Mode: Disable pause functionality
			if (isVideoGenerationMode && videoGenerationDisablePause)
			{
				return;
			}

			if ((state == LiveControllerState.Playing || state == LiveControllerState.ResumeCountDown) && result == 0)
			{
				state = LiveControllerState.Pause;
				SoundManager.Instance.Pause();
				LiveViewExt.Pause(liveViews);
				ShowPauseDialog();
			}
		}

		public void ShowPauseDialog()
		{
			if (BootData?.MusicData?.IsTestPlay == true)
			{
				ScreenLayerMusicScoreMaker.BootArg bootArg = MusicScoreMakerEntryPoint.BootData?.bootData;
				if (bootArg != null && bootArg.IsFromFullComboCheck)
				{
					liveOutUIController?.ShowMusicScoreMakerFullComboCheckPauseDialog(OnResume, OnReturnToMusicScoreMaker, OnTestPlayRetryConfirm);
				}
				else
				{
					liveOutUIController?.ShowMusicScoreMakerTestPlayPauseDialog(OnResume, OnReturnToMusicScoreMaker, OnTestPlayRetryConfirm);
				}
				return;
			}

			liveOutUIController?.ShowPauseDialog(OnResume, OnRetireConfirm, OnRetryConfirm);
		}

		public void PauseLive()
		{
			// Video Generation Mode: Disable pause functionality
			if (isVideoGenerationMode && videoGenerationDisablePause)
			{
				return;
			}

			if ((state != LiveControllerState.Playing && state != LiveControllerState.ResumeCountDown) || result != 0)
			{
				return;
			}

			state = LiveControllerState.Pause;
			SoundManager.Instance.Pause();
			LiveViewExt.Pause(liveViews);
		}

		protected override void OnResume()
		{
			if (state != LiveControllerState.Pause)
			{
				return;
			}

			state = LiveControllerState.ResumeCountDown;
			liveOutUIController?.Destroy();
			LiveViewExt.Countdown(liveViews);
			if (resumeCoroutine != null)
			{
				StopCoroutine(resumeCoroutine);
			}
			resumeCoroutine = StartCoroutine(ResumeCoroutine());
		}

		public void ResumeNoCountDown()
		{
			if (state != LiveControllerState.Pause)
			{
				return;
			}

			state = LiveControllerState.ResumeCountDown;
			liveOutUIController?.Destroy();
			SoundManager.Instance.ResumeIngame(currentMusicTimeMs);
			state = LiveControllerState.Playing;
			LiveViewExt.Resume(liveViews, currentAudioLatencyMusicTimeMs);
			liveLogic?.RefreshInput();
		}

		public void ShowConsecutiveAutoLiveRetireDialog()
		{
			CallPreExit();
		}

		protected override void OnRetry()
		{
			liveOutUIController?.Destroy();
			SoundManager.Instance.StopIngame();
			isTestPlayFinishedCalled = false;
			playHistoryRecorded = false;
			result = 0;
			state = LiveControllerState.None;
			currentMusicTimeMs = System.Math.Max(0L, BootData?.MusicData?.StartMusicTimeMs ?? 0L);
			RetryProcess();
			LiveViewExt.Retry(liveViews);
			StartCoroutine(LiveStart(6f));
		}

		private void RetryProcess()
		{
			if (liveLogic != null)
			{
				liveLogic.OnFinished -= OnFinished;
				liveLogic.OnFailure -= OnFailure;
			}

			liveLogic = new LiveLogic(liveBundleBuildData);
			liveLogic.Setup(BootData, liveViews);
			liveLogic.SetSkillLogic(new SkillLogic());
			liveLogic.SetScoreLogic(new ScoreLogic(liveBundleBuildData));
			liveLogic.OnFinished += OnFinished;
			liveLogic.OnFailure += OnFailure;
		}

		protected override void OnRetire()
		{
			liveOutUIController?.Destroy();
			result = 2;
			PreExit(0f, 4f);
		}

		private void OnRetireByMySelf()
		{
			liveOutUIController?.Destroy();
			result = 1;
			PreExit(0f, 0f);
		}

		private void OnConfirmCancel()
		{
			liveOutUIController?.ShowPauseDialog(OnResume, OnRetireConfirm, OnRetryConfirm);
		}

		private void OnTestPlayConfirmCancel()
		{
			liveOutUIController?.ShowMusicScoreMakerTestPlayPauseDialog(OnResume, OnReturnToMusicScoreMaker, OnTestPlayRetryConfirm);
		}

		private void OnRetireConfirm()
		{
			liveOutUIController?.ShowConfirmRetireDialog(OnRetireByMySelf, OnConfirmCancel);
		}

		private void OnRetryConfirm()
		{
			liveOutUIController?.ShowConfirmRetryDialog(Retry, OnConfirmCancel);
		}

		private void OnTestPlayRetryConfirm()
		{
			liveOutUIController?.ShowConfirmRetryDialog(Retry, OnTestPlayConfirmCancel);
		}

		private System.Collections.IEnumerator ResumeCoroutine()
		{
			liveLogic?.RefreshInput();
			yield return new WaitForSeconds(3f);
			SoundManager.Instance.ResumeIngame(currentMusicTimeMs);
			state = LiveControllerState.Playing;
			LiveViewExt.Resume(liveViews, currentAudioLatencyMusicTimeMs);
			SoundManager.Instance.SetAudioSyncedUnityTimer(cueId);
			resumeCoroutine = null;
		}

		private void OnFailure()
		{
			currentMusicTimeMs = SoundManager.Instance.GetAudioSyncedUnityTimer();
		}

		protected override void OnFinished()
		{
			if (BootData?.MusicData?.IsTestPlay == true)
			{
				OnTestPlayFinished();
			}
			else
			{
				base.OnFinished();
			}
		}

		private void OnTestPlayFinished()
		{
			if (isTestPlayFinishedCalled || state == LiveControllerState.Finish)
			{
				return;
			}

			isTestPlayFinishedCalled = true;
			UpdateFullComboDataHashIfNeeded();
			state = LiveControllerState.None;
			ShowTestPlayFinishDialog();
		}

		private void ShowTestPlayFinishDialog()
		{
			ScreenLayerMusicScoreMaker.BootArg bootArg = MusicScoreMakerEntryPoint.BootData?.bootData;
			if (bootArg != null && bootArg.IsFromFullComboCheck)
			{
				if (IsFullCombo())
				{
					liveOutUIController?.ShowMusicScoreMakerFullComboSuccessDialog(OnReturnToMusicScoreMaker, Retry, OnProceedToPublish);
				}
				else
				{
					liveOutUIController?.ShowMusicScoreMakerFullComboFailedDialog(OnReturnToMusicScoreMaker, Retry);
				}
				return;
			}

			liveOutUIController?.ShowMusicScoreMakerTestPlayFinishDialog(Retry, CallPreExit);
		}

		private void OnProceedToPublish()
		{
			ScreenLayerMusicScoreMaker.BootArg bootArg = MusicScoreMakerEntryPoint.BootData?.bootData;
			if (bootArg != null)
			{
				bootArg.ShouldProceedToPublish = true;
			}
			OnReturnToMusicScoreMaker();
		}

		private void OnReturnToMusicScoreMaker()
		{
			liveOutUIController?.Destroy();
			state = LiveControllerState.Finish;
			result = 0;
			SetFinish(0f, 0f, BootData?.MusicData?.IsTestPlay == true ? 0.1f : 2f);
		}

		protected override void PreExit(float delay, float waitTime)
		{
			if (state == LiveControllerState.Finish)
			{
				return;
			}

			if (result == 0)
			{
				result = 3;
			}

			TryAppendPlayHistory();
			state = LiveControllerState.Finish;
			SetFinish(delay, waitTime, BootData?.MusicData?.IsTestPlay == true ? 0.1f : 2f);
		}

		private void TryAppendPlayHistory()
		{
			if (playHistoryRecorded || result != 3 || BootData?.IsAuto == true || BootData?.MusicData?.IsTestPlay == true)
			{
				return;
			}

			playHistoryRecorded = true;
			if (BootData is FreeLiveBootData freeLiveBootData)
			{
				CustomMusicScorePlayHistoryStorage.AppendLiveResult(freeLiveBootData, liveLogic?.Score ?? default, currentMusicTimeMs, musicLength);
			}
		}

		protected override void OnExit()
		{
			// Stop video generation recording if in video generation mode
			if (isVideoGenerationMode)
			{
				TryStopVideoGenerationRecording();
			}

			// Restore Video Generation Mode settings
			RestoreVideoGenerationModeSettings();

			LiveViewExt.Finish3D(liveViews);
			LiveViewExt.OnUnload(liveViews);
			MenuScreenType? returnScreenType = (BootData as FreeLiveBootData)?.ReturnScreenType;
			base.OnExit();
			if (returnScreenType.HasValue)
			{
				if (returnScreenType.Value == MenuScreenType.MusicScoreMakerTop)
				{
					MusicScoreMakerEntryPoint.BootData = null;
				}
				SceneManager.Instance.RequestScene(SceneManager.Scene.MusicScoreMaker);
			}
			else if (BootData?.MusicData?.IsTestPlay == true)
			{
				SceneManager.Instance.RequestScene(SceneManager.Scene.MusicScoreMaker);
			}
		}

		private void TryStartVideoGenerationRecording(Camera targetCamera)
		{
			// 检查是否已经是视频生成模式且录制已启动
			if (VideoGenerationController.IsVideoGenerationRecording)
			{
				return;
			}

			// Get boot data from UserDataManager
			FreeLiveBootData bootData = UserDataManager.Instance.FreeLiveBootData;

			if (bootData == null)
			{
				return;
			}

			// Check if this is a video generation mode boot data
			if (bootData is VideoGenerationBootData videoGenData && videoGenData.IsVideoGenerationMode)
			{
				// Get score title from music data
				string scoreTitle = bootData.MusicData?.Music?.title ?? $"Score_{bootData.MusicData?.Music?.id}_{bootData.MusicData?.DifficultyString}";

				// Start video generation recording with explicit camera (BaseCamera)
				// 帧率设置为30fps，平衡画质和录制稳定性
				bool success = VideoGenerationController.StartVideoGenerationRecording(
					videoGenData,
					scoreTitle,
					1920,
					1080,
					30,  // 改为30fps，减少内存占用和帧捕获压力
					targetCamera  // 传入BaseCamera
				);

				if (success)
				{
					Debug.Log($"[SoloLiveController] Video generation recording started successfully at game start. Title: {scoreTitle}, Camera: {targetCamera?.name ?? "null"}");
				}
				else
				{
					Debug.LogWarning($"[SoloLiveController] Failed to start video generation recording for: {scoreTitle}");
				}
			}
		}

		private void TryStopVideoGenerationRecording()
		{
			if (!VideoGenerationController.IsVideoGenerationRecording)
			{
				return;
			}

			// Stop video generation recording and save the path
			string recordingPath = VideoGenerationController.StopVideoGenerationRecording();

			if (recordingPath != null)
			{
				Debug.Log($"[SoloLiveController] Video generation recording stopped. Path: {recordingPath}");

				// Task 9: 触发视频完成流程（后处理、保存、分享）
				StartVideoCompletionFlow();
			}
			else
			{
				Debug.LogWarning("[SoloLiveController] Failed to stop video generation recording.");
			}
		}

		/// <summary>
		/// Task 9: 触发视频完成流程
		/// 协调视频后处理、保存和分享
		/// </summary>
		private void StartVideoCompletionFlow()
		{
			Debug.Log("[SoloLiveController] 开始视频完成流程");

			// 使用VideoCompletionHandler协调整个流程
			VideoCompletionHandler.Instance.StartCompletionFlow();
		}

		private void SetFinish(float delay, float waitTime, float finishWaitSeconds)
		{
			if (finishCoroutine != null)
			{
				StopCoroutine(finishCoroutine);
			}
			if (liveLogic != null)
			{
				liveLogic.OnFinished -= OnFinished;
				liveLogic.OnFailure -= OnFailure;
			}
			finishCoroutine = StartCoroutine(SetFinishCoroutine(delay, waitTime, finishWaitSeconds));
		}

		private System.Collections.IEnumerator SetFinishCoroutine(float delay, float waitTime, float finishWaitSeconds)
		{
			if (delay > 0f)
			{
				yield return new WaitForSeconds(delay);
			}
			yield return ResultAnim(waitTime, finishWaitSeconds);
		}

		private System.Collections.IEnumerator ResultAnim(float waitTime, float finishWaitSeconds)
		{
			LiveViewExt.Result(liveViews, (int)GetLiveResultAnimationType());
			if (waitTime > 0f)
			{
				yield return new WaitForSeconds(waitTime);
			}
			LiveViewExt.Finish(liveViews, finishWaitSeconds);
			if (finishWaitSeconds > 0f)
			{
				yield return new WaitForSeconds(finishWaitSeconds);
			}
			yield return null;
			Exit();
		}

		private LiveResultAnimationType GetLiveResultAnimationType()
		{
			if (BootData?.MusicData?.IsTestPlay == true || result < 2)
			{
				return LiveResultAnimationType.None;
			}

			if (result == 2)
			{
				return LiveResultAnimationType.Failure;
			}

			if (result != 3)
			{
				Debug.LogErrorFormat("Unsupported live result: {0}", result);
				return LiveResultAnimationType.None;
			}

			LiveScore score = liveLogic?.Score ?? default;
			// Check if this is an Auto play
			bool isAutoPlay = BootData?.IsAuto == true;
			int autoResultAnimation = LiveSettingData.LoadFromStorage()?.AutoResultAnimationMode ?? LiveSettingData.AutoResultAnimationNone;

			// If all notes were Auto, handle based on settings
			if (score.totalComboCount > 0 && score.autoCount == score.totalComboCount)
			{
				if (isAutoPlay)
				{
					// Auto play with all Auto notes - use custom result animation setting
					switch (autoResultAnimation)
					{
						case LiveSettingData.AutoResultAnimationNone:
							return LiveResultAnimationType.None;
						case LiveSettingData.AutoResultAnimationAllPerfect:
							return LiveResultAnimationType.AllPerfect;
						case LiveSettingData.AutoResultAnimationFullCombo:
							return LiveResultAnimationType.FullCombo;
						case LiveSettingData.AutoResultAnimationClear:
							return LiveResultAnimationType.Clear;
						case LiveSettingData.AutoResultAnimationFinish:
							return LiveResultAnimationType.LifeZero;
					}
				}
				return LiveResultAnimationType.Clear;
			}

			if (score.totalComboCount == score.perfectCount)
			{
				return LiveResultAnimationType.AllPerfect;
			}
			if (score.totalComboCount == score.maxCombo)
			{
				return LiveResultAnimationType.FullCombo;
			}
			return score.life > 0 ? LiveResultAnimationType.Clear : LiveResultAnimationType.LifeZero;
		}

		private bool IsFullCombo()
		{
			LiveScore score = liveLogic?.Score ?? default;
			return score.totalComboCount > 0 && score.maxCombo >= score.totalComboCount;
		}

		private void UpdateFullComboDataHashIfNeeded()
		{
			ScreenLayerMusicScoreMaker.BootArg bootArg = MusicScoreMakerEntryPoint.BootData?.bootData;
			if (bootArg == null || !bootArg.IsFromFullComboCheck || !IsFullCombo())
			{
				return;
			}

			bootArg.FullComboDataHash = bootArg.MusicScoreDataHashAtTestPlay;
		}

		#region Video Generation Mode Methods

		private void DetectVideoGenerationMode()
		{
			if (BootData is VideoGenerationBootData videoGenData)
			{
				isVideoGenerationMode = videoGenData.IsVideoGenerationMode;
				videoGenerationSpeedMultiplier = videoGenData.VideoGenerationSpeedMultiplier;
				videoGenerationMuteAudio = videoGenData.VideoGenerationMuteAudio;
				videoGenerationDisablePause = videoGenData.VideoGenerationDisablePause;

				if (isVideoGenerationMode)
				{
					Debug.Log($"[VideoGenerationMode] Enabled with speed={videoGenerationSpeedMultiplier}x, mute={videoGenerationMuteAudio}, disablePause={videoGenerationDisablePause}");
				}
			}
			else
			{
				isVideoGenerationMode = false;
				videoGenerationSpeedMultiplier = 1;
				videoGenerationMuteAudio = false;
				videoGenerationDisablePause = false;
			}
		}

		private void ApplyVideoGenerationModeSettings()
		{
			if (!isVideoGenerationMode)
			{
				return;
			}

			// 新方案：录制完整游戏内容和游戏原声（含谱面音乐）
			// 1. 不应用倍速，保持正常速度
			// 2. 不静音BGM，录制完整游戏原声（打击音效 + 谱面音乐）
			// 3. 后期处理直接使用录制的音频

			// Apply mute BGM settings
			if (videoGenerationMuteAudio)
			{
				originalBgmVolume = 1.0f; // 假设默认BGM音量是1.0
				SoundManager.Instance.SetIngameBgmVolume(0f);
				Debug.Log($"[VideoGenerationMode] Applied mute BGM: volume=0, 保留打击音效");
			}
			else
			{
				Debug.Log($"[VideoGenerationMode] 不静音BGM，录制完整游戏原声（打击音效 + 谱面音乐）");
			}

			Debug.Log($"[VideoGenerationMode] 正常速度录制模式，录制完整游戏内容");
		}

		private void RestoreVideoGenerationModeSettings()
		{
			if (!isVideoGenerationMode)
			{
				return;
			}

			// Restore BGM volume
			if (videoGenerationMuteAudio)
			{
				SoundManager.Instance.SetIngameBgmVolume(originalBgmVolume);
				Debug.Log($"[VideoGenerationMode] Restored BGM volume: {originalBgmVolume}");
			}

			Debug.Log("[VideoGenerationMode] 录制完成，BGM音量已恢复");
		}

		#endregion
	}
}
