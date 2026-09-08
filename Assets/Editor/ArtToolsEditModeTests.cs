#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Sekai;
using Sekai.CustomMusicScoreManager;
using Sekai.Live;
using Sekai.Localization;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using UnityEngine;
using UnityEngine.UI;

public sealed class ArtToolsEditModeTests
{
	[TestCase(ArtGroupType.Text)]
	[TestCase(ArtGroupType.Image)]
	public void ArtGroupCanBeHitBetweenDisconnectedStrokes(ArtGroupType type)
	{
		var data = HitTestChart(type);
		Assert.AreEqual("outer", ArtGroupHitTest.FindGroup(data, Vector2.zero, new Vector2(1200, 1000), 0, 1000));
		Assert.IsEmpty(data.SelectedNoteIdList, "Hit testing must not change selection or chart data.");
	}

	[Test]
	public void ArtGroupHitUsesPaddingButDoesNotCaptureUnrelatedEmptySpace()
	{
		var data = HitTestChart();
		Assert.AreEqual("outer", ArtGroupHitTest.FindGroup(data, new Vector2(410, 310), new Vector2(1200, 1000), 0, 1000));
		Assert.IsNull(ArtGroupHitTest.FindGroup(data, new Vector2(430, 330), new Vector2(1200, 1000), 0, 1000));
	}

	[Test]
	public void ArtGroupHitFollowsScrollZoomAndEditedGeometry()
	{
		var data = HitTestChart();
		Assert.IsNull(ArtGroupHitTest.FindGroup(data, Vector2.zero, new Vector2(1200, 1000), 1000, 2000));
		Assert.AreEqual("outer", ArtGroupHitTest.FindGroup(data, Vector2.zero, new Vector2(1200, 1000), 300, 700));
		foreach (var note in data.NoteList) note.SetGuideBounds(0, .1f);
		Assert.IsNull(ArtGroupHitTest.FindGroup(data, Vector2.zero, new Vector2(1200, 1000), 0, 1000));
		Assert.AreEqual("outer", ArtGroupHitTest.FindGroup(data, new Vector2(-595, 0), new Vector2(1200, 1000), 0, 1000));
	}

	[Test]
	public void ArtGroupHitPrefersNestedGroupAndLatestGroupOnTie()
	{
		var data = HitTestChart();
		data.ArtGroups.Add(new ArtGroupData { Id = "inner" });
		var first = new MusicScoreNoteBase { id = 3, ArtGroupId = "inner", ticks = 450 };
		var last = new MusicScoreNoteBase { id = 4, ArtGroupId = "inner", ticks = 550 };
		first.SetGuideBounds(5, 5.1f); last.SetGuideBounds(6.9f, 7);
		data.NoteList.AddRange(new[] { first, last });
		Assert.AreEqual("inner", ArtGroupHitTest.FindGroup(data, Vector2.zero, new Vector2(1200, 1000), 0, 1000));
		data.ArtGroups.Add(new ArtGroupData { Id = "latest" });
		foreach (var note in new[] { first, last })
		{
			var copy = note.Clone(); copy.id += 2; copy.ArtGroupId = "latest"; data.NoteList.Add(copy);
		}
		Assert.AreEqual("latest", ArtGroupHitTest.FindGroup(data, Vector2.zero, new Vector2(1200, 1000), 0, 1000));
	}

	[Test]
	public void ArtGroupHitRejectsOutsideViewportAndInvalidRange()
	{
		var data = HitTestChart();
		Assert.IsNull(ArtGroupHitTest.FindGroup(data, new Vector2(0, 510), new Vector2(1200, 1000), 400, 600));
		Assert.IsNull(ArtGroupHitTest.FindGroup(data, Vector2.zero, new Vector2(1200, 1000), 1000, 1000));
		Assert.IsNull(ArtGroupHitTest.FindGroup(data, Vector2.zero, Vector2.zero, 0, 1000));
	}

	[Test]
	public void ArtGroupHitIgnoresUngroupedNotesAndMissingMetadata()
	{
		var data = HitTestChart();
		data.ArtGroups.Clear();
		Assert.IsNull(ArtGroupHitTest.FindGroup(data, Vector2.zero, new Vector2(1200, 1000), 0, 1000));
		data.ArtGroups.Add(new ArtGroupData { Id = "outer" });
		foreach (var note in data.NoteList) note.ArtGroupId = null;
		Assert.IsNull(ArtGroupHitTest.FindGroup(data, Vector2.zero, new Vector2(1200, 1000), 0, 1000));
	}

