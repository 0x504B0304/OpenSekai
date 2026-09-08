using System.Collections.Generic;
using Sekai.MusicScoreMaker.Ingame.Models;
using UnityEngine;

namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
	public static class ArtGroupHitTest
	{
		public static string FindGroup(MusicScoreMakerData data, Vector2 point, Vector2 previewSize,
			long startTicks, long endTicks, float padding = 15f)
		{
			if (data?.ArtGroups == null || data.NoteList == null || endTicks <= startTicks
				|| previewSize.x <= 0 || previewSize.y <= 0
				|| Mathf.Abs(point.x) > previewSize.x * .5f || Mathf.Abs(point.y) > previewSize.y * .5f) return null;

			var bounds = new Dictionary<string, Rect>();
			foreach (var note in data.NoteList)
			{
				if (string.IsNullOrEmpty(note?.ArtGroupId)) continue;
				float left = (note.GuideLeft / MusicScoreMakerModel.LaneCount - .5f) * previewSize.x;
				float right = (note.GuideRight / MusicScoreMakerModel.LaneCount - .5f) * previewSize.x;
				float y = MusicScoreMakerUtility.CalcPreviewPositionYFromTicks(startTicks, endTicks, previewSize, Vector2.zero, note.ticks);
				if (bounds.TryGetValue(note.ArtGroupId, out var rect))
					bounds[note.ArtGroupId] = Rect.MinMaxRect(Mathf.Min(rect.xMin, left), Mathf.Min(rect.yMin, y),
						Mathf.Max(rect.xMax, right), Mathf.Max(rect.yMax, y));
				else bounds.Add(note.ArtGroupId, Rect.MinMaxRect(left, y, right, y));
			}

			string result = null;
			float smallestArea = float.PositiveInfinity;
			// Prefer a nested small group over its enclosing group; ties favor the last added group.
			foreach (var group in data.ArtGroups)
			{
				if (string.IsNullOrEmpty(group?.Id) || !bounds.TryGetValue(group.Id, out var rect)) continue;
				if (point.x < rect.xMin - padding || point.x > rect.xMax + padding
					|| point.y < rect.yMin - padding || point.y > rect.yMax + padding) continue;
				float area = (rect.width + padding * 2) * (rect.height + padding * 2);
				if (area > smallestArea) continue;
				smallestArea = area;
				result = group.Id;
			}
			return result;
		}
	}
}
