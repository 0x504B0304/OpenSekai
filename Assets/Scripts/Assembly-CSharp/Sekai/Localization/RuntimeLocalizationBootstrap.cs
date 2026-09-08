using System.Collections.Generic;
using Sekai.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sekai.Localization
{
	public sealed class RuntimeLocalizationBootstrap : MonoBehaviour
	{
		private static readonly Dictionary<string, string> SourceKeys = new Dictionary<string, string>
		{
			["设置"] = "manager.settings",
			["刷新"] = "manager.refresh",
			["新建"] = "manager.new",
			["导入"] = "manager.import",
			["复制"] = "common.copy",
			["删除"] = "common.delete",
			["保存"] = "common.save",
			["取消"] = "common.cancel",
			["本地谱面"] = "manager.local_scores",
			["暂无本地谱面"] = "manager.no_scores",
			["请选择谱面"] = "manager.select_score",
			["最佳成绩"] = "manager.best_result",
			["暂无游玩记录"] = "manager.no_record",
			["编辑"] = "manager.edit",
			["游玩"] = "manager.play",
			["自动"] = "manager.auto",
			["导出ZIP"] = "manager.export_zip",
			["保存配置"] = "manager.save_config",
			["曲名"] = "manager.field.title",
			["谱面标题"] = "manager.field.score_title",
			["作者"] = "manager.field.author",
			["音频"] = "manager.field.audio",
			["封面"] = "manager.field.jacket",
			["谱面"] = "manager.field.score",
			["前置空白秒"] = "manager.field.filler",
			["编辑时长秒"] = "manager.field.duration",
			["难度"] = "manager.field.difficulty",
			["等级"] = "manager.field.level",
			["作曲"] = "manager.field.composer",
			["作词"] = "manager.field.lyricist",
			["编曲"] = "manager.field.arranger",
			["歌手"] = "manager.field.singer",
			["联动标签"] = "manager.field.collaboration",
			["描述"] = "manager.field.description",
			["语言"] = "settings.language",
			["音乐音量"] = "settings.bgm_volume",
			["音效音量"] = "settings.se_volume",
			["音符流速"] = "settings.note_speed",
			["判定偏移"] = "settings.timing_offset",
			["上隐挡板"] = "settings.lane_cover",
			["长条线不透明度"] = "settings.note_line_alpha",
			["Guide线不透明度"] = "settings.guide_line_alpha",
			["Note皮肤"] = "settings.note_skin",
			["Note音效"] = "settings.note_se",
			["击打特效"] = "settings.note_effect",
			["多押提示线"] = "settings.simultaneous_line",
			["MusicInfo显示"] = "settings.music_info",
			["Live背景"] = "settings.live_background",
			["判定偏差显示"] = "settings.fast_late",
			["桌面全屏"] = "settings.fullscreen",
			["关闭"] = "settings.off",
			["开启"] = "settings.on",
			["正常模式"] = "settings.normal_mode",
			["自制谱模式"] = "settings.custom_mode",
			["切换"] = "settings.toggle",
			["ミラー反転"] = "common.mirror",
			["上下反転"] = "common.flip_vertical",
			["つかいかた"] = "common.help",
			["投稿"] = "common.publish",
			["オプション"] = "common.options",
			["クリティカル"] = "common.critical",
			["ノーツ数確認"] = "editor.check_notes",
			["リセット"] = "common.reset",
			["一時保存"] = "common.draft_save",
			["チェンジ"] = "common.change",
			["コピー"] = "common.copy",
			["テストプレイ"] = "common.test_play",
			["削除"] = "common.delete",
			["保存"] = "common.save",
			["全体選択"] = "common.select_all",
			["範囲選択"] = "editor.range_select",
			["范围选择"] = "editor.range_select",
			["キャンセル"] = "common.cancel",
			["タブ"] = "common.tab",
			["ボタン"] = "common.button",
			["アイテム"] = "common.item",
			["テストプレイ開始"] = "testplay.start",
			["テストプレイを開始します。"] = "testplay.prompt",
			["表示位置からスタートする"] = "testplay.from_position",
			["オートプレイする"] = "testplay.auto",
			["編集画面に戻る"] = "testplay.return_editor",
			["テストプレイに戻る\r"] = "testplay.resume",
			["再生位置から\r"] = "testplay.from_playback",
			["一時停止しました。\nテストプレイに戻りますか？"] = "testplay.paused",
			["ライブを一時停止しました。\nライブに戻りますか？"] = "live.paused",
			["リタイア"] = "live.retire",
			["リトライ"] = "live.retry",
			["ライブに戻る"] = "live.resume",
			["不要なデータ削除中"] = "loading.removing",
			["データダウンロード中"] = "loading.downloading",
			["他のプレイヤーを待っています...(3/5)"] = "loading.waiting",
			["※通信環境の良いところで通信を行なってください。通信状況により時間がかかる場合があります。"] = "loading.network_notice",
			["指定したタイミングで楽曲の拍子を変更します。"] = "event.timesig_help",
			["1小節内のグリッドを4分音符を基準に設定できます。\n分割数を指定してください。"] = "editor.quantize_help",
			["4分"] = "editor.quarter_note",
			["グリッド"] = "editor.grid"
		};
		private static TMP_FontAsset _cjkFallback;
		private float _nextRuntimeScan;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Install()
		{
			Font sourceFont = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
			if (sourceFont != null)
			{
				_cjkFallback = TMP_FontAsset.CreateFontAsset(sourceFont);
				_cjkFallback.name = "NotoSansCJKsc Runtime Fallback";
				TMP_Settings.fallbackFontAssets ??= new List<TMP_FontAsset>();
				if (!TMP_Settings.fallbackFontAssets.Contains(_cjkFallback)) TMP_Settings.fallbackFontAssets.Add(_cjkFallback);
			}
			GameObject root = new GameObject(nameof(RuntimeLocalizationBootstrap));
			DontDestroyOnLoad(root);
			root.AddComponent<RuntimeLocalizationBootstrap>();
			_ = LocalizationManager.CurrentLanguage;
		}

		private void OnEnable()
		{
			UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
			LocalizationManager.LanguageChanged += Scan;
		}

		private void OnDisable()
		{
			UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
			LocalizationManager.LanguageChanged -= Scan;
		}

		private void Start() => Scan();
		private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Scan();

		private void Update()
		{
			if (Time.unscaledTime < _nextRuntimeScan) return;
			_nextRuntimeScan = Time.unscaledTime + 0.75f;
			Scan();
		}

		private void Scan() => RefreshAll();

		public static void RefreshAll()
		{
			CustomText[] wordingTexts = FindObjectsByType<CustomText>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (CustomText text in wordingTexts) text.UpdateWordingText();
			CustomTextMesh[] wordingMeshes = FindObjectsByType<CustomTextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (CustomTextMesh text in wordingMeshes) text.UpdateWordingText();
			TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (TMP_Text text in texts)
			{
				TryBind(text);
			}
			UnityEngine.UI.Text[] legacyTexts = FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (UnityEngine.UI.Text text in legacyTexts)
			{
				TryBind(text);
			}
		}

		public static bool TryBind(TMP_Text text)
		{
			if (text == null) return false;
			LocalizedTextBinding existing = text.GetComponent<LocalizedTextBinding>();
			if (existing != null)
			{
				existing.Refresh();
				return true;
			}
			if (!SourceKeys.TryGetValue(text.text, out string key)) return false;
			LocalizedTextBinding binding = text.gameObject.AddComponent<LocalizedTextBinding>();
			binding.Key = key;
			return true;
		}

		public static bool TryBind(UnityEngine.UI.Text text)
		{
			if (text == null) return false;
			LocalizedLegacyTextBinding existing = text.GetComponent<LocalizedLegacyTextBinding>();
			if (existing != null)
			{
				existing.Refresh();
				return true;
			}
			if (!SourceKeys.TryGetValue(text.text, out string key)) return false;
			LocalizedLegacyTextBinding binding = text.gameObject.AddComponent<LocalizedLegacyTextBinding>();
			binding.Key = key;
			return true;
		}

		public static bool TryGetLocalizationKey(string sourceText, out string key)
		{
			return SourceKeys.TryGetValue(sourceText ?? string.Empty, out key);
		}
	}
}
