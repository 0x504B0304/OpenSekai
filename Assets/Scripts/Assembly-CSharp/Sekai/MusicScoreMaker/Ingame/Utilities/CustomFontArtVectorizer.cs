using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Sekai.MusicScoreMaker.Ingame.Models;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
	public static class CustomFontArtVectorizer
	{
		private const int PointSize = 64;
		private const int GlyphTextureSize = 256;
		private const int GlyphPadding = 2;
		private const float OutputLineHeight = 32f;

		public static ArtStrokeData Vectorize(string value, string cachedFontHash, TextArtFontMode mode)
		{
			string[] lines = TextArtVectorizer.NormalizeAndValidate(value, true);
			if (!ArtAssetCache.TryRead(cachedFontHash, out byte[] bytes, out string extension)) throw new FileNotFoundException("The cached font source is missing.");
			string fontPath = ArtAssetCache.Find(cachedFontHash);
			if (string.Equals(extension, ".woff", StringComparison.OrdinalIgnoreCase))
			{
				fontPath = Path.Combine(ArtAssetCache.CacheRoot, cachedFontHash + ".otf");
				if (!File.Exists(fontPath)) File.WriteAllBytes(fontPath, Woff1Decoder.Decode(bytes));
			}

			object faceHandle = FontEngineBridge.LoadFontFace(fontPath, PointSize);
			try
			{
				Dictionary<int, RenderedGlyph> glyphs = new Dictionary<int, RenderedGlyph>();
				foreach (string line in lines)
				foreach (int codePoint in EnumerateCodePoints(line))
				{
					if (glyphs.ContainsKey(codePoint)) continue;
					if (!FontEngineBridge.TryGetGlyphIndex(faceHandle, (uint)codePoint, out uint glyphIndex) || glyphIndex == 0)
						throw new InvalidDataException($"The selected font does not contain {char.ConvertFromUtf32(codePoint)}.");
					glyphs.Add(codePoint, RenderGlyph(faceHandle, glyphIndex));
				}
				return BuildStrokeData(lines, glyphs, mode);
			}
			finally
			{
				FontEngineBridge.UnloadFontFace(faceHandle);
			}
		}

		private static RenderedGlyph RenderGlyph(object faceHandle, uint glyphIndex)
		{
			Texture2D texture = new Texture2D(GlyphTextureSize, GlyphTextureSize, TextureFormat.Alpha8, false, true);
			try
			{
				List<GlyphRect> freeRects = new List<GlyphRect> { new GlyphRect(0, 0, GlyphTextureSize, GlyphTextureSize) };
				List<GlyphRect> usedRects = new List<GlyphRect>();
				if (!FontEngineBridge.TryAddGlyphToTexture(faceHandle, glyphIndex, GlyphPadding, GlyphPackingMode.BestShortSideFit,
					freeRects, usedRects, GlyphRenderMode.SMOOTH_HINTED, texture, out Glyph glyph) || glyph == null)
				{
					throw new InvalidDataException($"Unity FontEngine could not rasterize glyph {glyphIndex}.");
				}

				texture.Apply(false, false);
				return new RenderedGlyph
				{
					Metrics = glyph.metrics,
					Bitmap = ReadGlyphBitmap(texture.GetPixels32(), texture.width, glyph.glyphRect)
				};
			}
			finally
			{
				if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
				else UnityEngine.Object.DestroyImmediate(texture);
			}
		}

		private static ArtStrokeData BuildStrokeData(string[] lines, IReadOnlyDictionary<int, RenderedGlyph> glyphs, TextArtFontMode mode)
		{
			ArtStrokeData result = new ArtStrokeData { MinX = 0, MinY = float.MaxValue, MaxY = float.MinValue };
			float scale = OutputLineHeight / PointSize;
			for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
			{
				float cursorX = 0;
				float baselineY = (lines.Length - 1 - lineIndex) * OutputLineHeight;
				foreach (int codePoint in EnumerateCodePoints(lines[lineIndex]))
				{
					RenderedGlyph glyph = glyphs[codePoint];
					List<List<Vector2>> paths = mode == TextArtFontMode.Centerline ? BitmapStrokeTracer.TraceCenterlines(glyph.Bitmap) : BitmapStrokeTracer.TraceOutlines(glyph.Bitmap);
					foreach (List<Vector2> sourcePath in paths)
					{
						List<ArtStrokePoint> path = new List<ArtStrokePoint>(sourcePath.Count);
						foreach (Vector2 point in sourcePath)
						{
							float x = cursorX + (glyph.Metrics.horizontalBearingX + point.x) * scale;
							float y = baselineY + (glyph.Metrics.horizontalBearingY - glyph.Bitmap.GetLength(1) + point.y) * scale;
							path.Add(new ArtStrokePoint(x, y));
							result.MinY = Mathf.Min(result.MinY, y);
							result.MaxY = Mathf.Max(result.MaxY, y);
						}
						if (path.Count >= 2) result.Strokes.Add(path);
					}
					cursorX += glyph.Metrics.horizontalAdvance * scale;
				}
				result.MaxX = Mathf.Max(result.MaxX, cursorX);
			}
			if (result.Strokes.Count == 0) { result.MinY = 0; result.MaxY = lines.Length * OutputLineHeight; }
			return result;
		}

		private sealed class RenderedGlyph
		{
			public GlyphMetrics Metrics;
			public bool[,] Bitmap;
		}

		private static class FontEngineBridge
		{
			private static Type _handleType;
			private static MethodInfo _loadMethod;
			private static MethodInfo _glyphIndexMethod;
			private static MethodInfo _addGlyphMethod;
			private static MethodInfo _unloadMethod;

			private static Type HandleType => _handleType ??= typeof(FontEngine).Assembly.GetType("UnityEngine.TextCore.LowLevel.FontFaceHandle")
				?? throw new TypeLoadException("Unity FontFaceHandle reflection metadata is unavailable.");

			public static object LoadFontFace(string path, int pointSize)
			{
				object[] arguments = { path, (float)pointSize, 0, null };
				_loadMethod ??= FindMethod("LoadFontFace", 4, typeof(string));
				FontEngineError result = (FontEngineError)_loadMethod.Invoke(null, arguments);
				if (result != FontEngineError.Success || arguments[3] == null)
					throw new InvalidDataException($"Unity FontEngine could not load the font ({result}).");
				return arguments[3];
			}

			public static bool TryGetGlyphIndex(object faceHandle, uint unicode, out uint glyphIndex)
			{
				object[] arguments = { faceHandle, unicode, 0u };
				_glyphIndexMethod ??= FindMethod("TryGetGlyphIndex", 3, HandleType);
				bool result = (bool)_glyphIndexMethod.Invoke(null, arguments);
				glyphIndex = (uint)arguments[2];
				return result;
			}

			public static bool TryAddGlyphToTexture(object faceHandle, uint glyphIndex, int padding, GlyphPackingMode packingMode,
				List<GlyphRect> freeRects, List<GlyphRect> usedRects, GlyphRenderMode renderMode, Texture2D texture, out Glyph glyph)
			{
				object[] arguments = { faceHandle, glyphIndex, padding, packingMode, freeRects, usedRects, renderMode, texture, null };
				_addGlyphMethod ??= FindMethod("TryAddGlyphToTexture", 9, HandleType);
				bool result = (bool)_addGlyphMethod.Invoke(null, arguments);
				glyph = arguments[8] as Glyph;
				return result;
			}

			public static void UnloadFontFace(object faceHandle)
			{
				if (faceHandle == null) return;
				_unloadMethod ??= FindMethod("UnloadFontFace", 1, HandleType);
				_unloadMethod.Invoke(null, new[] { faceHandle });
			}

			private static MethodInfo FindMethod(string name, int parameterCount, Type firstParameterType)
			{
				foreach (MethodInfo method in typeof(FontEngine).GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
				{
					ParameterInfo[] parameters = method.GetParameters();
					if (method.Name == name && parameters.Length == parameterCount && parameters[0].ParameterType == firstParameterType) return method;
				}
				throw new MissingMethodException(typeof(FontEngine).FullName, name);
			}
		}

		private static bool[,] ReadGlyphBitmap(Color32[] pixels, int atlasWidth, GlyphRect rect)
		{
			bool[,] result = new bool[rect.width, rect.height];
			for (int y = 0; y < rect.height; y++)
			for (int x = 0; x < rect.width; x++)
			{
				Color32 pixel = pixels[(rect.y + y) * atlasWidth + rect.x + x];
				result[x, y] = pixel.a >= 96 || pixel.r >= 96;
			}
			return result;
		}

		private static IEnumerable<int> EnumerateCodePoints(string value)
		{
			for (int i = 0; i < (value?.Length ?? 0); i++)
			{
				if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) yield return char.ConvertToUtf32(value[i], value[++i]);
				else yield return value[i];
			}
		}
	}

	internal static class BitmapStrokeTracer
	{
		private static readonly Vector2Int[] Neighbors =
		{
			new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(0,1), new Vector2Int(-1,1),
			new Vector2Int(-1,0), new Vector2Int(-1,-1), new Vector2Int(0,-1), new Vector2Int(1,-1)
		};

		public static List<List<Vector2>> TraceOutlines(bool[,] source)
		{
			int width = source.GetLength(0), height = source.GetLength(1);
			bool[,] boundary = new bool[width, height];
			for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
			{
				if (!source[x, y]) continue;
				for (int n = 0; n < 4; n++)
				{
					int nx = x + Neighbors[n * 2].x, ny = y + Neighbors[n * 2].y;
					if (nx < 0 || ny < 0 || nx >= width || ny >= height || !source[nx, ny]) { boundary[x, y] = true; break; }
				}
			}
			return TracePixelPaths(boundary);
		}

		public static List<List<Vector2>> TraceCenterlines(bool[,] source)
		{
			bool[,] skeleton = (bool[,])source.Clone();
			int width = skeleton.GetLength(0), height = skeleton.GetLength(1);
			List<Vector2Int> remove = new List<Vector2Int>();
			bool changed;
			do
			{
				changed = false;
				for (int pass = 0; pass < 2; pass++)
				{
					remove.Clear();
					for (int y = 1; y < height - 1; y++)
					for (int x = 1; x < width - 1; x++)
					{
						if (!skeleton[x, y]) continue;
						bool[] p = GetNeighbors(skeleton, x, y);
						int count = 0, transitions = 0;
						for (int i = 0; i < 8; i++) { if (p[i]) count++; if (!p[i] && p[(i + 1) % 8]) transitions++; }
						if (count < 2 || count > 6 || transitions != 1) continue;
						bool condition = pass == 0 ? !p[0] || !p[2] || !p[4] : !p[0] || !p[2] || !p[6];
						bool condition2 = pass == 0 ? !p[2] || !p[4] || !p[6] : !p[0] || !p[4] || !p[6];
						if (condition && condition2) remove.Add(new Vector2Int(x, y));
					}
					foreach (Vector2Int point in remove) skeleton[point.x, point.y] = false;
					changed |= remove.Count > 0;
				}
			}
			while (changed);
			return TracePixelPaths(skeleton);
		}

		private static bool[] GetNeighbors(bool[,] pixels, int x, int y)
		{
			bool[] result = new bool[8];
			for (int i = 0; i < 8; i++) result[i] = pixels[x + Neighbors[i].x, y + Neighbors[i].y];
			return result;
		}

		private static List<List<Vector2>> TracePixelPaths(bool[,] pixels)
		{
			int width = pixels.GetLength(0), height = pixels.GetLength(1);
			bool[,] visited = new bool[width, height];
			List<List<Vector2>> result = new List<List<Vector2>>();
			for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
			{
				if (!pixels[x, y] || visited[x, y]) continue;
				List<Vector2> path = new List<Vector2>();
				Vector2Int current = new Vector2Int(x, y);
				while (true)
				{
					visited[current.x, current.y] = true;
					path.Add(current);
					bool found = false;
					foreach (Vector2Int offset in Neighbors)
					{
						int nx = current.x + offset.x, ny = current.y + offset.y;
						if (nx < 0 || ny < 0 || nx >= width || ny >= height || !pixels[nx, ny] || visited[nx, ny]) continue;
						current = new Vector2Int(nx, ny); found = true; break;
					}
					if (!found) break;
				}
				if (path.Count >= 2) result.Add(Simplify(path));
			}
			return result;
		}

		private static List<Vector2> Simplify(List<Vector2> source)
		{
			if (source.Count <= 2) return source;
			List<Vector2> result = new List<Vector2> { source[0] };
			Vector2 previousDirection = (source[1] - source[0]).normalized;
			for (int i = 2; i < source.Count; i++)
			{
				Vector2 direction = (source[i] - source[i - 1]).normalized;
				if (Vector2.Dot(previousDirection, direction) < 0.999f) result.Add(source[i - 1]);
				previousDirection = direction;
			}
			result.Add(source[source.Count - 1]);
			return result;
		}
	}
}
