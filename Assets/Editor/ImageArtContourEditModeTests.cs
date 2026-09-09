#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;
using Sekai.CustomMusicScoreManager;
using Sekai.Live;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Presenters;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using Sekai.MusicScoreMaker.Ingame.Views;
using Sekai.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ImageArtContourEditModeTests
{
	[Test]
	public void ArtPanelOccludesUnderlyingControls()
	{
		var canvas = new GameObject("ArtPanelCanvas", typeof(RectTransform), typeof(Canvas));
		var toolWindow = new GameObject("ToolWindow", typeof(RectTransform));
		toolWindow.transform.SetParent(canvas.transform, false);
		try
		{
			var model = new MusicScoreMakerModel(null) { MusicScoreMakerData = new MusicScoreMakerData() };
			MusicScoreMakerPresenter presenter = CreatePresenter(model, null);
			ArtToolsRuntimePanel.Attach(toolWindow.transform, presenter);
			var component = toolWindow.GetComponent<ArtToolsRuntimePanel>();
			var panel = (GameObject)GetField(component, "_panel");
			var viewport = panel.transform.Find("ArtControlsViewport");
			var container = panel.GetComponentsInChildren<Transform>(true).First(item => item.name == "Container");

			Assert.AreEqual(255, ToByte(panel.GetComponent<Image>().color.a));
			Assert.AreEqual(255, ToByte(viewport.GetComponent<Image>().color.a));
			Assert.AreEqual(205, ToByte(container.GetComponent<Image>().color.a));
		}
		finally { UnityEngine.Object.DestroyImmediate(canvas); }
	}

	[Test]
	public void BlankChartClickClosesAnOpenArtPanel()
	{
		var canvas = new GameObject("ArtPanelCanvas", typeof(RectTransform), typeof(Canvas));
		var toolWindow = new GameObject("ToolWindow", typeof(RectTransform));
		toolWindow.transform.SetParent(canvas.transform, false);
		try
		{
			var view = toolWindow.AddComponent<MusicScoreMakerView>();
			var model = new MusicScoreMakerModel(null) { MusicScoreMakerData = new MusicScoreMakerData() };
			model.MusicScoreMakerData.AddNote(new MusicScoreNoteBase { id = 1, ticks = 100, laneStart = 1, laneEnd = 2 });
			model.MusicScoreMakerData.AddSelectedNote(1);
			MusicScoreMakerPresenter presenter = CreatePresenter(model, view);
			ArtToolsRuntimePanel.Attach(toolWindow.transform, presenter);
			var component = toolWindow.GetComponent<ArtToolsRuntimePanel>();
			SetField(view, "_artToolsRuntimePanel", component);
			var panel = (GameObject)GetField(component, "_panel");
			panel.SetActive(true);

			InvokePresenter(presenter, "OnMusicScorePreviewClick", new OnMusicScorePreviewClickEvent
			{
				EventData = new PointerEventData(null) { button = PointerEventData.InputButton.Left }
			});

			Assert.IsFalse(panel.activeSelf);
			Assert.IsEmpty(model.MusicScoreMakerData.SelectedNoteIdList);
		}
		finally { UnityEngine.Object.DestroyImmediate(canvas); }
	}

	[Test]
	public void TextArtPanelUsesMethodDropdownAndShowsFontNameInline()
	{
		var canvas = new GameObject("ArtPanelCanvas", typeof(RectTransform), typeof(Canvas));
		var toolWindow = new GameObject("ToolWindow", typeof(RectTransform));
		toolWindow.transform.SetParent(canvas.transform, false);
		try
		{
			var model = new MusicScoreMakerModel(null) { MusicScoreMakerData = new MusicScoreMakerData() };
			MusicScoreMakerPresenter presenter = CreatePresenter(model, null);
			ArtToolsRuntimePanel.Attach(toolWindow.transform, presenter);
			var component = toolWindow.GetComponent<ArtToolsRuntimePanel>();
			var options = (GameObject)GetField(component, "_fontModeDropdownOptions");
			var modeLabel = (TMPro.TMP_Text)GetField(component, "_fontModeDropdownLabel");
			var fileLabel = (TMPro.TMP_Text)GetField(component, "_fontFileNameLabel");
			var status = (TMPro.TMP_Text)GetField(component, "_status");

			Assert.IsFalse(options.activeSelf);
			Assert.AreEqual(3, options.GetComponentsInChildren<Button>(true).Length);
			InvokePrivate(component, "ToggleFontModeDropdown");
			Assert.IsTrue(options.activeSelf);
			InvokePrivate(component, "SetFontMode", TextArtFontMode.Outline);
			Assert.IsFalse(options.activeSelf);
			StringAssert.Contains(LocalizationManager.Get("art.font.outline"), modeLabel.text);

			const string fontName = "SmileySans-Oblique.ttf";
			InvokePrivate(component, "SetSelectedFont", new string('a', 64), fontName);
			Assert.AreEqual(fontName, fileLabel.text);
			Assert.AreEqual(string.Empty, status.text);
			StringAssert.DoesNotContain(fontName, status.text);
		}
		finally { UnityEngine.Object.DestroyImmediate(canvas); }
	}

	[Test]
	public void SettingsLanguageDropdownHasStableLanguageMapping()
	{
		Type screenType = typeof(ScreenLayerCustomMusicScoreManager);
		MethodInfo getName = screenType.GetMethod("GetLanguageDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(getName);
		Assert.AreEqual("简体中文", getName.Invoke(null, new object[] { LocalizationManager.SimplifiedChinese }));
		Assert.AreEqual("日本語", getName.Invoke(null, new object[] { LocalizationManager.Japanese }));
		Assert.AreEqual("English", getName.Invoke(null, new object[] { LocalizationManager.English }));
	}

	[Test]
	public void SettingsLanguageDropdownExpandsAndCollapsesItsOptions()
	{
		var options = new GameObject("Options");
		var layoutObject = new GameObject("Layout", typeof(RectTransform), typeof(LayoutElement));
		try
		{
			LayoutElement layout = layoutObject.GetComponent<LayoutElement>();
			MethodInfo setExpanded = typeof(ScreenLayerCustomMusicScoreManager).GetMethod("SetLanguageDropdownExpanded", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(GameObject), typeof(LayoutElement), typeof(bool) }, null);
			Assert.NotNull(setExpanded);
			setExpanded.Invoke(null, new object[] { options, layout, true });
			Assert.IsTrue(options.activeSelf);
			Assert.AreEqual(224f, layout.preferredHeight);
			setExpanded.Invoke(null, new object[] { options, layout, false });
			Assert.IsFalse(options.activeSelf);
			Assert.AreEqual(58f, layout.preferredHeight);
		}
		finally
		{
			UnityEngine.Object.DestroyImmediate(options);
			UnityEngine.Object.DestroyImmediate(layoutObject);
		}
	}

	[Test]
	public void SettingsUsesOneLanguageDropdownAndArtSidebarHasNoModeRows()
	{
		string manager = File.ReadAllText(Path.Combine(Application.dataPath, "CustomMusicScoreManager", "Runtime", "UI", "ScreenLayerCustomMusicScoreManager.cs"));
		StringAssert.Contains("CreateSettingLanguageSelector(settingsContent)", manager);
		StringAssert.DoesNotContain("LanguageZhHans", manager);
		StringAssert.DoesNotContain("LanguageJapanese", manager);
		StringAssert.DoesNotContain("LanguageEnglish", manager);

		string artPanel = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Assembly-CSharp", "Sekai", "MusicScoreMaker", "Ingame", "Views", "ArtToolsRuntimePanel.cs"));
		StringAssert.DoesNotContain("GameObject languages", artPanel);
		StringAssert.DoesNotContain("GameObject tabs", artPanel);
		StringAssert.Contains("CreateToolbarButton(toolbarContent, \"T\"", artPanel);
		StringAssert.Contains("CreateToolbarButton(toolbarContent, \"IMG\"", artPanel);
	}

	[Test]
	public void ArtGroupAutoOpenOnlyTriggersOnSelectionTransitions()
	{
		var root = new GameObject("ArtPanel");
		var view = root.AddComponent<ArtToolsRuntimePanel>();
		try
		{
			MethodInfo observe = typeof(ArtToolsRuntimePanel).GetMethod("ObserveSelection", BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(observe);
			var image = new ArtGroupData { Id = "image", Type = ArtGroupType.Image };
			var text = new ArtGroupData { Id = "text", Type = ArtGroupType.Text };
			Assert.IsTrue((bool)observe.Invoke(view, new object[] { image }));
			Assert.IsFalse((bool)observe.Invoke(view, new object[] { image.Clone() }), "An unchanged selection must not reopen a panel the user closed.");
			Assert.IsTrue((bool)observe.Invoke(view, new object[] { text }));
			Assert.IsFalse((bool)observe.Invoke(view, new object[] { null }));
			Assert.IsTrue((bool)observe.Invoke(view, new object[] { text.Clone() }), "Selecting the same group again after clearing selection is a new transition.");
		}
		finally { UnityEngine.Object.DestroyImmediate(root); }
	}

	[Test]
	public void ArtToolRowsKeepFixedHeightInsideFlexibleSidebar()
	{
		var root = new GameObject("Sidebar", typeof(RectTransform), typeof(VerticalLayoutGroup));
		var view = root.AddComponent<ArtToolsRuntimePanel>();
		try
		{
			var rootRect = (RectTransform)root.transform;
			rootRect.sizeDelta = new Vector2(560, 900);
			var layout = root.GetComponent<VerticalLayoutGroup>();
			layout.childControlHeight = true;
			layout.childForceExpandHeight = false;
			var createRow = typeof(ArtToolsRuntimePanel).GetMethod("CreateRow", BindingFlags.NonPublic | BindingFlags.Instance);
			var header = (GameObject)createRow.Invoke(view, new object[] { root.transform, 50f });
			var body = new GameObject("Body", typeof(RectTransform), typeof(LayoutElement));
			body.transform.SetParent(root.transform, false);
			body.GetComponent<LayoutElement>().flexibleHeight = 1;
			LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
			Assert.AreEqual(50f, ((RectTransform)header.transform).rect.height, .01f);
			Assert.Greater(((RectTransform)body.transform).rect.height, 800f);
		}
		finally { UnityEngine.Object.DestroyImmediate(root); }
	}

	[Test]
	public void ArtGroupActionsSwitchBetweenCreateAndEditModes()
	{
		var root = new GameObject("ArtPanel");
		var view = root.AddComponent<ArtToolsRuntimePanel>();
		var generate = new GameObject("Generate");
		var controls = new GameObject("Controls");
		var commands = new GameObject("Commands");
		try
		{
			SetField(view, "_generateButton", generate);
			SetField(view, "_groupControls", controls);
			SetField(view, "_groupCommands", commands);
			var method = typeof(ArtToolsRuntimePanel).GetMethod("SetGroupEditingState", BindingFlags.NonPublic | BindingFlags.Instance);
			method.Invoke(view, new object[] { false });
			Assert.IsTrue(generate.activeSelf);
			Assert.IsFalse(controls.activeSelf);
			Assert.IsFalse(commands.activeSelf);
			method.Invoke(view, new object[] { true });
			Assert.IsFalse(generate.activeSelf);
			Assert.IsTrue(controls.activeSelf);
			Assert.IsTrue(commands.activeSelf);
		}
		finally
		{
			UnityEngine.Object.DestroyImmediate(generate);
			UnityEngine.Object.DestroyImmediate(controls);
			UnityEngine.Object.DestroyImmediate(commands);
			UnityEngine.Object.DestroyImmediate(root);
		}
	}

	[Test]
	public void DynamicArtGroupStatusIsNotBoundToTheEmptySelectionKey()
	{
		var root = new GameObject("ArtPanel");
		var view = root.AddComponent<ArtToolsRuntimePanel>();
		try
		{
			var method = typeof(ArtToolsRuntimePanel).GetMethod("CreateLabel", BindingFlags.NonPublic | BindingFlags.Instance);
			var label = (TMPro.TMP_Text)method.Invoke(view, new object[]
			{
				root.transform, LocalizationManager.Get("art.group.none"), 18f, TMPro.TextAlignmentOptions.MidlineLeft, 30f, false
			});
			Assert.IsNull(label.GetComponent<LocalizedTextBinding>());
			label.text = LocalizationManager.Format("art.group.selected", LocalizationManager.Get("art.image"));
			RuntimeLocalizationBootstrap.RefreshAll();
			Assert.AreEqual(LocalizationManager.Format("art.group.selected", LocalizationManager.Get("art.image")), label.text);
		}
		finally { UnityEngine.Object.DestroyImmediate(root); }
	}

	private static void SetField(object owner, string name, object value) => owner.GetType()
		.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);

	private static object GetField(object owner, string name) => owner.GetType()
		.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);

	private static MusicScoreMakerPresenter CreatePresenter(MusicScoreMakerModel model, MusicScoreMakerView view)
	{
		return (MusicScoreMakerPresenter)Activator.CreateInstance(typeof(MusicScoreMakerPresenter), BindingFlags.Instance | BindingFlags.NonPublic,
			null, new object[] { model, view, null }, null);
	}

	private static void InvokePresenter(MusicScoreMakerPresenter presenter, string methodName, object argument)
	{
		typeof(MusicScoreMakerPresenter).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(presenter, new[] { argument });
	}

	private static void InvokePrivate(object owner, string methodName, params object[] arguments)
	{
		owner.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, arguments);
	}

	private static int ToByte(float value) => Mathf.RoundToInt(value * 255f);

	[Test]
	public void RectangleBecomesOneTwoNodeGuideInsteadOfColumns()
	{
		var image = Grid(96, 32, (x, y) => true);
		var group = Group();
		int id = 0;
		var notes = ArtGuideGenerator.GenerateImageNotes(image, group, () => ++id);
		Assert.AreEqual(2, notes.Count);
		Assert.AreEqual(0, notes[0].GuideLeft);
		Assert.AreEqual(12, notes[0].GuideRight);
		Assert.AreEqual(480, notes[0].ticks);
		Assert.AreEqual(480 + 32 * 12, notes[1].ticks);
		group.ImageSettings.SliceMode = ImageArtSliceMode.VerticalStrips;
		Assert.AreEqual(192, ArtGuideGenerator.GenerateImageNotes(image, group, () => ++id).Count);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void CircleUsesOneChainAndPreservesRasterSilhouette(bool autoEase)
	{
		var image = Circle(false);
		var chains = ImageArtContourGenerator.Generate(image, autoEase);
		Assert.AreEqual(1, chains.Count);
		Assert.Less(chains[0].Count, image.Height);
		if (autoEase) Assert.IsTrue(chains[0].Any(a => a.LineType != NoteLineType.Linear));
		else Assert.IsTrue(chains[0].All(a => a.LineType == NoteLineType.Linear));
		AssertSilhouette(image, chains);
	}

	[Test]
	public void RingSplitsAtHoleAndRejoinsWithoutFillingIt()
	{
		var image = Circle(true);
		var chains = ImageArtContourGenerator.Generate(image, true);
		Assert.AreEqual(4, chains.Count);
		AssertSilhouette(image, chains);
	}

	[Test]
	public void ThinDiagonalUsesOneConnectedChain()
	{
		var image = Grid(32, 32, (x, y) => x == y);
		var chains = ImageArtContourGenerator.Generate(image, true);
		Assert.AreEqual(1, chains.Count);
		AssertSilhouette(image, chains);
	}

	[Test]
	public void AutoEaseRoundsCircleTipsAndHoleJunctions()
	{
		var circle = ImageArtContourGenerator.Generate(Circle(false), true)[0];
		Assert.AreEqual(circle[0].Left, circle[0].Right);
		Assert.AreEqual(circle[circle.Count - 1].Left, circle[circle.Count - 1].Right);
		var linear = ImageArtContourGenerator.Generate(Circle(false), false)[0];
		Assert.Greater(linear[0].Right, linear[0].Left);
		var ring = ImageArtContourGenerator.Generate(Circle(true), true);
		var split = ring.GroupBy(c => c[0].Row).Single(g => g.Count() == 2).OrderBy(c => c[0].Left).ToArray();
		Assert.AreEqual(split[0][0].Right, split[1][0].Left);
	}

	[Test]
	public void RoundedFittingDoesNotJoinSeparateComponents()
	{
		var image = Grid(60, 20, (x, y) =>
		{
			int gap = Mathf.RoundToInt(4 * Mathf.Sqrt(y + 1));
			return x >= 1 && x < 30 - gap || x >= 30 + gap && x < 59;
		});
		var chains = ImageArtContourGenerator.Generate(image, true);
		Assert.AreEqual(2, chains.Count);
		Assert.Greater(chains[1][0].Left - chains[0][0].Right, 0);
		AssertSilhouette(image, chains);
	}

	[Test]
	public void ThinHolesAndSeparateComponentsRemainEmpty()
	{
		var image = Grid(40, 40, (x, y) => (x < 15 || x > 15 && x < 30) && (y < 10 || y > 10));
		AssertSilhouette(image, ImageArtContourGenerator.Generate(image, true));
	}

	[Test]
	public void BranchingShapePreservesAllArms()
	{
		var image = Grid(40, 40, (x, y) => y < 20 ? x >= 5 && x < 35 : x >= 5 && x < 10 || x >= 30 && x < 35);
		var chains = ImageArtContourGenerator.Generate(image, true);
		Assert.AreEqual(3, chains.Count);
		AssertSilhouette(image, chains);
	}

	[Test]
	public void NodesKeepEasingPreciseBoundsAndStrictTicksAtSmallScale()
	{
		var group = Group();
		group.OriginLane = 10; group.ScaleY = .01f;
		group.ImageSettings.RowSpacingTicks = 1;
		group.NoteType = NoteType.Critical;
		int id = 0;
		var notes = ArtGuideGenerator.GenerateImageNotes(Circle(false), group, () => ++id);
		Assert.IsTrue(notes.Any(n => n.noteLineType != NoteLineType.Linear));
		Assert.IsTrue(notes.All(n => n.GuideLeft >= 10 && n.GuideRight <= 12 && n.laneEnd <= 11));
		Assert.IsTrue(notes.All(n => n.ArtGroupId == group.Id && n.type == NoteType.Critical));
		for (int i = 1; i < notes.Count; i++)
		{
			Assert.Greater(notes[i].ticks, notes[i - 1].ticks);
			Assert.AreEqual(notes[i].id, notes[i - 1].nextConnectionId);
			Assert.AreEqual(notes[i - 1].id, notes[i].previousConnectionId);
			if (i < notes.Count - 1) Assert.AreEqual(NoteCategory.GuideHidden, notes[i].category);
		}
		var saved = JsonConvert.DeserializeObject<List<MusicScoreNoteBase>>(JsonConvert.SerializeObject(notes.Select(n => n.Clone()).ToList()));
		CollectionAssert.AreEqual(notes.Select(n => n.noteLineType), saved.Select(n => n.noteLineType));
		CollectionAssert.AreEqual(notes.Select(n => n.GuideLeft), saved.Select(n => n.GuideLeft));
	}

	[Test]
	public void SettingsRoundTripAndOldSettingsKeepVerticalMode()
	{
		var settings = Group().ImageSettings;
		settings.AutoEase = false;
		var json = JsonConvert.DeserializeObject<ImageArtSettings>(JsonConvert.SerializeObject(settings));
		var binary = MessagePackSerializer.Deserialize<ImageArtSettings>(MessagePackSerializer.Serialize(settings));
		foreach (var copy in new[] { settings.Clone(), json, binary })
		{
			Assert.AreEqual(ImageArtSliceMode.HorizontalContours, copy.SliceMode);
			Assert.IsFalse(copy.AutoEase);
		}
		Assert.AreEqual(ImageArtSliceMode.VerticalStrips, JsonConvert.DeserializeObject<ImageArtSettings>("{\"Width\":12}").SliceMode);
		Assert.AreEqual(ImageArtSliceMode.VerticalStrips, new ImageArtSettings().SliceMode);
	}

	[Test]
	public void NodeLimitIsCheckedBeforeIdsAreAllocated()
	{
		int id = 0;
		var image = Grid(128, 128, (x, y) => (x + y) % 2 == 0);
		Assert.Throws<InvalidOperationException>(() => ArtGuideGenerator.GenerateImageNotes(image, Group(), () => ++id));
		Assert.AreEqual(0, id);
		image = Grid(8000, 1, (x, y) => x % 2 == 0);
		Assert.AreEqual(8000, ArtGuideGenerator.GenerateImageNotes(image, Group(), () => ++id).Count);
	}

	[Test]
	public void CancelledAndEmptyPreviewsAreSafe()
	{
		var token = new CancellationToken(true);
		Assert.Throws<OperationCanceledException>(() => ImageArtContourGenerator.Generate(Circle(false), true, token));
		Assert.IsEmpty(ImageArtContourGenerator.Generate(new BinaryImageArt(), true));
		var preview = ImageArtContourGenerator.RenderPreview(new List<List<ImageArtContourAnchor>>(), 0, 0);
		Assert.AreEqual(1, preview.Pixels.Length);
		Assert.AreEqual(0, preview.ForegroundPixelCount);
	}

	[Test]
	public void FittedPreviewIsNonblankAndKeepsTheRingHole()
	{
		var source = Circle(true);
		var chains = ImageArtContourGenerator.Generate(source, true);
		var preview = ImageArtContourGenerator.RenderPreview(chains, source.Width, source.Height);
		Assert.Greater(preview.ForegroundPixelCount, 0);
		Assert.AreEqual(0, preview.Pixels[preview.Height / 2 * preview.Width + preview.Width / 2]);
		var texture = new Texture2D(preview.Width, preview.Height, TextureFormat.RGBA32, false);
		try
		{
			texture.SetPixels32(preview.Pixels.Select(p => p == 0 ? new Color32(25, 29, 34, 255) : new Color32(46, 204, 150, 255)).ToArray());
			texture.Apply();
			string output = Path.Combine(Application.dataPath, "..", "Logs", "horizontal-contour-preview.png");
			File.WriteAllBytes(output, texture.EncodeToPNG());
		}
		finally { UnityEngine.Object.DestroyImmediate(texture); }
	}

	private static ArtGroupData Group() => new ArtGroupData
	{
		OriginTicks = 480, Type = ArtGroupType.Image,
		ImageSettings = new ImageArtSettings { SliceMode = ImageArtSliceMode.HorizontalContours }
	};
	private static BinaryImageArt Circle(bool ring) => Grid(96, 96, (x, y) =>
	{
		float distance = (x - 47.5f) * (x - 47.5f) + (y - 47.5f) * (y - 47.5f);
		return distance < 46 * 46 && (!ring || distance > 24 * 24);
	});
	private static BinaryImageArt Grid(int width, int height, Func<int, int, bool> filled)
	{
		var image = new BinaryImageArt { Width = width, Height = height, Pixels = new byte[width * height] };
		for (int y = 0; y < height; y++)
		for (int x = 0; x < width; x++)
			if (filled(x, y)) { image.Pixels[y * width + x] = 1; image.ForegroundPixelCount++; }
		return image;
	}
	private static void AssertSilhouette(BinaryImageArt image, List<List<ImageArtContourAnchor>> chains)
	{
		for (int y = 0; y < image.Height; y++)
		for (int x = 0; x < image.Width; x++)
		{
			bool filled = false;
			foreach (var chain in chains)
			for (int i = 1; i < chain.Count; i++)
			{
				var a = chain[i - 1]; var b = chain[i];
				if (y + .5f < a.Row || y + .5f >= b.Row) continue;
				float t = ImageArtContourGenerator.Ease((y + .5f - a.Row) / (b.Row - a.Row), a.LineType);
				filled |= x + .5f >= Mathf.Lerp(a.Left, b.Left, t) && x + .5f < Mathf.Lerp(a.Right, b.Right, t);
			}
			bool expected = image.Pixels[y * image.Width + x] != 0;
			if (filled != expected)
			{
				bool edge = x > 0 && (image.Pixels[y * image.Width + x - 1] != 0) != expected
					|| x + 1 < image.Width && (image.Pixels[y * image.Width + x + 1] != 0) != expected;
				Assert.IsTrue(edge, $"Only subpixel edge fitting may differ at {x},{y}.");
			}
		}
	}
}
#endif
