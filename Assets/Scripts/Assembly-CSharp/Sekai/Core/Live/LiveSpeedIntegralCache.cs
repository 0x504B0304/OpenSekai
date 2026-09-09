using System;
using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;

namespace Sekai.Core.Live
{
	// A score's speed table is immutable for the lifetime of one LiveLogic.Setup.
	internal sealed class LiveSpeedIntegralCache
	{
		private const double FloatUnitRoundoff = 5.9604644775390625e-8;
		private const double DoubleUnitRoundoff = 1.1102230246251565e-16;
		private readonly MusicScoreInfo[] scoreInfos;
		private readonly float[] positions;
		private readonly double[] areas;
		private readonly double[] absoluteAreas;
		private readonly Dictionary<float, Endpoint> endpoints = new Dictionary<float, Endpoint>();
		private readonly bool canIntegrate;
		private float currentProgress = float.NaN;
		private float currentSpeed;
		private Endpoint current;
		private long updateVersion;

		private struct Endpoint
		{
			public double Area;
			public double AbsoluteArea;
			public int LowerBound;
			public long ResultVersion;
			public float Result;
		}

		public LiveSpeedIntegralCache(MusicScoreInfo[] scoreInfos, NoteBase[] notes)
		{
			this.scoreInfos = scoreInfos;
			int count = scoreInfos?.Length ?? 0;
			positions = new float[count];
			areas = new double[count];
			absoluteAreas = new double[count];
			canIntegrate = count > 0;
			for (int i = 0; i < count; i++)
			{
				positions[i] = scoreInfos[i].bar + scoreInfos[i].barProgress;
				if (!IsFinite(positions[i]) || !IsFinite(scoreInfos[i].speedRatio)
					|| (i > 0 && positions[i] < positions[i - 1]))
				{
					canIntegrate = false;
				}
				if (i > 0)
				{
					double area = ((double)positions[i] - positions[i - 1]) * scoreInfos[i - 1].speedRatio;
					areas[i] = areas[i - 1] + area;
					absoluteAreas[i] = absoluteAreas[i - 1] + Math.Abs(area);
				}
			}

			if (canIntegrate && notes != null)
			{
				var visited = new HashSet<NoteBase>();
				var pending = new Stack<NoteBase>(notes);
				while (pending.Count > 0)
				{
					NoteBase note = pending.Pop();
					if (note == null || !visited.Add(note)) continue;
					float progress = note.MusicScoreInfo.bar + note.MusicScoreInfo.barProgress;
					if (IsFinite(progress) && !endpoints.ContainsKey(progress))
					{
						endpoints.Add(progress, Evaluate(progress));
					}
					if (note.NoteList == null) continue;
					foreach (NoteBase child in note.NoteList) pending.Push(child);
				}
			}
		}

		// Called for every logical update, including repeated updates within a rendered frame.
		public void BeginUpdate(float progress, float frameSpeed)
		{
			currentProgress = progress;
			currentSpeed = frameSpeed;
			updateVersion++;
			if (canIntegrate && IsFinite(progress)) current = Evaluate(progress);
		}

		public float Calculate(float progress, float noteProgress, float frameSpeed)
		{
			if (!canIntegrate || !IsFinite(progress) || !IsFinite(noteProgress) || !IsFinite(frameSpeed))
			{
				return CalculateLegacy(scoreInfos, progress, noteProgress, frameSpeed);
			}
			if (noteProgress <= progress) return frameSpeed;
			if (currentProgress != progress || currentSpeed != frameSpeed) BeginUpdate(progress, frameSpeed);

			float range = noteProgress - progress;
			if (current.LowerBound > 0 && current.LowerBound < positions.Length
				&& positions[current.LowerBound] > noteProgress) return frameSpeed;
			if (Mathf.Approximately(range, 0f)) return frameSpeed;
			// Preserve the original pre-first-entry behavior, even when the note is before that entry.
			if (noteProgress <= positions[0]) return (positions[0] - progress) / range;

			if (!endpoints.TryGetValue(noteProgress, out Endpoint endpoint)) endpoint = Evaluate(noteProgress);
			if (endpoint.ResultVersion == updateVersion) return endpoint.Result;
			double area = endpoint.Area - current.Area;
			double absoluteArea = Math.Max(0d, endpoint.AbsoluteArea - current.AbsoluteArea);
			int terms = Math.Max(1, endpoint.LowerBound - current.LowerBound + 2);
			// A double prefix can cancel to zero where the original float sum retained a sign.
			// In that narrow region, retain the original sum and cache it for this logical update.
			double rounding = (3d * terms + 4d) * FloatUnitRoundoff;
			double floatError = rounding < 0.5d ? absoluteArea * rounding / (1d - rounding) : double.PositiveInfinity;
			double prefixError = 8d * positions.Length * DoubleUnitRoundoff
				* (Math.Abs(endpoint.AbsoluteArea) + Math.Abs(current.AbsoluteArea));
			float result = (float)(area / range);
			double error = floatError + prefixError + Math.Abs(area) * FloatUnitRoundoff;
			double allowedError = Math.Max(1e-5d, Math.Abs(result) * 1e-5d) * 0.9d;
			if (Math.Abs(area) <= floatError + prefixError || absoluteArea >= float.MaxValue
				|| absoluteArea < 1.1754943508222875e-38 || !IsFinite(result)
				|| Mathf.Approximately(result, 0f) || error > allowedError * range)
			{
				// Skip only history preceding the first participating segment; float summation order is unchanged.
				result = CalculateBySegments(scoreInfos, progress, noteProgress, frameSpeed, current.LowerBound);
			}
			endpoint.Result = result;
			endpoint.ResultVersion = updateVersion;
			endpoints[noteProgress] = endpoint;
			return result;
		}

