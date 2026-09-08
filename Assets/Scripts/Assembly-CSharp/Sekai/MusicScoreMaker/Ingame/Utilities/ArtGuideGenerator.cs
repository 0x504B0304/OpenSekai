using System;
using System.Collections.Generic;
using Sekai.Live;
using Sekai.MusicScoreMaker.Ingame.Models;
using UnityEngine;

namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
	public readonly struct ArtStrokePoint
	{
		public readonly float X;
		public readonly float Y;

		public ArtStrokePoint(float x, float y)
		{
			X = x;
			Y = y;
		}
	}

	public sealed class ArtStrokeData
	{
		public readonly List<List<ArtStrokePoint>> Strokes = new List<List<ArtStrokePoint>>();
		public float MinX;
		public float MaxX;
		public float MinY;
		public float MaxY;
	}

	public readonly struct ImageArtRun
	{
		public readonly int Column;
		public readonly int StartRow;
		public readonly int EndRow;

		public ImageArtRun(int column, int startRow, int endRow)
		{
			Column = column;
			StartRow = startRow;
			EndRow = endRow;
		}
	}

	public sealed class BinaryImageArt
	{
		public int Width;
		public int Height;
		public byte[] Pixels = Array.Empty<byte>();
		public int ForegroundPixelCount;
	}

	public static class ArtGuideGenerator
	{
		public const int MaxNodes = 8000;
		public const int MaxImageGridSize = 1024;
		public const float TextTicksPerLane = 96f;
		private const int LaneMin = 0;
		private const int LaneMax = 11;

		public static BinaryImageArt RasterizeImage(Texture2D source, ImageArtSettings settings)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (settings == null) throw new ArgumentNullException(nameof(settings));
			if (!float.IsFinite(settings.Width) || settings.Width <= 0 || !float.IsFinite(settings.AnchorWidth) || settings.AnchorWidth <= 0 || !float.IsFinite(settings.RowSpacingTicks) || settings.RowSpacingTicks <= 0) throw new ArgumentException("Image dimensions must be positive finite numbers.");
			Color32[] pixels = source.GetPixels32();
			if (!TryGetForegroundBounds(pixels, source.width, source.height, settings, out RectInt bounds))
			{
				return new BinaryImageArt();
			}

			int width = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(1f, settings.Width) / Mathf.Max(0.01f, settings.AnchorWidth)), 1, MaxImageGridSize);
			int height = Mathf.Max(1, Mathf.RoundToInt(width * bounds.height / (float)bounds.width));
			if (height > MaxImageGridSize)
			{
				width = Mathf.Max(1, Mathf.RoundToInt(width * MaxImageGridSize / (float)height));
				height = MaxImageGridSize;
			}

			byte[] result = new byte[width * height];
			int count = 0;
			for (int y = 0; y < height; y++)
			{
				float sourceY = bounds.yMin + (y + 0.5f) * bounds.height / height;
				int iy = Mathf.Clamp(Mathf.FloorToInt(sourceY), bounds.yMin, bounds.yMax - 1);
				for (int x = 0; x < width; x++)
				{
					float sourceX = bounds.xMin + (x + 0.5f) * bounds.width / width;
					int ix = Mathf.Clamp(Mathf.FloorToInt(sourceX), bounds.xMin, bounds.xMax - 1);
					Color32 pixel = pixels[iy * source.width + ix];
					if (!IsForeground(pixel, settings.Threshold, settings.LightForeground)) continue;
					result[y * width + x] = 1;
					count++;
				}
			}
			return new BinaryImageArt { Width = width, Height = height, Pixels = result, ForegroundPixelCount = count };
		}

		public static List<ImageArtRun> GetVerticalRuns(BinaryImageArt art)
		{
			List<ImageArtRun> runs = new List<ImageArtRun>();
			if (art == null || art.Width <= 0 || art.Height <= 0 || art.Pixels == null) return runs;
			for (int column = 0; column < art.Width; column++)
			{
				int startRow = -1;
				for (int row = 0; row <= art.Height; row++)
				{
					bool filled = row < art.Height && art.Pixels[row * art.Width + column] != 0;
					if (filled)
					{
						if (startRow < 0) startRow = row;
						continue;
					}
					if (startRow < 0) continue;
					runs.Add(new ImageArtRun(column, startRow, row - 1));
					startRow = -1;
				}
			}
			return runs;
		}

		public static List<MusicScoreNoteBase> GenerateImageNotes(BinaryImageArt art, ArtGroupData group, Func<int> getNewId)
		{
			if (group?.ImageSettings == null) throw new ArgumentNullException(nameof(group));
			if (group.ImageSettings.SliceMode == ImageArtSliceMode.HorizontalContours)
			{
				var contours = ImageArtContourGenerator.Generate(art, group.ImageSettings.AutoEase);
				var result = new List<MusicScoreNoteBase>();
				float xStep = Mathf.Min(group.ImageSettings.Width * group.ScaleX, 12f - group.LaneOrigin) / Mathf.Max(1, art.Width);
				float yStep = Mathf.Max(1f, group.ImageSettings.RowSpacingTicks) * Mathf.Max(.01f, group.ScaleY);
				foreach (var contour in contours)
				{
					var anchors = new List<GuideAnchor>(contour.Count);
					long previousTick = group.OriginTicks - 1;
					foreach (var point in contour)
					{
						long tick = Math.Max(previousTick + 1, group.OriginTicks + (long)Math.Round(point.Row * yStep));
						anchors.Add(new GuideAnchor(group.LaneOrigin + point.Left * xStep, group.LaneOrigin + point.Right * xStep, tick, point.LineType));
						previousTick = tick;
					}
					AddGuideChain(result, group, getNewId, anchors);
				}
				return result;
			}
			List<ImageArtRun> runs = GetVerticalRuns(art);
			EnsureNodeLimit(runs.Count * 2);
			List<MusicScoreNoteBase> notes = new List<MusicScoreNoteBase>(runs.Count * 2);
			float laneStep = Mathf.Min(group.ImageSettings.Width * group.ScaleX, 12f - group.LaneOrigin) / Mathf.Max(1, art.Width);
			float rowTicks = Mathf.Max(1f, group.ImageSettings.RowSpacingTicks) * Mathf.Max(0.01f, group.ScaleY);
			foreach (ImageArtRun run in runs)
			{
				float laneStart = group.LaneOrigin + run.Column * laneStep;
				float laneEnd = laneStart + laneStep;
				long startTicks = group.OriginTicks + (long)Mathf.Round(run.StartRow * rowTicks);
				long endTicks = group.OriginTicks + (long)Mathf.Round((run.EndRow + 1) * rowTicks);
				AddGuideChain(notes, group, getNewId, new[]
				{
					new GuideAnchor(laneStart, laneEnd, startTicks),
					new GuideAnchor(laneStart, laneEnd, Math.Max(startTicks + 1, endTicks))
				});
			}
			return notes;
		}

		public static List<MusicScoreNoteBase> GenerateTextNotes(ArtStrokeData art, ArtGroupData group, Func<int> getNewId)
		{
			if (art == null) throw new ArgumentNullException(nameof(art));
			if (group?.TextSettings == null) throw new ArgumentNullException(nameof(group));
			List<List<ArtStrokePoint>> monotonic = SplitIntoMonotonicStrokes(art.Strokes);
			var chains = new List<GuideAnchor[]>();
			float scaleX = group.ScaleX * group.TextSettings.HorizontalScale;
			float scaleY = group.ScaleY * group.TextSettings.VerticalScale;
			if (!float.IsFinite(scaleX) || !float.IsFinite(scaleY) || scaleX <= 0 || scaleY <= 0 || !float.IsFinite(group.LineWidth) || group.LineWidth <= 0) throw new ArgumentException("Art scales and stroke width must be positive finite numbers.");
			// Fit the entire inscription, never clamp individual letters into the last lane.
			float padding = group.LineWidth * .75f;
			scaleX = Mathf.Min(scaleX, (12f - group.LaneOrigin) / Mathf.Max(0.001f, art.MaxX - art.MinX + padding * 2));
			foreach (List<ArtStrokePoint> stroke in monotonic)
			{
				if (stroke.Count < 2) continue;
				bool horizontal = Mathf.Abs(stroke[stroke.Count - 1].Y - stroke[0].Y) < 0.001f;
				if (horizontal)
				{
					float minX = float.MaxValue;
					float maxX = float.MinValue;
					foreach (ArtStrokePoint point in stroke) { minX = Mathf.Min(minX, point.X); maxX = Mathf.Max(maxX, point.X); }
					float laneStart = group.LaneOrigin + (minX - art.MinX + padding - group.LineWidth * .5f) * scaleX;
					float laneEnd = group.LaneOrigin + (maxX - art.MinX + padding + group.LineWidth * .5f) * scaleX;
					long center = group.OriginTicks + (long)Mathf.Round((stroke[0].Y - art.MinY + padding) * scaleY * TextTicksPerLane);
					long half = Math.Max(1L, (long)Mathf.Round(group.LineWidth * scaleY * TextTicksPerLane * 0.5f));
					chains.Add(new[]
					{
						new GuideAnchor(Math.Min(laneStart, laneEnd), Math.Max(laneStart, laneEnd), Math.Max(0, center - half)),
						new GuideAnchor(Math.Min(laneStart, laneEnd), Math.Max(laneStart, laneEnd), Math.Max(1, center + half))
					});
					continue;
				}

				for (int i = 1; i < stroke.Count; i++)
					chains.Add(SliceThickSegment(stroke[i - 1], stroke[i], art, group, scaleX, scaleY, padding));
			}
			int nodeCount = 0;
			foreach (var chain in chains) nodeCount += chain.Length;
			EnsureNodeLimit(nodeCount);
			var notes = new List<MusicScoreNoteBase>(nodeCount);
			foreach (var chain in chains) AddGuideChain(notes, group, getNewId, chain);
			return notes;
		}

		private static GuideAnchor[] SliceThickSegment(ArtStrokePoint start, ArtStrokePoint end, ArtStrokeData art, ArtGroupData group, float scaleX, float scaleY, float padding)
		{
			Vector2 a = new Vector2(start.X, start.Y);
			Vector2 b = new Vector2(end.X, end.Y);
			Vector2 direction = (b - a).normalized;
			if (direction == Vector2.zero) return Array.Empty<GuideAnchor>();
			Vector2 cap = direction * (group.LineWidth * .5f);
			Vector2 normal = new Vector2(-cap.y, cap.x);
			// Square caps overlap at joins. Slicing this convex quad preserves thickness
			// even when a stroke is almost horizontal, without widening an entire lane.
			Vector2[] polygon = { a - cap + normal, a - cap - normal, b + cap - normal, b + cap + normal };
			float[] rows = { polygon[0].y, polygon[1].y, polygon[2].y, polygon[3].y };
			Array.Sort(rows);
			var anchors = new List<GuideAnchor>(4);
			float lastY = float.NegativeInfinity;
			foreach (float y in rows)
			{
				if (y - lastY < .00001f) continue;
				lastY = y;
				float left = float.PositiveInfinity, right = float.NegativeInfinity;
				for (int edge = 0; edge < 4; edge++)
				{
					Vector2 p = polygon[edge], q = polygon[(edge + 1) % 4];
					if (y < Mathf.Min(p.y, q.y) || y > Mathf.Max(p.y, q.y)) continue;
					if (Mathf.Abs(q.y - p.y) < .00001f)
					{
						left = Mathf.Min(left, p.x, q.x);
						right = Mathf.Max(right, p.x, q.x);
					}
					else
					{
						float x = Mathf.Lerp(p.x, q.x, (y - p.y) / (q.y - p.y));
						left = Mathf.Min(left, x);
						right = Mathf.Max(right, x);
					}
				}
				long ticks = group.OriginTicks + (long)Mathf.Round((y - art.MinY + padding) * scaleY * TextTicksPerLane);
				if (anchors.Count > 0) ticks = Math.Max(ticks, anchors[anchors.Count - 1].Ticks + 1);
				anchors.Add(new GuideAnchor(group.LaneOrigin + (left - art.MinX + padding) * scaleX,
					group.LaneOrigin + (right - art.MinX + padding) * scaleX, Math.Max(0, ticks)));
			}
			return anchors.ToArray();
		}

		public static List<List<ArtStrokePoint>> SplitIntoMonotonicStrokes(IEnumerable<List<ArtStrokePoint>> strokes)
		{
			List<List<ArtStrokePoint>> result = new List<List<ArtStrokePoint>>();
			if (strokes == null) return result;
			foreach (List<ArtStrokePoint> source in strokes)
			{
				if (source == null || source.Count < 2) continue;
				List<ArtStrokePoint> current = new List<ArtStrokePoint> { source[0] };
				int direction = 0;
				for (int i = 1; i < source.Count; i++)
				{
					ArtStrokePoint previous = source[i - 1];
					ArtStrokePoint point = source[i];
					float delta = point.Y - previous.Y;
					int nextDirection = Mathf.Abs(delta) < 0.001f ? 0 : Math.Sign(delta);
					if (current.Count >= 2 && direction != nextDirection)
					{
						if (current.Count >= 2) result.Add(NormalizeDirection(current));
						current = new List<ArtStrokePoint> { previous };
					}
					current.Add(point);
					direction = nextDirection;
				}
				if (current.Count >= 2) result.Add(NormalizeDirection(current));
			}
			return result;
		}

		private static List<ArtStrokePoint> NormalizeDirection(List<ArtStrokePoint> stroke)
		{
			if (stroke[0].Y <= stroke[stroke.Count - 1].Y) return stroke;
			stroke.Reverse();
			return stroke;
		}

		private readonly struct GuideAnchor
		{
			public readonly float LaneStart;
			public readonly float LaneEnd;
			public readonly long Ticks;
			public readonly NoteLineType LineType;
			public GuideAnchor(float laneStart, float laneEnd, long ticks, NoteLineType lineType = NoteLineType.Linear) { LaneStart = laneStart; LaneEnd = laneEnd; Ticks = ticks; LineType = lineType; }
		}

		private static void AddGuideChain(List<MusicScoreNoteBase> output, ArtGroupData group, Func<int> getNewId, IReadOnlyList<GuideAnchor> anchors)
		{
			if (getNewId == null) throw new ArgumentNullException(nameof(getNewId));
			if (anchors == null || anchors.Count < 2) return;
			MusicScoreNoteBase previous = null;
			for (int i = 0; i < anchors.Count; i++)
			{
				GuideAnchor anchor = anchors[i];
				bool first = i == 0;
				bool last = i == anchors.Count - 1;
				MusicScoreNoteBase note = new MusicScoreNoteBase(
					getNewId(), anchor.Ticks, 0, 0,
					first ? NoteCategory.Guide : last ? NoteCategory.GuideEnd : NoteCategory.GuideHidden,
					group.NoteType, 1f, anchor.LineType,
					first ? MusicScoreNoteBase.NoteBaseType.Guide : last ? MusicScoreNoteBase.NoteBaseType.GuideEnd : MusicScoreNoteBase.NoteBaseType.GuideHiddenConnection);
				note.ArtGroupId = group.Id;
				note.SetGuideBounds(anchor.LaneStart, anchor.LaneEnd);
				if (previous != null)
				{
					previous.nextConnectionId = note.id;
					note.previousConnectionId = previous.id;
				}
				output.Add(note);
				previous = note;
			}
		}

		private static bool TryGetForegroundBounds(Color32[] pixels, int width, int height, ImageArtSettings settings, out RectInt bounds)
		{
			if (!settings.CropForeground)
			{
				bounds = new RectInt(0, 0, width, height);
				return width > 0 && height > 0;
			}
			int minX = width, minY = height, maxX = -1, maxY = -1;
			for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
			{
				if (!IsForeground(pixels[y * width + x], settings.Threshold, settings.LightForeground)) continue;
				minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
			}
			bounds = maxX < minX ? default : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
			return maxX >= minX;
		}

		private static bool IsForeground(Color32 pixel, int threshold, bool lightForeground)
		{
			if (pixel.a < 16) return false;
			float luminance = pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
			return lightForeground ? luminance >= threshold : luminance < threshold;
		}

		private static int ClampLane(int lane) => Mathf.Clamp(lane, LaneMin, LaneMax);
		private static void EnsureNodeLimit(int count)
		{
			if (count > MaxNodes) throw new InvalidOperationException($"Art generation would create {count} nodes; the limit is {MaxNodes}.");
		}
	}
}