	private static MusicScoreMakerData HitTestChart(ArtGroupType type = ArtGroupType.Text)
	{
		var data = new MusicScoreMakerData();
		data.ArtGroups.Add(new ArtGroupData { Id = "outer", Type = type });
		var first = new MusicScoreNoteBase { id = 1, ArtGroupId = "outer", ticks = 200 };
		var last = new MusicScoreNoteBase { id = 2, ArtGroupId = "outer", ticks = 800 };
		first.SetGuideBounds(2, 2.1f); last.SetGuideBounds(9.9f, 10);
		data.NoteList.AddRange(new[] { first, last });
		return data;
	}

	[TestCase(0f)]
	[TestCase(.02f)]
	[TestCase(.5f)]
	[TestCase(5f)]
	[TestCase(10f)]
	public void StrokeAreaDoesNotCollapseNearHorizontal(float rise)
	{
		var art = new ArtStrokeData { MinX = 0, MaxX = 14, MinY = 0, MaxY = rise + 2 };
		art.Strokes.Add(new List<ArtStrokePoint> { new ArtStrokePoint(2, 2), new ArtStrokePoint(12, 2 + rise) });
		var group = new ArtGroupData { TextSettings = new TextArtSettings { HorizontalScale = .1f, VerticalScale = 1f } };
		int id = 0;
		var notes = ArtGuideGenerator.GenerateTextNotes(art, group, () => ++id);
		double area = 0;
		foreach (var note in notes)
		{
			Assert.That(note.GuideLeft, Is.InRange(0f, 12f));
			Assert.That(note.GuideRight, Is.InRange(note.GuideLeft, 12f));
			var next = notes.Find(n => n.id == note.nextConnectionId);
			if (next == null) continue;
			Assert.Greater(next.ticks, note.ticks);
			area += (note.GuideRight - note.GuideLeft + next.GuideRight - next.GuideLeft) * .5 * (next.ticks - note.ticks);
		}
		double expected = (Math.Sqrt(100 + rise * rise) + 1) * .1 * ArtGuideGenerator.TextTicksPerLane;
		Assert.AreEqual(expected, area, expected * .025, "Stroke thickness must be measured perpendicular to its direction.");
		if (rise == 0) Assert.AreEqual(2, notes.Count);
	}

	[Test]
	public void TextNodeLimitIncludesThickSegmentCornersBeforeAllocatingIds()
	{
		var art = new ArtStrokeData { MaxX = 10, MaxY = 10 };
		for (int i = 0; i < 2100; i++)
			art.Strokes.Add(new List<ArtStrokePoint> { new ArtStrokePoint(0, 0), new ArtStrokePoint(10, 1) });
		int idsAllocated = 0;
		Assert.Throws<InvalidOperationException>(() => ArtGuideGenerator.GenerateTextNotes(art,
			new ArtGroupData { TextSettings = new TextArtSettings() }, () => ++idsAllocated));
		Assert.AreEqual(0, idsAllocated);
	}

	[Test]
	public async Task TextUsesSubLaneStrokesAndSeparatesHorizontalSegments()
	{
		var art = await TextArtVectorizer.VectorizeHersheyAndCjkAsync("TEST", default);
		var group = new ArtGroupData { TextSettings = new TextArtSettings { HorizontalScale = .08f, VerticalScale = .08f } };
		int id = 0;
		var notes = ArtGuideGenerator.GenerateTextNotes(art, group, () => ++id);
		Assert.IsTrue(notes.Any(n => n.GuideRight - n.GuideLeft < .2f));
		Assert.Greater(notes.Select(n => n.GuideLeft).Distinct().Count(), 12);
		Assert.Less(notes.Max(n => n.ticks) - notes.Min(n => n.ticks), 300);
		var split = ArtGuideGenerator.SplitIntoMonotonicStrokes(new[] { new List<ArtStrokePoint> { new ArtStrokePoint(0, 0), new ArtStrokePoint(1, 1), new ArtStrokePoint(2, 1), new ArtStrokePoint(3, 2) } });
		Assert.AreEqual(3, split.Count);
	}

