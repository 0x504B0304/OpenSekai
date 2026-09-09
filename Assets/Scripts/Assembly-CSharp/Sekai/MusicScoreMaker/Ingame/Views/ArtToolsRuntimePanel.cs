using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sekai.Live;
using Sekai.Localization;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Presenters;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.MusicScoreMaker.Ingame.Views
{
	public sealed class ArtToolsRuntimePanel : MonoBehaviour
	{
		private const byte PanelBackgroundAlpha = 255;

		private const byte SectionBackgroundAlpha = 205;

		private MusicScoreMakerPresenter _presenter;
		private GameObject _panel;
		private TMP_Text _title;
		private TMP_Text _status;
		private TMP_Text _groupStatus;
		private TMP_InputField _textInput;
		private TMP_InputField _tickInput;
		private TMP_InputField _laneInput;
		private TMP_InputField _scaleXInput;
		private TMP_InputField _scaleYInput;
		private TMP_InputField _lineWidthInput;
		private TMP_Text _fontModeDropdownLabel;
		private GameObject _fontModeDropdownOptions;
		private TMP_Text _fontFileNameLabel;
		private TMP_InputField _imageWidthInput;
		private TMP_InputField _rowSpacingInput;
		private TMP_InputField _anchorWidthInput;
		private Slider _threshold;
		private Toggle _cropForegroundToggle;
		private Toggle _verticalSlicesToggle, _horizontalSlicesToggle, _autoEaseToggle;
		private Toggle _criticalToggle;
		private GameObject _generateButton;
		private GameObject _groupCommands;
		private RawImage _preview;
		private GameObject _textControls;
		private GameObject _imageControls;
		private GameObject _scaleControls;
		private string _loadedGroupSignature;
		private bool _generating;
		private GameObject _groupControls;
		private GameObject _manualConfirm;
		private string _pendingRegenerateGroupId;
		private float _nextSelectionRefresh;
		private bool _selectionObserved;
		private string _observedGroupId;
		private string _fontHash;
		private string _fontDisplayHash;
		private string _fontDisplayName;
		private string _imageHash;
		private TextArtFontMode _fontMode = TextArtFontMode.Hershey;
		private bool _lightForeground;
		private NoteType _noteType = NoteType.Default;
		private Texture2D _previewTexture;
		private CancellationTokenSource _previewCts;
		private TMP_FontAsset _font;
		private string _statusLocalizationKey;
		private object[] _statusLocalizationArguments;

		public static void Attach(Transform toolWindow, MusicScoreMakerPresenter presenter)
		{
			if (toolWindow == null || presenter == null) return;
			ArtToolsRuntimePanel component = toolWindow.GetComponent<ArtToolsRuntimePanel>();
			if (component == null) component = toolWindow.gameObject.AddComponent<ArtToolsRuntimePanel>();
			component._presenter = presenter;
			component._selectionObserved = false;
			if (component._panel == null) component.Build();
		}

		private void Build()
		{
			TMP_Text existing = FindFirstObjectByType<TMP_Text>();
			_font = existing != null ? existing.font : null;
			Transform toolbarContent = FindToolbarContent(transform);
			CreateToolbarButton(toolbarContent, "T", () => Open(false));
			CreateToolbarButton(toolbarContent, "IMG", () => Open(true));

			Canvas canvas = GetComponentInParent<Canvas>();
			if (canvas == null) return;
			_panel = CreateRect("ArtToolsPanel", canvas.transform, new Color32(28, 33, 39, PanelBackgroundAlpha));
			RectTransform panelRect = (RectTransform)_panel.transform;
			panelRect.anchorMin = new Vector2(1, 0);
			panelRect.anchorMax = new Vector2(1, 1);
			panelRect.pivot = new Vector2(1, 0.5f);
			panelRect.sizeDelta = new Vector2(560, -24);
			panelRect.anchoredPosition = new Vector2(-12, 0);
			VerticalLayoutGroup layout = _panel.AddComponent<VerticalLayoutGroup>();
			layout.padding = new RectOffset(20, 20, 16, 16);
			layout.spacing = 8;
			layout.childControlHeight = true;
			layout.childForceExpandHeight = false;

			GameObject header = CreateRow(_panel.transform, 50);
			header.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
			_title = CreateLabel(header.transform, "art.text", 30, TextAlignmentOptions.MidlineLeft);
			_title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
			CreateCommandButton(header.transform, "×", Close, 52);
			Transform body = CreateScrollableBody(_panel.transform);
			_textControls = CreateContainer(body);
			_textInput = CreateInput(_textControls.transform, "art.text.placeholder", 108, true, string.Empty, true);
			_textInput.characterLimit = 300;
			CreateFontMethodDropdown(_textControls.transform);
			CreateFontFileRow(_textControls.transform);

			_imageControls = CreateContainer(body);
			CreateLocalizedButton(_imageControls.transform, "art.import", PickImage, 42);
			GameObject sliceModes = CreateRow(_imageControls.transform, 42);
			ToggleGroup sliceGroup = sliceModes.AddComponent<ToggleGroup>();
			_verticalSlicesToggle = CreateToggle(sliceModes.transform, "art.image.vertical", true, OnSliceModeChanged);
			_horizontalSlicesToggle = CreateToggle(sliceModes.transform, "art.image.horizontal", false, OnSliceModeChanged);
			_verticalSlicesToggle.group = sliceGroup;
			_horizontalSlicesToggle.group = sliceGroup;
			_autoEaseToggle = CreateToggle(_imageControls.transform, "art.image.auto_ease", true, ScheduleImagePreview);
			_autoEaseToggle.gameObject.SetActive(false);
			GameObject previewRoot = CreateRect("BinaryPreview", _imageControls.transform, new Color32(20, 24, 29, 255));
			previewRoot.AddComponent<LayoutElement>().preferredHeight = 150;
			_preview = CreateRect("PreviewImage", previewRoot.transform, Color.clear).AddComponent<RawImage>();
			_preview.color = Color.white;
			_preview.enabled = false;
			_preview.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
			RectTransform previewRect = _preview.rectTransform;
			previewRect.anchorMin = Vector2.zero;
			previewRect.anchorMax = Vector2.one;
			previewRect.offsetMin = previewRect.offsetMax = Vector2.zero;
			_threshold = CreateLabeledSlider(_imageControls.transform, "art.field.threshold", 0, 255, 128);
			_threshold.onValueChanged.AddListener(_ => ScheduleImagePreview());
			_cropForegroundToggle = CreateToggle(_imageControls.transform, "art.crop_foreground", true, ScheduleImagePreview);
			GameObject foreground = CreateRow(_imageControls.transform, 42);
			CreateLocalizedButton(foreground.transform, "art.foreground.dark", () => { _lightForeground = false; ScheduleImagePreview(); });
			CreateLocalizedButton(foreground.transform, "art.foreground.light", () => { _lightForeground = true; ScheduleImagePreview(); });
			GameObject imageNumbers = CreateRow(_imageControls.transform, 48);
			_imageWidthInput = CreateInput(imageNumbers.transform, "art.field.width", 48, false, "12", true);
			_rowSpacingInput = CreateInput(imageNumbers.transform, "art.field.row_spacing", 48, false, "12", true);
			_anchorWidthInput = CreateInput(imageNumbers.transform, "art.field.anchor", 48, false, "0.125", true);
			_imageWidthInput.onValueChanged.AddListener(_ => ScheduleImagePreview());
			_anchorWidthInput.onValueChanged.AddListener(_ => ScheduleImagePreview());
			_rowSpacingInput.onValueChanged.AddListener(_ => ScheduleImagePreview());

			GameObject placement = CreateRow(body, 48);
			_tickInput = CreateInput(placement.transform, "art.field.tick", 48, false, _presenter.CurrentFocusTicks.ToString(), true);
			_laneInput = CreateInput(placement.transform, "art.field.lane", 48, false, "0", true);
			GameObject scale = CreateRow(body, 48);
			_scaleControls = scale;
			_scaleXInput = CreateInput(scale.transform, "art.field.scale_x", 48, false, "0.08", true);
			_scaleYInput = CreateInput(scale.transform, "art.field.scale_y", 48, false, "0.08", true);
			_lineWidthInput = CreateInput(scale.transform, "art.field.line_width", 48, false, "1", true);
			_criticalToggle = CreateToggle(body, "art.critical", false,
				() => _noteType = _criticalToggle != null && _criticalToggle.isOn ? NoteType.Critical : NoteType.Default);
			_generateButton = CreateLocalizedButton(body, "art.generate", Generate, 50);
			// This is dynamic formatted text. A fixed binding would overwrite it on
			// each global localization scan and make the two states appear to race.
			_groupStatus = CreateLabel(body, LocalizationManager.Get("art.group.none"), 18, TextAlignmentOptions.MidlineLeft, 30, false);
			_groupControls = CreateRow(body, 42);
			CreateCommandButton(_groupControls.transform, "←", () => MoveSelected(-1, 0));
			CreateCommandButton(_groupControls.transform, "→", () => MoveSelected(1, 0));
			CreateCommandButton(_groupControls.transform, "↓", () => MoveSelected(0, -1));
			CreateCommandButton(_groupControls.transform, "↑", () => MoveSelected(0, 1));
			CreateCommandButton(_groupControls.transform, "W−", () => ScaleSelected(0.8f, 1f));
			CreateCommandButton(_groupControls.transform, "W+", () => ScaleSelected(1.25f, 1f));
			CreateCommandButton(_groupControls.transform, "H−", () => ScaleSelected(1f, 0.8f));
			CreateCommandButton(_groupControls.transform, "H+", () => ScaleSelected(1f, 1.25f));
			GameObject groupCommands = CreateRow(body, 42);
			_groupCommands = groupCommands;
			CreateLocalizedButton(groupCommands.transform, "art.regenerate", RegenerateSelected);
			CreateLocalizedButton(groupCommands.transform, "common.delete", DeleteSelected);
			_groupControls.SetActive(false);
			groupCommands.name = "ArtGroupCommands";
			groupCommands.SetActive(false);

			_manualConfirm = CreateContainer(body);
			CreateLabel(_manualConfirm.transform, "art.confirm.manual", 18, TextAlignmentOptions.MidlineLeft, 36);
			GameObject confirmButtons = CreateRow(_manualConfirm.transform, 38);
			CreateLocalizedButton(confirmButtons.transform, "common.ok", ConfirmRegenerate);
			CreateLocalizedButton(confirmButtons.transform, "common.cancel", CancelRegenerate);
			_manualConfirm.SetActive(false);
			_status = CreateLabel(body, string.Empty, 20, TextAlignmentOptions.TopLeft, 58, false);
			LocalizationManager.LanguageChanged += RefreshLabels;
			SetImageMode(false);
			_panel.SetActive(false);
		}

		private void Update()
		{
			if (_panel == null || _presenter == null || Time.unscaledTime < _nextSelectionRefresh) return;
			_nextSelectionRefresh = Time.unscaledTime + 0.2f;
			ArtGroupData group = _presenter.GetSelectedArtGroup();
			bool shouldOpen = ObserveSelection(group);
			if (shouldOpen) OpenSelectedGroup(group);
			else if (_panel.activeSelf) RefreshSelectedGroup(group);
		}

		private void Open(bool image)
		{
			if (_panel == null || _presenter == null) return;
			_presenter.SelectArtGroup(null);
			_selectionObserved = true;
			_observedGroupId = null;
			_tickInput.text = _presenter.CurrentFocusTicks.ToString();
			SetImageMode(image);
			_panel.SetActive(true);
			_panel.transform.SetAsLastSibling();
			_loadedGroupSignature = null;
			RefreshSelectedGroup(null);
		}

		public void Close() => _panel?.SetActive(false);

		private void SetImageMode(bool image)
		{
			_textControls?.SetActive(!image);
			_imageControls?.SetActive(image);
			_scaleControls?.SetActive(!image);
			if (_title != null)
			{
				string key = image ? "art.image" : "art.text";
				LocalizedTextBinding binding = _title.GetComponent<LocalizedTextBinding>();
				if (binding != null) binding.Key = key;
				else _title.text = LocalizationManager.Get(key);
			}
		}

		private void SetFontMode(TextArtFontMode mode)
		{
			_fontMode = mode;
			SetFontModeDropdownExpanded(false);
			RefreshFontModeDropdownLabel();
			RefreshFontFileLabel();
		}

		private void CreateFontMethodDropdown(Transform parent)
		{
			CreateLabel(parent, "art.font.method", 18, TextAlignmentOptions.MidlineLeft, 26);
			GameObject field = CreateCommandButton(parent, string.Empty, ToggleFontModeDropdown, 0, 42);
			field.name = "FontModeDropdown";
			_fontModeDropdownLabel = field.GetComponentInChildren<TMP_Text>();
			_fontModeDropdownLabel.alignment = TextAlignmentOptions.MidlineLeft;
			_fontModeDropdownLabel.rectTransform.offsetMin = new Vector2(14, 2);
			_fontModeDropdownOptions = CreateRect("FontModeDropdownOptions", parent, Color.clear);
			VerticalLayoutGroup optionsLayout = _fontModeDropdownOptions.AddComponent<VerticalLayoutGroup>();
			optionsLayout.spacing = 4;
			optionsLayout.childControlHeight = true;
			optionsLayout.childForceExpandHeight = false;
			CreateLocalizedButton(_fontModeDropdownOptions.transform, "art.font.hershey", () => SetFontMode(TextArtFontMode.Hershey), 38);
			CreateLocalizedButton(_fontModeDropdownOptions.transform, "art.font.outline", () => SetFontMode(TextArtFontMode.Outline), 38);
			CreateLocalizedButton(_fontModeDropdownOptions.transform, "art.font.centerline", () => SetFontMode(TextArtFontMode.Centerline), 38);
			SetFontModeDropdownExpanded(false);
			RefreshFontModeDropdownLabel();
		}

		private void CreateFontFileRow(Transform parent)
		{
			GameObject row = CreateRow(parent, 46);
			row.name = "FontFileRow";
			TMP_Text label = CreateLabel(row.transform, "art.font.file", 18, TextAlignmentOptions.MidlineLeft);
			LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
			labelLayout.preferredWidth = 72;
			labelLayout.flexibleWidth = 0;
			_fontFileNameLabel = CreateLabel(row.transform, string.Empty, 16, TextAlignmentOptions.MidlineLeft, 0, false);
			_fontFileNameLabel.name = "FontFileName";
			_fontFileNameLabel.textWrappingMode = TextWrappingModes.NoWrap;
			_fontFileNameLabel.overflowMode = TextOverflowModes.Ellipsis;
			GameObject button = CreateLocalizedButton(row.transform, "art.import", PickFont, 42);
			LayoutElement buttonLayout = button.GetComponent<LayoutElement>();
			buttonLayout.preferredWidth = 170;
			buttonLayout.flexibleWidth = 0;
			RefreshFontFileLabel();
		}

		private void ToggleFontModeDropdown()
		{
			SetFontModeDropdownExpanded(_fontModeDropdownOptions != null && !_fontModeDropdownOptions.activeSelf);
		}

		private void SetFontModeDropdownExpanded(bool expanded)
		{
			_fontModeDropdownOptions?.SetActive(expanded);
		}

		private void RefreshFontModeDropdownLabel()
		{
			if (_fontModeDropdownLabel == null) return;
			string key = _fontMode switch
			{
				TextArtFontMode.Outline => "art.font.outline",
				TextArtFontMode.Centerline => "art.font.centerline",
				_ => "art.font.hershey"
			};
			_fontModeDropdownLabel.text = LocalizationManager.Get(key) + "  ▼";
		}

		private void RefreshFontFileLabel()
		{
			if (_fontFileNameLabel == null) return;
			if (_fontMode == TextArtFontMode.Hershey)
			{
				_fontFileNameLabel.text = LocalizationManager.Get("art.font.builtin");
				return;
			}
			if (string.IsNullOrEmpty(_fontHash))
			{
				_fontFileNameLabel.text = LocalizationManager.Get("art.font.none");
				return;
			}
			if (string.Equals(_fontDisplayHash, _fontHash, StringComparison.Ordinal) && !string.IsNullOrEmpty(_fontDisplayName))
			{
				_fontFileNameLabel.text = _fontDisplayName;
				return;
			}
			string cachedPath = ArtAssetCache.Find(_fontHash);
			_fontFileNameLabel.text = cachedPath == null
				? LocalizationManager.Get("art.font.missing")
				: LocalizationManager.Format("art.font.cached", Path.GetExtension(cachedPath).TrimStart('.').ToUpperInvariant());
		}

		private bool ObserveSelection(ArtGroupData group)
		{
			string groupId = group?.Id;
			bool changed = !_selectionObserved || !string.Equals(_observedGroupId, groupId, StringComparison.Ordinal);
			_selectionObserved = true;
			_observedGroupId = groupId;
			return changed && group != null;
		}

		private void OpenSelectedGroup(ArtGroupData group)
		{
			if (_panel == null || group == null) return;
			_tickInput.text = group.OriginTicks.ToString();
			_panel.SetActive(true);
			_panel.transform.SetAsLastSibling();
			_loadedGroupSignature = null;
			RefreshSelectedGroup(group);
		}

		private void RefreshSelectedGroup()
		{
			RefreshSelectedGroup(_presenter?.GetSelectedArtGroup());
		}

		private void RefreshSelectedGroup(ArtGroupData group)
		{
			bool selected = group != null;
			string signature = selected ? Newtonsoft.Json.JsonConvert.SerializeObject(group) : null;
			if (signature != _loadedGroupSignature)
			{
				_loadedGroupSignature = signature;
				CancelRegenerate();
				if (selected) LoadGroupSettings(group);
			}
			SetGroupEditingState(selected);
			if (_groupStatus != null)
			{
				_groupStatus.text = selected
					? LocalizationManager.Format("art.group.selected", LocalizationManager.Get(group.Type == ArtGroupType.Text ? "art.text" : "art.image")) + (group.IsManuallyEdited ? " *" : string.Empty)
					: LocalizationManager.Get("art.group.none");
			}
		}

		private void SetGroupEditingState(bool selected)
		{
			_generateButton?.SetActive(!selected);
			_groupControls?.SetActive(selected);
			_groupCommands?.SetActive(selected);
		}

		private void LoadGroupSettings(ArtGroupData group)
		{
			SetImageMode(group.Type == ArtGroupType.Image);
			_tickInput.SetTextWithoutNotify(group.OriginTicks.ToString());
			_laneInput.SetTextWithoutNotify(group.OriginLane.ToString());
			_noteType = group.NoteType;
			_criticalToggle?.SetIsOnWithoutNotify(_noteType == NoteType.Critical);
			if (group.TextSettings != null)
			{
				var settings = group.TextSettings;
				_textInput.SetTextWithoutNotify(settings.Text);
				_fontMode = settings.FontMode;
				if (!string.Equals(_fontDisplayHash, settings.FontAssetHash, StringComparison.Ordinal))
				{
					_fontDisplayHash = settings.FontAssetHash;
					_fontDisplayName = null;
				}
				_fontHash = settings.FontAssetHash;
				SetFontModeDropdownExpanded(false);
				RefreshFontModeDropdownLabel();
				RefreshFontFileLabel();
				SetNumber(_scaleXInput, settings.HorizontalScale * group.ScaleX);
				SetNumber(_scaleYInput, settings.VerticalScale * group.ScaleY);
				SetNumber(_lineWidthInput, group.LineWidth);
			}
			if (group.ImageSettings != null)
			{
				var settings = group.ImageSettings;
				_imageHash = settings.ImageAssetHash;
				_threshold.SetValueWithoutNotify(settings.Threshold);
				_cropForegroundToggle.SetIsOnWithoutNotify(settings.CropForeground);
				bool horizontal = settings.SliceMode == ImageArtSliceMode.HorizontalContours;
				_horizontalSlicesToggle.SetIsOnWithoutNotify(horizontal);
				_verticalSlicesToggle.SetIsOnWithoutNotify(!horizontal);
				_autoEaseToggle.SetIsOnWithoutNotify(settings.AutoEase);
				_autoEaseToggle.gameObject.SetActive(horizontal);
				_lightForeground = settings.LightForeground;
				SetNumber(_imageWidthInput, settings.Width * group.ScaleX);
				SetNumber(_rowSpacingInput, settings.RowSpacingTicks * group.ScaleY);
				SetNumber(_anchorWidthInput, settings.AnchorWidth * group.ScaleX);
				ScheduleImagePreview();
			}
		}

		private static void SetNumber(TMP_InputField input, float value) => input.SetTextWithoutNotify(value.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture));

		private TextArtSettings CurrentTextSettings() => new TextArtSettings
		{
			Text = _textInput.text, FontMode = _fontMode, FontAssetHash = _fontHash,
			HorizontalScale = ParseFloat(_scaleXInput, .08f), VerticalScale = ParseFloat(_scaleYInput, .08f), StrokeWidth = ParseFloat(_lineWidthInput, 1)
		};

		private void MoveSelected(int deltaLane, int tickSteps)
		{
			ArtGroupData group = _presenter.GetSelectedArtGroup();
			if (group == null) return;
			_presenter.TransformArtGroup(group.Id, deltaLane, tickSteps * _presenter.CurrentQuantizeTicks, 1f, 1f);
		}

		private void ScaleSelected(float scaleX, float scaleY)
		{
			ArtGroupData group = _presenter.GetSelectedArtGroup();
			if (group == null) return;
			_presenter.TransformArtGroup(group.Id, 0, 0, scaleX, scaleY);
		}

		private void DeleteSelected()
		{
			ArtGroupData group = _presenter.GetSelectedArtGroup();
			if (group == null) return;
			_presenter.DeleteArtGroup(group.Id);
			RefreshSelectedGroup();
		}

		private void RegenerateSelected()
		{
			ArtGroupData group = _presenter.GetSelectedArtGroup();
			if (group == null) return;
			if (group.IsManuallyEdited)
			{
				_pendingRegenerateGroupId = group.Id;
				_manualConfirm.SetActive(true);
				return;
			}
			RegenerateAsync(group.Id, false).Forget();
		}

		private void ConfirmRegenerate()
		{
			string groupId = _pendingRegenerateGroupId;
			CancelRegenerate();
			if (!string.IsNullOrEmpty(groupId)) RegenerateAsync(groupId, true).Forget();
		}

		private void CancelRegenerate()
		{
			_pendingRegenerateGroupId = null;
			_manualConfirm?.SetActive(false);
		}

		private async UniTaskVoid RegenerateAsync(string groupId, bool overwriteManualEdits)
		{
			if (_generating) return;
			var replacement = _presenter.GetSelectedArtGroup();
			if (replacement?.Id != groupId) return;
			_generating = true;
			try
			{
				SetLocalizedStatus("art.status.working");
				replacement.OriginTicks = Math.Max(0, ParseLong(_tickInput, replacement.OriginTicks));
				int lane = Mathf.Clamp((int)ParseLong(_laneInput, replacement.OriginLane), 0, 11);
				if (lane != replacement.OriginLane) replacement.OriginLaneOffset = 0;
				replacement.OriginLane = lane;
				replacement.NoteType = _noteType;
				replacement.ScaleX = replacement.ScaleY = 1;
				if (replacement.Type == ArtGroupType.Text)
				{
					replacement.TextSettings = CurrentTextSettings();
					replacement.LineWidth = replacement.TextSettings.StrokeWidth;
					replacement.SourceAssetHash = _fontHash;
				}
				else
				{
					replacement.ImageSettings = CurrentImageSettings();
					replacement.LineWidth = replacement.ImageSettings.AnchorWidth;
					replacement.SourceAssetHash = _imageHash;
				}
				bool replaced = await _presenter.RegenerateArtGroupAsync(groupId, overwriteManualEdits, this.GetCancellationTokenOnDestroy(), replacement);
				SetLocalizedStatus(replaced ? "art.status.replaced" : "art.confirm.manual");
			}
			catch (Exception exception)
			{
				SetLocalizedStatus(GetArtErrorKey(exception));
				Debug.LogException(exception);
			}
			finally { _generating = false; }
		}

		private async void Generate()
		{
			if (_generating) return;
			_generating = true;
			try
			{
				SetLocalizedStatus("art.status.working");
				long ticks = ParseLong(_tickInput, _presenter.CurrentFocusTicks);
				int lane = Mathf.Clamp((int)ParseLong(_laneInput, 0), 0, 11);
				if (_imageControls.activeSelf)
				{
					ImageArtSettings settings = CurrentImageSettings();
					_presenter.CreateImageArt(settings, ticks, lane, _noteType);
				}
				else
				{
					TextArtSettings settings = CurrentTextSettings();
					await _presenter.CreateTextArtAsync(settings, ticks, lane, _noteType, this.GetCancellationTokenOnDestroy());
				}
				SetLocalizedStatus("art.status.generated");
			}
			catch (Exception exception)
			{
				SetLocalizedStatus(GetArtErrorKey(exception));
				Debug.LogException(exception);
			}
			finally { _generating = false; }
		}

		private ImageArtSettings CurrentImageSettings()
		{
			return new ImageArtSettings
			{
				ImageAssetHash = _imageHash,
				Threshold = Mathf.RoundToInt(_threshold.value),
				LightForeground = _lightForeground,
				Width = ParseFloat(_imageWidthInput, 12),
				RowSpacingTicks = ParseFloat(_rowSpacingInput, 12),
				AnchorWidth = ParseFloat(_anchorWidthInput, .125f),
				CropForeground = _cropForegroundToggle == null || _cropForegroundToggle.isOn,
				SliceMode = _horizontalSlicesToggle.isOn ? ImageArtSliceMode.HorizontalContours : ImageArtSliceMode.VerticalStrips,
				AutoEase = _autoEaseToggle.isOn
			};
		}

		private void PickFont() => PickFile(new[] { "ttf", "otf", "woff" }, path =>
		{
			SetSelectedFont(ArtAssetCache.Import(path), Path.GetFileName(path));
		});

		private void SetSelectedFont(string hash, string displayName)
		{
			_fontHash = hash;
			_fontDisplayHash = hash;
			_fontDisplayName = displayName;
			RefreshFontFileLabel();
			SetStatusLiteral(string.Empty);
		}
		private void PickImage() => PickFile(new[] { "png", "jpg", "jpeg" }, path => { _imageHash = ArtAssetCache.Import(path); SetStatusLiteral(Path.GetFileName(path)); ScheduleImagePreview(); });

		private void PickFile(string[] extensions, Action<string> callback)
		{
#if UNITY_ANDROID || UNITY_IOS
			if (NativeFilePicker.IsFilePickerBusy()) return;
			NativeFilePicker.PickFile(path => { if (this != null && !string.IsNullOrEmpty(path)) callback(path); }, Array.Empty<string>());
#else
			string[] paths = StandaloneFileBrowser.OpenFilePanel(LocalizationManager.Get("art.import"), string.Empty, new[] { new ExtensionFilter("Files", extensions) }, false);
			if (paths != null && paths.Length > 0) callback(paths[0]);
#endif
		}

		private void ScheduleImagePreview()
		{
			_previewCts?.Cancel();
			_previewCts?.Dispose();
			_previewCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
			UpdateImagePreviewAsync(_previewCts.Token).Forget();
		}

		private void OnSliceModeChanged()
		{
			if (_autoEaseToggle == null || _horizontalSlicesToggle == null) return;
			_autoEaseToggle.gameObject.SetActive(_horizontalSlicesToggle.isOn);
			ScheduleImagePreview();
		}

		private async UniTaskVoid UpdateImagePreviewAsync(CancellationToken token)
		{
			try
			{
				await UniTask.Delay(250, cancellationToken: token);
				if (!ArtAssetCache.TryRead(_imageHash, out byte[] bytes, out _)) return;
				Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
				if (!source.LoadImage(bytes)) { Destroy(source); return; }
				ImageArtSettings settings = CurrentImageSettings();
				BinaryImageArt art;
				try { art = ArtGuideGenerator.RasterizeImage(source, settings); }
				finally { Destroy(source); }
				int foregroundCount = art.ForegroundPixelCount;
				int nodeCount;
				if (settings.SliceMode == ImageArtSliceMode.HorizontalContours)
				{
					var contours = await UniTask.RunOnThreadPool(() => ImageArtContourGenerator.Generate(art, settings.AutoEase, token), cancellationToken: token);
					nodeCount = 0;
					foreach (var contour in contours) nodeCount += contour.Count;
					art = await UniTask.RunOnThreadPool(() => ImageArtContourGenerator.RenderPreview(contours, art.Width, art.Height, token), cancellationToken: token);
				}
				else nodeCount = ArtGuideGenerator.GetVerticalRuns(art).Count * 2;
				token.ThrowIfCancellationRequested();
				if (_previewTexture != null) Destroy(_previewTexture);
				_previewTexture = new Texture2D(Mathf.Max(1, art.Width), Mathf.Max(1, art.Height), TextureFormat.RGBA32, false);
				Color32[] pixels = new Color32[_previewTexture.width * _previewTexture.height];
				for (int i = 0; i < pixels.Length; i++) pixels[i] = art.Pixels.Length > i && art.Pixels[i] != 0 ? new Color32(46, 204, 150, 255) : new Color32(25, 29, 34, 255);
				_previewTexture.SetPixels32(pixels);
				_previewTexture.Apply();
				_preview.texture = _previewTexture;
				_preview.enabled = true;
				_previewTexture.filterMode = settings.SliceMode == ImageArtSliceMode.HorizontalContours ? FilterMode.Bilinear : FilterMode.Point;
				_preview.GetComponent<AspectRatioFitter>().aspectRatio = _previewTexture.width / (float)_previewTexture.height;
				SetLocalizedStatus("art.status.preview", foregroundCount, nodeCount);
			}
			catch (OperationCanceledException) { }
			catch (Exception exception) { _preview.enabled = false; SetLocalizedStatus(GetArtErrorKey(exception)); }
		}

		private Transform CreateScrollableBody(Transform parent)
		{
			GameObject viewport = CreateRect("ArtControlsViewport", parent, new Color32(28, 33, 39, PanelBackgroundAlpha));
			viewport.AddComponent<LayoutElement>().flexibleHeight = 1;
			viewport.AddComponent<RectMask2D>();
			ScrollRect scroll = viewport.AddComponent<ScrollRect>();
			scroll.horizontal = false;
			scroll.movementType = ScrollRect.MovementType.Clamped;
			scroll.scrollSensitivity = 48;
			scroll.viewport = (RectTransform)viewport.transform;
			GameObject content = CreateRect("ArtControlsContent", viewport.transform, Color.clear);
			VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
			layout.spacing = 8; layout.childControlHeight = true; layout.childForceExpandHeight = false;
			RectTransform rect = (RectTransform)content.transform;
			rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
			rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = Vector2.zero;
			content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			scroll.content = rect;
			return content.transform;
		}

		private void RefreshLabels()
		{
			if (_panel == null) return;
			foreach (LocalizedTextBinding binding in _panel.GetComponentsInChildren<LocalizedTextBinding>(true)) binding.Refresh();
			SetImageMode(_imageControls != null && _imageControls.activeSelf);
			RefreshFontModeDropdownLabel();
			RefreshFontFileLabel();
			RefreshSelectedGroup();
			RefreshStatus();
		}

		private void SetLocalizedStatus(string key, params object[] arguments)
		{
			_statusLocalizationKey = key;
			_statusLocalizationArguments = arguments;
			RefreshStatus();
		}

		private void SetStatusLiteral(string value)
		{
			_statusLocalizationKey = null;
			_statusLocalizationArguments = null;
			if (_status != null) _status.text = value ?? string.Empty;
		}

		private void RefreshStatus()
		{
			if (_status == null || string.IsNullOrEmpty(_statusLocalizationKey)) return;
			_status.text = _statusLocalizationArguments == null || _statusLocalizationArguments.Length == 0
				? LocalizationManager.Get(_statusLocalizationKey)
				: LocalizationManager.Format(_statusLocalizationKey, _statusLocalizationArguments);
		}

		private void OnDestroy()
		{
			LocalizationManager.LanguageChanged -= RefreshLabels;
			_previewCts?.Cancel();
			_previewCts?.Dispose();
			if (_previewTexture != null) Destroy(_previewTexture);
		}

		private static Transform FindToolbarContent(Transform root)
		{
			Transform exact = root.Find("List/Content/Root/Viewport/Anchor/Content");
			if (exact != null) return exact;
			Transform result = root;
			foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == "Content") result = child;
			return result;
		}

		private void CreateToolbarButton(Transform parent, string icon, Action action)
		{
			GameObject button = CreateRect("ArtTool_" + icon, parent, new Color32(37, 51, 61, 255));
			button.transform.SetSiblingIndex(icon == "T" ? 0 : 1);
			button.AddComponent<LayoutElement>().preferredHeight = 64;
			Button clickable = button.AddComponent<Button>();
			clickable.onClick.AddListener(() => action());
			CreateLabel(button.transform, icon, 32, TextAlignmentOptions.Center, 64, false);
		}

		private GameObject CreateContainer(Transform parent)
		{
			GameObject result = CreateRect("Container", parent, new Color32(34, 40, 47, SectionBackgroundAlpha));
			VerticalLayoutGroup layout = result.AddComponent<VerticalLayoutGroup>();
			layout.padding = new RectOffset(8, 8, 8, 8); layout.spacing = 10; layout.childControlHeight = true; layout.childForceExpandHeight = false;
			return result;
		}

		private GameObject CreateRow(Transform parent, float height)
		{
			GameObject row = CreateRect("Row", parent, Color.clear);
			LayoutElement rowLayout = row.AddComponent<LayoutElement>();
			rowLayout.minHeight = height;
			rowLayout.preferredHeight = height;
			rowLayout.flexibleHeight = 0;
			HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = 10; layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childControlHeight = true;
			return row;
		}

		private GameObject CreateRect(string name, Transform parent, Color color)
		{
			GameObject result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
			result.transform.SetParent(parent, false);
			if (color.a > 0) { Image image = result.AddComponent<Image>(); image.color = color; }
			return result;
		}

		private TMP_Text CreateLabel(Transform parent, string textOrKey, float size, TextAlignmentOptions alignment, float height = 0, bool localized = true)
		{
			GameObject go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
			go.transform.SetParent(parent, false);
			TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
			text.font = _font; text.fontSize = size; text.color = Color.white; text.alignment = alignment; text.textWrappingMode = TextWrappingModes.Normal; text.text = localized ? LocalizationManager.Get(textOrKey) : textOrKey;
			if (height > 0) go.AddComponent<LayoutElement>().preferredHeight = height;
			if (localized) go.AddComponent<LocalizedTextBinding>().Key = textOrKey;
			return text;
		}

		private GameObject CreateLocalizedButton(Transform parent, string key, Action action, float height = 0)
		{
			GameObject go = CreateCommandButton(parent, LocalizationManager.Get(key), action, 0, height);
			go.GetComponentInChildren<TMP_Text>().gameObject.AddComponent<LocalizedTextBinding>().Key = key;
			return go;
		}

		private GameObject CreateCommandButton(Transform parent, string label, Action action, float width = 0, float height = 0)
		{
			GameObject go = CreateRect("Button", parent, new Color32(31, 151, 119, 255));
			LayoutElement element = go.AddComponent<LayoutElement>();
			if (width > 0) { element.preferredWidth = width; element.flexibleWidth = 0; }
			if (height > 0) element.preferredHeight = height;
			Button button = go.AddComponent<Button>(); button.onClick.AddListener(() => action());
			CreateLabel(go.transform, label, 20, TextAlignmentOptions.Center, 0, false).rectTransform.anchorMin = Vector2.zero;
			TMP_Text text = go.GetComponentInChildren<TMP_Text>(); text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = new Vector2(6, 2); text.rectTransform.offsetMax = new Vector2(-6, -2);
			return go;
		}

		private TMP_InputField CreateInput(Transform parent, string placeholder, float height, bool multiline, string value = "", bool localizedPlaceholder = false)
		{
			GameObject root = CreateRect("Input", parent, new Color32(20, 24, 29, 255)); root.AddComponent<LayoutElement>().preferredHeight = height;
			TMP_Text text = CreateLabel(root.transform, value, 20, multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft, 0, false);
			bool hasInitialValue = !string.IsNullOrEmpty(value);
			text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = new Vector2(10, 4); text.rectTransform.offsetMax = new Vector2(-10, hasInitialValue ? -15 : -4);
			string hintValue = localizedPlaceholder ? LocalizationManager.Get(placeholder) : placeholder;
			TMP_Text hint = CreateLabel(root.transform, hintValue, hasInitialValue ? 11 : 18, multiline || hasInitialValue ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft, 0, false); hint.color = new Color(1, 1, 1, hasInitialValue ? 0.55f : 0.38f);
			hint.rectTransform.anchorMin = Vector2.zero; hint.rectTransform.anchorMax = Vector2.one; hint.rectTransform.offsetMin = new Vector2(10, 4); hint.rectTransform.offsetMax = new Vector2(-10, -4);
			if (localizedPlaceholder) hint.gameObject.AddComponent<LocalizedTextBinding>().Key = placeholder;
			TMP_InputField input = root.AddComponent<TMP_InputField>(); input.textComponent = text; input.placeholder = hasInitialValue ? null : hint; input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine; input.text = value;
			return input;
		}

		private Slider CreateLabeledSlider(Transform parent, string labelKey, float min, float max, float value)
		{
			GameObject row = CreateRow(parent, 38);
			TMP_Text label = CreateLabel(row.transform, labelKey, 18, TextAlignmentOptions.MidlineLeft);
			LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
			labelLayout.preferredWidth = 150;
			labelLayout.flexibleWidth = 0;
			return CreateSlider(row.transform, min, max, value);
		}

		private Toggle CreateToggle(Transform parent, string labelKey, bool initialValue, Action onChanged)
		{
			GameObject root = CreateRect("Toggle", parent, Color.clear);
			root.AddComponent<LayoutElement>().preferredHeight = 38;
			GameObject box = CreateRect("Background", root.transform, new Color32(58, 66, 74, 255));
			RectTransform boxRect = (RectTransform)box.transform;
			boxRect.anchorMin = boxRect.anchorMax = new Vector2(0, 0.5f);
			boxRect.pivot = new Vector2(0, 0.5f);
			boxRect.anchoredPosition = new Vector2(4, 0);
			boxRect.sizeDelta = new Vector2(30, 30);
			GameObject check = CreateRect("Checkmark", box.transform, new Color32(46, 204, 150, 255));
			RectTransform checkRect = (RectTransform)check.transform;
			checkRect.anchorMin = Vector2.zero;
			checkRect.anchorMax = Vector2.one;
			checkRect.offsetMin = new Vector2(6, 6);
			checkRect.offsetMax = new Vector2(-6, -6);
			TMP_Text label = CreateLabel(root.transform, labelKey, 18, TextAlignmentOptions.MidlineLeft);
			label.rectTransform.anchorMin = Vector2.zero;
			label.rectTransform.anchorMax = Vector2.one;
			label.rectTransform.offsetMin = new Vector2(46, 0);
			label.rectTransform.offsetMax = Vector2.zero;
			Toggle toggle = root.AddComponent<Toggle>();
			toggle.targetGraphic = box.GetComponent<Image>();
			toggle.graphic = check.GetComponent<Image>();
			toggle.isOn = initialValue;
			toggle.onValueChanged.AddListener(_ => onChanged?.Invoke());
			return toggle;
		}

		private static Slider CreateSlider(Transform parent, float min, float max, float value)
		{
			GameObject root = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider), typeof(LayoutElement)); root.transform.SetParent(parent, false); root.GetComponent<LayoutElement>().preferredHeight = 32;
			root.GetComponent<Image>().color = Color.clear;
			GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image)); background.transform.SetParent(root.transform, false); background.GetComponent<Image>().color = new Color32(58, 66, 74, 255); RectTransform bg = (RectTransform)background.transform; bg.anchorMin = new Vector2(0, .35f); bg.anchorMax = new Vector2(1, .65f); bg.offsetMin = bg.offsetMax = Vector2.zero;
			GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform)); fillArea.transform.SetParent(root.transform, false); RectTransform fillAreaRect = (RectTransform)fillArea.transform; fillAreaRect.anchorMin = new Vector2(.02f, .35f); fillAreaRect.anchorMax = new Vector2(.98f, .65f); fillAreaRect.offsetMin = fillAreaRect.offsetMax = Vector2.zero;
			GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(fillArea.transform, false); fill.GetComponent<Image>().color = new Color32(230, 188, 50, 255); RectTransform fillRect = (RectTransform)fill.transform; fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
			GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)); handleArea.transform.SetParent(root.transform, false); RectTransform handleAreaRect = (RectTransform)handleArea.transform; handleAreaRect.anchorMin = new Vector2(.02f, 0); handleAreaRect.anchorMax = new Vector2(.98f, 1); handleAreaRect.offsetMin = handleAreaRect.offsetMax = Vector2.zero;
			GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(handleArea.transform, false); handle.GetComponent<Image>().color = Color.white; RectTransform handleRect = (RectTransform)handle.transform; handleRect.sizeDelta = new Vector2(20, 30);
			Slider slider = root.GetComponent<Slider>(); slider.fillRect = fillRect; slider.handleRect = handleRect; slider.targetGraphic = handle.GetComponent<Image>(); slider.minValue = min; slider.maxValue = max; slider.value = value;
			return slider;
		}

		private static string GetArtErrorKey(Exception exception)
		{
			if (exception is FileNotFoundException) return "art.error.missing";
			if (exception is InvalidOperationException && exception.Message.IndexOf("8000", StringComparison.Ordinal) >= 0) return "art.error.limit";
			if (exception is ArgumentException) return "art.error.invalid_input";
			if (exception is NotSupportedException) return "art.error.unsupported";
			if (exception is InvalidDataException) return "art.error.invalid_data";
			if (exception is IOException) return "art.error.download";
			return "art.error.generic";
		}

		private static float ParseFloat(TMP_InputField input, float fallback) => float.TryParse(input?.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
		private static long ParseLong(TMP_InputField input, long fallback) => long.TryParse(input?.text, out long value) ? value : fallback;
	}
}
