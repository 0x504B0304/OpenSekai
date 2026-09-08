using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.MusicScoreMaker.Ingame.Models;
using UnityEngine;

namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
	public static class ArtGroupTransform
	{
		public static List<MusicScoreNoteBase> Resize(IReadOnlyList<MusicScoreNoteBase> source, ArtGroupData group,
			float left, float right, long bottom, long top)
		{
			float oldLeft = source.Min(n => n.GuideLeft), oldRight = source.Max(n => n.GuideRight);
			long oldBottom = source.Min(n => n.ticks), oldTop = source.Max(n => n.ticks);
			left = Mathf.Clamp(left, 0f, 11.99f);
			right = Mathf.Clamp(right, left + 0.01f, 12f);
			bottom = Math.Max(0, bottom);
			top = Math.Max(bottom + 1, top);
			float sx = (right - left) / Mathf.Max(0.001f, oldRight - oldLeft);
			double sy = (top - bottom) / (double)Math.Max(1, oldTop - oldBottom);
			var result = new List<MusicScoreNoteBase>(source.Count);
			foreach (var original in source)
			{
				var note = original.Clone();
				note.SetGuideBounds(left + (original.GuideLeft - oldLeft) * sx, left + (original.GuideRight - oldLeft) * sx);
				note.ticks = bottom + (long)Math.Round((original.ticks - oldBottom) * sy);
				result.Add(note);
			}
			// Compression can quantize adjacent nodes to the same tick. Preserve every chain.
			var byId = result.ToDictionary(n => n.id);
			foreach (var note in result.OrderBy(n => n.ticks))
				if (byId.TryGetValue(note.previousConnectionId, out var previous)) note.ticks = Math.Max(previous.ticks + 1, note.ticks);
			float origin = left + (group.LaneOrigin - oldLeft) * sx;
			group.OriginLane = Mathf.Clamp(Mathf.FloorToInt(origin), 0, 11);
			group.OriginLaneOffset = origin - group.OriginLane;
			group.OriginTicks = Math.Max(0, bottom + (long)Math.Round((group.OriginTicks - oldBottom) * sy));
			group.ScaleX *= sx;
			group.ScaleY *= (float)sy;
			return result;
		}
	}
}
