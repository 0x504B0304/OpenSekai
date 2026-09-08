using Sekai.Localization;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using Sekai.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.MusicScoreMaker.Ingame.Views
{
	public class AreaSelectModeToggleView : MonoBehaviour
	{
		[SerializeField]
		private UIPartsToggle _toggle;

		private CustomButton _button;
		private const string LocalizedLabelName = "LocalizedAreaSelectLabel";

		private static readonly IsMusicPlayingEvent IsMusicPlayingEventCache;

		private void Awake()
		{
			Setup();
		}

		private void OnDestroy()
		{
			Dispose();
		}

		public void Setup()
		{
			if (_toggle == null)
			{
				_toggle = GetComponent<UIPartsToggle>();
			}
			if (_toggle == null)
			{
				return;
			}
			_button = _toggle.GetComponent<CustomButton>();
			_toggle.Setup(UIPartsToggleBase.State.Off);
			_toggle.OnToggleOn = OnToggleOn;
			_toggle.OnToggleOff = OnToggleOff;
			EnsureLocalizedLabel();
			SetupEventDispatcher();
			UpdateInteractable();
		}

		private void EnsureLocalizedLabel()
		{
			Transform existing = transform.Find(LocalizedLabelName);
			if (existing != null)
			{
				existing.SetAsLastSibling();
				return;
			}

			GameObject overlay = new GameObject(LocalizedLabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			overlay.transform.SetParent(transform, false);
			RectTransform overlayRect = (RectTransform)overlay.transform;
			overlayRect.anchorMin = new Vector2(0f, 1f);
			overlayRect.anchorMax = new Vector2(1f, 1f);
			overlayRect.pivot = new Vector2(0.5f, 1f);
			overlayRect.anchoredPosition = Vector2.zero;
			overlayRect.sizeDelta = new Vector2(0f, 34f);
			Image background = overlay.GetComponent<Image>();
			background.color = new Color32(12, 17, 48, 255);
			background.raycastTarget = false;

			GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
			labelObject.transform.SetParent(overlay.transform, false);
			TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
			TMP_Text reference = FindFirstObjectByType<TMP_Text>();
			if (reference != null) label.font = reference.font;
			label.fontSize = 20f;
			label.color = Color.white;
			label.alignment = TextAlignmentOptions.MidlineLeft;
			label.textWrappingMode = TextWrappingModes.NoWrap;
			label.overflowMode = TextOverflowModes.Ellipsis;
			label.raycastTarget = false;
			RectTransform labelRect = label.rectTransform;
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = new Vector2(18f, 0f);
			labelRect.offsetMax = new Vector2(-4f, 0f);
			labelObject.AddComponent<LocalizedTextBinding>().Key = "editor.range_select";
			overlay.transform.SetAsLastSibling();
		}

		public void Dispose()
		{
			if (_toggle != null)
			{
				_toggle.OnToggleOn = null;
				_toggle.OnToggleOff = null;
			}
			DisposeEventDispatcher();
		}

		private void SetupEventDispatcher()
		{
			MusicScoreMakerEventDispatcher dispatcher = MusicScoreMakerEventDispatcher.Instance;
			dispatcher.Remove<UpdateButtonSelectionStateEvent>(OnUpdateButtonSelectionState);
			dispatcher.Remove<PlayMusicEvent>(OnPlayMusicEvent);
			dispatcher.Remove<PauseMusicEvent>(OnPauseMusicEvent);
			dispatcher.Register<UpdateButtonSelectionStateEvent>(OnUpdateButtonSelectionState);
			dispatcher.Register<PlayMusicEvent>(OnPlayMusicEvent);
			dispatcher.Register<PauseMusicEvent>(OnPauseMusicEvent);
		}

		private void DisposeEventDispatcher()
		{
			if (!MusicScoreMakerEventDispatcher.ExistsInstance)
			{
				return;
			}
			MusicScoreMakerEventDispatcher.Instance.Remove<UpdateButtonSelectionStateEvent>(OnUpdateButtonSelectionState);
			MusicScoreMakerEventDispatcher.Instance.Remove<PlayMusicEvent>(OnPlayMusicEvent);
			MusicScoreMakerEventDispatcher.Instance.Remove<PauseMusicEvent>(OnPauseMusicEvent);
		}

		private void OnUpdateButtonSelectionState(UpdateButtonSelectionStateEvent obj)
		{
			UpdateInteractable();
		}

		private void OnPlayMusicEvent(PlayMusicEvent obj)
		{
			UpdateInteractable();
		}

		private void OnPauseMusicEvent(PauseMusicEvent obj)
		{
			UpdateInteractable();
		}

		private void UpdateInteractable()
		{
			if (_toggle == null)
			{
				return;
			}
			bool isEventSettingMode = MusicScoreMakerEventDispatcher.Instance.PublishFirst<GetIsEventSettingModeEvent, bool>(new GetIsEventSettingModeEvent());
			bool isPlaying = MusicScoreMakerEventDispatcher.Instance.PublishFirst<IsMusicPlayingEvent, bool>(IsMusicPlayingEventCache);
			bool canSelect = !isEventSettingMode && !isPlaying;
			if (_button != null)
			{
				_button.interactable = canSelect;
			}
			_toggle.Setup(!canSelect ? UIPartsToggleBase.State.Disable : MusicScoreMakerUtility.IsAreaSelectMode() ? UIPartsToggleBase.State.On : UIPartsToggleBase.State.Off);
		}

		private void OnToggleOn()
		{
			MusicScoreMakerEventDispatcher.Instance.Publish(new SwitchAreaSelectModeEvent());
		}

		private void OnToggleOff()
		{
			MusicScoreMakerEventDispatcher.Instance.Publish(new SwitchAreaSelectModeEvent());
		}

		public AreaSelectModeToggleView()
		{
		}

		static AreaSelectModeToggleView()
		{
			IsMusicPlayingEventCache = new IsMusicPlayingEvent();
		}
	}
}
