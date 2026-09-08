using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Unity.VectorGraphics;

namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
	public static class CjkStrokeCache
	{
		private const string ChineseBaseUrl = "https://cdn.jsdelivr.net/npm/hanzi-writer-data@2.0.1";
		private const string JapaneseBaseUrl = "https://cdn.jsdelivr.net/npm/@k1low/hanzi-writer-data-jp@0.8.0";
		private const string KanaBaseUrl = "https://cdn.jsdelivr.net/gh/KanjiVG/kanjivg@r20250816/kanji/";
		private const float GlyphSize = 28f;
		private const float Padding = 2f;
		private const float SourceSize = 1024f;

		private sealed class CharacterData
		{
			[JsonProperty("medians")]
			public List<List<List<float>>> Medians;
		}

		public static string CacheRoot => Path.Combine(Application.persistentDataPath, "ArtAssets", "cjk");

		public static bool IsSupportedCharacter(string character)
		{
			if (string.IsNullOrEmpty(character)) return false;
			int codePoint = char.ConvertToUtf32(character, 0);
			return codePoint >= 0x3400 && codePoint <= 0x9fff ||
				codePoint >= 0x20000 && codePoint <= 0x3134f ||
				codePoint >= 0x3000 && codePoint <= 0x30ff ||
				codePoint >= 0x31f0 && codePoint <= 0x31ff ||
				codePoint >= 0xff01 && codePoint <= 0xff60;
		}

		internal static async UniTask<HersheyStrokeFont.Glyph> GetAsync(string character, CancellationToken cancellationToken = default)
		{
			if (!IsSupportedCharacter(character)) throw new NotSupportedException($"No stroke source supports '{character}'.");
			Directory.CreateDirectory(CacheRoot);
			int codePoint = char.ConvertToUtf32(character, 0);
			if (codePoint >= 0x3040 && codePoint <= 0x30ff || codePoint >= 0x31f0 && codePoint <= 0x31ff)
				return await GetKanaAsync(codePoint, cancellationToken);
			string cachePath = Path.Combine(CacheRoot, char.ConvertToUtf32(character, 0).ToString("X", CultureInfo.InvariantCulture) + ".json");
			string json = File.Exists(cachePath) ? File.ReadAllText(cachePath) : null;
			if (string.IsNullOrEmpty(json))
			{
				string escaped = UnityWebRequest.EscapeURL(character);
				string[] sources = IsJapanese(character) ? new[] { JapaneseBaseUrl } : new[] { ChineseBaseUrl, JapaneseBaseUrl };
				foreach (string source in sources)
				{
					using UnityWebRequest request = UnityWebRequest.Get(source + "/" + escaped + ".json");
					UnityWebRequestAsyncOperation operation = request.SendWebRequest();
					while (!operation.isDone)
					{
						cancellationToken.ThrowIfCancellationRequested();
						await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
					}
					if (request.responseCode == 404) continue;
					if (request.result != UnityWebRequest.Result.Success) throw new IOException($"Failed to download stroke data ({request.responseCode}).");
					json = request.downloadHandler.text;
					File.WriteAllText(cachePath, json);
					break;
				}
			}

			CharacterData data = string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<CharacterData>(json);
			HersheyStrokeFont.Glyph glyph = new HersheyStrokeFont.Glyph { Width = GlyphSize + Padding * 2f };
			var points = data?.Medians?.Where(m => m != null).SelectMany(m => m).Where(p => p != null && p.Count >= 2).ToList();
			float minX = points?.Count > 0 ? Mathf.Min(0, points.Min(p => p[0])) : 0;
			float minY = points?.Count > 0 ? Mathf.Min(-124, points.Min(p => p[1])) : -124;
			float maxX = points?.Count > 0 ? Mathf.Max(SourceSize, points.Max(p => p[0])) : SourceSize;
			float maxY = points?.Count > 0 ? Mathf.Max(900, points.Max(p => p[1])) : 900;
			float sourceScale = GlyphSize / Mathf.Max(maxX - minX, maxY - minY);
			if (data?.Medians != null)
			{
				foreach (List<List<float>> median in data.Medians)
				{
					List<ArtStrokePoint> path = new List<ArtStrokePoint>();
					if (median != null)
					foreach (List<float> point in median)
					{
						if (point == null || point.Count < 2) continue;
						ArtStrokePoint next = new ArtStrokePoint(Padding + (point[0] - minX) * sourceScale, Padding + (point[1] - minY) * sourceScale);
						if (path.Count == 0 || path[path.Count - 1].X != next.X || path[path.Count - 1].Y != next.Y) path.Add(next);
					}
					if (path.Count >= 2) glyph.Paths.Add(path);
				}
			}
			if (glyph.Paths.Count == 0) throw new InvalidDataException($"No stroke data is available for '{character}'.");
			return glyph;
		}

		private static async UniTask<HersheyStrokeFont.Glyph> GetKanaAsync(int codePoint, CancellationToken cancellationToken)
		{
			string name = codePoint.ToString("x5", CultureInfo.InvariantCulture);
			string cachePath = Path.Combine(CacheRoot, name + ".kanjivg-r20250816.svg");
			if (File.Exists(cachePath)) return ParseKana(File.ReadAllText(cachePath));
			using var request = UnityWebRequest.Get(KanaBaseUrl + name + ".svg");
			request.timeout = 30;
			var operation = request.SendWebRequest();
			while (!operation.isDone) await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
			if (request.result != UnityWebRequest.Result.Success) throw new IOException($"Failed to download kana stroke data ({request.responseCode}).");
			string svg = request.downloadHandler.text;
			var glyph = ParseKana(svg);
			File.WriteAllText(cachePath, svg);
			return glyph;
		}

		private static HersheyStrokeFont.Glyph ParseKana(string svg)
		{
			// Resolve only the small internal namespace declarations in KanjiVG's DTD.
			// External entities/resources and non-path SVG content are never imported.
			var xmlSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, MaxCharactersFromEntities = 4096, MaxCharactersInDocument = 1000000 };
			using var reader = XmlReader.Create(new StringReader(svg), xmlSettings);
			var document = XDocument.Load(reader);
			XNamespace ns = "http://www.w3.org/2000/svg";
			var paths = document.Descendants(ns + "path").Where(p => ((string)p.Attribute("id"))?.Contains("-s") == true)
				.Select(p => new XElement(ns + "path", new XAttribute("d", (string)p.Attribute("d") ?? ""))).ToArray();
			if (paths.Length == 0) throw new InvalidDataException("No kana stroke paths were found.");
			var clean = new XElement(ns + "svg", new XAttribute("width", 109), new XAttribute("height", 109),
				new XAttribute("viewBox", "0 0 109 109"), new XElement(ns + "g", new XAttribute("fill", "none"), new XAttribute("stroke", "black"), paths));
			var scene = SVGParser.ImportSVG(new StringReader(clean.ToString()), 0, 1, 109, 109, false);
			var glyph = new HersheyStrokeFont.Glyph { Width = GlyphSize + Padding * 2 };
			foreach (var node in VectorUtils.WorldTransformedSceneNodes(scene.Scene.Root, scene.NodeOpacity))
			{
				if (node.Node.Shapes == null) continue;
				foreach (var shape in node.Node.Shapes)
				foreach (var contour in shape.Contours)
				{
					var path = new List<ArtStrokePoint>();
					foreach (var segment in VectorUtils.SegmentsInPath(contour.Segments, contour.Closed))
					{
						int steps = Mathf.Clamp(Mathf.CeilToInt(VectorUtils.SegmentLength(segment) / 3f), 2, 128);
						for (int i = path.Count == 0 ? 0 : 1; i <= steps; i++)
						{
							Vector2 point = node.WorldTransform * VectorUtils.Eval(segment, i / (float)steps);
							path.Add(new ArtStrokePoint(Padding + point.x / 109f * GlyphSize, Padding + (109f - point.y) / 109f * GlyphSize));
						}
					}
					if (path.Count >= 2) glyph.Paths.Add(path);
				}
			}
			if (glyph.Paths.Count == 0) throw new InvalidDataException("No kana stroke geometry was found.");
			return glyph;
		}

		private static bool IsJapanese(string character)
		{
			int codePoint = char.ConvertToUtf32(character, 0);
			return codePoint >= 0x3000 && codePoint <= 0x30ff || codePoint >= 0x31f0 && codePoint <= 0x31ff || codePoint >= 0xff01 && codePoint <= 0xff60;
		}
	}
}
