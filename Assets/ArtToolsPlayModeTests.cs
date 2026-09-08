#if UNITY_INCLUDE_TESTS
using Newtonsoft.Json;
using NUnit.Framework;
using Sekai.Live;
using Sekai.Localization;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Utilities;

public sealed class ArtToolsPlayModeTests
{
	[Test]
	public void LanguageChangesImmediately()
	{
		string original = LocalizationManager.CurrentLanguage;
		int changes = 0;
		void OnChanged() => changes++;
		LocalizationManager.LanguageChanged += OnChanged;
		LocalizationManager.SetLanguage(LocalizationManager.English);
		Assert.AreEqual("Text art", LocalizationManager.Get("art.text"));
		LocalizationManager.SetLanguage(LocalizationManager.Japanese);
		Assert.AreEqual("文字譜面", LocalizationManager.Get("art.text"));
		Assert.GreaterOrEqual(changes, original == LocalizationManager.Japanese ? 1 : 2);
		LocalizationManager.LanguageChanged -= OnChanged;
		LocalizationManager.SetLanguage(original);
	}

	[Test]
	public void GeneratedArtSurvivesSaveAndReload()
	{
		MusicScoreMakerData source = new MusicScoreMakerData();
		ArtGroupData group = new ArtGroupData
		{
			Id = "playmode-group",
			Type = ArtGroupType.Image,
			OriginLane = 2,
			OriginTicks = 480,
			ImageSettings = new ImageArtSettings { ImageAssetHash = new string('a', 64), Width = 8f }
		};
		BinaryImageArt image = new BinaryImageArt { Width = 2, Height = 2, Pixels = new byte[] { 1, 0, 1, 1 } };
		int id = 0;
		source.ArtGroups.Add(group);
		source.NoteList.AddRange(ArtGuideGenerator.GenerateImageNotes(image, group, () => ++id));

		string json = JsonConvert.SerializeObject(source);
		MusicScoreMakerData loaded = JsonConvert.DeserializeObject<MusicScoreMakerData>(json);
		loaded.MigrateToCurrentVersion();
		Assert.AreEqual(1, loaded.ArtGroups.Count);
		Assert.AreEqual("playmode-group", loaded.ArtGroups[0].Id);
		Assert.IsNotEmpty(loaded.NoteList);
		Assert.IsTrue(loaded.NoteList.TrueForAll(note => note.ArtGroupId == "playmode-group"));
	}
}
#endif
