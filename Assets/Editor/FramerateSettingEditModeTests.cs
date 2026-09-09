#if UNITY_INCLUDE_TESTS
using CP;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Sekai.EditorTools.Tests
{
	public sealed class FramerateSettingEditModeTests
	{
		[TestCase("{}", 0)]
		[TestCase("{\"Use120FPS\":true}", 120)]
		[TestCase("{\"Use120FPS\":true,\"MaxFrameRate\":60}", 60)]
		[TestCase("{\"Use120FPS\":true,\"MaxFrameRate\":0}", 0)]
		[TestCase("{\"MaxFrameRate\":-1}", 0)]
		[TestCase("{\"MaxFrameRate\":2147483647}", 0)]
		public void OldAndInvalidSettingsHaveSafeLimits(string json, int expected)
		{
			Assert.That(FramerateUtility.GetConfiguredFrameRate(JsonConvert.DeserializeObject<LiveSettingData>(json)), Is.EqualTo(expected));
		}

		[Test]
		public void EverySelectableLimitSurvivesJsonRoundTrip()
		{
			foreach (int fps in FramerateUtility.FrameRateOptions)
			{
				var settings = new LiveSettingData { MaxFrameRate = fps };
				var restored = JsonConvert.DeserializeObject<LiveSettingData>(JsonConvert.SerializeObject(settings));
				Assert.That(FramerateUtility.GetConfiguredFrameRate(restored), Is.EqualTo(fps));
			}
		}

		[Test]
		public void NumericChoiceSurvivesLocalizationRefreshWithoutApplyingUnsavedLimit()
		{
			var root = new GameObject("Framerate setting test");
			root.SetActive(false);
			int previousLimit = Application.targetFrameRate;
			try
			{
				var screen = root.AddComponent<Sekai.CustomMusicScoreManager.ScreenLayerCustomMusicScoreManager>();
				var labelObject = new GameObject("Label", typeof(RectTransform));
				labelObject.transform.SetParent(root.transform);
				var label = labelObject.AddComponent<TMPro.TextMeshProUGUI>();
				labelObject.AddComponent<Sekai.Localization.LocalizedTextBinding>();
				const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
				screen.GetType().GetField("_settingMaxFrameRateLabel", flags).SetValue(screen, label);
				var setter = screen.GetType().GetMethod("SetMaxFrameRate", flags);
				setter.Invoke(screen, new object[] { 0 });
				Sekai.Localization.RuntimeLocalizationBootstrap.TryBind(label);
				setter.Invoke(screen, new object[] { 120 });
				Sekai.Localization.RuntimeLocalizationBootstrap.TryBind(label);
				Assert.That(label.text, Is.EqualTo("120 FPS"));
				Assert.That(Application.targetFrameRate, Is.EqualTo(previousLimit));
				setter.Invoke(screen, new object[] { 0 });
				Assert.That(label.text, Is.EqualTo(Sekai.Localization.LocalizationManager.Get("settings.max_fps.auto")));
			}
			finally { Object.DestroyImmediate(root); }
		}

		[Test]
		public void SceneAndResumeCallsRetainTheSavedLimit()
		{
			var cache = typeof(LiveSettingData).GetField("cachedStorage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
			object previousSettings = cache.GetValue(null);
			int previousLimit = Application.targetFrameRate;
			int previousVSync = QualitySettings.vSyncCount;
			try
			{
				cache.SetValue(null, new LiveSettingData { MaxFrameRate = 144 });
				FramerateUtility.SetFrameRate();
				Assert.That(Application.targetFrameRate, Is.EqualTo(144));
				FramerateUtility.SetFrameRate(-1);
				Assert.That(Application.targetFrameRate, Is.EqualTo(144));
				cache.SetValue(null, new LiveSettingData { MaxFrameRate = 60 });
				FramerateUtility.SetFrameRate();
				Assert.That(Application.targetFrameRate, Is.EqualTo(60));
			}
			finally
			{
				cache.SetValue(null, previousSettings);
				Application.targetFrameRate = previousLimit;
				QualitySettings.vSyncCount = previousVSync;
			}
		}

		[Test]
		public void ExplicitLimitOverridesDesktopVSyncAndCanBeChangedAgain()
		{
			int previousLimit = Application.targetFrameRate;
			int previousVSync = QualitySettings.vSyncCount;
			try
			{
				QualitySettings.vSyncCount = 1;
				FramerateUtility.SetFrameRate(120);
				Assert.That(Application.targetFrameRate, Is.EqualTo(120));
				Assert.That(QualitySettings.vSyncCount, Is.Zero);
				FramerateUtility.SetFrameRate(30);
				Assert.That(Application.targetFrameRate, Is.EqualTo(30));
			}
			finally
			{
				Application.targetFrameRate = previousLimit;
				QualitySettings.vSyncCount = previousVSync;
			}
		}
	}
}
#endif