		private Endpoint Evaluate(float progress)
		{
			int low = 0;
			int high = positions.Length;
			while (low < high)
			{
				int middle = low + (high - low) / 2;
				if (positions[middle] < progress) low = middle + 1;
				else high = middle;
			}
			int lowerBound = low;
			// At an exact boundary all repeated positions have the same integral.
			if (low < positions.Length && positions[low] == progress)
			{
				return new Endpoint { Area = areas[low], AbsoluteArea = absoluteAreas[low], LowerBound = lowerBound };
			}
			if (low == 0)
			{
				double distance = (double)progress - positions[0];
				return new Endpoint { Area = distance, AbsoluteArea = distance, LowerBound = 0 };
			}
			int previous = low - 1;
			double extra = ((double)progress - positions[previous]) * scoreInfos[previous].speedRatio;
			return new Endpoint
			{
				Area = areas[previous] + extra,
				AbsoluteArea = absoluteAreas[previous] + Math.Abs(extra),
				LowerBound = lowerBound
			};
		}

		private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

		// Kept unchanged as the compatibility path for malformed tables and float cancellation.
		public static float CalculateLegacy(MusicScoreInfo[] scoreInfos, float currentProgress, float noteProgress, float frameSpeed)
		{
			return CalculateBySegments(scoreInfos, currentProgress, noteProgress, frameSpeed, 0);
		}

		private static float CalculateBySegments(MusicScoreInfo[] scoreInfos, float currentProgress, float noteProgress, float frameSpeed, int start)
		{
			if (noteProgress <= currentProgress) return frameSpeed;
			if (scoreInfos == null || scoreInfos.Length == 0) return frameSpeed;
			float accumulated = 0f;
			bool hasRange = false;
			int previous = Math.Max(0, start - 1);
			float previousProgress = scoreInfos[previous].bar + scoreInfos[previous].barProgress;
			float previousSpeedRatio = scoreInfos[previous].speedRatio;
			for (int i = start; i <= scoreInfos.Length; i++)
			{
				float segmentProgress = i < scoreInfos.Length ? scoreInfos[i].bar + scoreInfos[i].barProgress : noteProgress;
				float segmentSpeedRatio = i < scoreInfos.Length ? scoreInfos[i].speedRatio : previousSpeedRatio;
				if (segmentProgress >= currentProgress)
				{
					if (hasRange)
					{
						if (segmentProgress > noteProgress)
						{
							accumulated += (noteProgress - previousProgress) * previousSpeedRatio;
							break;
						}
						accumulated += (segmentProgress - previousProgress) * previousSpeedRatio;
					}
					else if (i > 0)
					{
						if (segmentProgress > noteProgress) return frameSpeed;
						accumulated += (segmentProgress - currentProgress) * scoreInfos[i - 1].speedRatio;
					}
					else accumulated += segmentProgress - currentProgress;
					hasRange = true;
				}
				if (segmentProgress >= noteProgress) break;
				previousProgress = segmentProgress;
				previousSpeedRatio = segmentSpeedRatio;
			}
			if (!hasRange) return previousSpeedRatio;
			float range = noteProgress - currentProgress;
			return Mathf.Approximately(range, 0f) ? frameSpeed : accumulated / range;
		}
	}
}