	[Test]
	public void PreciseGuideGeometrySurvivesCloneSerializationAndUndoSnapshot()
	{
		var note = new MusicScoreNoteBase { id = 1, ticks = 480 };
		note.SetGuideBounds(1.25f, 1.35f);
		var before = note.GetCurrentNoteOperation();
		var saved = JsonConvert.DeserializeObject<MusicScoreNoteBase>(JsonConvert.SerializeObject(note.Clone()));
		Assert.AreEqual(1.25f, saved.GuideLeft, .0001f);
		Assert.AreEqual(1.35f, saved.GuideRight, .0001f);
		note.SetGuideBounds(3, 5);
		note.SetData(before);
		Assert.AreEqual(saved.GuideLeft, note.GuideLeft);
		Assert.AreEqual(saved.GuideRight, note.GuideRight);
	}

	[Test]
	public void VerticalResizeKeepsWidthAndStrictTickOrder()
	{
		var group = new ArtGroupData { OriginTicks = 480, OriginLane = 1, ImageSettings = new ImageArtSettings() };
		var first = new MusicScoreNoteBase { id = 1, ticks = 480, nextConnectionId = 2 };
		var last = new MusicScoreNoteBase { id = 2, ticks = 960, previousConnectionId = 1 };
		first.SetGuideBounds(1.1f, 1.2f);
		last.SetGuideBounds(1.1f, 1.2f);
		var result = ArtGroupTransform.Resize(new[] { first, last }, group, 1.1f, 1.2f, 480, 1440);
		Assert.AreEqual(480, result[0].ticks);
		Assert.AreEqual(1440, result[1].ticks);
		Assert.AreEqual(.1f, result[1].GuideRight - result[1].GuideLeft, .0001f);
		Assert.AreEqual(2f, group.ScaleY);
		Assert.AreEqual(960, last.ticks);
	}

	[Test]
	public void ZeroMoveIsNotAnEditForPreciseGuides()
	{
		var note = new MusicScoreNoteBase { id = 1, ticks = 480 };
		note.SetGuideBounds(1.25f, 1.35f);
		Assert.IsTrue(note.GetCurrentNoteOperation() == note.CalcMoveOperation(new SelectedTargetOperation(), new MusicScoreMakerData()));
	}

	[Test]
	public void ImageColumnsHaveExactAdjacentEdgesWithoutOneLaneExpansion()
	{
		var image = new BinaryImageArt { Width = 96, Height = 1, Pixels = Enumerable.Repeat((byte)1, 96).ToArray() };
		var group = new ArtGroupData { ImageSettings = new ImageArtSettings() };
		int id = 0;
		var notes = ArtGuideGenerator.GenerateImageNotes(image, group, () => ++id);
		Assert.AreEqual(.125f, notes[0].GuideRight - notes[0].GuideLeft, .0001f);
		Assert.AreEqual(notes[0].GuideRight, notes[2].GuideLeft, .0001f);
		Assert.AreEqual(12f, notes.Last().GuideRight, .0001f);
	}

