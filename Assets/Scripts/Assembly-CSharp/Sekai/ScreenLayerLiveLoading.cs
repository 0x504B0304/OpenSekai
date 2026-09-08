using System;
using System.Collections;
using DG.Tweening;
using Sekai.CustomMusicScoreManager;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class ScreenLayerLiveLoading : ScreenLayer
	{
		[SerializeField]
		private GameObject transitionerPrefab;

		[SerializeField]
		private SekaiBG background;

		[SerializeField]
		private CanvasGroup frontGroup;

		[SerializeField]
		private CustomImage progressBar;

		[SerializeField]
		private LoadingIndicatorAnimation gaugeAnimation;

		[SerializeField]
		private CustomImage whiteOutImage;

		private bool transitionStarted;
		private LiveTransitioner transitioner;

		protected override void OnInitComponent()
		{
			base.OnInitComponent();

			// 不在此处启动录制，而是在游戏实际开始时启动（SoloLiveController.OnMusicStart）
			// 这样可以避免录制加载场景的UI相机
			// TryStartVideoGenerationRecording();

			if (frontGroup != null)
			{
				frontGroup.alpha = 0f;
				DOTween.To(() => frontGroup.alpha, value => frontGroup.alpha = value, 1f, 0.2f).SetDelay(0.1f);
			}
			if (whiteOutImage != null)
			{
				Color color = whiteOutImage.color;
				color.a = 1f;
				whiteOutImage.color = color;
			}
			if (background != null)
			{
				background.Initialize();
				background.gameObject.SetActive(false);
			}
			UpdateProgress(0f);
			if (gaugeAnimation != null)
			{
				var gaugeCanvas = gaugeAnimation.GetComponent<Canvas>();
				if (gaugeCanvas != null)
				{
					gaugeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
					gaugeCanvas.overrideSorting = true;
					gaugeCanvas.sortingOrder = 1001;
				}
			}

			StartCoroutine(PlayStartWhiteOut());
			StartTransition(OnFinishTransitionFadeOut);
			StartCoroutine(CompleteGaugeAfterStartAnimation());
		}

		private void TryStartVideoGenerationRecording()
		{
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

				// Start video generation recording with default settings (1920x1080 @ 30fps)
				bool success = VideoGenerationController.StartVideoGenerationRecording(
					videoGenData,
					scoreTitle,
					1920,
					1080,
					30);

				if (success)
				{
					Debug.Log("[ScreenLayerLiveLoading] Video generation recording started successfully.");
				}
				else
				{
					Debug.LogWarning("[ScreenLayerLiveLoading] Failed to start video generation recording.");
				}
			}
		}

		private IEnumerator PlayStartWhiteOut()
		{
			yield return new WaitForSeconds(0.2f);
			if (background != null)
			{
				background.gameObject.SetActive(true);
			}

			if (whiteOutImage == null)
			{
				yield break;
			}

			const float duration = 0.8f;
			var color = whiteOutImage.color;
			var elapsed = 0f;
			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				color.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / duration));
				whiteOutImage.color = color;
				yield return null;
			}

			color.a = 0f;
			whiteOutImage.color = color;
		}

		private IEnumerator CompleteGaugeAfterStartAnimation()
		{
			yield return new WaitForSeconds(1.2f);
			UpdateProgress(1f);
		}

		private void UpdateProgress(float progress)
		{
			progress = Mathf.Clamp01(progress);
			if (progressBar != null)
			{
				progressBar.fillAmount = progress;
			}

			if (gaugeAnimation != null)
			{
				gaugeAnimation.PlayAnimation(progress);
			}
		}

		private void StartTransition(Action onFinished)
		{
			if (transitionStarted)
			{
				return;
			}

			transitionStarted = true;
			if (background != null)
			{
				background.Transition();
			}

			if (transitionerPrefab == null)
			{
				onFinished?.Invoke();
				return;
			}

			GameObject transitionerObject = Instantiate(transitionerPrefab);
			transitionerObject.name = transitionerPrefab.name;
			transitioner = transitionerObject.GetComponent<LiveTransitioner>();
			if (transitioner == null)
			{
				Destroy(transitionerObject);
				onFinished?.Invoke();
				return;
			}

			transitioner.Play(onFinished, "SE_AREA_TRANSITION_SEKAI", false, 0f);
		}

		private void OnFinishTransitionFadeOut()
		{
			SceneManager.Instance.RequestScene(SceneManager.Scene.Core);
		}
	}
}
