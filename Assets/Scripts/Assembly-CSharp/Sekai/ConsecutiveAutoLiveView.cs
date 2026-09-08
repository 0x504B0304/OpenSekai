using System;
using UnityEngine;

namespace Sekai
{
	public class ConsecutiveAutoLiveView : MonoBehaviour
	{
		[SerializeField]
		private SpriteRenderer pauseButton;

		[SerializeField]
		private SpriteRenderer pauseButtonDisable;

		[SerializeField]
		private LiveSpriteButton pauseButtonController;

		[SerializeField]
		private SpriteRenderer autoLivePauseButton;

		[SerializeField]
		private SpriteRenderer autoLivePauseButtonDisable;

		[SerializeField]
		private LiveSpriteButton autoLivePauseButtonController;

		[SerializeField]
		private SpriteRenderer autoLabel;

		private LiveOutUIController liveOutUIController;

		public void Setup(Action onClickButton, Action onClickDialogResume, Action onClickDialogCancel, Camera baseCamera)
		{
			Hide();
			liveOutUIController.Initialize(LivePlayMode.Free, baseCamera);
			SetupButton(onClickButton);
		}

		private void SetupButton(Action callback)
		{
			SetActive(autoLivePauseButton, callback != null);
			SetActive(autoLivePauseButtonDisable, false);
			if (autoLivePauseButtonController != null)
			{
				autoLivePauseButtonController.Setup(callback);
			}
		}

		public void Pause()
		{
			autoLivePauseButtonController?.OnClick();
		}

		public void CalculateButtonBounds(Camera camera)
		{
			if (autoLivePauseButtonController != null)
			{
				autoLivePauseButtonController.CalculateBounds(camera);
			}
		}

		public void Show()
		{
			bool useFakePerfect = LiveSettingData.LoadFromStorage()?.UsesAutoFakePerfectMode ?? false;
			SetActive(pauseButton, false);
			SetActive(pauseButtonDisable, false);
			// Hide auto pause button in Fake Perfect mode
			SetActive(autoLivePauseButton, !useFakePerfect);
			SetActive(autoLivePauseButtonDisable, false);
			// Hide auto label in Fake Perfect mode
			SetActive(autoLabel, !useFakePerfect);
			gameObject.SetActive(true);
		}

		public void Hide()
		{
			SetActive(pauseButton, true);
			SetActive(pauseButtonDisable, false);
			SetActive(autoLivePauseButton, true);
			SetActive(autoLivePauseButtonDisable, true);
			SetActive(autoLabel, true);
			gameObject.SetActive(false);
		}

		public ConsecutiveAutoLiveView()
		{
			liveOutUIController = new LiveOutUIController();
		}

		private static void SetActive(SpriteRenderer renderer, bool active)
		{
			if (renderer != null)
			{
				renderer.gameObject.SetActive(active);
			}
		}
	}
}