	[Test]
	public void LiveGuideConversionPreservesSubLaneEdgesWithoutArtMetadata()
	{
		var source = new MusicScoreNoteBase { id = 1, ticks = 0, noteBaseType = MusicScoreNoteBase.NoteBaseType.Guide, category = NoteCategory.Guide, ArtGroupId = "test" };
		source.SetGuideBounds(.25f, .35f);
		var generate = typeof(MusicScoreNoteBase).GetMethod("GenerateNote", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		var live = (NoteBase)generate.Invoke(null, new object[] { source, null, new MusicScoreInfo(), null });
		Assert.AreEqual(.25f, live.DefaultLeftLaneF, .0001f);
		Assert.AreEqual(.35f, live.DefaultRightLaneF + 1f, .0001f);
		Assert.IsNull(live.GetType().GetField("ArtGroupId"));
		Assert.IsNull(live.GetType().GetProperty("ArtGroupId"));
		int id = 1;
		var restored = MusicScoreNoteBase.FromNoteBaseArray(live, () => ++id, Array.Empty<MusicScoreInfo>())[0];
		Assert.AreEqual(source.GuideLeft, restored.GuideLeft, .0001f);
		Assert.AreEqual(source.GuideRight, restored.GuideRight, .0001f);
	}
	[Test]
	public void VersionOneMigratesWithoutChangingNotes()
	{
		MusicScoreMakerData data = JsonConvert.DeserializeObject<MusicScoreMakerData>("{\"VersionCode\":1,\"NoteList\":[{\"id\":7,\"ticks\":480,\"laneStart\":2,\"laneEnd\":3}],\"MusicScoreEventDataList\":[]}");
		data.MigrateToCurrentVersion();
		Assert.AreEqual(MusicScoreMakerData.VERSION_2, data.VersionCode);
		Assert.NotNull(data.ArtGroups);
		Assert.AreEqual(7, data.NoteList[0].id);
		Assert.AreEqual(480, data.NoteList[0].ticks);
	}

	[Test]
	public void NoteAndGroupClonePreserveArtMetadata()
	{
		MusicScoreNoteBase note = new MusicScoreNoteBase { ArtGroupId = "group-a", id = 9 };
		Assert.AreEqual("group-a", note.Clone().ArtGroupId);
		ArtGroupData group = new ArtGroupData { Id = "group-a", Type = ArtGroupType.Text, IsManuallyEdited = true, TextSettings = new TextArtSettings { Text = "ABC", FontAssetHash = "hash" } };
		ArtGroupData copy = group.Clone("group-b");
		Assert.AreEqual("group-b", copy.Id);
		Assert.AreEqual("ABC", copy.TextSettings.Text);
		Assert.IsTrue(copy.IsManuallyEdited);
	}

	[Test]
	public void TextValidationCountsUnicodeScalarsAndLines()
	{
		Assert.AreEqual(2, TextArtVectorizer.NormalizeAndValidate("A\n中").Length);
		Assert.Throws<ArgumentException>(() => TextArtVectorizer.NormalizeAndValidate(string.Join("\n", Enumerable.Repeat("x", 9))));
		Assert.Throws<ArgumentException>(() => TextArtVectorizer.NormalizeAndValidate(new string('x', 33)));
	}

	[Test]
	public void ImagePixelsMergeIntoVerticalRuns()
	{
		BinaryImageArt art = new BinaryImageArt { Width = 2, Height = 5, Pixels = new byte[] { 1,0, 1,1, 0,1, 1,1, 1,0 } };
		List<ImageArtRun> runs = ArtGuideGenerator.GetVerticalRuns(art);
		Assert.AreEqual(3, runs.Count);
		Assert.AreEqual(0, runs[0].Column);
		Assert.AreEqual(0, runs[0].StartRow);
		Assert.AreEqual(1, runs[0].EndRow);
	}

	[Test]
	public void ImageGenerationRejectsMoreThanEightThousandNodes()
	{
		BinaryImageArt art = new BinaryImageArt { Width = 4001, Height = 1, Pixels = Enumerable.Repeat((byte)1, 4001).ToArray() };
		ArtGroupData group = new ArtGroupData { Id = "g", Type = ArtGroupType.Image, ImageSettings = new ImageArtSettings() };
		int id = 0;
		Assert.Throws<InvalidOperationException>(() => ArtGuideGenerator.GenerateImageNotes(art, group, () => ++id));
	}

	[Test]
	public void GeneratedGuidesStayInsideTwelveLanes()
	{
		BinaryImageArt art = new BinaryImageArt { Width = 2, Height = 1, Pixels = new byte[] { 1, 1 } };
		ArtGroupData group = new ArtGroupData { Id = "g", OriginLane = 10, Type = ArtGroupType.Image, ImageSettings = new ImageArtSettings { Width = 12, AnchorWidth = 4 } };
		int id = 0;
		List<MusicScoreNoteBase> notes = ArtGuideGenerator.GenerateImageNotes(art, group, () => ++id);
		Assert.IsTrue(notes.All(note => note.laneStart >= 0 && note.laneEnd <= 11));
		Assert.IsTrue(notes.All(note => note.ArtGroupId == "g"));
	}

	[Test]
	public void WoffDecoderRejectsWoff2AndTruncatedInput()
	{
		Assert.Throws<InvalidDataException>(() => Woff1Decoder.Decode(new byte[4]));
		byte[] woff2Header = new byte[44];
		woff2Header[0] = (byte)'w'; woff2Header[1] = (byte)'O'; woff2Header[2] = (byte)'F'; woff2Header[3] = (byte)'2';
		Assert.Throws<InvalidDataException>(() => Woff1Decoder.Decode(woff2Header));
	}

	[Test]
	public void Woff1DecoderRestoresARealOpenTypeFont()
	{
		byte[] woff = File.ReadAllBytes(Path.Combine(Application.dataPath, "Editor", "RobotoAscii.woff"));
		byte[] sfnt = Woff1Decoder.Decode(woff);
		Assert.Greater(sfnt.Length, woff.Length);
		Assert.AreEqual(0x00, sfnt[0]);
		Assert.AreEqual(0x01, sfnt[1]);
		Assert.AreEqual(0x00, sfnt[2]);
		Assert.AreEqual(0x00, sfnt[3]);
	}

	[Test]
	public void CustomFontVectorizerRejectsMissingCachedFont()
	{
		Assert.Throws<FileNotFoundException>(() => CustomFontArtVectorizer.Vectorize("ABC", new string('a', 64), TextArtFontMode.Outline));
	}

	[Test]
	public void RealWoffFontVectorizesInBothModes()
	{
		string hash = ArtAssetCache.Import(Path.Combine(Application.dataPath, "Editor", "RobotoAscii.woff"));
		Assert.IsNotEmpty(CustomFontArtVectorizer.Vectorize("ABC", hash, TextArtFontMode.Outline).Strokes);
		Assert.IsNotEmpty(CustomFontArtVectorizer.Vectorize("ABC", hash, TextArtFontMode.Centerline).Strokes);
	}

	[Test]
	public void CustomFontVectorizerRejectsMissingGlyph()
	{
		string hash = ArtAssetCache.Import(Path.Combine(Application.dataPath, "Editor", "RobotoAscii.woff"));
		Assert.Throws<InvalidDataException>(() => CustomFontArtVectorizer.Vectorize("中", hash, TextArtFontMode.Outline));
	}

	[Test]
	public void CustomFontVectorizerDoesNotDependOnTmpFontAssetCreation()
	{
		string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Assembly-CSharp", "Sekai", "MusicScoreMaker", "Ingame", "Utilities", "CustomFontArtVectorizer.cs"));
		string linker = File.ReadAllText(Path.Combine(Application.dataPath, "link.xml"));
		StringAssert.Contains("FontEngineBridge.LoadFontFace", source);
		StringAssert.Contains("FontEngineBridge.TryAddGlyphToTexture", source);
		StringAssert.DoesNotContain("TMP_FontAsset", source);
		StringAssert.Contains("UnityEngine.TextCoreFontEngineModule", linker);
	}

	[Test]
	public async Task CjkStrokeDataDownloadsAndPersistsInCache()
	{
		ArtStrokeData first = await TextArtVectorizer.VectorizeHersheyAndCjkAsync("文");
		Assert.IsNotEmpty(first.Strokes);
		string cachePath = Path.Combine(CjkStrokeCache.CacheRoot, "6587.json");
		Assert.IsTrue(File.Exists(cachePath));
		ArtStrokeData second = await TextArtVectorizer.VectorizeHersheyAndCjkAsync("文");
		Assert.AreEqual(first.Strokes.Count, second.Strokes.Count);
	}

	[TestCase("\u304b", "0304b", 3)]
	[TestCase("\u306a", "0306a", 4)]
	[TestCase("\u30ab", "030ab", 2)]
	[TestCase("\u30ca", "030ca", 2)]
	public async Task KanaUsesCompleteKanjiVgStrokesAndPersistentCache(string text, string code, int expectedStrokes)
	{
		var first = await TextArtVectorizer.VectorizeHersheyAndCjkAsync(text);
		Assert.AreEqual(expectedStrokes, first.Strokes.Count);
		Assert.IsTrue(first.Strokes.SelectMany(s => s).All(p => p.Y >= 0 && p.Y <= 32));
		string file = Path.Combine(CjkStrokeCache.CacheRoot, code + ".kanjivg-r20250816.svg");
		Assert.IsTrue(File.Exists(file));
		DateTime written = File.GetLastWriteTimeUtc(file);
		var second = await TextArtVectorizer.VectorizeHersheyAndCjkAsync(text);
		Assert.AreEqual(first.Strokes.Count, second.Strokes.Count);
		Assert.AreEqual(written, File.GetLastWriteTimeUtc(file));
		if (text == "\u306a")
		{
			var loop = first.Strokes.Last();
			Assert.Greater(loop.Count(p => p.Y < 10), 4, "The lower loop must not be clamped into a horizontal bar.");
			Assert.Greater(loop.Where(p => p.Y < 10).Select(p => p.Y).Distinct().Count(), 4);
		}
	}

	[Test]
	public void LocalizationTablesHaveMatchingKeysAndFormatSlots()
	{
		Dictionary<string, string>[] tables = new[] { "en", "ja", "zh-Hans" }
			.Select(language => JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Application.dataPath, "Resources", "Localization", language + ".json"))))
			.ToArray();
		CollectionAssert.AreEquivalent(tables[0].Keys, tables[1].Keys);
		CollectionAssert.AreEquivalent(tables[0].Keys, tables[2].Keys);
		Regex slots = new Regex("\\{\\d+(?:[^}]*)\\}");
		foreach (string key in tables[0].Keys)
		{
			CollectionAssert.AreEquivalent(slots.Matches(tables[0][key]).Cast<Match>().Select(match => match.Value), slots.Matches(tables[1][key]).Cast<Match>().Select(match => match.Value));
			CollectionAssert.AreEquivalent(slots.Matches(tables[0][key]).Cast<Match>().Select(match => match.Value), slots.Matches(tables[2][key]).Cast<Match>().Select(match => match.Value));
		}
	}

	[Test]
	public void LegacyRuntimeTextTracksLanguageChanges()
	{
		string original = LocalizationManager.CurrentLanguage;
		GameObject gameObject = new GameObject("LegacyLocalizedText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
		try
		{
			Text text = gameObject.GetComponent<Text>();
			text.text = "範囲選択";
			Assert.IsTrue(RuntimeLocalizationBootstrap.TryBind(text));
			LocalizationManager.SetLanguage(LocalizationManager.English);
			RuntimeLocalizationBootstrap.RefreshAll();
			Assert.AreEqual("Area select", text.text);
			LocalizationManager.SetLanguage(LocalizationManager.SimplifiedChinese);
			RuntimeLocalizationBootstrap.RefreshAll();
			Assert.AreEqual("范围选择", text.text);
		}
		finally
		{
			LocalizationManager.SetLanguage(original);
			UnityEngine.Object.DestroyImmediate(gameObject);
		}
	}

	[Test]
	public void ExistingWordingKeysUseCurrentLanguage()
	{
		string original = LocalizationManager.CurrentLanguage;
		try
		{
			LocalizationManager.SetLanguage(LocalizationManager.English);
			Assert.AreEqual("Area select", WordingManager.Get("WORD_RANGE_SELECT"));
			LocalizationManager.SetLanguage(LocalizationManager.SimplifiedChinese);
			Assert.AreEqual("范围选择", WordingManager.Get("WORD_RANGE_SELECT"));
		}
		finally
		{
			LocalizationManager.SetLanguage(original);
		}
	}

	[Test]
	public void RuntimeLocalizationReferencesExistAndStatusTextHasNoCjkLiterals()
	{
		Assert.DoesNotThrow(RuntimeLocalizationBootstrap.RefreshAll);
		Dictionary<string, string> english = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Application.dataPath, "Resources", "Localization", "en.json")));
		string[] localizedSources =
		{
			Path.Combine(Application.dataPath, "CustomMusicScoreManager", "Runtime", "UI", "ScreenLayerCustomMusicScoreManager.cs"),
			Path.Combine(Application.dataPath, "Scripts", "Assembly-CSharp", "Sekai", "MusicScoreMaker", "Ingame", "Views", "ArtToolsRuntimePanel.cs"),
			Path.Combine(Application.dataPath, "Scripts", "Assembly-CSharp", "Sekai", "MusicScoreMaker", "Ingame", "Views", "MusicScoreMakerOptionDialog.cs")
		};
		Regex keyReference = new Regex("LocalizationManager\\.(?:Get|Format)\\(\"([^\"]+)\"");
		foreach (string sourcePath in localizedSources)
		{
			string source = File.ReadAllText(sourcePath);
			foreach (Match match in keyReference.Matches(source))
			{
				string key = match.Groups[1].Value;
				if (!key.EndsWith(".", StringComparison.Ordinal)) Assert.IsTrue(english.ContainsKey(key), $"Missing localization key {key} referenced by {sourcePath}");
			}
		}

		string managerSource = File.ReadAllText(localizedSources[0]);
		Assert.IsFalse(Regex.IsMatch(managerSource, "(?:SetStatus|SetMessageBodyText)\\(\"[^\"]*[\\p{IsCJKUnifiedIdeographs}ぁ-ゟァ-ヿ]"));
		string original = LocalizationManager.CurrentLanguage;
		try
		{
			LocalizationManager.SetLanguage(LocalizationManager.English);
			Assert.AreEqual("Missing score", new CustomMusicScoreManagerItem(null, DateTime.MinValue, true, false, true, true).StatusText);
		}
		finally
		{
			LocalizationManager.SetLanguage(original);
		}
	}
}
#endif
