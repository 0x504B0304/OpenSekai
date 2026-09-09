#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Sekai.EditorTools.Tests
{
	public sealed class HighQualityDynamicFontProviderEditModeTests
	{
#if UNITY_6000_0_OR_NEWER
		[SetUp]
		public void ResetSampling() => HighQualityDynamicFontProvider.Resample(90);

		[TestCase(1f, 1920, 1080, 90)]
		[TestCase(1.5f, 1920, 1080, 135)]
		[TestCase(2f, 1920, 1080, 180)]
		[TestCase(1f, 3840, 2160, 180)]
		[TestCase(4f, 7680, 4320, 270)]
		public void SamplingTracksDpiAndRenderSize(float dpi, int width, int height, int expected)
		{
			Assert.That(HighQualityDynamicFontProvider.CalculateSamplingPointSize(dpi, width, height), Is.EqualTo(expected));
		}

		[TestCase("DB", false)]
		[TestCase("EB", false)]
		[TestCase("DB", true)]
		[TestCase("EB", true)]
		public void MenuCaptionsRemainVisibleAtEverySamplingSize(string weight, bool reopen)
		{
			var original = Resources.Load<TMP_FontAsset>($"font/FOT-RodinNTLGPro-{weight} SDF_Dynamic");
			var font = HighQualityDynamicFontProvider.Get(original);
			var shell = Object.Instantiate(Resources.Load<TMP_FontAsset>($"font/FOT-RodinNTLGPro-{weight} SDF_Base"));
			shell.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { font };
			var root = new GameObject("Menu caption regression", typeof(Canvas));
			var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
			go.transform.SetParent(root.transform, false);
			try
			{
				var label = go.GetComponent<TextMeshProUGUI>();
				label.font = shell;
				label.fontStyle = weight == "EB" ? FontStyles.Bold : FontStyles.Normal;
				label.overflowMode = TextOverflowModes.Ellipsis;
				label.textWrappingMode = TextWrappingModes.NoWrap;
				foreach (int pointSize in new[] { 90, 135, 180, 270, 90 })
				{
					if (reopen) root.SetActive(false);
					HighQualityDynamicFontProvider.Resample(pointSize);
					if (reopen) root.SetActive(true);
					foreach (bool title in new[] { false, true })
					{
						label.text = title ? "设置" : "背景音乐音量";
						label.fontSize = title ? 36 : 23;
						label.rectTransform.sizeDelta = new Vector2(600, title ? 46 : 30);
						label.ForceMeshUpdate();
						Assert.That(label.isTextTruncated, Is.False, $"{weight} sampling {pointSize}, title={title}");
						Assert.That(label.textInfo.characterCount, Is.EqualTo(label.text.Length));
						for (int i = 0; i < label.textInfo.characterCount; i++)
							Assert.That(label.textInfo.characterInfo[i].isVisible, Is.True);
					}
				}
			}
			finally
			{
				Object.DestroyImmediate(root);
				shell.atlasTextures = System.Array.Empty<Texture2D>();
				shell.material = null;
				Object.DestroyImmediate(shell);
			}
		}

		[TestCase("DB")]
		[TestCase("EB")]
		public void ResamplingRetainsFallbackReferencesAndRebuildsVisibleText(string weight)
		{
			var original = Resources.Load<TMP_FontAsset>($"font/FOT-RodinNTLGPro-{weight} SDF_Dynamic");
			var font = HighQualityDynamicFontProvider.Get(original);
			var shell = Object.Instantiate(Resources.Load<TMP_FontAsset>($"font/FOT-RodinNTLGPro-{weight} SDF_Base"));
			shell.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { font };
			var root = new GameObject("DPI font regression", typeof(Canvas));
			var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
			go.transform.SetParent(root.transform, false);
			try
			{
				var label = go.GetComponent<TextMeshProUGUI>();
				label.font = shell;
				label.text = "OpenSekai 本地谱面 设置";
				label.rectTransform.sizeDelta = new Vector2(1600, 200);
				label.ForceMeshUpdate();
				float originalAdvance = font.characterLookupTable['O'].glyph.metrics.horizontalAdvance / font.faceInfo.pointSize;
				var material = font.material;
				foreach (int pointSize in new[] { 135, 180, 90 })
				{
					HighQualityDynamicFontProvider.Resample(pointSize);
					Assert.That(HighQualityDynamicFontProvider.Get(original), Is.SameAs(font));
					Assert.That(font.material, Is.SameAs(material));
					Assert.That(font.faceInfo.pointSize, Is.EqualTo(pointSize));
					Assert.That(font.characterLookupTable['O'].glyph.metrics.horizontalAdvance / pointSize,
						Is.EqualTo(originalAdvance).Within(0.03f), "Layout must not grow when sampling grows.");
					Assert.That(label.textInfo.characterCount, Is.EqualTo(label.text.Length));
					for (int i = 0; i < label.textInfo.characterCount; i++)
					{
						if (char.IsWhiteSpace(label.text[i])) continue;
						Assert.That(label.textInfo.characterInfo[i].fontAsset, Is.SameAs(font));
						Assert.That(label.textInfo.characterInfo[i].isVisible, Is.True);
					}
					Assert.That(font.atlasTexture.GetPixels32(), Has.Some.Matches<Color32>(c => c.a > 0 && c.a < 255),
						"Regenerated atlas must contain distance gradients, not blank glyph rectangles.");
				}
			}
			finally
			{
				Object.DestroyImmediate(root);
				// The clone borrows the bundled atlas; it does not own that texture.
				shell.atlasTextures = System.Array.Empty<Texture2D>();
				shell.material = null;
				Object.DestroyImmediate(shell);
			}
		}
#endif
		[TestCase("font/FOT-RodinNTLGPro-DB SDF_Dynamic")]
		[TestCase("font/FOT-RodinNTLGPro-EB SDF_Dynamic")]
		public void GetCreatesHighResolutionFontWithUiGlyphs(string resourcePath)
		{
			TMP_FontAsset bundledFont = Resources.Load<TMP_FontAsset>(resourcePath);

			Assert.That(bundledFont, Is.Not.Null);

			TMP_FontAsset runtimeFont = HighQualityDynamicFontProvider.Get(bundledFont);

			Assert.That(runtimeFont, Is.Not.Null);
#if UNITY_6000_0_OR_NEWER
			Assert.That(runtimeFont, Is.Not.SameAs(bundledFont));
			Assert.That(runtimeFont.faceInfo.pointSize, Is.EqualTo(90));
			Assert.That(runtimeFont.atlasWidth, Is.EqualTo(2048));
			Assert.That(runtimeFont.atlasHeight, Is.EqualTo(2048));
			Assert.That(runtimeFont.atlasPadding, Is.EqualTo(9));
#endif
			const string caption = "OpenSekai 本地谱面 设置 刷新 导入";
			// TMP returns false when every requested glyph is already cached. Other
			// UI tests can legitimately warm this shared runtime font before this case.
			runtimeFont.TryAddCharacters(caption);
			foreach (char character in caption)
				Assert.That(runtimeFont.HasCharacter(character), Is.True, $"Missing UI glyph: {character}");
		}
	}
}
#endif
