using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.CustomMusicScoreManager
{
	// Own canvas survives the Live -> menu transition and does not use scaled animations.
	public sealed class VideoExportOverlay : MonoBehaviour
	{
		private TMP_Text title, status;
		private Slider progress;
		private GameObject buttons;
		private TMP_FontAsset font;

		public static VideoExportOverlay Create()
		{
			var root = new GameObject("Video export", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			if (Application.isPlaying) DontDestroyOnLoad(root);
			var canvas = root.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = short.MaxValue;
			var scaler = root.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920, 1080);
			scaler.matchWidthOrHeight = 0.5f;
			var overlay = root.AddComponent<VideoExportOverlay>();
			overlay.Build();
			return overlay;
		}

		private void Build()
		{
			font = HighQualityDynamicFontProvider.Get(Resources.Load<TMP_FontAsset>("font/FOT-RodinNTLGPro-DB SDF_Dynamic"));
			var background = Rect("Background", transform, Vector2.zero, Vector2.zero);
			background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one;
			background.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
			var panel = Rect("Panel", transform, new Vector2(960, 460), Vector2.zero);
			panel.gameObject.AddComponent<Image>().color = new Color32(26, 34, 44, 255);
			title = Label(panel, "Title", new Vector2(860, 70), new Vector2(0, 160), 32);
			status = Label(panel, "Status", new Vector2(860, 185), new Vector2(0, 20), 23);
			var bar = Rect("Progress", panel, new Vector2(860, 18), new Vector2(0, -110));
			bar.gameObject.AddComponent<Image>().color = new Color32(55, 65, 75, 255);
			var fill = Rect("Fill", bar, Vector2.zero, Vector2.zero);
			fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
			fill.gameObject.AddComponent<Image>().color = new Color32(51, 221, 204, 255);
			progress = bar.gameObject.AddComponent<Slider>();
			progress.fillRect = fill; progress.interactable = false; progress.transition = Selectable.Transition.None;
			buttons = Rect("Buttons", panel, new Vector2(860, 65), new Vector2(0, -170)).gameObject;
			buttons.SetActive(false);
			UpdateProgress(0, "准备生成视频…");
		}

		public void UpdateProgress(float value, string message)
		{
			progress.gameObject.SetActive(true);
			buttons.SetActive(false);
			title.text = $"正在生成视频 · {Mathf.RoundToInt(Mathf.Clamp01(value) * 100)}%";
			status.text = message;
			progress.value = Mathf.Clamp01(value);
		}

		public void ShowResult(bool success, string message, Action open = null, string actionLabel = null)
		{
			title.text = success ? "视频已生成" : "视频生成失败";
			status.text = message;
			progress.gameObject.SetActive(false);
			buttons.SetActive(true);
			foreach (Transform child in buttons.transform) Destroy(child.gameObject);
			AddButton("关闭", open == null ? 0 : -210, () => Destroy(gameObject));
			if (open != null) AddButton(actionLabel ?? (Application.platform == RuntimePlatform.Android ? "分享" : "打开文件位置"), 210, open);
		}

		private void AddButton(string text, float x, Action action)
		{
			var rect = Rect(text, buttons.transform, new Vector2(360, 60), new Vector2(x, 0));
			rect.gameObject.AddComponent<Image>().color = new Color32(45, 100, 109, 255);
			var button = rect.gameObject.AddComponent<Button>();
			button.onClick.AddListener(() => action());
			Label(rect, "Label", new Vector2(340, 60), Vector2.zero, 23).text = text;
		}

		private TMP_Text Label(Transform parent, string name, Vector2 size, Vector2 position, int pointSize)
		{
			var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
			text.font = font; text.fontSize = pointSize; text.color = Color.white;
			text.alignment = TextAlignmentOptions.Center;
			text.textWrappingMode = TextWrappingModes.Normal;
			text.overflowMode = TextOverflowModes.Overflow;
			text.raycastTarget = false;
			return text;
		}

		private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
		{
			var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
			rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = size; rect.anchoredPosition = position;
			return rect;
		}
	}
}
