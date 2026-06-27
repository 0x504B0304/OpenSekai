using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cysharp.Threading.Tasks;
using Sekai;
using Sekai.Live;
using Sekai.MusicScoreMaker.Common;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Presenters;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using Sekai.UI;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CustomMusicScoreManager.Helpers;

namespace Sekai.CustomMusicScoreManager
{
	public sealed class ScreenLayerCustomMusicScoreManager : ScreenLayer
	{
		private const string BaseFontEbPath = "font/FOT-RodinNTLGPro-EB SDF_Base";

		private const string DynamicFontEbPath = "font/FOT-RodinNTLGPro-EB SDF_Dynamic";

		private const string BaseFontDbPath = "font/FOT-RodinNTLGPro-DB SDF_Base";

		private const string DynamicFontDbPath = "font/FOT-RodinNTLGPro-DB SDF_Dynamic";

		private const int MaxManifestFieldColumnCount = 3;

		private const float ManifestFieldMinWidth = 300f;

		private const float ManifestFieldSpacingX = 22f;

		private const float ManifestFieldLeftPadding = 20f;

		private const float ManifestFieldRightScrollPadding = 44f;

		private const float ActionButtonWidth = 196f;

		private const float ActionButtonHeight = 54f;

		private const float ActionButtonSpacing = 12f;

		private const float ActionButtonsTop = 256f;

		private const float ActionButtonsToFormGap = 22f;

		private const float ManifestFormBottom = 112f;

		private const float BestResultPreferredWidth = 500f;

		private const float BestResultMinWidth = 420f;

		private const float BestResultHeight = 168f;

		private const float BestResultTop = 28f;

		private const float BestResultRight = 28f;

		private const float BestResultGap = 24f;

		private const int NoteSkinTypeCount = 2;

		private const int NoteSeTypeCount = 2;

		private const int NoteEffectTypeCount = 2;

		private const int AutoSaveIntervalTypeCount = 6;

		private static readonly string[] AutoSaveIntervalOptions =
		{
			"关闭",
			"2分钟",
			"4分钟",
			"6分钟",
			"8分钟",
			"10分钟"
		};

		private const int ScoreMakerPreviewModeTypeCount = 3;

		private static readonly string[] ScoreMakerPreviewModeOptions =
		{
			"内容缩略图",
			"音频波形图",
			"叠加"
		};

		private const float MinVisualAlphaPercent = 10f;

		private const float MaxVisualAlphaPercent = 100f;

		private static readonly string[] DifficultyTypes =
		{
			"easy",
			"normal",
			"hard",
			"expert",
			"master",
			"append"
		};

		private static readonly string[] DifficultyDisplayNames =
		{
			"EASY",
			"NORMAL",
			"HARD",
			"EXPERT",
			"MASTER",
			"APPEND"
		};

		private static TMP_FontAsset _baseFontEB;

		private static TMP_FontAsset _baseFontDB;

		private static bool _fontAssetSetup;

		private readonly List<RowView> _rows = new List<RowView>();
		private RectTransform _listContent;
		private RectTransform _actionButtonsContent;
		private ScrollRect _manifestFormScroll;
		private RectTransform _manifestFormScrollRect;
		private RectTransform _manifestFieldGrid;
		private GridLayoutGroup _manifestFieldLayout;
		private HorizontalLayoutGroup _actionButtonsLayout;
		private TextMeshProUGUI _emptyText;
		private TextMeshProUGUI _statusText;
		private RectTransform _detailPanel;
		private TextMeshProUGUI _detailTitle;
		private TextMeshProUGUI _detailMeta;
		private TextMeshProUGUI _detailStatus;
		private RectTransform _bestResultPanel;
		private TextMeshProUGUI _bestResultLeftText;
		private TextMeshProUGUI _bestResultRightText;
		private Image _jacketImage;
		private Button _editButton;
		private Button _playButton;
		private Button _autoButton;
		private Button _duplicateButton;
		private Button _deleteButton;
		private Button _exportButton;
		private Button _saveManifestButton;
		private Button _calculateDurationButton;
		private Button _generateVideoButton;
		private Button _audioSelectButton;
		private Button _jacketSelectButton;
		private Button _scoreSelectButton;
		private Button _videoSelectButton;
		private RectTransform _settingsOverlay;
		private TMP_InputField _settingLiveBgmInput;
		private TMP_InputField _settingLiveSeInput;
		private TMP_InputField _settingNoteSpeedInput;
		private TMP_InputField _settingTimingAdjustInput;
		private TMP_InputField _settingNoteShowRateInput;
		private TMP_InputField _settingBackgroundBrightnessInput;
		private TMP_InputField _settingNoteLineAlphaInput;
		private TMP_InputField _settingGuideLineAlphaInput;
		private TMP_InputField _settingJudgeLineAlphaInput;
		private TextMeshProUGUI _settingAutoSaveIntervalLabel;
		private int _settingAutoSaveIntervalIndex;
		private TextMeshProUGUI _settingScoreMakerPreviewModeLabel;
		private int _settingScoreMakerPreviewModeIndex;
		private TextMeshProUGUI _settingNoteSkinLabel;
		private int _settingNoteSkinIndex;
		private TextMeshProUGUI _settingNoteSeLabel;
		private int _settingNoteSeIndex;
		private TextMeshProUGUI _settingNoteEffectLabel;
		private int _settingNoteEffectIndex;
		private TextMeshProUGUI _settingSimultaneousLineLabel;
		private bool _settingSimultaneousLineEnabled;
		private TextMeshProUGUI _settingMusicInfoDisplayModeLabel;
		private int _settingMusicInfoDisplayMode;
		private TextMeshProUGUI _settingUseMaimaiMusicInfoSeLabel;
		private bool _settingUseMaimaiMusicInfoSeEnabled;
		private TextMeshProUGUI _settingAutoFakePerfectModeLabel;
		private int _settingAutoFakePerfectMode;
		private TextMeshProUGUI _settingAutoResultAnimationLabel;
		private int _settingAutoResultAnimation;
		private TextMeshProUGUI _settingLiveBackgroundModeLabel;
		private int _settingLiveBackgroundMode;
		private TextMeshProUGUI _settingFastLateFlickLabel;
		private bool _settingFastLateFlickEnabled;
		private TextMeshProUGUI _settingFullscreenLabel;
		private bool _settingFullscreenEnabled;
		private string _lastExportPath;
		private TMP_InputField _titleInput;
		private TMP_InputField _scoreTitleInput;
		private TMP_InputField _userInput;
		private TMP_InputField _composerInput;
		private TMP_InputField _lyricistInput;
		private TMP_InputField _arrangerInput;
		private TMP_InputField _singerInput;
		private TMP_InputField _collaborationLabelInput;
		private TMP_InputField _descriptionInput;
		private RectTransform _difficultyFieldRect;
		private TextMeshProUGUI _difficultyLabel;
		private Button _difficultyButton;
		private int _difficultyIndex = 4;
		private TMP_InputField _levelInput;
		private TMP_InputField _durationInput;
		private TMP_InputField _fillerInput;
		private TMP_InputField _audioInput;
		private TMP_InputField _jacketInput;
		private TMP_InputField _scoreInput;
		private TMP_InputField _videoInput;
		private IReadOnlyList<CustomMusicScoreManagerItem> _items = Array.Empty<CustomMusicScoreManagerItem>();
		private CustomMusicScoreManagerItem _selected;
		private Sprite _jacketSprite;

		protected override void Awake()
		{
			base.Awake();
			BuildView();
		}

		protected override void OnBoot(BootArgBase bootData)
		{
			RefreshList();
		}

		protected override void OnScreenStart()
		{
			RefreshList();
		}

		private void LateUpdate()
		{
			UpdateActionButtonLayout();
			UpdateManifestFieldLayout();
			UpdateDetailHeaderLayout();
		}

		protected override void OnExited()
		{
			ClearLoadedJacket();
			base.OnExited();
		}

		private void BuildView()
		{
			RectTransform root = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
			Stretch(root);

			Image background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
			background.color = new Color32(21, 25, 31, 255);

			RectTransform topBar = CreatePanel("TopBar", root, new Color32(31, 37, 45, 255));
			SetStretchTop(topBar, 0f, 0f, 0f, 108f);

			TextMeshProUGUI title = CreateText("Title", topBar, $"Ojsk Community {Application.version}", 40, FontStyles.Bold, TextAlignmentOptions.Left);
			SetAnchor(title.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(560f, 0f));

			RectTransform toolbar = CreateRect("Toolbar", topBar);
			SetAnchor(toolbar, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(980f, 0f));
			HorizontalLayoutGroup toolbarLayout = toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
			toolbarLayout.childAlignment = TextAnchor.MiddleRight;
			toolbarLayout.childControlWidth = false;
			toolbarLayout.childControlHeight = false;
			toolbarLayout.spacing = 14f;
			CreateButton("SettingsButton", toolbar, "设置", OpenSettings, 150f, 56f);
			CreateButton("RefreshButton", toolbar, "刷新", RefreshButtonClicked, 150f, 56f);
			CreateButton("NewButton", toolbar, "新建", CreateEntry, 132f, 56f);
			CreateButton("ImportButton", toolbar, "导入", ImportEntry, 150f, 56f);

			RectTransform body = CreateRect("Body", root);
			SetStretchOffsets(body, 28f, 28f, 28f, 136f);

			RectTransform listPanel = CreatePanel("ListPanel", body, new Color32(26, 31, 38, 255));
			SetAnchor(listPanel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(620f, 0f));

			RectTransform listHeader = CreateRect("ListHeader", listPanel);
			SetStretchTop(listHeader, 18f, 20f, 18f, 50f);
			TextMeshProUGUI listTitle = CreateText("ListTitle", listHeader, "本地谱面", 26, FontStyles.Bold, TextAlignmentOptions.Left);
			Stretch(listTitle.rectTransform);

			ScrollRect scrollRect = CreateScrollRect("ScoreScroll", listPanel, out _listContent);
			SetStretchOffsets(scrollRect.GetComponent<RectTransform>(), 16f, 16f, 16f, 88f);

			_emptyText = CreateText("EmptyText", listPanel, "暂无本地谱面", 24, FontStyles.Normal, TextAlignmentOptions.Center);
			SetStretchOffsets(_emptyText.rectTransform, 28f, 100f, 28f, 100f);

			RectTransform detailPanel = CreatePanel("DetailPanel", body, new Color32(28, 34, 42, 255));
			_detailPanel = detailPanel;
			SetStretchOffsets(detailPanel, 688f, 0f, 0f, 0f);

			_jacketImage = CreateImage("Jacket", detailPanel, new Color32(42, 49, 58, 255));
			SetAnchor(_jacketImage.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(200f, 200f));
			_jacketImage.preserveAspect = true;

			_detailTitle = CreateText("DetailTitle", detailPanel, "请选择谱面", 38, FontStyles.Bold, TextAlignmentOptions.Left);
			SetStretchTop(_detailTitle.rectTransform, 252f, 30f, 30f, 54f);

			_detailMeta = CreateText("DetailMeta", detailPanel, string.Empty, 22, FontStyles.Normal, TextAlignmentOptions.Left);
			SetStretchTop(_detailMeta.rectTransform, 252f, 88f, 30f, 88f);

			_detailStatus = CreateText("DetailStatus", detailPanel, string.Empty, 22, FontStyles.Bold, TextAlignmentOptions.Left);
			SetStretchTop(_detailStatus.rectTransform, 252f, 184f, 30f, 38f);

			_bestResultPanel = CreatePanel("BestResult", detailPanel, new Color32(22, 27, 34, 255));
			SetAnchor(_bestResultPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-BestResultRight, -BestResultTop), new Vector2(BestResultPreferredWidth, BestResultHeight));
			VerticalLayoutGroup bestResultLayout = _bestResultPanel.gameObject.AddComponent<VerticalLayoutGroup>();
			bestResultLayout.padding = new RectOffset(18, 18, 14, 14);
			bestResultLayout.spacing = 6f;
			bestResultLayout.childControlWidth = true;
			bestResultLayout.childControlHeight = true;
			bestResultLayout.childForceExpandWidth = true;
			bestResultLayout.childForceExpandHeight = false;
			TextMeshProUGUI bestResultTitle = CreateText("Title", _bestResultPanel, "最佳成绩", 22, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement bestResultTitleLayout = bestResultTitle.gameObject.AddComponent<LayoutElement>();
			bestResultTitleLayout.minHeight = 30f;
			bestResultTitleLayout.preferredHeight = 30f;
			RectTransform bestResultContent = CreateRect("Content", _bestResultPanel);
			LayoutElement bestResultContentLayout = bestResultContent.gameObject.AddComponent<LayoutElement>();
			bestResultContentLayout.minHeight = 92f;
			bestResultContentLayout.preferredHeight = 92f;
			HorizontalLayoutGroup bestResultContentGroup = bestResultContent.gameObject.AddComponent<HorizontalLayoutGroup>();
			bestResultContentGroup.spacing = 24f;
			bestResultContentGroup.childControlWidth = true;
			bestResultContentGroup.childControlHeight = true;
			bestResultContentGroup.childForceExpandWidth = true;
			bestResultContentGroup.childForceExpandHeight = true;
			_bestResultLeftText = CreateText("LeftText", bestResultContent, string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.Left);
			_bestResultLeftText.enableWordWrapping = false;
			_bestResultLeftText.overflowMode = TextOverflowModes.Overflow;
			_bestResultLeftText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
			_bestResultRightText = CreateText("RightText", bestResultContent, string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.Left);
			_bestResultRightText.enableWordWrapping = false;
			_bestResultRightText.overflowMode = TextOverflowModes.Overflow;
			_bestResultRightText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
			_bestResultPanel.gameObject.SetActive(false);

			RectTransform actionButtons = CreateRect("ActionButtons", detailPanel);
			SetStretchTop(actionButtons, 28f, ActionButtonsTop, 28f, ActionButtonHeight);
			_actionButtonsContent = actionButtons;
			_actionButtonsLayout = actionButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
			_actionButtonsLayout.spacing = ActionButtonSpacing;
			_actionButtonsLayout.childAlignment = TextAnchor.MiddleLeft;
			_actionButtonsLayout.childControlWidth = true;
			_actionButtonsLayout.childControlHeight = true;
			_actionButtonsLayout.childForceExpandWidth = true;
			_actionButtonsLayout.childForceExpandHeight = false;
			_editButton = CreateButton("EditButton", actionButtons, "编辑", OpenEditor, ActionButtonWidth, ActionButtonHeight);
			_playButton = CreateButton("PlayButton", actionButtons, "游玩", PlaySelected, ActionButtonWidth, ActionButtonHeight);
			_autoButton = CreateButton("AutoButton", actionButtons, "自动", AutoPlaySelected, ActionButtonWidth, ActionButtonHeight);
			_duplicateButton = CreateButton("DuplicateButton", actionButtons, "复制", DuplicateSelected, ActionButtonWidth, ActionButtonHeight);
			_exportButton = CreateButton("ExportButton", actionButtons, "导出ZIP", ExportSelected, ActionButtonWidth, ActionButtonHeight);
			_deleteButton = CreateButton("DeleteButton", actionButtons, "删除", DeleteSelected, ActionButtonWidth, ActionButtonHeight, new Color32(110, 49, 57, 255));

			ScrollRect formScroll = CreateMaskedScrollRect("ManifestFormScroll", detailPanel, out RectTransform form);
			_manifestFormScroll = formScroll;
			_manifestFormScrollRect = formScroll.GetComponent<RectTransform>();
			SetStretchOffsets(_manifestFormScrollRect, 28f, ManifestFormBottom, 28f, ActionButtonsTop + ActionButtonHeight + ActionButtonsToFormGap);
			VerticalLayoutGroup formLayout = form.gameObject.AddComponent<VerticalLayoutGroup>();
			formLayout.spacing = 14f;
			formLayout.padding = new RectOffset(Mathf.RoundToInt(ManifestFieldLeftPadding), Mathf.RoundToInt(ManifestFieldRightScrollPadding), 20, 20);
			formLayout.childControlWidth = true;
			formLayout.childControlHeight = false;
			ContentSizeFitter formFitter = form.gameObject.AddComponent<ContentSizeFitter>();
			formFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			_manifestFieldGrid = CreateRect("FieldGrid", form);
			_manifestFieldLayout = _manifestFieldGrid.gameObject.AddComponent<GridLayoutGroup>();
			_manifestFieldLayout.cellSize = new Vector2(ManifestFieldMinWidth, 116f);
			_manifestFieldLayout.spacing = new Vector2(ManifestFieldSpacingX, 20f);
			_manifestFieldLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
			_manifestFieldLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			_manifestFieldLayout.constraintCount = MaxManifestFieldColumnCount;
			ContentSizeFitter fieldFitter = _manifestFieldGrid.gameObject.AddComponent<ContentSizeFitter>();
			fieldFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			_titleInput = CreateInputField(_manifestFieldGrid, "曲名", "title");
			_scoreTitleInput = CreateInputField(_manifestFieldGrid, "谱面标题", "scoreTitle");
			_userInput = CreateInputField(_manifestFieldGrid, "作者", "userName");
			_audioInput = CreateInputField(_manifestFieldGrid, "音频", "audioFileName", ReplaceSelectedAudio, out _audioSelectButton);
			_jacketInput = CreateInputField(_manifestFieldGrid, "封面", "jacketFileName", ReplaceSelectedJacket, out _jacketSelectButton);
			_scoreInput = CreateInputField(_manifestFieldGrid, "谱面", "scoreFileName", ReplaceSelectedScore, out _scoreSelectButton);
			_videoInput = CreateInputField(_manifestFieldGrid, "2DMV", "videoFileName", ReplaceSelectedVideo, out _videoSelectButton);
			_fillerInput = CreateInputField(_manifestFieldGrid, "前置空白秒", "fillerSec");
			_durationInput = CreateInputField(_manifestFieldGrid, "编辑时长秒", "secForMusicScoreMaker");
			_difficultyButton = CreateDifficultySelector(_manifestFieldGrid);
			_levelInput = CreateInputField(_manifestFieldGrid, "等级", "playLevel");
			_composerInput = CreateInputField(_manifestFieldGrid, "作曲", "composer");
			_lyricistInput = CreateInputField(_manifestFieldGrid, "作词", "lyricist");
			_arrangerInput = CreateInputField(_manifestFieldGrid, "编曲", "arranger");
			_singerInput = CreateInputField(_manifestFieldGrid, "歌手", "singer");
			_collaborationLabelInput = CreateInputField(_manifestFieldGrid, "联动标签", "collaborationLabel");
			_descriptionInput = CreateInputField(_manifestFieldGrid, "描述", "description");
			_generateVideoButton = CreateButtonField(_manifestFieldGrid, "生成视频", "点击开始", OnGenerateVideoClicked);
			RectTransform saveRow = CreateRect("SaveManifestRow", detailPanel);
			SetStretchBottom(saveRow, 28f, 28f, 28f, 64f);
			HorizontalLayoutGroup saveRowGroup = saveRow.gameObject.AddComponent<HorizontalLayoutGroup>();
			saveRowGroup.childControlWidth = true;
			saveRowGroup.childControlHeight = true;
			saveRowGroup.childForceExpandWidth = false;
			saveRowGroup.childForceExpandHeight = false;
			saveRowGroup.childAlignment = TextAnchor.MiddleLeft;
			_saveManifestButton = CreateButton("SaveManifestButton", saveRow, "保存配置", SaveSelectedManifest, 230f, 58f);
			_calculateDurationButton = CreateButton("CalculateDurationButton", saveRow, "计算时长", OnCalculateDurationClicked, 160f, 58f);
			_statusText = CreateText("StatusText", saveRow, string.Empty, 21, FontStyles.Normal, TextAlignmentOptions.Right);
			_statusText.raycastTarget = false;
			LayoutElement statusLayout = _statusText.gameObject.AddComponent<LayoutElement>();
			statusLayout.flexibleWidth = 1f;
			statusLayout.minHeight = 58f;
			statusLayout.preferredHeight = 58f;

			BuildSettingsOverlay(root);
			UpdateSelection(null);
		}

