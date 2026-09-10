using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.TextCore.LowLevel;
#endif

namespace Sekai
{
	public static class HighQualityDynamicFontProvider
	{
		private const int SamplingPointSize = 90;

		private const int AtlasPadding = 9;

		private const int AtlasSize = 2048;

		private static readonly Dictionary<TMP_FontAsset, TMP_FontAsset> RuntimeFontAssets =
			new Dictionary<TMP_FontAsset, TMP_FontAsset>();

		private static int currentPointSize = SamplingPointSize;

		private static readonly Dictionary<Font, TMP_FontAsset> SourceFonts = new Dictionary<Font, TMP_FontAsset>();

		// Menu fonts own their native metrics and never borrow the gameplay atlases.
		public static TMP_FontAsset GetFromSource(Font source)
		{
			if (source == null) return null;
			if (SourceFonts.TryGetValue(source, out var cached) && cached != null) return cached;
			var font = TMP_FontAsset.CreateFontAsset(source, currentPointSize, AtlasPadding,
				UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, AtlasSize, AtlasSize,
				AtlasPopulationMode.Dynamic, true);
			font.name = source.name + " Menu SDF";
			font.hideFlags = HideFlags.HideAndDontSave;
			font.material.hideFlags = HideFlags.HideAndDontSave;
			foreach (var atlas in font.atlasTextures) atlas.hideFlags = HideFlags.HideAndDontSave;
			SourceFonts[source] = font;
			RuntimeFontAssets[font] = font;
			return font;
		}

		public static int CalculateSamplingPointSize(float dpiScale, int width, int height)
		{
			float renderScale = Mathf.Min(width / 1920f, height / 1080f);
			float scale = Mathf.Clamp(Mathf.Max(dpiScale, renderScale), 1f, 3f);
			return Mathf.CeilToInt(SamplingPointSize * scale / 15f) * 15;
		}

#if UNITY_6000_0_OR_NEWER
		public static void Resample(int pointSize)
		{
			currentPointSize = Mathf.Clamp(pointSize, SamplingPointSize, SamplingPointSize * 3);
			foreach (var entry in RuntimeFontAssets)
			{
				TMP_FontAsset font = entry.Value;
				if (font == null || FontEngine.LoadFontFace(font.sourceFontFile, currentPointSize) != FontEngineError.Success)
					continue;

				var characters = new uint[font.characterTable.Count];
				for (int i = 0; i < characters.Length; i++) characters[i] = font.characterTable[i].unicode;

				// Only clear owned runtime textures; bundled PNG atlases must never be reset.
				// Keep padding and material identity stable so TMP fallback presets stay valid.
				font.faceInfo = FontEngine.GetFaceInfo();
				PreserveLayoutMetrics(entry.Key, font);
				font.ClearFontAssetData();
				if (characters.Length > 0) font.TryAddCharacters(characters);
			}

			// Includes inactive panels so reopening them cannot reuse stale atlas UVs.
			foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				text.havePropertiesChanged = true;
				if (text.isActiveAndEnabled) text.ForceMeshUpdate(false, true);
			}
		}

		private static void PreserveLayoutMetrics(TMP_FontAsset original, TMP_FontAsset runtime)
		{
			// The bundled Rodin assets have authored UI metrics. Reading the OTF again
			// uses taller native ascenders/descenders, which makes TMP's Ellipsis mode
			// discard entire captions in fixed-height rows. Sampling may change glyph
			// resolution, but must retain these metrics in the original em proportions.
			var source = original.faceInfo;
			if (source.pointSize <= 0) return;
			var face = runtime.faceInfo;
			float ratio = face.pointSize / (float)source.pointSize;
			face.scale = source.scale;
			face.lineHeight = source.lineHeight * ratio;
			face.ascentLine = source.ascentLine * ratio;
			face.capLine = source.capLine * ratio;
			face.meanLine = source.meanLine * ratio;
			face.baseline = source.baseline * ratio;
			face.descentLine = source.descentLine * ratio;
			face.superscriptOffset = source.superscriptOffset * ratio;
			face.superscriptSize = source.superscriptSize;
			face.subscriptOffset = source.subscriptOffset * ratio;
			face.subscriptSize = source.subscriptSize;
			face.underlineOffset = source.underlineOffset * ratio;
			face.underlineThickness = source.underlineThickness * ratio;
			face.strikethroughOffset = source.strikethroughOffset * ratio;
			face.strikethroughThickness = source.strikethroughThickness * ratio;
			face.tabWidth = source.tabWidth * ratio;
			runtime.faceInfo = face;
		}
#endif

		public static TMP_FontAsset Get(TMP_FontAsset originalFontAsset)
		{
#if UNITY_6000_0_OR_NEWER
			if (originalFontAsset == null)
			{
				return null;
			}

			if (RuntimeFontAssets.TryGetValue(originalFontAsset, out TMP_FontAsset runtimeFontAsset) &&
				runtimeFontAsset != null)
			{
				return runtimeFontAsset;
			}

			Font sourceFont = originalFontAsset.sourceFontFile;
			if (sourceFont == null)
			{
				Debug.LogWarning($"Unable to create a high-quality font asset for {originalFontAsset.name}: source font is missing.");
				return originalFontAsset;
			}

			runtimeFontAsset = TMP_FontAsset.CreateFontAsset(
				sourceFont,
				currentPointSize,
				AtlasPadding,
				GlyphRenderMode.SDFAA,
				AtlasSize,
				AtlasSize,
				AtlasPopulationMode.Dynamic,
				true);

			if (runtimeFontAsset == null)
			{
				Debug.LogWarning($"Unable to create a high-quality font asset for {originalFontAsset.name}; using the bundled fallback.");
				return originalFontAsset;
			}

			PreserveLayoutMetrics(originalFontAsset, runtimeFontAsset);
			runtimeFontAsset.name = originalFontAsset.name + " Runtime HQ";
			runtimeFontAsset.hideFlags = HideFlags.HideAndDontSave;
			RuntimeFontAssets[originalFontAsset] = runtimeFontAsset;
			return runtimeFontAsset;
#else
			return originalFontAsset;
#endif
		}
	}
}
