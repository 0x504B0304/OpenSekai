using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
	public static class TextArtVectorizer
	{
		private const float LineHeight = 32f;

		public static string[] NormalizeAndValidate(string value, bool customFont = false)
		{
			string normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormC).Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
			string[] lines = normalized.Split('\n');
			if (lines.Length > 8) throw new ArgumentException("A maximum of 8 lines is supported.", nameof(value));
			bool hasText = false;
			foreach (string line in lines)
			{
				int count = new StringInfo(line).LengthInTextElements;
				if (count > 32) throw new ArgumentException("A maximum of 32 Unicode characters per line is supported.", nameof(value));
				if (count > 0) hasText = true;
				if (!customFont)
				foreach (string element in EnumerateTextElements(line))
				{
					int codePoint = char.ConvertToUtf32(element, 0);
					if (codePoint > 126 && element != "\u3000" && !CjkStrokeCache.IsSupportedCharacter(element))
						throw new NotSupportedException($"The text contains an unsupported character: '{element}'.");
				}
			}
			if (!hasText) throw new ArgumentException("Text is required.", nameof(value));
			return lines;
		}

		public static async UniTask<ArtStrokeData> VectorizeHersheyAndCjkAsync(string value, CancellationToken cancellationToken = default)
		{
			string[] lines = NormalizeAndValidate(value);
			Dictionary<string, HersheyStrokeFont.Glyph> glyphs = new Dictionary<string, HersheyStrokeFont.Glyph>();
			foreach (string line in lines)
			foreach (string element in EnumerateTextElements(line))
			{
				if (glyphs.ContainsKey(element)) continue;
				if (element == "\u3000") glyphs[element] = new HersheyStrokeFont.Glyph { Width = LineHeight };
				else if (element.Length == 1 && element[0] <= 126) glyphs[element] = HersheyStrokeFont.Get(element[0]);
				else glyphs[element] = await CjkStrokeCache.GetAsync(element, cancellationToken);
			}

			ArtStrokeData result = new ArtStrokeData { MinX = 0, MinY = float.MaxValue, MaxY = float.MinValue };
			for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
			{
				float cursorX = 0;
				float offsetY = (lines.Length - 1 - lineIndex) * LineHeight;
				foreach (string element in EnumerateTextElements(lines[lineIndex]))
				{
					HersheyStrokeFont.Glyph glyph = glyphs[element];
					foreach (List<ArtStrokePoint> glyphPath in glyph.Paths)
					{
						List<ArtStrokePoint> path = new List<ArtStrokePoint>(glyphPath.Count);
						foreach (ArtStrokePoint point in glyphPath)
						{
							ArtStrokePoint next = new ArtStrokePoint(cursorX + point.X, offsetY + point.Y);
							path.Add(next);
							result.MinY = Math.Min(result.MinY, next.Y);
							result.MaxY = Math.Max(result.MaxY, next.Y);
						}
						result.Strokes.Add(path);
					}
					cursorX += glyph.Width;
				}
				result.MaxX = Math.Max(result.MaxX, cursorX);
			}
			if (result.Strokes.Count == 0) { result.MinY = 0; result.MaxY = lines.Length * LineHeight; }
			return result;
		}

		private static IEnumerable<string> EnumerateTextElements(string value)
		{
			TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value ?? string.Empty);
			while (enumerator.MoveNext()) yield return enumerator.GetTextElement();
		}
	}
}