		private void BuildSettingsOverlay(RectTransform root)
		{
			_settingsOverlay = CreatePanel("SettingsOverlay", root, new Color32(0, 0, 0, 176));
			Stretch(_settingsOverlay);
			_settingsOverlay.gameObject.SetActive(false);

			RectTransform dialog = CreatePanel("SettingsDialog", _settingsOverlay, new Color32(31, 37, 45, 255));
			SetAnchor(dialog, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 960f));

			VerticalLayoutGroup dialogLayout = dialog.gameObject.AddComponent<VerticalLayoutGroup>();
			dialogLayout.padding = new RectOffset(32, 32, 32, 32);
			dialogLayout.spacing = 16f;
			dialogLayout.childControlWidth = true;
			dialogLayout.childControlHeight = true;
			dialogLayout.childForceExpandWidth = true;
			dialogLayout.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Title", dialog, "设置", 36, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredHeight = 46f;

			ScrollRect settingsScroll = CreateMaskedScrollRect("SettingsScroll", dialog, out RectTransform settingsContent);
			settingsScroll.scrollSensitivity = 48f;
			LayoutElement settingsScrollLayout = settingsScroll.gameObject.AddComponent<LayoutElement>();
			settingsScrollLayout.preferredHeight = 760f;
			settingsScrollLayout.minHeight = 1f;
			settingsScrollLayout.flexibleHeight = 1f;

			VerticalLayoutGroup settingsLayout = settingsContent.gameObject.AddComponent<VerticalLayoutGroup>();
			settingsLayout.padding = new RectOffset(16, 16, 16, 16);
			settingsLayout.spacing = 16f;
			settingsLayout.childControlWidth = true;
			settingsLayout.childControlHeight = true;
			settingsLayout.childForceExpandWidth = true;
			settingsLayout.childForceExpandHeight = false;
			ContentSizeFitter settingsFitter = settingsContent.gameObject.AddComponent<ContentSizeFitter>();
			settingsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			_settingLiveBgmInput = CreateInputField(settingsContent, "音乐音量", "0.0 - 1.0");
			_settingLiveSeInput = CreateInputField(settingsContent, "音效音量", "0.0 - 1.0");
			_settingNoteSpeedInput = CreateInputField(settingsContent, "音符流速", "1.0 - 12.0");
			_settingTimingAdjustInput = CreateInputField(settingsContent, "判定偏移", "-20.0 - 20.0");
			_settingNoteShowRateInput = CreateInputField(settingsContent, "上隐挡板", "0 - 100");
			_settingBackgroundBrightnessInput = CreateInputField(settingsContent, "背景亮度", "0 - 100");
			_settingNoteLineAlphaInput = CreateInputField(settingsContent, "长条线不透明度", "10 - 100");
			_settingGuideLineAlphaInput = CreateInputField(settingsContent, "Guide线不透明度", "10 - 100");
			_settingJudgeLineAlphaInput = CreateInputField(settingsContent, "判定线不透明度", "0 - 100");
			CreateSettingAutoSaveIntervalSelector(settingsContent);
			CreateSettingNoteSkinSelector(settingsContent);
			CreateSettingNoteSeSelector(settingsContent);
			CreateSettingNoteEffectSelector(settingsContent);
			CreateSettingSimultaneousLineSelector(settingsContent);
			CreateSettingMusicInfoDisplayModeSelector(settingsContent);
			CreateSettingMaimaiMusicInfoSeSelector(settingsContent);
			CreateSettingAutoFakePerfectModeSelector(settingsContent);
			CreateSettingAutoResultAnimationSelector(settingsContent);
			CreateSettingScoreMakerPreviewModeSelector(settingsContent);
			CreateSettingLiveBackgroundModeSelector(settingsContent);
			CreateSettingFastLateFlickSelector(settingsContent);
			CreateSettingFullscreenSelector(settingsContent);
			CreateSettingBackupSelector(settingsContent);
			CreateSettingRestoreSelector(settingsContent);

			RectTransform buttonRow = CreateRect("ButtonRow", dialog);
			LayoutElement buttonRowLayout = buttonRow.gameObject.AddComponent<LayoutElement>();
			buttonRowLayout.preferredHeight = 58f;
			buttonRowLayout.minHeight = 58f;
			HorizontalLayoutGroup buttonRowGroup = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
			buttonRowGroup.spacing = 14f;
			buttonRowGroup.childAlignment = TextAnchor.MiddleRight;
			buttonRowGroup.childControlWidth = false;
			buttonRowGroup.childControlHeight = false;
			buttonRowGroup.childForceExpandWidth = false;
			buttonRowGroup.childForceExpandHeight = false;

			CreateButton("CancelButton", buttonRow, "取消", CloseSettings, 150f, 52f);
			CreateButton("SaveButton", buttonRow, "保存", SaveSettings, 150f, 52f);
		}

