using System;
using System.Collections.Generic;
using System.Threading;
using Sekai.Live;
using UnityEngine;

namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
	public struct ImageArtContourAnchor
	{
		public float Left, Right, Row;
		public NoteLineType LineType;
		public ImageArtContourAnchor(float left, float right, float row)
		{
			Left = left; Right = right; Row = row; LineType = NoteLineType.Linear;
		}
	}

	public static class ImageArtContourGenerator
	{
		private const float FitTolerancePixels = .75f;
		private sealed class RowSpan
		{
			public int Left, Right, Neighbors;
			public RowSpan Previous;
			public List<ImageArtContourAnchor> Chain;
			public float Tolerance = FitTolerancePixels;
		}

		public static List<List<ImageArtContourAnchor>> Generate(BinaryImageArt art, bool autoEase, CancellationToken token = default)
		{
			var chains = new List<List<ImageArtContourAnchor>>();
			if (art == null || art.Width <= 0 || art.Height <= 0) return chains;
			if (art.Pixels == null || (long)art.Width * art.Height != art.Pixels.Length) throw new ArgumentException("Invalid image grid.");
			var previous = new List<RowSpan>();
			var tolerances = new Dictionary<List<ImageArtContourAnchor>, float>();
			for (int row = 0; row <= art.Height; row++)
			{
				token.ThrowIfCancellationRequested();
				var current = new List<RowSpan>();
				if (row < art.Height)
				{
					for (int x = 0; x < art.Width;)
					{
						if (art.Pixels[row * art.Width + x] == 0) { x++; continue; }
						int left = x++;
						while (x < art.Width && art.Pixels[row * art.Width + x] != 0) x++;
						current.Add(new RowSpan { Left = left, Right = x });
					}
				}

				// Continue only one-to-one row overlaps. Holes, splits and merges must
				// terminate chains, since a single Guide cannot contain an empty interval.
				foreach (var span in previous) span.Neighbors = 0;
				int firstCandidate = 0;
				foreach (var span in current)
				{
					while (firstCandidate < previous.Count && previous[firstCandidate].Right < span.Left) firstCandidate++;
					for (int i = firstCandidate; i < previous.Count && previous[i].Left <= span.Right; i++)
					{
						span.Neighbors++;
						span.Previous = previous[i];
						previous[i].Neighbors++;
					}
				}
				var continued = new HashSet<List<ImageArtContourAnchor>>();
				for (int i = 1; i < current.Count; i++)
					if (current[i].Left - current[i - 1].Right == 1) current[i].Tolerance = current[i - 1].Tolerance = .49f;
				foreach (var span in current)
				{
					if (span.Neighbors == 1 && span.Previous.Neighbors == 1)
					{
						span.Chain = span.Previous.Chain;
						tolerances[span.Chain] = Mathf.Min(tolerances[span.Chain], span.Tolerance);
						continued.Add(span.Chain);
					}
					else
					{
						span.Chain = new List<ImageArtContourAnchor> { new ImageArtContourAnchor(span.Left, span.Right, row) };
						chains.Add(span.Chain);
						tolerances.Add(span.Chain, span.Tolerance);
						CheckLimit(chains.Count * 2);
					}
					span.Chain.Add(new ImageArtContourAnchor(span.Left, span.Right, row + .5f));
				}
				foreach (var span in previous)
					if (!continued.Contains(span.Chain)) span.Chain.Add(new ImageArtContourAnchor(span.Left, span.Right, row));
				previous = current;
			}

			if (autoEase)
			{
				RoundCaps(chains, false);
				RoundCaps(chains, true);
			}
			int count = 0;
			for (int i = 0; i < chains.Count; i++)
			{
				chains[i] = Fit(chains[i], autoEase, tolerances[chains[i]], token);
				count += chains[i].Count;
				CheckLimit(count);
			}
			return chains;
		}

		private static void RoundCaps(List<List<ImageArtContourAnchor>> chains, bool end)
		{
			var boundaries = new Dictionary<float, List<List<ImageArtContourAnchor>>>();
			var oppositeBoundaries = new Dictionary<float, List<ImageArtContourAnchor>>();
			foreach (var chain in chains)
			{
				var edge = EdgeSample(chain, !end, 0);
				if (!oppositeBoundaries.TryGetValue(edge.Row, out var edges)) oppositeBoundaries[edge.Row] = edges = new List<ImageArtContourAnchor>();
				edges.Add(edge);
			}
			foreach (var chain in chains)
			{
				if (chain.Count < 5) continue;
				var a = EdgeSample(chain, end, 1); var b = EdgeSample(chain, end, 2); var c = EdgeSample(chain, end, 3);
				int edge = end ? chain.Count - 1 : 0;
				float row = chain[edge].Row;
				bool joinsAnother = oppositeBoundaries.TryGetValue(row, out var neighbors) && neighbors.Exists(n => n.Right >= a.Left && n.Left <= a.Right);
				if (!joinsAnother && CanRound(a.Right - a.Left, b.Right - b.Left, c.Right - c.Left, (a.Left + a.Right) * .5f, (b.Left + b.Right) * .5f))
				{
					var cap = chain[edge];
					cap.Left = cap.Right = (a.Left + a.Right) * .5f;
					chain[edge] = cap;
				}
				if (!boundaries.TryGetValue(row, out var peers)) boundaries[row] = peers = new List<List<ImageArtContourAnchor>>();
				peers.Add(chain);
			}
			// At a rounded hole's split/merge, let the two boundary anchors meet.
			// A constant-width gap remains square, as do constant-width outer edges.
			foreach (var peers in boundaries.Values)
			{
				peers.Sort((a, b) => EdgeSample(a, end, 1).Left.CompareTo(EdgeSample(b, end, 1).Left));
				for (int i = 1; i < peers.Count; i++)
				{
					var left = peers[i - 1]; var right = peers[i];
					var a = EdgeSample(left, end, 1); var b = EdgeSample(right, end, 1);
					float row = EdgeSample(left, end, 0).Row;
					if (!oppositeBoundaries.TryGetValue(row, out var joinedEdges)
						|| !joinedEdges.Exists(edge => edge.Left < a.Right && edge.Right > b.Left)) continue;
					var a2 = EdgeSample(left, end, 2); var b2 = EdgeSample(right, end, 2);
					float gap3 = EdgeSample(right, end, 3).Left - EdgeSample(left, end, 3).Right;
					if (!CanRound(b.Left - a.Right, b2.Left - a2.Right, gap3, (b.Left + a.Right) * .5f, (b2.Left + a2.Right) * .5f)) continue;
					int li = end ? left.Count - 1 : 0, ri = end ? right.Count - 1 : 0;
					var lcap = left[li]; var rcap = right[ri];
					lcap.Right = rcap.Left = (a.Right + b.Left) * .5f;
					left[li] = lcap; right[ri] = rcap;
				}
			}
		}

		private static ImageArtContourAnchor EdgeSample(List<ImageArtContourAnchor> chain, bool end, int offset) => chain[end ? chain.Count - 1 - offset : offset];
		private static bool CanRound(float width, float next, float third, float center, float nextCenter)
		{
			float growth = next - width;
			return width > 0 && growth > 1 && width <= growth * 2 && third >= next && third - next <= growth
				&& Mathf.Abs(nextCenter - center) < growth * .5f;
		}

		private static List<ImageArtContourAnchor> Fit(List<ImageArtContourAnchor> source, bool autoEase, float tolerance, CancellationToken token)
		{
			var result = new List<ImageArtContourAnchor>();
			var pending = new Stack<(int first, int last)>();
			pending.Push((0, source.Count - 1));
			while (pending.Count > 0)
			{
				token.ThrowIfCancellationRequested();
				var range = pending.Pop();
				var start = source[range.first];
				var end = source[range.last];
				float bestError = float.PositiveInfinity;
				int split = range.first + 1;
				NoteLineType bestType = NoteLineType.Linear;
				for (int type = 0; type <= (autoEase ? 2 : 0); type++)
				{
					float error = 0;
					int worst = range.first + 1;
					for (int i = range.first + 1; i < range.last; i++)
					{
						var sample = source[i];
						float t = Ease((sample.Row - start.Row) / (end.Row - start.Row), (NoteLineType)type);
						float deviation = Mathf.Max(Mathf.Abs(Mathf.Lerp(start.Left, end.Left, t) - sample.Left),
							Mathf.Abs(Mathf.Lerp(start.Right, end.Right, t) - sample.Right));
						if (deviation > error) { error = deviation; worst = i; }
					}
					if (error < bestError) { bestError = error; split = worst; bestType = (NoteLineType)type; }
				}
				if (bestError > tolerance && range.last > range.first + 1)
				{
					pending.Push((split, range.last));
					pending.Push((range.first, split));
				}
				else
				{
					start.LineType = bestType;
					result.Add(start);
				}
			}
			result.Add(source[source.Count - 1]);
			return result;
		}

		public static float Ease(float rate, NoteLineType type)
		{
			return type == NoteLineType.EaseIn ? rate * rate : type == NoteLineType.EaseOut ? 1 - (1 - rate) * (1 - rate) : rate;
		}

		public static BinaryImageArt RenderPreview(List<List<ImageArtContourAnchor>> chains, int sourceWidth, int sourceHeight, CancellationToken token = default)
		{
			float scale = Mathf.Min(4f, 512f / Mathf.Max(1, sourceWidth, sourceHeight));
			int width = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
			int height = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
			var image = new BinaryImageArt { Width = width, Height = height, Pixels = new byte[width * height] };
			foreach (var chain in chains)
			for (int i = 1; i < chain.Count; i++)
			{
				token.ThrowIfCancellationRequested();
				var a = chain[i - 1]; var b = chain[i];
				for (int y = Mathf.Max(0, Mathf.CeilToInt(a.Row * height / sourceHeight - .5f)); y < height && (y + .5f) * sourceHeight / height < b.Row; y++)
				{
					float t = Ease(((y + .5f) * sourceHeight / height - a.Row) / (b.Row - a.Row), a.LineType);
					float left = Mathf.Lerp(a.Left, b.Left, t) * width / sourceWidth;
					float right = Mathf.Lerp(a.Right, b.Right, t) * width / sourceWidth;
					for (int x = Mathf.Max(0, Mathf.CeilToInt(left - .5f)); x < width && x + .5f < right; x++) image.Pixels[y * width + x] = 1;
				}
			}
			foreach (byte pixel in image.Pixels) image.ForegroundPixelCount += pixel;
			return image;
		}

		private static void CheckLimit(int count)
		{
			if (count > ArtGuideGenerator.MaxNodes) throw new InvalidOperationException($"Art generation would create {count} nodes; the limit is {ArtGuideGenerator.MaxNodes}.");
		}
	}
}