		private void OpenSettings()
		{
			ApplicationLocalSettings localSettings = ApplicationLocalSettings.LoadFromStorage();
			if (localSettings.LiveVolume == null)
			{
				localSettings.SetupLiveVolume();
			}
			ApplicationLocalSettings.VolumeSettings liveVolume = localSettings.LiveVolume;
			LiveSettingData liveSettingData = LiveSettingData.LoadFromStorage();

			_settingLiveBgmInput.SetTextWithoutNotify(FormatSettingValue(liveVolume.Bgm));
			_settingLiveSeInput.SetTextWithoutNotify(FormatSettingValue(liveVolume.Se));
			_settingNoteSpeedInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.NoteSpeed));
			_settingTimingAdjustInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.TimingAdjustData));
			_settingNoteShowRateInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData._noteShowRate * 100f));
			_settingBackgroundBrightnessInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.Brightness * 100f));
			_settingNoteLineAlphaInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.GetNoteAlpha() * 100f));
			_settingGuideLineAlphaInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.GetGuideAlpha() * 100f));
			_settingJudgeLineAlphaInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.GetJudgeLineAlpha() * 100f));
			SetAutoSaveInterval(liveSettingData.AutoSaveIntervalIndex);
			SetScoreMakerPreviewMode(liveSettingData.ScoreMakerPreviewModeIndex);
			SetSettingNoteSkinIndex(liveSettingData.NoteSkinIndex);
			SetSettingNoteSeIndex(liveSettingData.NoteSeIndex);
			SetSettingNoteEffectIndex(liveSettingData.NoteEffect);
			SetSettingSimultaneousLine(liveSettingData.UseSimultaneousPushingLine);
			SetSettingMusicInfoDisplayMode(liveSettingData.CustomMusicScoreMusicInfoDisplayMode ?? LiveSettingData.MusicInfoDisplayModeCustomScore);
			SetSettingMaimaiMusicInfoSe(liveSettingData.UseMaimaiMusicInfoSe);
			SetSettingAutoFakePerfectMode(liveSettingData.CustomMusicScoreAutoFakePerfectMode ?? LiveSettingData.AutoFakePerfectModeAuto);
			SetSettingAutoResultAnimation(liveSettingData.AutoResultAnimationMode);
			SetSettingLiveBackgroundMode(liveSettingData.CustomMusicScoreLiveBackgroundMode ?? LiveSettingData.CustomMusicScoreBackgroundMode2DMV);
			SetSettingFastLateFlick(liveSettingData.IsFastLateFlick);
			SetSettingFullscreen(localSettings.FullscreenEnabled ?? Screen.fullScreen);
			_settingsOverlay.gameObject.SetActive(true);
			_settingsOverlay.SetAsLastSibling();
		}

		private void CloseSettingsAndReturnToEditor()
		{
			UnityEngine.Debug.Log("CloseSettingsAndReturnToEditor called");
			if (_settingsOverlay != null)
			{
				_settingsOverlay.gameObject.SetActive(false);
			}
			UnityEngine.Debug.Log("Settings closed, now returning to editor");
			// Return to editor with saved state
			OpenEditorAfterSettingsClosed();
		}

		public static void OpenSettingsAfterReturnFromEditor()
		{
			UnityEngine.Debug.Log("OpenSettingsAfterReturnFromEditor called");
			// Open settings (this method is called on the instance itself)
			var screenLayer = UnityEngine.Object.FindObjectOfType<ScreenLayerCustomMusicScoreManager>();
			UnityEngine.Debug.Log("ScreenLayer found: " + (screenLayer != null));
			if (screenLayer != null)
			{
				screenLayer.OpenSettings();
				UnityEngine.Debug.Log("Settings opened");
				// Override buttons in the settings overlay by finding them recursively
				OverrideButtonsInOverlay(screenLayer);
			}
		}

		private static void OverrideButtonsInOverlay(ScreenLayerCustomMusicScoreManager screenLayer)
		{
			if (screenLayer._settingsOverlay == null) return;

			// Find all buttons in the overlay
			var buttons = screenLayer._settingsOverlay.GetComponentsInChildren<UnityEngine.UI.Button>(true);
			UnityEngine.Debug.Log("Found " + buttons.Length + " buttons in overlay");

			foreach (var button in buttons)
			{
				string buttonName = button.name;
				UnityEngine.Debug.Log("Button name: " + buttonName);

				// Try to find text to determine which button this is
				string buttonText = "";
				var textComponent = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
				if (textComponent != null)
				{
					buttonText = textComponent.text;
				}
				var uiText = button.GetComponentInChildren<UnityEngine.UI.Text>();
				if (uiText != null)
				{
					buttonText = uiText.text;
				}
				UnityEngine.Debug.Log("Button text: " + buttonText);

				button.onClick.RemoveAllListeners();
				if (buttonName == "CancelButton" || buttonText == "取消" || buttonText == "Cancel")
				{
					button.onClick.AddListener(() => screenLayer.CloseSettingsAndReturnToEditor());
					UnityEngine.Debug.Log("Cancel button listener added");
				}
				else if (buttonName == "SaveButton" || buttonText == "保存" || buttonText == "Save")
				{
					button.onClick.AddListener(() => screenLayer.SaveSettingsAndReturnToEditor());
					UnityEngine.Debug.Log("Save button listener added");
				}
			}
		}

		private void SaveSettingsAndReturnToEditor()
		{
			UnityEngine.Debug.Log("SaveSettingsAndReturnToEditor called");
			// Save all settings (this includes custom music score settings)
			SaveSettings();
			UnityEngine.Debug.Log("Settings saved, now returning to editor");
			// Close settings and return to editor
			CloseSettingsAndReturnToEditor();
		}

		private void OpenEditorAfterSettingsClosed()
		{
			UnityEngine.Debug.Log("OpenEditorAfterSettingsClosed called");
			// Get the saved state before clearing
			var savedBootData = Sekai.MusicScoreMaker.Ingame.Presenters.MusicScoreMakerEntryPoint.BootDataForSettingsReturn;
			UnityEngine.Debug.Log("SavedBootData: " + (savedBootData != null));

			// Clear the saved state to prevent reuse
			Sekai.MusicScoreMaker.Ingame.Presenters.MusicScoreMakerEntryPoint.BootDataForSettingsReturn = null;

			// Push the editor with saved state
			if (savedBootData != null)
			{
				UnityEngine.Debug.Log("Pushing editor screen");
				ScreenManager.Instance?.PushUIScreen(MenuScreenType.MusicScoreMaker, savedBootData, false);
			}
			else
			{
				UnityEngine.Debug.LogWarning("No saved boot data found, cannot return to editor");
			}
		}

		private void CloseSettings()
		{
			if (_settingsOverlay != null)
			{
				_settingsOverlay.gameObject.SetActive(false);
			}
		}

		private void SaveSettings()
		{
			ApplicationLocalSettings localSettings = ApplicationLocalSettings.LoadFromStorage();
			ApplicationLocalSettings.VolumeSettings liveVolume = localSettings.LiveVolume ?? localSettings.SetupLiveVolume();
			LiveSettingData liveSettingData = LiveSettingData.LoadFromStorage();

			liveVolume.Bgm = ParseClampedSetting(_settingLiveBgmInput.text, 0f, 1f, liveVolume.Bgm);
			liveVolume.Se = ParseClampedSetting(_settingLiveSeInput.text, 0f, 1f, liveVolume.Se);
			liveSettingData.NoteSpeed = ParseClampedSetting(_settingNoteSpeedInput.text, LiveConfig.MinNoteSpeed, LiveConfig.MaxNoteSpeed, liveSettingData.NoteSpeed);
			liveSettingData.TimingAdjustData = ParseClampedSetting(
				_settingTimingAdjustInput.text,
				LiveConfig.MinNoteTiming,
				LiveConfig.MaxNoteTiming,
				liveSettingData.TimingAdjustData);
			liveSettingData.SetNoteShowRate(ParseClampedSetting(
				_settingNoteShowRateInput.text,
				LiveConfig.MinNoteShowRate,
				LiveConfig.MaxNoteShowRate,
				liveSettingData._noteShowRate * 100f));
			liveSettingData.Brightness = ParseClampedSetting(
				_settingBackgroundBrightnessInput.text,
				0f,
				100f,
				liveSettingData.Brightness * 100f) / 100f;
			liveSettingData.NoteAlpha = ParseClampedSetting(
				_settingNoteLineAlphaInput.text,
				MinVisualAlphaPercent,
				MaxVisualAlphaPercent,
				liveSettingData.GetNoteAlpha() * 100f) / 100f;
			liveSettingData.GuideAlpha = ParseClampedSetting(
				_settingGuideLineAlphaInput.text,
				MinVisualAlphaPercent,
				MaxVisualAlphaPercent,
				liveSettingData.GetGuideAlpha() * 100f) / 100f;
			liveSettingData.JudgeLineAlpha = ParseClampedSetting(
				_settingJudgeLineAlphaInput.text,
				0f,
				MaxVisualAlphaPercent,
				liveSettingData.GetJudgeLineAlpha() * 100f) / 100f;
			liveSettingData.NoteSkinIndex = _settingNoteSkinIndex;
			liveSettingData.AutoSaveIntervalIndex = _settingAutoSaveIntervalIndex;
			liveSettingData.ScoreMakerPreviewModeIndex = _settingScoreMakerPreviewModeIndex;
			liveSettingData.NoteSeIndex = _settingNoteSeIndex;
			liveSettingData.NoteEffect = _settingNoteEffectIndex;
			liveSettingData.UseSimultaneousPushingLine = _settingSimultaneousLineEnabled;
			liveSettingData.CustomMusicScoreMusicInfoDisplayMode = _settingMusicInfoDisplayMode;
			liveSettingData.UseMaimaiMusicInfoSe = _settingUseMaimaiMusicInfoSeEnabled;
			liveSettingData.CustomMusicScoreAutoFakePerfectMode = _settingAutoFakePerfectMode;
			liveSettingData.CustomMusicScoreAutoResultAnimation = _settingAutoResultAnimation;
			liveSettingData.CustomMusicScoreLiveBackgroundMode = _settingLiveBackgroundMode;
			liveSettingData.IsFastLateFlick = _settingFastLateFlickEnabled;
			if (ShouldShowDesktopFullscreenSetting())
			{
				localSettings.FullscreenEnabled = _settingFullscreenEnabled;
			}

			ApplicationLocalSettings.SaveToStorage(localSettings);
			LiveSettingData.SaveToStorage(liveSettingData);
			if (ShouldShowDesktopFullscreenSetting())
			{
				ScreenConfig.SetStandaloneFullscreen(_settingFullscreenEnabled);
			}
			SoundManager.Instance.SetupVolume(1f, liveVolume.Bgm, liveVolume.Se, liveVolume.Voice);
			LiveConfig.LongNoteAlpha = liveSettingData.GetNoteAlpha();
			LiveConfig.GuideAlpha = liveSettingData.GetGuideAlpha();
			LiveConfig.JudgeLineAlpha = liveSettingData.GetJudgeLineAlpha();
			LiveConfig.SetNoteSkinAssetBundleName(liveSettingData.NoteSkinIndex);
			LiveConfig.SetNoteSeName(liveSettingData.NoteSeIndex);
			LiveConfig.SetNoteEffectName(liveSettingData.NoteEffect);
			LiveConfig.ScoreMakerPreviewModeIndex = liveSettingData.ScoreMakerPreviewModeIndex;

			_settingLiveBgmInput.SetTextWithoutNotify(FormatSettingValue(liveVolume.Bgm));
			_settingLiveSeInput.SetTextWithoutNotify(FormatSettingValue(liveVolume.Se));
			_settingNoteSpeedInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.NoteSpeed));
			_settingTimingAdjustInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.TimingAdjustData));
			_settingNoteShowRateInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData._noteShowRate * 100f));
			_settingBackgroundBrightnessInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.Brightness * 100f));
			_settingNoteLineAlphaInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.GetNoteAlpha() * 100f));
			_settingGuideLineAlphaInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.GetGuideAlpha() * 100f));
			_settingJudgeLineAlphaInput.SetTextWithoutNotify(FormatSettingValue(liveSettingData.GetJudgeLineAlpha() * 100f));
			SetAutoSaveInterval(liveSettingData.AutoSaveIntervalIndex);
			SetScoreMakerPreviewMode(liveSettingData.ScoreMakerPreviewModeIndex);
			SetSettingNoteSkinIndex(liveSettingData.NoteSkinIndex);
			SetSettingNoteSeIndex(liveSettingData.NoteSeIndex);
			SetSettingNoteEffectIndex(liveSettingData.NoteEffect);
			SetSettingSimultaneousLine(liveSettingData.UseSimultaneousPushingLine);
			SetSettingMusicInfoDisplayMode(liveSettingData.CustomMusicScoreMusicInfoDisplayMode ?? LiveSettingData.MusicInfoDisplayModeCustomScore);
			SetSettingMaimaiMusicInfoSe(liveSettingData.UseMaimaiMusicInfoSe);
			SetSettingAutoFakePerfectMode(liveSettingData.CustomMusicScoreAutoFakePerfectMode ?? LiveSettingData.AutoFakePerfectModeAuto);
			SetSettingAutoResultAnimation(liveSettingData.AutoResultAnimationMode);
			SetSettingLiveBackgroundMode(liveSettingData.CustomMusicScoreLiveBackgroundMode ?? LiveSettingData.CustomMusicScoreBackgroundMode2DMV);
			SetSettingFastLateFlick(liveSettingData.IsFastLateFlick);
			SetSettingFullscreen(localSettings.FullscreenEnabled ?? Screen.fullScreen);
			CloseSettings();
			SetStatus("设置已保存。");
			ShowSuccessDialog("设置已保存。");
		}

		private static float ParseClampedSetting(string text, float min, float max, float fallback)
		{
			if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
			{
				value = fallback;
			}
			return Mathf.Clamp(value, min, max);
		}

		private static string FormatSettingValue(float value)
		{
			return value.ToString("0.###", CultureInfo.InvariantCulture);
		}

		private void CreateSettingAutoSaveIntervalSelector(Transform parent)
		{
			RectTransform row = CreateRect("AutoSaveIntervalSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "自动保存间隔", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleAutoSaveInterval, 220f, 54f);
			_settingAutoSaveIntervalLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetAutoSaveInterval(0);
		}

		private void CycleAutoSaveInterval()
		{
			SetAutoSaveInterval((_settingAutoSaveIntervalIndex + 1) % AutoSaveIntervalTypeCount);
		}

		private void SetAutoSaveInterval(int index)
		{
			_settingAutoSaveIntervalIndex = Mathf.Clamp(index, 0, AutoSaveIntervalTypeCount - 1);
			if (_settingAutoSaveIntervalLabel != null)
			{
				_settingAutoSaveIntervalLabel.text = AutoSaveIntervalOptions[_settingAutoSaveIntervalIndex];
			}
		}

		private void CreateSettingScoreMakerPreviewModeSelector(Transform parent)
		{
			RectTransform row = CreateRect("ScoreMakerPreviewModeSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "制谱器预览方式", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleScoreMakerPreviewMode, 220f, 54f);
			_settingScoreMakerPreviewModeLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetScoreMakerPreviewMode(0);
		}

		private void CycleScoreMakerPreviewMode()
		{
			SetScoreMakerPreviewMode((_settingScoreMakerPreviewModeIndex + 1) % ScoreMakerPreviewModeTypeCount);
		}

		private void SetScoreMakerPreviewMode(int index)
		{
			_settingScoreMakerPreviewModeIndex = Mathf.Clamp(index, 0, ScoreMakerPreviewModeTypeCount - 1);
			LiveConfig.ScoreMakerPreviewModeIndex = _settingScoreMakerPreviewModeIndex;
			if (_settingScoreMakerPreviewModeLabel != null)
			{
				_settingScoreMakerPreviewModeLabel.text = ScoreMakerPreviewModeOptions[_settingScoreMakerPreviewModeIndex];
			}
		}

		private void CreateSettingNoteSkinSelector(Transform parent)
		{
			RectTransform row = CreateRect("NoteSkinSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "Note皮肤", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleSettingNoteSkin, 220f, 54f);
			_settingNoteSkinLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetSettingNoteSkinIndex(0);
		}

		private void CycleSettingNoteSkin()
		{
			SetSettingNoteSkinIndex((_settingNoteSkinIndex + 1) % NoteSkinTypeCount);
		}

		private void SetSettingNoteSkinIndex(int index)
		{
			_settingNoteSkinIndex = Mathf.Clamp(index, 0, NoteSkinTypeCount - 1);
			if (_settingNoteSkinLabel != null)
			{
				_settingNoteSkinLabel.text = string.Format("custom{0:00}", _settingNoteSkinIndex + 1);
			}
		}

		private void CreateSettingNoteSeSelector(Transform parent)
		{
			RectTransform row = CreateRect("NoteSeSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "Note音效", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleSettingNoteSe, 220f, 54f);
			_settingNoteSeLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetSettingNoteSeIndex(0);
		}

		private void CycleSettingNoteSe()
		{
			SetSettingNoteSeIndex((_settingNoteSeIndex + 1) % NoteSeTypeCount);
		}

		private void SetSettingNoteSeIndex(int index)
		{
			_settingNoteSeIndex = Mathf.Clamp(index, 0, NoteSeTypeCount - 1);
			if (_settingNoteSeLabel != null)
			{
				_settingNoteSeLabel.text = LiveConfig.GetNoteSeViewName(_settingNoteSeIndex);
			}
		}

		private void CreateSettingNoteEffectSelector(Transform parent)
		{
			RectTransform row = CreateRect("NoteEffectSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "击打特效", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleSettingNoteEffect, 220f, 54f);
			_settingNoteEffectLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetSettingNoteEffectIndex(0);
		}

		private void CycleSettingNoteEffect()
		{
			SetSettingNoteEffectIndex((_settingNoteEffectIndex + 1) % NoteEffectTypeCount);
		}

		private void SetSettingNoteEffectIndex(int index)
		{
			_settingNoteEffectIndex = Mathf.Clamp(index, 0, NoteEffectTypeCount - 1);
			if (_settingNoteEffectLabel != null)
			{
				_settingNoteEffectLabel.text = string.Format("TYPE {0}", _settingNoteEffectIndex + 1);
			}
		}

		private void CreateSettingSimultaneousLineSelector(Transform parent)
		{
			RectTransform row = CreateRect("SimultaneousLineSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "多押提示线", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleSettingSimultaneousLine, 220f, 54f);
			_settingSimultaneousLineLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetSettingSimultaneousLine(true);
		}

		private void CycleSettingSimultaneousLine()
		{
			SetSettingSimultaneousLine(!_settingSimultaneousLineEnabled);
		}

		private void SetSettingSimultaneousLine(bool enabled)
		{
			_settingSimultaneousLineEnabled = enabled;
			if (_settingSimultaneousLineLabel != null)
			{
				_settingSimultaneousLineLabel.text = enabled ? "开启" : "关闭";
			}
		}

		private void CreateSettingMusicInfoDisplayModeSelector(Transform parent)
		{
			RectTransform row = CreateRect("MusicInfoDisplayModeSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "MusicInfo显示", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleSettingMusicInfoDisplayMode, 220f, 54f);
			_settingMusicInfoDisplayModeLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetSettingMusicInfoDisplayMode(LiveSettingData.MusicInfoDisplayModeCustomScore);
		}

		private void CycleSettingMusicInfoDisplayMode()
		{
			int nextMode;
			switch (_settingMusicInfoDisplayMode)
			{
				case LiveSettingData.MusicInfoDisplayModeNormal:
					nextMode = LiveSettingData.MusicInfoDisplayModeCustomScore;
					break;
				case LiveSettingData.MusicInfoDisplayModeCustomScore:
					nextMode = LiveSettingData.MusicInfoDisplayModeSkip;
					break;
				case LiveSettingData.MusicInfoDisplayModeSkip:
				default:
					nextMode = LiveSettingData.MusicInfoDisplayModeNormal;
					break;
			}
			SetSettingMusicInfoDisplayMode(nextMode);
		}

		private void SetSettingMusicInfoDisplayMode(int mode)
		{
			_settingMusicInfoDisplayMode = mode;
			if (_settingMusicInfoDisplayModeLabel != null)
			{
				string labelText;
				switch (_settingMusicInfoDisplayMode)
				{
					case LiveSettingData.MusicInfoDisplayModeCustomScore:
						labelText = "自制谱模式";
						break;
					case LiveSettingData.MusicInfoDisplayModeSkip:
						labelText = "跳过";
						break;
					case LiveSettingData.MusicInfoDisplayModeNormal:
					default:
						labelText = "正常模式";
						break;
				}
				_settingMusicInfoDisplayModeLabel.text = labelText;
			}
		}

		private void CreateSettingMaimaiMusicInfoSeSelector(Transform parent)
		{
			RectTransform row = CreateRect("MaimaiMusicInfoSeSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "舞萌音效", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleSettingMaimaiMusicInfoSe, 220f, 54f);
			_settingUseMaimaiMusicInfoSeLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetSettingMaimaiMusicInfoSe(false);
		}

		private void CycleSettingMaimaiMusicInfoSe()
		{
			SetSettingMaimaiMusicInfoSe(!_settingUseMaimaiMusicInfoSeEnabled);
		}

		private void SetSettingMaimaiMusicInfoSe(bool enabled)
		{
			_settingUseMaimaiMusicInfoSeEnabled = enabled;
			if (_settingUseMaimaiMusicInfoSeLabel != null)
			{
				_settingUseMaimaiMusicInfoSeLabel.text = enabled ? "开启" : "关闭";
			}
		}

		private void CreateSettingAutoFakePerfectModeSelector(Transform parent)
		{
			RectTransform row = CreateRect("AutoFakePerfectModeSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "Auto模式", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleSettingAutoFakePerfectMode, 220f, 54f);
			_settingAutoFakePerfectModeLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetSettingAutoFakePerfectMode(LiveSettingData.AutoFakePerfectModeAuto);
		}

		private void CycleSettingAutoFakePerfectMode()
		{
			int nextMode = _settingAutoFakePerfectMode == LiveSettingData.AutoFakePerfectModeAuto
				? LiveSettingData.AutoFakePerfectModeFakePerfect
				: LiveSettingData.AutoFakePerfectModeAuto;
			SetSettingAutoFakePerfectMode(nextMode);
		}

		private void SetSettingAutoFakePerfectMode(int mode)
		{
			_settingAutoFakePerfectMode = mode;
			if (_settingAutoFakePerfectModeLabel != null)
			{
				_settingAutoFakePerfectModeLabel.text = _settingAutoFakePerfectMode == LiveSettingData.AutoFakePerfectModeFakePerfect
					? "伪Perfect"
					: "Auto模式";
			}
		}

		private void CreateSettingAutoResultAnimationSelector(Transform parent)
		{
			RectTransform row = CreateRect("AutoResultAnimationSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "Auto结算动画", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleSettingAutoResultAnimation, 220f, 54f);
			_settingAutoResultAnimationLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetSettingAutoResultAnimation(LiveSettingData.AutoResultAnimationClear);
		}

		private void CycleSettingAutoResultAnimation()
		{
			int nextMode = (_settingAutoResultAnimation + 1) % 5;
			SetSettingAutoResultAnimation(nextMode);
		}

		private void SetSettingAutoResultAnimation(int mode)
		{
			_settingAutoResultAnimation = mode;
			if (_settingAutoResultAnimationLabel != null)
			{
				_settingAutoResultAnimationLabel.text = mode switch
				{
					LiveSettingData.AutoResultAnimationNone => "不结算",
					LiveSettingData.AutoResultAnimationAllPerfect => "AP动画",
					LiveSettingData.AutoResultAnimationFullCombo => "FC动画",
					LiveSettingData.AutoResultAnimationClear => "Clear动画",
					LiveSettingData.AutoResultAnimationFinish => "Finish动画",
					_ => "不结算"
				};
			}
		}

		private void CreateSettingLiveBackgroundModeSelector(Transform parent)
		{
			RectTransform row = CreateRect("LiveBackgroundModeSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "Live背景", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleSettingLiveBackgroundMode, 220f, 54f);
			_settingLiveBackgroundModeLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetSettingLiveBackgroundMode(LiveSettingData.CustomMusicScoreBackgroundMode2DMV);
		}

		private void CycleSettingLiveBackgroundMode()
		{
			int nextMode = _settingLiveBackgroundMode == LiveSettingData.CustomMusicScoreBackgroundMode2DMV
				? LiveSettingData.CustomMusicScoreBackgroundModeJacket
				: LiveSettingData.CustomMusicScoreBackgroundMode2DMV;
			SetSettingLiveBackgroundMode(nextMode);
		}

		private void SetSettingLiveBackgroundMode(int mode)
		{
			_settingLiveBackgroundMode = mode == LiveSettingData.CustomMusicScoreBackgroundModeJacket
				? LiveSettingData.CustomMusicScoreBackgroundModeJacket
				: LiveSettingData.CustomMusicScoreBackgroundMode2DMV;
			if (_settingLiveBackgroundModeLabel != null)
			{
				_settingLiveBackgroundModeLabel.text = _settingLiveBackgroundMode == LiveSettingData.CustomMusicScoreBackgroundMode2DMV
					? "2DMV"
					: "封面";
			}
		}

		private void CreateSettingFastLateFlickSelector(Transform parent)
		{
			RectTransform row = CreateRect("FastLateFlickSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "判定偏差显示", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleSettingFastLateFlick, 220f, 54f);
			_settingFastLateFlickLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetSettingFastLateFlick(false);
		}

		private void CycleSettingFastLateFlick()
		{
			SetSettingFastLateFlick(!_settingFastLateFlickEnabled);
		}

		private void SetSettingFastLateFlick(bool enabled)
		{
			_settingFastLateFlickEnabled = enabled;
			if (_settingFastLateFlickLabel != null)
			{
				_settingFastLateFlickLabel.text = enabled ? "开启" : "关闭";
			}
		}

		private void CreateSettingFullscreenSelector(Transform parent)
		{
			RectTransform row = CreateRect("FullscreenSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;
			row.gameObject.SetActive(ShouldShowDesktopFullscreenSetting());

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "桌面全屏", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, string.Empty, CycleSettingFullscreen, 220f, 54f);
			_settingFullscreenLabel = button.GetComponentInChildren<TextMeshProUGUI>();
			SetSettingFullscreen(Screen.fullScreen);
		}

		private void CycleSettingFullscreen()
		{
			SetSettingFullscreen(!_settingFullscreenEnabled);
		}

		private void SetSettingFullscreen(bool enabled)
		{
			_settingFullscreenEnabled = enabled;
			if (_settingFullscreenLabel != null)
			{
				_settingFullscreenLabel.text = enabled ? "开启" : "关闭";
			}
		}

		private void CreateSettingBackupSelector(Transform parent)
		{
			RectTransform row = CreateRect("BackupSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "应用程序备份", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, "点击开始", OnBackupButtonClick, 220f, 54f);
		}

		private void OnBackupButtonClick()
		{
			Debug.Log("[UI] OnBackupButtonClick called");
			ScreenManager.Instance?.Show2ButtonDialog<Common2ButtonDialog>(
				DialogType.Common2ButtonDialog,
				() => OnBackupConfirmed(),
				null,
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				true)?.SetMessageBodyText("是否进行应用程序备份？\n这将备份所有谱面数据和个性化设置");
		}

		private void OnBackupConfirmed()
		{
			Debug.Log("[UI] OnBackupConfirmed called, starting backup service");
			BackupService.SetCallbacks(OnBackupProgress, OnBackupComplete);
			BackupService.StartBackup(this.gameObject.name, "OnBackupComplete", "OnBackupProgress");
		}

		private void OnBackupProgress(string progress)
		{
			Debug.Log("[UI] OnBackupProgress called: " + progress);
			if (_pleaseWaitDialog != null)
			{
				_pleaseWaitDialog.SetMessageBodyText(progress);
			}
			else
			{
				Debug.Log("[UI] Creating please wait dialog");
				_pleaseWaitDialog = ScreenManager.Instance?.Show1ButtonDialog<Common1ButtonDialog>(
					DialogType.Common1ButtonDialog,
					null,
					"WORD_DECIDE",
					null,
					DisplayLayerType.Layer_Dialog,
					DialogSize.Manual,
					false);
				_pleaseWaitDialog?.SetMessageBodyText(progress);
			}
		}

		private void OnBackupComplete(string result)
		{
			Debug.Log("[UI] OnBackupComplete called: " + result);
			_pleaseWaitDialog?.Close();
			_pleaseWaitDialog = null;
			if (result.StartsWith("success:"))
			{
				string backupPath = result.Substring("success:".Length);
				Debug.Log("[UI] Backup succeeded, showing share dialog for: " + backupPath);
				ScreenManager.Instance?.Show2ButtonDialog<Common2ButtonDialog>(
					DialogType.Common2ButtonDialog,
					() => OnShareBackupConfirmed(backupPath),
					null,
					DisplayLayerType.Layer_Dialog,
					DialogSize.Manual,
					true)?.SetMessageBodyText("备份已保存到\n" + backupPath + "\n是否立即分享备份？");
			}
			else if (result == "cancelled")
			{
				SetStatus("已取消备份");
			}
			else
			{
				ScreenManager.Instance?.Show1ButtonDialog<Common1ButtonDialog>(
					DialogType.Common1ButtonDialog,
					null,
					"WORD_DECIDE",
					null,
					DisplayLayerType.Layer_Dialog,
					DialogSize.Manual,
					true)?.SetMessageBodyText("备份失败：" + result);
			}
		}

		private void OnShareBackupConfirmed(string backupPath)
		{
			Debug.Log("[UI] OnShareBackupConfirmed called, path: " + backupPath);
			BackupService.ShareBackup(backupPath);
		}

		private void CreateSettingRestoreSelector(Transform parent)
		{
			RectTransform row = CreateRect("RestoreSelector", parent);
			LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 58f;
			rowLayout.minHeight = 58f;

			HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
			rowGroup.spacing = 16f;
			rowGroup.childAlignment = TextAnchor.MiddleLeft;
			rowGroup.childControlWidth = true;
			rowGroup.childControlHeight = true;
			rowGroup.childForceExpandWidth = false;
			rowGroup.childForceExpandHeight = false;

			TextMeshProUGUI title = CreateText("Label", row, "备份内容恢复", 24, FontStyles.Bold, TextAlignmentOptions.Left);
			LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
			titleLayout.preferredWidth = 180f;
			titleLayout.minWidth = 180f;
			titleLayout.preferredHeight = 58f;
			titleLayout.minHeight = 58f;

			Button button = CreateButton("Button", row, "导入备份", OnRestoreButtonClick, 220f, 54f);
		}

		private void OnRestoreButtonClick()
		{
			// Use native file picker for all non-Standalone platforms
			// StandaloneFileBrowser is unreliable on Android
			if (Application.platform == RuntimePlatform.WindowsEditor ||
			    Application.platform == RuntimePlatform.WindowsPlayer ||
			    Application.platform == RuntimePlatform.OSXEditor ||
			    Application.platform == RuntimePlatform.OSXPlayer ||
			    Application.platform == RuntimePlatform.LinuxEditor ||
			    Application.platform == RuntimePlatform.LinuxPlayer)
			{
				string[] paths = null;
				try
				{
					paths = StandaloneFileBrowser.OpenFilePanel("选择备份文件", "", "zip", false);
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[UI] StandaloneFileBrowser failed: " + ex.Message);
				}

				if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
				{
					_restoreBackupPath = paths[0];
					ShowRestoreScopeDialog();
					return;
				}
				// Fallback to native picker if StandaloneFileBrowser failed or returned empty
			}

#if UNITY_ANDROID
			// Use ShareExportHelper for Android
			OpenFileForRestore();
#else
			// Use NativeFilePicker for iOS and other platforms
			PickNativeFileForRestore();
#endif
		}

		private void PickNativeFileForRestore()
		{
			if (NativeFilePicker.IsFilePickerBusy())
			{
				SetStatus("文件选择器已经打开。");
				return;
			}

			Debug.Log("[UI] PickNativeFileForRestore: Calling NativeFilePicker.PickFile");
			SetStatus("选择备份文件...");
			NativeFilePicker.PickFile(path =>
			{
				if (this == null)
				{
					return;
				}

				Debug.Log("[UI] PickNativeFileForRestore callback, path: " + path);
				if (string.IsNullOrEmpty(path))
				{
					SetStatus("选择备份文件已取消。");
					return;
				}

				_restoreBackupPath = path;
				ShowRestoreScopeDialog();
			}, CreateNativeFileTypes("zip"));
		}

#if UNITY_ANDROID
		private void OpenFileForRestore()
		{
			Debug.Log("[UI] OpenFileForRestore: Calling ShareExportHelper.OpenFile");
			SetStatus("选择备份文件...");
			using (AndroidJavaClass helper = new AndroidJavaClass("com.opensekai.ShareExportHelper"))
			{
				helper.CallStatic("OpenFile", gameObject.name, "OnOpenFileForRestoreComplete");
			}
		}

		private void OnOpenFileForRestoreComplete(string result)
		{
			Debug.Log("[UI] OnOpenFileForRestoreComplete: " + result);
			if (string.IsNullOrEmpty(result))
			{
				SetStatus("选择备份文件已取消。");
				return;
			}

			if (result.StartsWith("success:"))
			{
				_restoreBackupPath = result.Substring("success:".Length);
				Debug.Log("[UI] File restored to: " + _restoreBackupPath);
				ShowRestoreScopeDialog();
			}
			else if (result == "cancelled")
			{
				SetStatus("选择备份文件已取消。");
			}
			else
			{
				SetStatus("选择文件失败：" + result);
			}
		}
#endif

		private void ShowRestoreScopeDialog()
		{
			// Use localization keys for button labels: "RESTORE_SCORES" and "RESTORE_ALL"
			// Use message key "MSG_RESTORE_SCOPE" for dialog body text
			ScreenManager.Instance?.Show2ButtonDialog<Common2ButtonDialog>(
				DialogType.Common2ButtonDialog,
				"MSG_RESTORE_SCOPE",
				"RESTORE_SCORES",
				"RESTORE_ALL",
				() => StartRestore(false),
				() => OnRestoreAllSelected(),
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				true);
		}

		private void OnRestoreAllSelected()
		{
			// Use message key "MSG_RESTORE_ALL_CONFIRM" for dialog body text
			ScreenManager.Instance?.Show2ButtonDialog<Common2ButtonDialog>(
				DialogType.Common2ButtonDialog,
				"MSG_RESTORE_ALL_CONFIRM",
				"WORD_DECIDE",
				"WORD_CANCEL",
				() => StartRestore(true),
				null,
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				true);
		}

		private string _restoreBackupPath;
		private Common1ButtonDialog _pleaseWaitDialog;

		private void StartRestore(bool restoreAll)
		{
			BackupService.StartRestore(_restoreBackupPath, restoreAll, this.gameObject.name, "OnRestoreComplete", "OnRestoreProgress");
		}

		private void OnRestoreProgress(string progress)
		{
			if (_pleaseWaitDialog != null)
			{
				_pleaseWaitDialog.SetMessageBodyText(progress);
			}
			else
			{
				_pleaseWaitDialog = ScreenManager.Instance?.Show1ButtonDialog<Common1ButtonDialog>(
					DialogType.Common1ButtonDialog,
					null,
					"WORD_DECIDE",
					null,
					DisplayLayerType.Layer_Dialog,
					DialogSize.Manual,
					false);
				_pleaseWaitDialog?.SetMessageBodyText(progress);
			}
		}

		private void OnRestoreComplete(string result)
		{
			_pleaseWaitDialog?.Close();
			_pleaseWaitDialog = null;
			if (result == "success")
			{
				ScreenManager.Instance?.Show1ButtonDialog<Common1ButtonDialog>(
					DialogType.Common1ButtonDialog,
					null,
					"WORD_DECIDE",
					null,
					DisplayLayerType.Layer_Dialog,
					DialogSize.Manual,
					true)?.SetMessageBodyText("恢复完成");
			}
			else if (result == "cancelled")
			{
				SetStatus("已取消恢复");
			}
			else
			{
				ScreenManager.Instance?.Show1ButtonDialog<Common1ButtonDialog>(
					DialogType.Common1ButtonDialog,
					null,
					"WORD_DECIDE",
					null,
					DisplayLayerType.Layer_Dialog,
					DialogSize.Manual,
					true)?.SetMessageBodyText("恢复失败，请查看日志。");
			}
		}

		private static bool ShouldShowDesktopFullscreenSetting()
		{
#if UNITY_STANDALONE || UNITY_EDITOR
			return true;
#else
			return false;
#endif
		}

		private void UpdateManifestFieldLayout()
		{
			if (_manifestFieldGrid == null || _manifestFieldLayout == null)
			{
				return;
			}

			float availableWidth = GetManifestFieldAvailableWidth();
			if (availableWidth <= 0f)
			{
				return;
			}

			int columnCount = CalculateManifestColumnCount(availableWidth);
			float spacingWidth = _manifestFieldLayout.spacing.x * (columnCount - 1);
			float cellWidth = Mathf.Floor((availableWidth - spacingWidth) / columnCount);
			cellWidth = Mathf.Max(1f, cellWidth);
			Vector2 cellSize = _manifestFieldLayout.cellSize;
			bool gridWidthChanged = Mathf.Abs(_manifestFieldGrid.rect.width - availableWidth) > 0.5f;
			if (_manifestFieldLayout.constraintCount == columnCount && Mathf.Abs(cellSize.x - cellWidth) <= 0.5f && !gridWidthChanged)
			{
				return;
			}

			_manifestFieldGrid.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, availableWidth);
			_manifestFieldLayout.constraintCount = columnCount;
			_manifestFieldLayout.cellSize = new Vector2(cellWidth, cellSize.y);
			LayoutRebuilder.MarkLayoutForRebuild(_manifestFieldGrid);
			if (_manifestFieldGrid.parent is RectTransform parent)
			{
				LayoutRebuilder.MarkLayoutForRebuild(parent);
			}
		}

		private float GetManifestFieldAvailableWidth()
		{
			if (_manifestFormScroll != null && _manifestFormScroll.viewport != null)
			{
				return _manifestFormScroll.viewport.rect.width - ManifestFieldLeftPadding - ManifestFieldRightScrollPadding;
			}

			return _manifestFieldGrid.rect.width;
		}

		private void UpdateActionButtonLayout()
		{
			if (_actionButtonsContent == null || _actionButtonsLayout == null)
			{
				return;
			}

			float availableWidth = _actionButtonsContent.rect.width;
			if (availableWidth <= 0f)
			{
				return;
			}

			int childCount = _actionButtonsContent.childCount;
			if (childCount <= 0)
			{
				return;
			}

			float totalSpacing = ActionButtonSpacing * Mathf.Max(0, childCount - 1);
			float buttonWidth = Mathf.Max(1f, Mathf.Floor((availableWidth - totalSpacing) / childCount));
			bool layoutChanged = false;
			for (int i = 0; i < childCount; i++)
			{
				LayoutElement layoutElement = _actionButtonsContent.GetChild(i).GetComponent<LayoutElement>();
				if (layoutElement == null)
				{
					continue;
				}

				if (Mathf.Abs(layoutElement.minWidth - 1f) > 0.5f
					|| Mathf.Abs(layoutElement.preferredWidth - buttonWidth) > 0.5f
					|| Mathf.Abs(layoutElement.flexibleWidth - 1f) > 0.5f
					|| Mathf.Abs(layoutElement.minHeight - ActionButtonHeight) > 0.5f
					|| Mathf.Abs(layoutElement.preferredHeight - ActionButtonHeight) > 0.5f)
				{
					layoutElement.minWidth = 1f;
					layoutElement.preferredWidth = buttonWidth;
					layoutElement.flexibleWidth = 1f;
					layoutElement.preferredHeight = ActionButtonHeight;
					layoutElement.minHeight = ActionButtonHeight;
					layoutChanged = true;
				}
			}

			float actionHeight = ActionButtonHeight;
			float formTop = ActionButtonsTop + actionHeight + ActionButtonsToFormGap;
			bool actionHeightChanged = Mathf.Abs(_actionButtonsContent.rect.height - actionHeight) > 0.5f;
			bool formTopChanged = _manifestFormScrollRect != null && Mathf.Abs(_manifestFormScrollRect.offsetMax.y + formTop) > 0.5f;
			if (!layoutChanged && !actionHeightChanged && !formTopChanged)
			{
				return;
			}

			SetStretchTop(_actionButtonsContent, 28f, ActionButtonsTop, 28f, actionHeight);
			if (_manifestFormScrollRect != null)
			{
				SetStretchOffsets(_manifestFormScrollRect, 28f, ManifestFormBottom, 28f, formTop);
			}
			LayoutRebuilder.MarkLayoutForRebuild(_actionButtonsContent);
		}

		private void UpdateDetailHeaderLayout()
		{
			if (_detailPanel == null || _detailTitle == null || _detailMeta == null || _detailStatus == null)
			{
				return;
			}

			float panelWidth = _detailPanel.rect.width;
			if (panelWidth <= 0f)
			{
				return;
			}

			bool showBestResult = _selected != null && panelWidth >= 980f;
			float bestWidth = Mathf.Clamp(panelWidth * 0.36f, BestResultMinWidth, BestResultPreferredWidth);
			float textRight = showBestResult ? BestResultRight + bestWidth + BestResultGap : 30f;
			SetStretchTop(_detailTitle.rectTransform, 252f, 30f, textRight, 54f);
			SetStretchTop(_detailMeta.rectTransform, 252f, 88f, textRight, 88f);
			SetStretchTop(_detailStatus.rectTransform, 252f, 184f, textRight, 38f);

			if (_bestResultPanel == null)
			{
				return;
			}

			_bestResultPanel.gameObject.SetActive(showBestResult);
			if (!showBestResult)
			{
				return;
			}

			SetAnchor(_bestResultPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-BestResultRight, -BestResultTop), new Vector2(bestWidth, BestResultHeight));
		}

		private static int CalculateManifestColumnCount(float availableWidth)
		{
			for (int columns = MaxManifestFieldColumnCount; columns > 1; columns--)
			{
				float requiredWidth = ManifestFieldMinWidth * columns + ManifestFieldSpacingX * (columns - 1);
				if (availableWidth >= requiredWidth)
				{
					return columns;
				}
			}

			return 1;
		}

		private void RefreshButtonClicked()
		{
			RefreshList();
			string statusMessage = "已加载 " + _items.Count.ToString(CultureInfo.InvariantCulture) + " 个谱面。";
			ShowSuccessDialog(statusMessage);
		}

		private void RefreshList()
		{
			_items = CustomMusicScoreManagerService.LoadItems();
			foreach (RowView row in _rows)
			{
				if (row.Root != null)
				{
					Destroy(row.Root);
				}
			}
			_rows.Clear();

			foreach (CustomMusicScoreManagerItem item in _items)
			{
				RowView row = CreateRow(_listContent, item);
				_rows.Add(row);
			}

			_emptyText.gameObject.SetActive(_items.Count == 0);
			CustomMusicScoreManagerItem selected = null;
			if (_selected != null)
			{
				string selectedPath = _selected.Entry.RootDirectory;
				for (int i = 0; i < _items.Count; i++)
				{
					if (string.Equals(_items[i].Entry.RootDirectory, selectedPath, StringComparison.OrdinalIgnoreCase))
					{
						selected = _items[i];
						break;
					}
				}
			}
			UpdateSelection(selected ?? (_items.Count > 0 ? _items[0] : null));
			SetStatus("已加载 " + _items.Count.ToString(CultureInfo.InvariantCulture) + " 个谱面。");
		}

		private RowView CreateRow(RectTransform parent, CustomMusicScoreManagerItem item)
		{
			GameObject root = new GameObject("EntryCell", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
			root.transform.SetParent(parent, false);
			RectTransform rect = root.GetComponent<RectTransform>();
			rect.sizeDelta = new Vector2(0f, 116f);
			LayoutElement layout = root.GetComponent<LayoutElement>();
			layout.preferredHeight = 116f;
			layout.minHeight = 116f;

			Image image = root.GetComponent<Image>();
			image.color = new Color32(39, 46, 55, 255);
			Button button = root.GetComponent<Button>();
			button.targetGraphic = image;
			button.onClick.AddListener(() => UpdateSelection(item));

			TextMeshProUGUI title = CreateText("Title", rect, item.Entry.Manifest.scoreTitle, 26, FontStyles.Bold, TextAlignmentOptions.Left);
			SetAnchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(-44f, 36f));

			string metaText = item.Entry.Manifest.title;
			if (!string.IsNullOrEmpty(item.Entry.Manifest.userName))
			{
				metaText += "  " + item.Entry.Manifest.userName;
			}
			if (!string.IsNullOrEmpty(item.Entry.Manifest.musicDifficultyType))
			{
				metaText += "  " + item.Entry.Manifest.musicDifficultyType.ToUpperInvariant();
			}
			TextMeshProUGUI meta = CreateText("Meta", rect, metaText, 20, FontStyles.Normal, TextAlignmentOptions.Left);
			SetAnchor(meta.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(22f, 16f), new Vector2(-210f, 30f));

			TextMeshProUGUI status = CreateText("Status", rect, item.StatusText, 20, FontStyles.Bold, TextAlignmentOptions.Right);
			SetAnchor(status.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 16f), new Vector2(178f, 30f));
			status.color = item.HasAudio && item.HasScore ? new Color32(126, 221, 166, 255) : new Color32(255, 184, 100, 255);

			return new RowView(root, image, item);
		}

		private void UpdateSelection(CustomMusicScoreManagerItem item)
		{
			_selected = item;
			foreach (RowView row in _rows)
			{
				row.Background.color = row.Item == item ? new Color32(52, 91, 112, 255) : new Color32(39, 46, 55, 255);
			}

			bool hasSelection = item != null;
			_editButton.interactable = hasSelection && item.IsReadyForEdit;
			_playButton.interactable = hasSelection && item.HasScore && item.HasAudio;
			_autoButton.interactable = hasSelection && item.HasScore && item.HasAudio;
			_duplicateButton.interactable = hasSelection;
			_deleteButton.interactable = hasSelection;
			_exportButton.interactable = hasSelection;
			_saveManifestButton.interactable = hasSelection;
			_calculateDurationButton.interactable = hasSelection && item.HasAudio;
			_audioSelectButton.interactable = hasSelection;
			_jacketSelectButton.interactable = hasSelection;
			_scoreSelectButton.interactable = hasSelection;
			_videoSelectButton.interactable = hasSelection;
			SetFormInteractable(hasSelection);

			if (!hasSelection)
			{
				_detailTitle.text = "请选择谱面";
				_detailMeta.text = string.Empty;
				_detailStatus.text = string.Empty;
				SetBestResultText(null);
				ClearLoadedJacket();
				ClearForm();
				return;
			}

			CustomMusicScoreManifest manifest = item.Entry.Manifest;
			_detailTitle.text = manifest.scoreTitle;
			_detailMeta.text = string.Format(
				CultureInfo.InvariantCulture,
				"曲名：{0}\nID：{1}\n路径：{2}\n更新：{3:yyyy-MM-dd HH:mm}",
				manifest.title,
				manifest.id,
				item.Entry.RootDirectory,
				item.LastWriteTime);
			_detailStatus.text = item.StatusText;
			_detailStatus.color = item.HasAudio && item.HasScore ? new Color32(126, 221, 166, 255) : new Color32(255, 184, 100, 255);
			UpdateBestResult(manifest);
			LoadJacket(item.Entry.JacketPath);
			LoadForm(manifest);
		}

		private void UpdateBestResult(CustomMusicScoreManifest manifest)
		{
			if (manifest != null && CustomMusicScorePlayHistoryStorage.TryLoadBestResult(manifest.id, out CustomMusicScorePlayHistoryRecord record))
			{
				SetBestResultText(record);
				return;
			}

			SetBestResultText(null);
		}

		private void SetBestResultText(CustomMusicScorePlayHistoryRecord record)
		{
			if (_bestResultLeftText == null || _bestResultRightText == null)
			{
				return;
			}

			if (record == null)
			{
				_bestResultLeftText.text = _selected == null ? string.Empty : "暂无游玩记录";
				_bestResultRightText.text = string.Empty;
				return;
			}

			_bestResultLeftText.text = string.Format(
				CultureInfo.InvariantCulture,
				"COMBO: {0}/{1}\nPERFECT: {2}\nGREAT: {3}",
				record.maxCombo,
				record.totalComboCount,
				record.perfectCount,
				record.greatCount);
			_bestResultRightText.text = string.Format(
				CultureInfo.InvariantCulture,
				"GOOD: {0}\nBAD: {1}\nMISS: {2}",
				record.goodCount,
				record.badCount,
				record.missCount);
		}

		private void CreateEntry()
		{
			CustomMusicScoreEntry entry = CustomMusicScoreManagerService.CreateNewEntry();
			_selected = entry == null ? null : new CustomMusicScoreManagerItem(
				entry,
				DateTime.Now,
				true,
				File.Exists(entry.ScorePath),
				File.Exists(entry.AudioPath),
				File.Exists(entry.JacketPath));
			RefreshList();
			SetStatus("已创建谱面。");
		}

		private void OpenEditor()
		{
			if (_selected?.Entry == null)
			{
				return;
			}

			CustomMusicScoreEntry entry = CustomMusicScoreStorage.LoadEntry(_selected.Entry.RootDirectory);
			if (entry == null)
			{
				SetStatus("无法加载谱面。");
				RefreshList();
				return;
			}

			ScreenLayerMusicScoreMaker.BootArg bootArg = new ScreenLayerMusicScoreMaker.BootArg
			{
				musicId = entry.MusicId,
				difficulty = entry.Manifest.musicDifficultyType,
				vocalId = 0,
				baseMusicDifficultyId = -1,
				MusicScoreMakerData = entry.LoadScore(),
				LastSavedDataHash = null,
				CurrentMusicScoreScale = 1f,
				FromScreenType = MenuScreenType.MusicScoreMakerTop,
				CustomMusicScoreEntry = entry
			};

			ScreenManager.Instance?.PushUIScreen(MenuScreenType.MusicScoreMaker, bootArg, false);
		}

		private void PlaySelected()
		{
			PlaySelectedAsync(false).Forget();
		}

		private void AutoPlaySelected()
		{
			PlaySelectedAsync(true).Forget();
		}

		private async UniTask PlaySelectedAsync(bool isAuto)
		{
			if (_selected?.Entry == null)
			{
				return;
			}

			CustomMusicScoreEntry entry = SaveSelectedManifestFromForm(refreshList: false);
			entry ??= CustomMusicScoreStorage.LoadEntry(_selected.Entry.RootDirectory);
			if (entry == null)
			{
				SetStatus("无法加载谱面。");
				RefreshList();
				return;
			}

			if (!File.Exists(entry.ScorePath))
			{
				SetStatus("找不到谱面文件。");
				return;
			}
			if (!File.Exists(entry.AudioPath))
			{
				SetStatus("找不到音频文件。");
				return;
			}

			MusicScoreMakerData scoreData = entry.LoadScore();
			if (scoreData == null)
			{
				SetStatus("无法加载谱面文件。");
				return;
			}
			if (!HasPlayableNotes(scoreData))
			{
				SetStatus("没有可游玩的音符。");
				return;
			}

			SetStatus("正在加载音频...");
			bool audioReady = await entry.RegisterAudioAsync(this.GetCancellationTokenOnDestroy());
			if (!audioReady)
			{
				SetStatus("无法加载音频文件。");
				return;
			}

			FreeLiveBootData bootData = CreateDirectPlayBootData(entry, scoreData, isAuto);
			if (bootData == null)
			{
				SetStatus("无法创建游玩启动数据。");
				return;
			}

			UserDataManager.Instance.FreeLiveBootData = bootData;
			Sekai.Core.EntryPoint.PlayMode = Sekai.Core.PlayMode.SoloLive;
			LiveTransitioner.SafeForceFinish(null);
			ScreenManager.Instance?.PushUIScreen(MenuScreenType.LiveLoading, false);
			SetStatus(isAuto ? "正在开始自动游玩..." : "正在开始游玩...");
		}

		private static bool HasPlayableNotes(MusicScoreMakerData data)
		{
			return data?.NoteList != null && data.NoteList.Exists(note => note != null);
		}

		private FreeLiveBootData CreateDirectPlayBootData(CustomMusicScoreEntry entry, MusicScoreMakerData scoreData, bool isAuto)
		{
			if (entry == null || scoreData == null)
			{
				return null;
			}

			LiveBundleBuildData liveBundleBuildData = Resources.Load<LiveBundleBuildData>(LiveConfig.ConfigBundleNamePath);
			MusicScore musicScore = scoreData.ToMusicScore(liveBundleBuildData);
			int deckId = UserDataManager.Instance.SelectedDeckId;
			MasterMusicDifficulty difficulty = CreateDirectPlayDifficulty(entry, musicScore);
			string difficultyString = difficulty?.musicDifficulty ?? "master";
			LiveSettingData liveSettingData = LiveSettingData.LoadFromStorage();
			MusicCategory musicCategory = ResolveDirectPlayMusicCategory(liveSettingData);

			// Matches the original manager play route: normal FreeLive, not editor test play.
			FreeLiveBootData bootData = new FreeLiveBootData(
				entry.MusicId,
				difficultyString,
				0,
				deckId,
				LivePlayMode.Free,
				LiveMusicData.CollaborationModeState.Off,
				musicCategory);

			bootData.LiveEventData = new LiveEventData(Array.Empty<IngameLotterySkill>(), Array.Empty<IngameComboCutin>(), deckId, isAuto);
			bootData.LiveSettingData = liveSettingData;
			bootData.MVQualityType = bootData.LiveSettingData?.QualityType ?? Sekai.MVQualityType.Default;
			bootData.MusicCategory = musicCategory;
			bootData.IsAuto = isAuto;
			bootData.IsCustomMusicScore = true;
			bootData.IsOfficialMusicScore = false;
			bootData.ReturnScreenType = MenuScreenType.MusicScoreMakerTop;
			bootData.canSkipDisplayMusicInfo = false;
			bootData.ReleaseTransitionBeforeMusicStart = true;
			bootData.CustomMusicScoreId = entry.Manifest.id;
			bootData.CustomMusicScorePath = entry.RootDirectory;
			bootData.CustomMusicScoreTitle = entry.Manifest.scoreTitle;
			bootData.CustomMusicScoreAuthorName = entry.Manifest.userName;
			bootData.CustomMusicScoreCollaborationLabel = entry.Manifest.collaborationLabel;

			if (bootData.MusicData != null)
			{
				bootData.MusicData.Music = CreateDirectPlayMusic(entry);
				bootData.MusicData.Difficulty = difficulty;
				bootData.MusicData.Vocal = CreateDirectPlayVocal(entry);
				bootData.MusicData.Score = new MasterPlayLevelScore
				{
					liveType = LiveType.solo.ToString(),
					playLevel = entry.Manifest.playLevel
				};
				bootData.MusicData.IsTestPlay = false;
				bootData.MusicData.IsUseCustomScore = true;
				bootData.MusicData.CustomPlayLevel = entry.Manifest.playLevel;
				bootData.MusicData.MusicScore = musicScore;
				bootData.MusicData.StartMusicTimeMs = 0L;
				// Skip MusicInfo display if skip mode is enabled
				bootData.MusicData.PlayStartEffectEnabled = !(liveSettingData?.SkipsCustomMusicScoreMusicInfo ?? false);
			}

			return bootData;
		}

		private static MusicCategory ResolveDirectPlayMusicCategory(LiveSettingData liveSettingData)
		{
			return liveSettingData?.UsesCustomMusicScore2DMVBackground == true
				? MusicCategory.mv_2d
				: MusicCategory.image;
		}

		private static MasterMusicDifficulty CreateDirectPlayDifficulty(CustomMusicScoreEntry entry, MusicScore musicScore)
		{
			return new MasterMusicDifficulty
			{
				id = MasterMusicDifficulty.INVALID_ID,
				musicId = entry.MusicId,
				musicDifficulty = NormalizeDifficulty(entry.Manifest.musicDifficultyType),
				playLevel = entry.Manifest.playLevel,
				totalNoteCount = LiveUtility.CalculateTotalComboCount(musicScore)
			};
		}

		private static MasterMusic CreateDirectPlayMusic(CustomMusicScoreEntry entry)
		{
			return new MasterMusic
			{
				id = entry.MusicId,
				title = entry.Manifest.title,
				lyricist = entry.Manifest.lyricist,
				composer = entry.Manifest.composer,
				arranger = entry.Manifest.arranger,
				assetbundleName = entry.AudioCueName,
				fillerSec = entry.Manifest.fillerSec,
				secForMusicScoreMaker = entry.MusicDurationSec,
				isAvailableForMusicScoreMaker = true
			};
		}

		private static MasterMusicVocal CreateDirectPlayVocal(CustomMusicScoreEntry entry)
		{
			return new MasterMusicVocal
			{
				id = 0,
				musicId = entry.MusicId,
				musicVocalType = MusicVocalType.original_song.ToString(),
				caption = entry.Manifest.singer,
				assetbundleName = entry.AudioCueName
			};
		}

		private static string NormalizeDifficulty(string difficulty)
		{
			return string.IsNullOrWhiteSpace(difficulty) || string.Equals(difficulty, "none", StringComparison.OrdinalIgnoreCase)
				? "master"
				: difficulty.ToLowerInvariant();
		}

		private void DuplicateSelected()
		{
			if (_selected == null)
			{
				return;
			}

			CustomMusicScoreEntry entry = CustomMusicScoreManagerService.DuplicateEntry(_selected.Entry);
			_selected = entry == null ? null : new CustomMusicScoreManagerItem(entry, DateTime.Now, true, File.Exists(entry.ScorePath), File.Exists(entry.AudioPath), File.Exists(entry.JacketPath));
			RefreshList();
			SetStatus("已复制谱面。");
			ShowSuccessDialog("已复制谱面。");
		}

		private void DeleteSelected()
		{
			if (_selected?.Entry == null)
			{
				return;
			}

			CustomMusicScoreEntry entry = _selected.Entry;
			string title = string.IsNullOrWhiteSpace(entry.Manifest.scoreTitle) ? entry.Manifest.title : entry.Manifest.scoreTitle;
			Common2ButtonDialog dialog = ScreenManager.Instance?.Show2ButtonDialog<Common2ButtonDialog>(
				DialogType.Common2ButtonDialog,
				null,
				"WORD_DELETE",
				"WORD_CANCEL",
				() => ConfirmDeleteSelected(entry, title),
				null,
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				true);
			if (dialog != null)
			{
				dialog.SetMessageBodyText("确定要删除谱面吗？\n\n" + title + "\n\n这个操作无法撤销。");
				return;
			}

			ConfirmDeleteSelected(entry, title);
		}

		private void ConfirmDeleteSelected(CustomMusicScoreEntry entry, string title)
		{
			if (entry == null)
			{
				return;
			}

			string deletedRoot = entry.RootDirectory;
			CustomMusicScoreManagerService.DeleteEntry(entry);
			if (_selected != null && string.Equals(_selected.Entry.RootDirectory, deletedRoot, StringComparison.OrdinalIgnoreCase))
			{
				_selected = null;
			}
			RefreshList();
			SetStatus("已删除 " + title + "。");
		}

		private void ExportSelected()
		{
			if (_selected == null)
			{
				return;
			}

#if UNITY_EDITOR || UNITY_STANDALONE
			string defaultName = _selected.Entry.Manifest.scoreTitle + "_" + _selected.Entry.Manifest.id;
			string destination = SaveStandaloneFile("导出自制谱", CustomMusicScoreStorage.RootDirectory, defaultName, "zip");
			if (string.IsNullOrEmpty(destination))
			{
				return;
			}

			string path = CustomMusicScoreManagerService.ExportZip(_selected.Entry, destination);
			if (string.IsNullOrEmpty(path))
			{
				SetStatus("导出失败。");
				ShowExportFailedDialog();
			}
			else
			{
				SetStatus("已导出：" + path);
				AskShareAfterExport(path);
			}
#elif UNITY_ANDROID || UNITY_IOS
			ExportSelectedNative();
#else
			string path = CustomMusicScoreManagerService.ExportZip(_selected.Entry);
			if (string.IsNullOrEmpty(path))
			{
				SetStatus("导出失败。");
				ShowExportFailedDialog();
			}
			else
			{
				SetStatus("已导出：" + path);
				AskShareAfterExport(path);
			}
#endif
		}

		private void ShowExportFailedDialog()
		{
			ScreenManager.Instance?.Show1ButtonDialog<Common1ButtonDialog>(
				DialogType.Common1ButtonDialog,
				null,
				"WORD_DECIDE",
				null,
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				true)?.SetMessageBodyText("导出失败，请重试。");
		}

		private void AskShareAfterExport(string path)
		{
			Debug.Log("[UI] AskShareAfterExport called, path: " + path);
			Common2ButtonDialog dialog = ScreenManager.Instance?.Show2ButtonDialog<Common2ButtonDialog>(
				DialogType.Common2ButtonDialog,
				() => OnShareConfirmed(path),
				null,
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				true);
			dialog?.SetMessageBodyText("是否立即分享谱面？");
		}

		private void OnShareConfirmed(string path)
		{
			Debug.Log("[UI] OnShareConfirmed called, path: " + path);
			Debug.Log("[UI] UNITY_ANDROID: " +
#if UNITY_ANDROID
				"true"
#else
				"false"
#endif
			);
			Debug.Log("[UI] UNITY_STANDALONE: " +
#if UNITY_STANDALONE
				"true"
#else
				"false"
#endif
			);
#if UNITY_ANDROID
			Debug.Log("[UI] Calling Android ShareFile");
			using (AndroidJavaClass helper = new AndroidJavaClass("com.opensekai.ShareExportHelper"))
			{
				helper.CallStatic("ShareFile", path);
			}
#elif UNITY_STANDALONE || UNITY_EDITOR
			Debug.Log("[UI] Calling OpenInExplorer");
			OpenInExplorer(path);
#endif
		}

		private void OpenInExplorer(string path)
		{
			Debug.Log("[OpenInExplorer] START, path: " + path);
#if UNITY_STANDALONE || UNITY_EDITOR
			try
			{
				Debug.Log("[OpenInExplorer] Inside UNITY_STANDALONE block");
				string directory = Path.GetDirectoryName(path);
				Debug.Log("[OpenInExplorer] directory: " + directory);
				Debug.Log("[OpenInExplorer] Directory.Exists: " + Directory.Exists(directory));
				if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
				{
					// explorer.exe /select, requires backslashes
					string normalizedPath = path.Replace("/", "\\");
					Debug.Log("[OpenInExplorer] Launching explorer with: " + normalizedPath);
					System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + normalizedPath + "\"");
					Debug.Log("[OpenInExplorer] Explorer launched successfully");
				}
				else
				{
					Debug.LogWarning("[OpenInExplorer] Directory does not exist or path is invalid");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError("[OpenInExplorer] Exception: " + ex.GetType().Name + ", " + ex.Message);
				Debug.LogError("[OpenInExplorer] StackTrace: " + ex.StackTrace);
				SetStatus("无法打开文件资源管理器。");
			}
#else
			Debug.Log("[OpenInExplorer] Not on UNITY_STANDALONE || UNITY_EDITOR");
#endif
		}

		private void ShareFileAndroid(string path)
		{
#if UNITY_ANDROID
			try
			{
				using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
				{
					using (AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent"))
					{
						intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
						intentObject.Call<AndroidJavaObject>("setType", "application/zip");

						using (AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri"))
						{
							using (AndroidJavaObject fileObject = new AndroidJavaObject("java.io.File", path))
							{
								using (AndroidJavaObject uriObject = uriClass.CallStatic<AndroidJavaObject>("fromFile", fileObject))
								{
									intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uriObject);
									intentObject.Call<AndroidJavaObject>("addFlags", intentClass.GetStatic<int>("FLAG_GRANT_READ_URI_PERMISSION"));
								}
							}
						}

						using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
						{
							using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
							{
								currentActivity.Call("startActivity", intentObject);
							}
						}
					}
				}
			}
			catch (Exception)
			{
				SetStatus("分享失败。");
			}
#endif
		}

		private void ImportEntry()
		{
#if UNITY_EDITOR || UNITY_STANDALONE
			CustomMusicScoreEntry entry = null;
			string path = PickStandaloneFile(
				"导入自制谱ZIP",
				string.Empty,
				new ExtensionFilter("自制谱ZIP", "zip"));
			if (!string.IsNullOrEmpty(path))
			{
				entry = CustomMusicScoreManagerService.ImportZip(path);
			}
			else
			{
				string folder = PickStandaloneFolder("导入自制谱文件夹", string.Empty);
				if (!string.IsNullOrEmpty(folder))
				{
					entry = CustomMusicScoreManagerService.ImportFolder(folder);
				}
			}
			ApplyImportedEntry(entry);
#elif UNITY_ANDROID || UNITY_IOS
			PickNativeFile(
				"导入自制谱ZIP",
				"导入已取消或失败。",
				path => ApplyImportedEntry(CustomMusicScoreManagerService.ImportZip(path)),
				"zip");
			return;
#else
			SetStatus("当前平台暂不支持运行时导入，请手动复制谱面。");
			return;
#endif
        }

        private void ApplyImportedEntry(CustomMusicScoreEntry entry)
		{
			if (entry != null)
			{
				_selected = new CustomMusicScoreManagerItem(entry, DateTime.Now, true, File.Exists(entry.ScorePath), File.Exists(entry.AudioPath), File.Exists(entry.JacketPath));
				RefreshList();
				SetStatus("已导入谱面。");
				ShowSuccessDialog("已导入谱面。");
			}
			else
			{
				SetStatus("导入已取消或失败。");
			}
		}

		private void ReplaceSelectedAudio()
		{
			if (_selected?.Entry == null)
			{
				return;
			}

#if UNITY_EDITOR || UNITY_STANDALONE
			string path = PickStandaloneFile(
				"导入音频文件",
				string.Empty,
				new ExtensionFilter("音频文件", "ogg", "mp3", "wav"));
			if (string.IsNullOrEmpty(path))
			{
				SetStatus("已取消导入音频。");
				return;
			}

			ReplaceSelectedFile(path, CustomMusicScoreManagerService.ReplaceAudioFile, "已导入音频");
#elif UNITY_ANDROID || UNITY_IOS
			PickNativeFile(
				"导入音频文件",
				"已取消导入音频。",
				path => ReplaceSelectedFile(path, CustomMusicScoreManagerService.ReplaceAudioFile, "已导入音频"),
				"ogg",
				"mp3",
				"wav");
#else
			SetStatus("当前平台暂不支持导入音频文件，请手动复制。");
#endif
		}

		private void ReplaceSelectedJacket()
		{
			if (_selected?.Entry == null)
			{
				return;
			}

#if UNITY_EDITOR || UNITY_STANDALONE
			string path = PickStandaloneFile(
				"导入封面文件",
				string.Empty,
				new ExtensionFilter("图片文件", "png", "jpg", "jpeg"));
			if (string.IsNullOrEmpty(path))
			{
				SetStatus("已取消导入封面。");
				return;
			}

			ReplaceSelectedFile(path, CustomMusicScoreManagerService.ReplaceJacketFile, "已导入封面");
#elif UNITY_ANDROID || UNITY_IOS
			PickNativeFile(
				"导入封面文件",
				"已取消导入封面。",
				path => ReplaceSelectedFile(path, CustomMusicScoreManagerService.ReplaceJacketFile, "已导入封面"),
				"png",
				"jpg",
				"jpeg");
#else
			SetStatus("当前平台暂不支持导入封面文件，请手动复制。");
#endif
		}

		private void ReplaceSelectedScore()
		{
			if (_selected?.Entry == null)
			{
				return;
			}

#if UNITY_EDITOR || UNITY_STANDALONE
			string path = PickStandaloneFile(
				"导入谱面文件",
				string.Empty,
				new ExtensionFilter("谱面文件", "json", "txt", "sus"));
			if (string.IsNullOrEmpty(path))
			{
				SetStatus("已取消导入谱面。");
				return;
			}

			ReplaceSelectedFile(path, CustomMusicScoreManagerService.ReplaceScoreFile, "已导入谱面");
#elif UNITY_ANDROID || UNITY_IOS
			PickNativeFile(
				"导入谱面文件",
				"已取消导入谱面。",
				path => ReplaceSelectedFile(path, CustomMusicScoreManagerService.ReplaceScoreFile, "已导入谱面"),
				"json",
				"txt",
				"sus");
#else
			SetStatus("当前平台暂不支持导入谱面文件，请手动复制。");
#endif
		}

		private void ReplaceSelectedVideo()
		{
			if (_selected?.Entry == null)
			{
				return;
			}

#if UNITY_EDITOR || UNITY_STANDALONE
			string path = PickStandaloneFile(
				"导入2DMV视频",
				string.Empty,
				new ExtensionFilter("MP4视频", "mp4"));
			if (string.IsNullOrEmpty(path))
			{
				SetStatus("已取消导入2DMV。");
				return;
			}

			ReplaceSelectedFile(path, CustomMusicScoreManagerService.ReplaceVideoFile, "已导入2DMV");
#elif UNITY_ANDROID || UNITY_IOS
			PickNativeFile(
				"导入2DMV视频",
				"已取消导入2DMV。",
				path => ReplaceSelectedFile(path, CustomMusicScoreManagerService.ReplaceVideoFile, "已导入2DMV"),
				"mp4");
#else
			SetStatus("当前平台暂不支持导入2DMV视频，请手动复制。");
#endif
		}

#if UNITY_EDITOR || UNITY_STANDALONE
		private static string PickStandaloneFile(string title, string directory, params ExtensionFilter[] filters)
		{
			string[] paths = StandaloneFileBrowser.OpenFilePanel(title, directory ?? string.Empty, filters, false);
			return paths != null && paths.Length > 0 ? paths[0] : string.Empty;
		}

		private static string PickStandaloneFolder(string title, string directory)
		{
			string[] paths = StandaloneFileBrowser.OpenFolderPanel(title, directory ?? string.Empty, false);
			return paths != null && paths.Length > 0 ? paths[0] : string.Empty;
		}

		private static string SaveStandaloneFile(string title, string directory, string defaultName, string extension)
		{
			return StandaloneFileBrowser.SaveFilePanel(title, directory ?? string.Empty, defaultName ?? string.Empty, extension);
		}
#endif

#if UNITY_ANDROID
		private void ExportSelectedNative()
		{
			_lastExportPath = CustomMusicScoreManagerService.ExportZip(_selected.Entry);
			if (string.IsNullOrEmpty(_lastExportPath))
			{
				SetStatus("导出失败。");
				ShowExportFailedDialog();
				return;
			}

			SetStatus("请选择导出位置...");
			using (AndroidJavaClass helper = new AndroidJavaClass("com.opensekai.ShareExportHelper"))
			{
				helper.CallStatic("SaveAndShare", _lastExportPath, gameObject.name, "OnSaveAndShareComplete");
			}
		}

		private void OnSaveAndShareComplete(string result)
		{
			string path = _lastExportPath;
			if (string.IsNullOrEmpty(_selected?.Entry?.Manifest?.scoreTitle))
			{
				return;
			}

			string title = _selected.Entry.Manifest.scoreTitle;
			if (result.StartsWith("success"))
			{
				SetStatus("已导出到：" + title);
				if (!string.IsNullOrEmpty(path))
				{
					AskShareAfterExport(path);
				}
			}
			else if (result == "cancelled")
			{
				SetStatus("已取消导出");
			}
			else
			{
				SetStatus("导出失败：" + result);
			}
		}
#elif UNITY_IOS
		private void ExportSelectedNative()
		{
			if (NativeFilePicker.IsFilePickerBusy())
			{
				SetStatus("文件选择器已经打开。");
				return;
			}

			string path = CustomMusicScoreManagerService.ExportZip(_selected.Entry);
			if (string.IsNullOrEmpty(path))
			{
				SetStatus("导出失败。");
				ShowExportFailedDialog();
				return;
			}

			if (!NativeFilePicker.CanExportFiles())
			{
				SetStatus("当前平台不支持文件导出，已导出到：" + path);
				AskShareAfterExport(path);
				return;
			}

			SetStatus("请选择导出位置...");
			NativeFilePicker.ExportFile(path, success =>
			{
				if (this == null)
				{
					return;
				}

				if (success)
				{
					SetStatus("已导出：" + path);
					AskShareAfterExport(path);
				}
				else
				{
					SetStatus("导出已取消或失败。");
				}
			});
		}
#endif

		private void PickNativeFile(string title, string cancelStatus, Action<string> onPicked, params string[] extensions)
		{
			if (NativeFilePicker.IsFilePickerBusy())
			{
				SetStatus("文件选择器已经打开。");
				return;
			}

			SetStatus(title + "...");
			NativeFilePicker.PickFile(path =>
			{
				if (this == null)
				{
					return;
				}

				if (string.IsNullOrEmpty(path))
				{
					SetStatus(cancelStatus);
					return;
				}

				onPicked?.Invoke(path);
			}, CreateNativeFileTypes(extensions));
		}

		private static string[] CreateNativeFileTypes(params string[] extensions)
		{
#if UNITY_ANDROID
			// Android SAF filters by MIME rather than extension, and custom extensions
			// such as .sus are often greyed out. Let the picker show all files and
			// validate the selected extension in the import/replace service instead.
			return Array.Empty<string>();
#else
			if (extensions == null || extensions.Length == 0)
			{
				return Array.Empty<string>();
			}

			List<string> fileTypes = new List<string>();
			for (int i = 0; i < extensions.Length; i++)
			{
				string extension = extensions[i];
				if (string.IsNullOrWhiteSpace(extension))
				{
					continue;
				}

				extension = extension.Trim().TrimStart('.');
				AddNativeFileTypesForExtension(fileTypes, extension);
			}

			return fileTypes.ToArray();
#endif
		}

#if !UNITY_ANDROID
		private static void AddNativeFileTypesForExtension(List<string> fileTypes, string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return;
			}

			if (extension.IndexOf("/", StringComparison.Ordinal) >= 0)
			{
				AddNativeFileType(fileTypes, extension);
				return;
			}

			string fileType = NativeFilePicker.ConvertExtensionToFileType(extension);
			AddNativeFileType(fileTypes, fileType);
		}

		private static void AddNativeFileType(List<string> fileTypes, string fileType)
		{
			if (!string.IsNullOrEmpty(fileType) && !fileTypes.Contains(fileType))
			{
				fileTypes.Add(fileType);
			}
		}
#endif

		private void ReplaceSelectedFile(
			string sourcePath,
			Func<CustomMusicScoreEntry, string, CustomMusicScoreEntry> replaceFile,
			string successStatus)
		{
			try
			{
				CustomMusicScoreEntry entry = replaceFile(_selected.Entry, sourcePath);
				if (entry == null)
				{
					SetStatus("导入失败。");
					return;
				}

				_selected = new CustomMusicScoreManagerItem(
					entry,
					DateTime.Now,
					File.Exists(entry.ManifestPath),
					File.Exists(entry.ScorePath),
					File.Exists(entry.AudioPath),
					File.Exists(entry.JacketPath));
				RefreshList();
				SetStatus(successStatus + ": " + Path.GetFileName(sourcePath));
			}
			catch (Exception ex)
			{
				SetStatus(ex.Message);
			}
		}

		private void SaveSelectedManifest()
		{
			CustomMusicScoreEntry savedEntry = SaveSelectedManifestFromForm(refreshList: true);
			string statusMessage = savedEntry != null ? "配置已保存。" : "保存配置失败。";
			SetStatus(statusMessage);
			if (savedEntry != null)
			{
				ShowSuccessDialog("配置已保存。");
			}
		}

		private void OnCalculateDurationClicked()
		{
			ScreenManager.Instance?.Show2ButtonDialog<Common2ButtonDialog>(
				DialogType.Common2ButtonDialog,
				null,
				"WORD_DECIDE",
				"WORD_CANCEL",
				() => CalculateDurationAsync().Forget(),
				null,
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				allowCloseExternal: true)?.SetMessageBodyText("是否一键设置谱面时长秒？");
		}

		private async UniTaskVoid CalculateDurationAsync()
		{
			if (_selected?.Entry == null)
			{
				ShowSuccessDialog("无法完成自动计算，请检查谱面数据");
				return;
			}

			CustomMusicScoreEntry entry = _selected.Entry;
			if (!File.Exists(entry.AudioPath))
			{
				ShowSuccessDialog("无法完成自动计算，请检查谱面数据");
				return;
			}

			SetStatus("正在加载音频...");
			bool audioReady = await entry.RegisterAudioAsync(this.GetCancellationTokenOnDestroy());
			if (!audioReady || entry.AudioLengthMs <= 0)
			{
				ShowSuccessDialog("无法完成自动计算，请检查谱面数据");
				return;
			}

			float audioSeconds = entry.AudioLengthMs / 1000f;
			float fillerSec = entry.Manifest.fillerSec;
			int calculatedDuration = Mathf.CeilToInt(audioSeconds - fillerSec + 2f);
			calculatedDuration = Mathf.Max(1, calculatedDuration);

			entry.Manifest.secForMusicScoreMaker = calculatedDuration;
			CustomMusicScoreEntry savedEntry = CustomMusicScoreManagerService.SaveManifest(entry, entry.Manifest);
			if (savedEntry != null)
			{
				_selected = new CustomMusicScoreManagerItem(
					savedEntry,
					DateTime.Now,
					File.Exists(savedEntry.ManifestPath),
					File.Exists(savedEntry.ScorePath),
					File.Exists(savedEntry.AudioPath),
					File.Exists(savedEntry.JacketPath));
				LoadForm(savedEntry.Manifest);
				RefreshList();
				SetStatus("时长已自动计算。");
				ShowSuccessDialog($"已自动计算，时长为：{calculatedDuration}秒");
			}
			else
			{
				ShowSuccessDialog("无法完成自动计算，请检查谱面数据");
			}
		}

		private void OnGenerateVideoClicked()
	{
		Debug.Log("[UI] OnGenerateVideoClicked called");
		ScreenManager.Instance?.Show2ButtonDialog<Common2ButtonDialog>(
			DialogType.Common2ButtonDialog,
			null,  // messageBodyKey (通过SetMessageBodyText设置)
			"WORD_DECIDE",  // okButtonLabelKey
			"WORD_CANCEL",  // cancelButtonLabelKey
			() => StartVideoGeneration(),  // onClickOK
			null,  // onClickCancel
			DisplayLayerType.Layer_Dialog,
			DialogSize.Manual,
			true)?.SetMessageBodyText("是否开始生成视频？\n期间不可暂停");
	}

	private void StartVideoGeneration()
	{
		Debug.Log("[UI] StartVideoGeneration called");

#if UNITY_ANDROID
		// Android平台：需要进行权限检查
		Debug.Log("[UI] Android platform detected, checking permissions");
		CheckVideoGenerationPermissions();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR
		// Windows平台或编辑器：直接进入创建启动数据流程
		Debug.Log("[UI] Windows/Editor platform detected, proceeding with video generation");
		CreateVideoGenerationStartupData();
#else
		// 其他平台暂不支持
		Debug.LogWarning("[UI] Video generation not supported on this platform");
		SetStatus("当前平台不支持视频生成功能");
#endif
	}

	private void CheckVideoGenerationPermissions()
	{
		Debug.Log("[UI] CheckVideoGenerationPermissions: Checking gallery permission");

		// 检查是否已有相册权限
		if (PermissionHelper.HasGalleryPermission())
		{
			Debug.Log("[UI] Gallery permission already granted, proceeding with video generation");
			CreateVideoGenerationStartupData();
			return;
		}

		// 申请相册权限
		Debug.Log("[UI] Gallery permission not granted, requesting permission");
		PermissionHelper.RequestGalleryPermission((granted) =>
		{
			if (granted)
			{
				// 权限申请成功，继续视频生成流程
				Debug.Log("[UI] Gallery permission granted, proceeding with video generation");
				CreateVideoGenerationStartupData();
			}
			else
			{
				// 权限申请失败，显示提示并返回首页
				Debug.LogWarning("[UI] Gallery permission denied");
				ShowPermissionDeniedDialog();
			}
		});
	}

	/// <summary>
	/// 显示权限被拒绝的对话框，然后返回首页
	/// </summary>
	private void ShowPermissionDeniedDialog()
	{
		ScreenManager.Instance?.Show1ButtonDialog<Common1ButtonDialog>(
			DialogType.Common1ButtonDialog,
			null,  // messageBodyKey (通过SetMessageBodyText设置)
			"WORD_OK",  // okButtonLabelKey
			() =>
			{
				// 返回首页
				Debug.Log("[UI] Permission denied, returning to home");
				ReturnToHome();
			},
			DisplayLayerType.Layer_Dialog,
			DialogSize.Manual,
			true)?.SetMessageBodyText("无法获取相册权限\n请在系统设置中授予应用相册访问权限");
	}

	/// <summary>
	/// 返回首页
	/// </summary>
	private void ReturnToHome()
	{
		// 返回到谱面管理首页
		try
		{
			MusicScoreMaker.Ingame.Utilities.MusicScoreMakerUtility.RequestTransitionToOutGame(MenuScreenType.MusicScoreMakerTop);
		}
		catch (Exception ex)
		{
			Debug.LogError($"[UI] ReturnToHome异常: {ex.Message}");
			// 尝试使用ScreenManager直接切换
			if (ScreenManager.Instance != null)
			{
				ScreenManager.Instance.ChangeUIScreen(MenuScreenType.MusicScoreMakerTop, false, true);
			}
		}
	}

	private void CreateVideoGenerationStartupData()
	{
		CreateVideoGenerationStartupDataAsync().Forget();
	}

	private async UniTask CreateVideoGenerationStartupDataAsync()
	{
		if (_selected?.Entry == null)
		{
			SetStatus("请选择一个谱面。");
			return;
		}

		CustomMusicScoreEntry entry = SaveSelectedManifestFromForm(refreshList: false);
		entry ??= CustomMusicScoreStorage.LoadEntry(_selected.Entry.RootDirectory);
		if (entry == null)
		{
			SetStatus("无法加载谱面。");
			RefreshList();
			return;
		}

		if (!File.Exists(entry.ScorePath))
		{
			SetStatus("找不到谱面文件。");
			return;
		}
		if (!File.Exists(entry.AudioPath))
		{
			SetStatus("找不到音频文件。");
			return;
		}

		MusicScoreMakerData scoreData = entry.LoadScore();
		if (scoreData == null)
		{
			SetStatus("无法加载谱面文件。");
			return;
		}
		if (!HasPlayableNotes(scoreData))
		{
			SetStatus("没有可游玩的音符。");
			return;
		}

		SetStatus("正在加载音频...");
		bool audioReady = await entry.RegisterAudioAsync(this.GetCancellationTokenOnDestroy());
		if (!audioReady)
		{
			SetStatus("无法加载音频文件。");
			return;
		}

		VideoGenerationBootData bootData = CreateVideoGenerationBootData(entry, scoreData);
		if (bootData == null)
		{
			SetStatus("无法创建视频生成启动数据。");
			return;
		}

		UserDataManager.Instance.FreeLiveBootData = bootData;
		Sekai.Core.EntryPoint.PlayMode = Sekai.Core.PlayMode.SoloLive;
		LiveTransitioner.SafeForceFinish(null);
		ScreenManager.Instance?.PushUIScreen(MenuScreenType.LiveLoading, false);
		SetStatus("正在启动视频生成模式...");
	}

	private VideoGenerationBootData CreateVideoGenerationBootData(CustomMusicScoreEntry entry, MusicScoreMakerData scoreData)
	{
		if (entry == null || scoreData == null)
		{
			return null;
		}

		LiveBundleBuildData liveBundleBuildData = Resources.Load<LiveBundleBuildData>(LiveConfig.ConfigBundleNamePath);
		MusicScore musicScore = scoreData.ToMusicScore(liveBundleBuildData);
		int deckId = UserDataManager.Instance.SelectedDeckId;
		MasterMusicDifficulty difficulty = CreateDirectPlayDifficulty(entry, musicScore);
		string difficultyString = difficulty?.musicDifficulty ?? "master";
		LiveSettingData liveSettingData = LiveSettingData.LoadFromStorage();
		MusicCategory musicCategory = ResolveDirectPlayMusicCategory(liveSettingData);

		// 创建VideoGenerationBootData实例
		VideoGenerationBootData bootData = new VideoGenerationBootData(
			entry.MusicId,
			difficultyString,
			0,
			deckId,
			LivePlayMode.Free,
			LiveMusicData.CollaborationModeState.Off,
			musicCategory);

		bootData.LiveEventData = new LiveEventData(Array.Empty<IngameLotterySkill>(), Array.Empty<IngameComboCutin>(), deckId, true);
		bootData.LiveSettingData = liveSettingData;
		bootData.MVQualityType = bootData.LiveSettingData?.QualityType ?? Sekai.MVQualityType.Default;
		bootData.MusicCategory = musicCategory;
		bootData.IsAuto = true;
		bootData.IsCustomMusicScore = true;
		bootData.IsOfficialMusicScore = false;
		bootData.ReturnScreenType = MenuScreenType.MusicScoreMakerTop;
		bootData.canSkipDisplayMusicInfo = false;
		bootData.ReleaseTransitionBeforeMusicStart = true;
		bootData.CustomMusicScoreId = entry.Manifest.id;
		bootData.CustomMusicScorePath = entry.RootDirectory;
		bootData.CustomMusicScoreTitle = entry.Manifest.scoreTitle;
		bootData.CustomMusicScoreAuthorName = entry.Manifest.userName;
		bootData.CustomMusicScoreCollaborationLabel = entry.Manifest.collaborationLabel;

		// 设置视频生成专用属性
		bootData.IsVideoGenerationMode = true;
		bootData.VideoGenerationSpeedMultiplier = 1; // 正常速度录制
		bootData.VideoGenerationMuteAudio = false; // 不静音BGM，录制完整游戏原声（含谱面音乐）
		bootData.VideoGenerationDisablePause = true;
		bootData.VideoGenerationAudioPath = null; // 不使用后期添加音乐，直接使用录制的音频

		if (bootData.MusicData != null)
		{
			bootData.MusicData.Music = CreateDirectPlayMusic(entry);
			bootData.MusicData.Difficulty = difficulty;
			bootData.MusicData.Vocal = CreateDirectPlayVocal(entry);
			bootData.MusicData.Score = new MasterPlayLevelScore
			{
				liveType = LiveType.solo.ToString(),
				playLevel = entry.Manifest.playLevel
			};
			bootData.MusicData.IsTestPlay = false;
			bootData.MusicData.IsUseCustomScore = true;
			bootData.MusicData.CustomPlayLevel = entry.Manifest.playLevel;
			bootData.MusicData.MusicScore = musicScore;
			bootData.MusicData.StartMusicTimeMs = 0L;
			// Skip MusicInfo display if skip mode is enabled
			bootData.MusicData.PlayStartEffectEnabled = !(liveSettingData?.SkipsCustomMusicScoreMusicInfo ?? false);
		}

		return bootData;
	}

		private CustomMusicScoreEntry SaveSelectedManifestFromForm(bool refreshList)
		{
			if (_selected?.Entry == null)
			{
				return null;
			}

			CustomMusicScoreManifest manifest = _selected.Entry.Manifest;
			manifest.title = _titleInput.text;
			manifest.scoreTitle = _scoreTitleInput.text;
			manifest.userName = _userInput.text;
			manifest.composer = _composerInput.text;
			manifest.lyricist = _lyricistInput.text;
			manifest.arranger = _arrangerInput.text;
			manifest.singer = _singerInput.text;
			manifest.collaborationLabel = _collaborationLabelInput.text;
			manifest.description = _descriptionInput.text;
			manifest.musicDifficultyType = GetSelectedDifficultyType();
			manifest.audioFileName = Path.GetFileName(_audioInput.text);
			manifest.jacketFileName = Path.GetFileName(_jacketInput.text);
			manifest.scoreFileName = Path.GetFileName(_scoreInput.text);
			manifest.videoFileName = Path.GetFileName(_videoInput.text);
			if (int.TryParse(_levelInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
			{
				manifest.playLevel = level;
			}
			if (int.TryParse(_durationInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int duration))
			{
				manifest.secForMusicScoreMaker = duration;
			}
			if (float.TryParse(_fillerInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float filler))
			{
				manifest.fillerSec = filler;
			}

			CustomMusicScoreEntry savedEntry = CustomMusicScoreManagerService.SaveManifest(_selected.Entry, manifest);
			if (savedEntry != null)
			{
				_selected = new CustomMusicScoreManagerItem(
					savedEntry,
					DateTime.Now,
					File.Exists(savedEntry.ManifestPath),
					File.Exists(savedEntry.ScorePath),
					File.Exists(savedEntry.AudioPath),
					File.Exists(savedEntry.JacketPath));
			}
			if (refreshList)
			{
				RefreshList();
			}
			return savedEntry;
		}

		private void LoadForm(CustomMusicScoreManifest manifest)
		{
			_titleInput.SetTextWithoutNotify(manifest.title);
			_scoreTitleInput.SetTextWithoutNotify(manifest.scoreTitle);
			_userInput.SetTextWithoutNotify(manifest.userName);
			_composerInput.SetTextWithoutNotify(manifest.composer);
			_lyricistInput.SetTextWithoutNotify(manifest.lyricist);
			_arrangerInput.SetTextWithoutNotify(manifest.arranger);
			_singerInput.SetTextWithoutNotify(manifest.singer);
			_collaborationLabelInput.SetTextWithoutNotify(manifest.collaborationLabel);
			_descriptionInput.SetTextWithoutNotify(manifest.description);
			SetDifficultyDropdownValue(manifest.musicDifficultyType);
			_levelInput.SetTextWithoutNotify(manifest.playLevel.ToString(CultureInfo.InvariantCulture));
			_durationInput.SetTextWithoutNotify(manifest.secForMusicScoreMaker.ToString(CultureInfo.InvariantCulture));
			_fillerInput.SetTextWithoutNotify(manifest.fillerSec.ToString(CultureInfo.InvariantCulture));
			_audioInput.SetTextWithoutNotify(manifest.audioFileName);
			_jacketInput.SetTextWithoutNotify(manifest.jacketFileName);
			_scoreInput.SetTextWithoutNotify(manifest.scoreFileName);
			_videoInput.SetTextWithoutNotify(manifest.videoFileName);
		}

		private void ClearForm()
		{
			TMP_InputField[] inputs =
			{
				_titleInput, _scoreTitleInput, _userInput, _composerInput, _lyricistInput, _arrangerInput, _singerInput, _collaborationLabelInput, _descriptionInput, _levelInput,
				_durationInput, _fillerInput, _audioInput, _jacketInput, _scoreInput, _videoInput
			};
			foreach (TMP_InputField input in inputs)
			{
				input.SetTextWithoutNotify(string.Empty);
			}
			SetDifficultyDropdownValue("master");
		}

		private void SetFormInteractable(bool interactable)
		{
			TMP_InputField[] inputs =
			{
				_titleInput, _scoreTitleInput, _userInput, _composerInput, _lyricistInput, _arrangerInput, _singerInput, _collaborationLabelInput, _descriptionInput, _levelInput,
				_durationInput, _fillerInput, _audioInput, _jacketInput, _scoreInput, _videoInput
			};
			foreach (TMP_InputField input in inputs)
			{
				input.interactable = interactable;
			}
			_difficultyButton.interactable = interactable;
		}

		private string GetSelectedDifficultyType()
		{
			int index = _difficultyIndex;
			if (index < 0 || index >= DifficultyTypes.Length)
			{
				return "master";
			}
			return DifficultyTypes[index];
		}

		private void SetDifficultyDropdownValue(string difficultyType)
		{
			int index = GetDifficultyIndex(difficultyType);
			_difficultyIndex = index;
			if (_difficultyLabel != null)
			{
				_difficultyLabel.text = DifficultyDisplayNames[index];
			}
		}

		private static int GetDifficultyIndex(string difficultyType)
		{
			for (int i = 0; i < DifficultyTypes.Length; i++)
			{
				if (string.Equals(DifficultyTypes[i], difficultyType, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}
			return 4;
		}

		private void LoadJacket(string path)
		{
			ClearLoadedJacket();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				_jacketImage.sprite = null;
				_jacketImage.color = new Color32(42, 49, 58, 255);
				return;
			}

			byte[] bytes = File.ReadAllBytes(path);
			Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (!texture.LoadImage(bytes))
			{
				Destroy(texture);
				return;
			}

			_jacketSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
			_jacketImage.sprite = _jacketSprite;
			_jacketImage.color = Color.white;
		}

		private void ClearLoadedJacket()
		{
			if (_jacketSprite == null)
			{
				return;
			}

			Texture texture = _jacketSprite.texture;
			Destroy(_jacketSprite);
			if (texture != null)
			{
				Destroy(texture);
			}
			_jacketSprite = null;
			if (_jacketImage != null)
			{
				_jacketImage.sprite = null;
			}
		}

		private void SetStatus(string message)
		{
			if (_statusText != null)
			{
				_statusText.text = message ?? string.Empty;
			}
		}

		private void ShowSuccessDialog(string message)
		{
			Common1ButtonDialog dialog = ScreenManager.Instance?.Show1ButtonDialog<Common1ButtonDialog>(
				DialogType.Common1ButtonDialog,
				null,
				"WORD_DECIDE",
				null,
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				true);
			dialog?.SetMessageBodyText(message);
		}

		private static TMP_InputField CreateInputField(Transform parent, string label, string placeholder)
		{
			Button actionButton;
			return CreateInputField(parent, label, placeholder, null, out actionButton);
		}

		private Button CreateDifficultySelector(Transform parent)
		{
			GameObject root = new GameObject("DifficultyInput", typeof(RectTransform));
			root.transform.SetParent(parent, false);
			LayoutElement layoutElement = root.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 116f;
			layoutElement.minHeight = 116f;

			VerticalLayoutGroup vertical = root.AddComponent<VerticalLayoutGroup>();
			vertical.spacing = 12f;
			vertical.childControlWidth = true;
			vertical.childControlHeight = false;

			TextMeshProUGUI labelText = CreateText("Label", root.transform, "难度", 23, FontStyles.Bold, TextAlignmentOptions.Left);
			labelText.rectTransform.sizeDelta = new Vector2(0f, 30f);

			GameObject fieldObject = new GameObject("Field", typeof(RectTransform), typeof(Image), typeof(Button));
			fieldObject.transform.SetParent(root.transform, false);
			_difficultyFieldRect = fieldObject.GetComponent<RectTransform>();
			_difficultyFieldRect.sizeDelta = new Vector2(0f, 70f);
			Image fieldImage = fieldObject.GetComponent<Image>();
			fieldImage.color = new Color32(22, 26, 32, 255);

			Button button = fieldObject.GetComponent<Button>();
			button.targetGraphic = fieldImage;
			button.transition = Selectable.Transition.ColorTint;
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color32(220, 238, 255, 255);
			colors.pressedColor = new Color32(180, 213, 242, 255);
			colors.disabledColor = new Color32(120, 126, 132, 120);
			button.colors = colors;
			button.onClick.AddListener(CycleDifficulty);

			_difficultyLabel = CreateText("Label", _difficultyFieldRect, string.Empty, 26, FontStyles.Normal, TextAlignmentOptions.Left);
			_difficultyLabel.raycastTarget = false;
			SetStretchOffsets(_difficultyLabel.rectTransform, 18f, 0f, 124f, 0f);

			TextMeshProUGUI hint = CreateText("Hint", _difficultyFieldRect, "切换", 19, FontStyles.Bold, TextAlignmentOptions.Center);
			hint.raycastTarget = false;
			SetAnchor(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(104f, 0f));
			SetDifficultyDropdownValue("master");
			return button;
		}

		private void CycleDifficulty()
		{
			_difficultyIndex = (_difficultyIndex + 1) % DifficultyTypes.Length;
			SetDifficultyDropdownValue(DifficultyTypes[_difficultyIndex]);
		}

		private static TMP_InputField CreateInputField(
			Transform parent,
			string label,
			string placeholder,
			UnityEngine.Events.UnityAction onClick,
			out Button actionButton)
		{
			actionButton = null;
			GameObject root = new GameObject(label + "Input", typeof(RectTransform));
			root.transform.SetParent(parent, false);
			LayoutElement layoutElement = root.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 116f;
			layoutElement.minHeight = 116f;
			VerticalLayoutGroup vertical = root.AddComponent<VerticalLayoutGroup>();
			vertical.spacing = 12f;
			vertical.childControlWidth = true;
			vertical.childControlHeight = false;

			TextMeshProUGUI labelText = CreateText("Label", root.transform, label, 23, FontStyles.Bold, TextAlignmentOptions.Left);
			labelText.rectTransform.sizeDelta = new Vector2(0f, 30f);

			GameObject fieldObject = new GameObject("Field", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
			fieldObject.transform.SetParent(root.transform, false);
			RectTransform fieldRect = fieldObject.GetComponent<RectTransform>();
			fieldRect.sizeDelta = new Vector2(0f, 70f);
			fieldObject.GetComponent<Image>().color = new Color32(22, 26, 32, 255);

			RectTransform textArea = CreateRect("Text Area", fieldRect);
			textArea.gameObject.AddComponent<RectMask2D>();
			SetStretchOffsets(textArea, 14f, 7f, onClick == null ? 14f : 126f, 7f);

			TextMeshProUGUI text = CreateText("Text", textArea, string.Empty, 26, FontStyles.Normal, TextAlignmentOptions.Left);
			SetStretchOffsets(text.rectTransform, 4f, 0f, 4f, 0f);
			text.raycastTarget = false;

			TextMeshProUGUI placeholderText = CreateText("Placeholder", textArea, placeholder, 26, FontStyles.Normal, TextAlignmentOptions.Left);
			SetStretchOffsets(placeholderText.rectTransform, 4f, 0f, 4f, 0f);
			placeholderText.color = new Color32(136, 146, 158, 180);
			placeholderText.raycastTarget = false;

			if (onClick != null)
			{
				GameObject actionObject = new GameObject("SelectButton", typeof(RectTransform), typeof(Image), typeof(Button));
				actionObject.transform.SetParent(fieldRect, false);
				RectTransform actionRect = actionObject.GetComponent<RectTransform>();
				SetAnchor(actionRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-7f, 0f), new Vector2(110f, -14f));

				Image actionImage = actionObject.GetComponent<Image>();
				actionImage.color = new Color32(62, 78, 92, 255);
				actionButton = actionObject.GetComponent<Button>();
				actionButton.targetGraphic = actionImage;
				actionButton.transition = Selectable.Transition.ColorTint;
				ColorBlock colors = actionButton.colors;
				colors.normalColor = Color.white;
				colors.highlightedColor = new Color32(220, 238, 255, 255);
				colors.pressedColor = new Color32(180, 213, 242, 255);
				colors.disabledColor = new Color32(120, 126, 132, 120);
				actionButton.colors = colors;
				actionButton.onClick.AddListener(onClick);

				TextMeshProUGUI actionLabel = CreateText("Label", actionRect, "导入", 20, FontStyles.Bold, TextAlignmentOptions.Center);
				actionLabel.enableWordWrapping = false;
				actionLabel.overflowMode = TextOverflowModes.Ellipsis;
				Stretch(actionLabel.rectTransform);
			}

			TMP_InputField input = fieldObject.GetComponent<TMP_InputField>();
			input.textViewport = textArea;
			input.textComponent = text;
			input.placeholder = placeholderText;
			input.targetGraphic = fieldObject.GetComponent<Image>();
			return input;
		}

		private static Button CreateButtonField(Transform parent, string label, string buttonText, UnityEngine.Events.UnityAction onClick)
		{
			GameObject root = new GameObject(label + "ButtonField", typeof(RectTransform));
			root.transform.SetParent(parent, false);
			LayoutElement layoutElement = root.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 116f;
			layoutElement.minHeight = 116f;
			VerticalLayoutGroup vertical = root.AddComponent<VerticalLayoutGroup>();
			vertical.spacing = 12f;
			vertical.childControlWidth = true;
			vertical.childControlHeight = false;

			TextMeshProUGUI labelText = CreateText("Label", root.transform, label, 23, FontStyles.Bold, TextAlignmentOptions.Left);
			labelText.rectTransform.sizeDelta = new Vector2(0f, 30f);

			GameObject buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
			buttonObject.transform.SetParent(root.transform, false);
			RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
			buttonRect.sizeDelta = new Vector2(0f, 70f);

			Image image = buttonObject.GetComponent<Image>();
			image.color = new Color32(62, 78, 92, 255);
			Button button = buttonObject.GetComponent<Button>();
			button.targetGraphic = image;
			button.transition = Selectable.Transition.ColorTint;
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color32(220, 238, 255, 255);
			colors.pressedColor = new Color32(180, 213, 242, 255);
			colors.disabledColor = new Color32(120, 126, 132, 120);
			button.colors = colors;
			button.onClick.AddListener(onClick);

			TextMeshProUGUI buttonLabel = CreateText("Label", buttonRect, buttonText, 26, FontStyles.Bold, TextAlignmentOptions.Center);
			buttonLabel.enableWordWrapping = false;
			buttonLabel.overflowMode = TextOverflowModes.Ellipsis;
			Stretch(buttonLabel.rectTransform);
			return button;
		}

		private static Button CreateFormButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
		{
			GameObject root = new GameObject(name + "Cell", typeof(RectTransform), typeof(LayoutElement));
			root.transform.SetParent(parent, false);
			LayoutElement layoutElement = root.GetComponent<LayoutElement>();
			layoutElement.preferredHeight = 86f;
			layoutElement.minHeight = 86f;

			GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
			buttonObject.transform.SetParent(root.transform, false);
			RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
			SetStretchOffsets(buttonRect, 0f, 10f, 0f, 10f);

			Image image = buttonObject.GetComponent<Image>();
			image.color = new Color32(54, 67, 80, 255);
			Button button = buttonObject.GetComponent<Button>();
			button.targetGraphic = image;
			button.transition = Selectable.Transition.ColorTint;
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color32(220, 238, 255, 255);
			colors.pressedColor = new Color32(180, 213, 242, 255);
			colors.disabledColor = new Color32(120, 126, 132, 120);
			button.colors = colors;
			button.onClick.AddListener(onClick);

			TextMeshProUGUI text = CreateText("Label", buttonRect, label, 20, FontStyles.Bold, TextAlignmentOptions.Center);
			text.enableWordWrapping = false;
			text.overflowMode = TextOverflowModes.Ellipsis;
			Stretch(text.rectTransform);
			return button;
		}

		private static Button CreateButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick, float width, float height, Color32? color = null)
		{
			GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
			root.transform.SetParent(parent, false);
			RectTransform rect = root.GetComponent<RectTransform>();
			rect.sizeDelta = new Vector2(width, height);
			LayoutElement layout = root.GetComponent<LayoutElement>();
			layout.preferredWidth = width;
			layout.preferredHeight = height;

			Image image = root.GetComponent<Image>();
			image.color = color ?? new Color32(54, 67, 80, 255);
			Button button = root.GetComponent<Button>();
			button.targetGraphic = image;
			button.transition = Selectable.Transition.ColorTint;
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color32(220, 238, 255, 255);
			colors.pressedColor = new Color32(180, 213, 242, 255);
			colors.disabledColor = new Color32(120, 126, 132, 120);
			button.colors = colors;
			button.onClick.AddListener(onClick);

			TextMeshProUGUI text = CreateText("Label", rect, label, 20, FontStyles.Bold, TextAlignmentOptions.Center);
			text.enableWordWrapping = false;
			text.overflowMode = TextOverflowModes.Ellipsis;
			Stretch(text.rectTransform);
			return button;
		}

		private static ScrollRect CreateHorizontalScrollRect(string name, Transform parent, out RectTransform content)
		{
			GameObject root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
			root.transform.SetParent(parent, false);
			ScrollRect scrollRect = root.GetComponent<ScrollRect>();
			scrollRect.horizontal = true;
			scrollRect.vertical = false;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;

			RectTransform viewport = CreateRect("Viewport", root.transform);
			viewport.gameObject.AddComponent<RectMask2D>();
			Stretch(viewport);

			content = CreateRect("Content", viewport);
			content.anchorMin = new Vector2(0f, 0f);
			content.anchorMax = new Vector2(0f, 1f);
			content.pivot = new Vector2(0f, 0.5f);
			content.anchoredPosition = Vector2.zero;
			content.sizeDelta = Vector2.zero;

			HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = 12f;
			layout.padding = new RectOffset(0, 0, 7, 7);
			layout.childControlWidth = false;
			layout.childControlHeight = false;
			layout.childAlignment = TextAnchor.MiddleLeft;

			ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
			fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			scrollRect.viewport = viewport;
			scrollRect.content = content;
			return scrollRect;
		}

		private static ScrollRect CreateScrollRect(string name, Transform parent, out RectTransform content)
		{
			GameObject root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
			root.transform.SetParent(parent, false);
			ScrollRect scrollRect = root.GetComponent<ScrollRect>();

			Image viewportImage = CreateImage("Viewport", root.transform, new Color32(20, 24, 30, 255));
			Mask mask = viewportImage.gameObject.AddComponent<Mask>();
			mask.showMaskGraphic = false;
			RectTransform viewport = viewportImage.rectTransform;
			Stretch(viewport);

			content = CreateRect("Content", viewport);
			content.anchorMin = new Vector2(0f, 1f);
			content.anchorMax = new Vector2(1f, 1f);
			content.pivot = new Vector2(0.5f, 1f);
			content.anchoredPosition = Vector2.zero;
			content.sizeDelta = Vector2.zero;
			VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
			layout.spacing = 12f;
			layout.padding = new RectOffset(10, 10, 10, 10);
			layout.childControlWidth = true;
			layout.childControlHeight = false;
			ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			scrollRect.viewport = viewport;
			scrollRect.content = content;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;
			return scrollRect;
		}

		private static ScrollRect CreateMaskedScrollRect(string name, Transform parent, out RectTransform content)
		{
			GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
			root.transform.SetParent(parent, false);
			root.GetComponent<Image>().color = new Color32(24, 29, 36, 255);
			ScrollRect scrollRect = root.GetComponent<ScrollRect>();

			RectTransform viewport = CreateRect("Viewport", root.transform);
			viewport.gameObject.AddComponent<RectMask2D>();
			Stretch(viewport);

			content = CreateRect("Content", viewport);
			content.anchorMin = new Vector2(0f, 1f);
			content.anchorMax = new Vector2(1f, 1f);
			content.pivot = new Vector2(0.5f, 1f);
			content.anchoredPosition = Vector2.zero;
			content.sizeDelta = new Vector2(0f, 430f);

			scrollRect.viewport = viewport;
			scrollRect.content = content;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;
			return scrollRect;
		}

		private static RectTransform CreatePanel(string name, Transform parent, Color32 color)
		{
			Image image = CreateImage(name, parent, color);
			return image.rectTransform;
		}

		private static Image CreateImage(string name, Transform parent, Color32 color)
		{
			GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
			go.transform.SetParent(parent, false);
			Image image = go.GetComponent<Image>();
			image.color = color;
			return image;
		}

		private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
		{
			GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
			go.transform.SetParent(parent, false);
			TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
			TMP_FontAsset fontAsset = GetOriginalFontAsset(style);
			if (fontAsset != null)
			{
				tmp.font = fontAsset;
			}
			tmp.text = text;
			tmp.fontSize = fontSize;
			tmp.fontStyle = style;
			tmp.alignment = alignment;
			tmp.color = new Color32(238, 243, 247, 255);
			tmp.enableWordWrapping = false;
			tmp.overflowMode = TextOverflowModes.Ellipsis;
			return tmp;
		}

		private static TMP_FontAsset GetOriginalFontAsset(FontStyles style)
		{
			SetupOriginalFontAssets();
			return (style & FontStyles.Bold) != 0 ? _baseFontEB : _baseFontDB;
		}

		private static void SetupOriginalFontAssets()
		{
			if (_fontAssetSetup)
			{
				return;
			}

			_baseFontEB = Resources.Load<TMP_FontAsset>(BaseFontEbPath);
			_baseFontDB = Resources.Load<TMP_FontAsset>(BaseFontDbPath);
			TMP_FontAsset dynamicFontEB = Resources.Load<TMP_FontAsset>(DynamicFontEbPath);
			TMP_FontAsset dynamicFontDB = Resources.Load<TMP_FontAsset>(DynamicFontDbPath);
			AddFallbackFontAsset(_baseFontEB, dynamicFontEB);
			AddFallbackFontAsset(_baseFontDB, dynamicFontDB);
			_fontAssetSetup = true;
		}

		private static void AddFallbackFontAsset(TMP_FontAsset fontAsset, TMP_FontAsset fallbackFontAsset)
		{
			if (fontAsset == null || fallbackFontAsset == null)
			{
				return;
			}

			if (fontAsset.fallbackFontAssetTable == null)
			{
				fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
			}

			if (!fontAsset.fallbackFontAssetTable.Contains(fallbackFontAsset))
			{
				fontAsset.fallbackFontAssetTable.Add(fallbackFontAsset);
			}
		}

		private static RectTransform CreateRect(string name, Transform parent)
		{
			GameObject go = new GameObject(name, typeof(RectTransform));
			go.transform.SetParent(parent, false);
			return go.GetComponent<RectTransform>();
		}

		private static void Stretch(RectTransform rect)
		{
			SetAnchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
		}

		private static void SetStretchOffsets(RectTransform rect, float left, float bottom, float right, float top)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.offsetMin = new Vector2(left, bottom);
			rect.offsetMax = new Vector2(-right, -top);
		}

		private static void SetStretchTop(RectTransform rect, float left, float top, float right, float height)
		{
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.offsetMin = new Vector2(left, -top - height);
			rect.offsetMax = new Vector2(-right, -top);
		}

		private static void SetStretchBottom(RectTransform rect, float left, float bottom, float right, float height)
		{
			rect.anchorMin = new Vector2(0f, 0f);
			rect.anchorMax = new Vector2(1f, 0f);
			rect.pivot = new Vector2(0.5f, 0f);
			rect.offsetMin = new Vector2(left, bottom);
			rect.offsetMax = new Vector2(-right, bottom + height);
		}

		private static void SetAnchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
		{
			rect.anchorMin = anchorMin;
			rect.anchorMax = anchorMax;
			rect.pivot = pivot;
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = sizeDelta;
		}

		private sealed class RowView
		{
			public GameObject Root { get; }

			public Image Background { get; }

			public CustomMusicScoreManagerItem Item { get; }

			public RowView(GameObject root, Image background, CustomMusicScoreManagerItem item)
			{
				Root = root;
				Background = background;
				Item = item;
			}
		}
	}
}
