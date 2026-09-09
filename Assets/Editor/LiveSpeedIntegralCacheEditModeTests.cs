#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace Sekai.EditorTools.Tests
{
	public sealed class LiveSpeedIntegralCacheEditModeTests
	{
		private static readonly Type CacheType = typeof(LiveLogic).Assembly.GetType("Sekai.Core.Live.LiveSpeedIntegralCache", true);
		private static readonly Func<MusicScoreInfo[], float, float, float, float> Legacy =
			(Func<MusicScoreInfo[], float, float, float, float>)Delegate.CreateDelegate(
				typeof(Func<MusicScoreInfo[], float, float, float, float>), CacheType.GetMethod("CalculateLegacy"));

		private sealed class Cache
		{
			public readonly object Instance;
			public readonly Action<float, float> Begin;
			public readonly Func<float, float, float, float> Calculate;
			public Cache(MusicScoreInfo[] infos, NoteBase[] notes = null)
			{
				Instance = Activator.CreateInstance(CacheType, new object[] { infos, notes });
				Begin = (Action<float, float>)Delegate.CreateDelegate(typeof(Action<float, float>), Instance, "BeginUpdate");
				Calculate = (Func<float, float, float, float>)Delegate.CreateDelegate(typeof(Func<float, float, float, float>), Instance, "Calculate");
			}
			public int EndpointCount => ((IDictionary)CacheType.GetField("endpoints", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Instance)).Count;
		}

		private static MusicScoreInfo Info(float position, float speed)
		{
			int bar = float.IsNaN(position) || float.IsInfinity(position) ? 0 : (int)Math.Floor(position);
			return new MusicScoreInfo(bar, position - bar, position, 120f, 4f, speed, 1f);
		}

		private static NoteBase Note(float position) => new NoteBase { MusicScoreInfo = Info(position, 1f) };

		[TestCase(0f, 1f, 9f, 2f)]
		[TestCase(0f, 2f, 9f, 1f)]
		[TestCase(2.5f, 3.5f, 9f, 9f)]
		[TestCase(2.5f, 4f, 9f, 2f)]
		[TestCase(4f, 5f, 9f, -2f)]
		[TestCase(7f, 8f, 9f, 3f)]
		[TestCase(5f, 5f, 9f, 9f)]
		[TestCase(5f, 4f, 9f, 9f)]
		public void SpecialRangesAndDuplicateBoundariesPreserveExactBehavior(float current, float note, float frameSpeed, float expected)
		{
			var infos = new[] { Info(2f, 2f), Info(4f, 8f), Info(4f, -2f), Info(6f, 3f) };
			var cache = new Cache(infos);
			Assert.That(Legacy(infos, current, note, frameSpeed), Is.EqualTo(expected));
			Assert.That(cache.Calculate(current, note, frameSpeed), Is.EqualTo(expected));
		}

		[Test]
		public void EmptyAndMalformedTablesKeepLegacyFallback()
		{
			var tables = new MusicScoreInfo[][]
			{
				null, Array.Empty<MusicScoreInfo>(),
				new[] { Info(0f, 2f), Info(2f, -1f), Info(1f, 3f) },
				new[] { Info(0f, 1f), Info(float.NaN, 2f) },
				new[] { Info(0f, float.PositiveInfinity), Info(2f, 1f) },
				new[] { Info(0f, float.NaN) }
			};
			foreach (var infos in tables)
			{
				var cache = new Cache(infos);
				foreach (float note in new[] { -1f, 0f, 0.5f, 1f, 3f, float.PositiveInfinity, float.NaN })
				{
					AssertEquivalent(Legacy(infos, 0f, note, 7f), cache.Calculate(0f, note, 7f), true);
				}
			}
		}

		[Test]
		public void FloatCancellationOverflowAndUnderflowPreserveZeroAndSign()
		{
			var fixtures = new[]
			{
				new[] { Info(0f, 1e8f), Info(1f, 1f), Info(2f, -1e8f), Info(3f, 0f) },
				new[] { Info(0f, 1e8f), Info(1f, 1f), Info(2f, -1e8f), Info(3f, -1f), Info(4f, 0f) },
				new[] { Info(0f, 1f), Info(1f, -1f), Info(2f, 0f) },
				new[] { Info(0f, 0f), Info(1f, 0f), Info(2f, 0f) },
				new[] { Info(0f, float.MaxValue), Info(1f, float.MaxValue), Info(2f, -float.MaxValue), Info(3f, 0f) },
				new[] { Info(0f, 1e-36f), Info(1e-10f, 0f) }
			};
			foreach (var infos in fixtures)
			{
				float note = infos[infos.Length - 1].bar + infos[infos.Length - 1].barProgress;
				var cache = new Cache(infos, new[] { Note(note) });
				float expected = Legacy(infos, 0f, note, 1f);
				cache.Begin(0f, 1f);
				AssertEquivalent(expected, cache.Calculate(0f, note, 1f), true);
				AssertEquivalent(expected, cache.Calculate(0f, note, 1f), true);
			}
		}

		[Test]
		public void RandomTablesMatchLegacyAcrossPositiveNegativeAndZeroSpeeds()
		{
			var random = new System.Random(1617);
			for (int chart = 0; chart < 64; chart++)
			{
				var infos = new MusicScoreInfo[128];
				float position = (float)random.NextDouble();
				for (int i = 0; i < infos.Length; i++)
				{
					position += i % 7 == 0 ? 0f : (float)random.NextDouble();
					float speed = i % 11 == 0 ? 0f : (float)(random.NextDouble() * 8d - 4d);
					infos[i] = Info(position, speed);
				}
				var cache = new Cache(infos);
				for (int query = 0; query < 128; query++)
				{
					float current = (float)(random.NextDouble() * (position + 4f) - 2f);
					float note = query % 8 == 0 ? infos[query].bar + infos[query].barProgress
						: current + (float)random.NextDouble() * 8f;
					float frameSpeed = (float)random.NextDouble() * 3f;
					cache.Begin(current, frameSpeed);
					AssertEquivalent(Legacy(infos, current, note, frameSpeed), cache.Calculate(current, note, frameSpeed));
				}
			}
		}

		[Test]
		public void LongRangesKeepLegacyAccumulationWithinTolerance()
		{
			foreach (int count in new[] { 8192, 32768 })
			{
				var infos = new MusicScoreInfo[count];
				for (int i = 0; i < count; i++) infos[i] = Info(i, 0.7f);
				var cache = new Cache(infos, new[] { Note(count - 1f) });
				foreach (float current in new[] { 0f, 0.25f, count / 2f, count - 4.25f })
				{
					AssertEquivalent(Legacy(infos, current, count - 1f, 1f), cache.Calculate(current, count - 1f, 1f));
				}
			}
		}

		[Test]
		public void NumericFallbackStartsAtTheSameFirstParticipatingSegment()
		{
			var infos = new[] { Info(0f, 3f), Info(1f, 1e8f), Info(2f, 1f), Info(2f, 1f), Info(3f, -1e8f), Info(4f, -1f), Info(5f, 1e-36f) };
			var cache = new Cache(infos);
			foreach (float current in new[] { -1f, 0f, 0.5f, 1f, 1.25f, 2f, 5f, 6f })
			{
				foreach (float note in new[] { 4f, 5f, 6.000001f, 8f })
				{
					AssertEquivalent(Legacy(infos, current, note, 7f), cache.Calculate(current, note, 7f));
				}
			}
		}

		[Test]
		public void UpdatesHandleSeekingRepeatedProgressChangedSpeedAndNewScore()
		{
			var infos = new[] { Info(0f, 1f), Info(2f, -2f), Info(4f, 3f) };
			var cache = new Cache(infos, new[] { Note(5f) });
			foreach (float current in new[] { 0f, 2.5f, 4.5f, 4.5f, -1f, 0f, 3f })
			{
				cache.Begin(current, 7f);
				AssertEquivalent(Legacy(infos, current, 5f, 7f), cache.Calculate(current, 5f, 7f));
			}
			cache.Begin(0.5f, 7f);
			Assert.That(cache.Calculate(0.5f, 1f, 7f), Is.EqualTo(7f));
			cache.Begin(0.5f, -3f);
			Assert.That(cache.Calculate(0.5f, 1f, -3f), Is.EqualTo(-3f));
			var replacement = new Cache(new[] { Info(0f, 4f) }, new[] { Note(5f) });
			Assert.That(replacement.Calculate(0f, 5f, 1f), Is.EqualTo(4f));
		}

		[Test]
		public void SetupCachesAllChildEndpointsWithoutFollowingCyclesForever()
		{
			var root = Note(1f);
			var child = Note(2f);
			var grandchild = Note(3f);
			root.NoteList = new List<NoteBase> { root, child, null };
			child.NoteList = new List<NoteBase> { grandchild, root };
			grandchild.NoteList = new List<NoteBase> { child };
			var infos = new[] { Info(0f, 1f), Info(2f, -1f), Info(4f, 2f) };
			var cache = new Cache(infos, new[] { root, null, child });
			Assert.That(cache.EndpointCount, Is.EqualTo(3));
			cache.Begin(0f, 1f);
			for (int i = 1; i <= 3; i++) AssertEquivalent(Legacy(infos, 0f, i, 1f), cache.Calculate(0f, i, 1f));
			Assert.That(cache.EndpointCount, Is.EqualTo(3));
		}

		[TestCase(128)]
		[TestCase(1024)]
		[TestCase(8192)]
		[Explicit("Reports speed-query cost only; wall-clock timings are not pass/fail thresholds.")]
		public void ReportCachedQueryCostIncludingEveryLogicalUpdate(int tableSize)
		{
			var infos = new MusicScoreInfo[tableSize];
			for (int i = 0; i < tableSize; i++) infos[i] = Info(i / 8f, 0.5f + i % 13 / 8f);
			const int endpointCount = 384;
			const int frames = 64;
			float start = tableSize / 16f;
			var notes = new NoteBase[endpointCount];
			var endpoints = new float[endpointCount];
			for (int i = 0; i < endpointCount; i++)
			{
				endpoints[i] = start + 0.5f + i / 128f;
				notes[i] = Note(endpoints[i]);
			}
			var cache = new Cache(infos, notes);
			for (int i = 0; i < endpointCount; i++)
			{
				cache.Calculate(start, endpoints[i], 1f);
				Legacy(infos, start, endpoints[i], 1f);
			}
			var timer = new Stopwatch();
			double legacySum = 0d;
			long before = GC.GetAllocatedBytesForCurrentThread();
			timer.Start();
			for (int frame = 0; frame < frames; frame++)
			{
				float current = start + frame / 256f;
				for (int i = 0; i < endpointCount; i++) legacySum += Legacy(infos, current, endpoints[i], 1f);
			}
			timer.Stop();
			long legacyBytes = GC.GetAllocatedBytesForCurrentThread() - before;
			double legacyMs = timer.Elapsed.TotalMilliseconds;
			double cachedSum = 0d;
			before = GC.GetAllocatedBytesForCurrentThread();
			timer.Restart();
			for (int frame = 0; frame < frames; frame++)
			{
				float current = start + frame / 256f;
				cache.Begin(current, 1f);
				for (int i = 0; i < endpointCount; i++) cachedSum += cache.Calculate(current, endpoints[i], 1f);
			}
			timer.Stop();
			long cachedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
			Assert.That(cachedSum, Is.EqualTo(legacySum).Within(Math.Abs(legacySum) * 1e-5d));
			Assert.That(cachedBytes, Is.Zero, "Prewarmed endpoint queries must not allocate.");
			TestContext.WriteLine($"{tableSize} entries, {frames} updates x {endpointCount} distinct endpoints: legacy {legacyMs:F3} ms / {legacyBytes} B; cached {timer.Elapsed.TotalMilliseconds:F3} ms / {cachedBytes} B.");
		}

		private static void AssertEquivalent(float expected, float actual, bool exact = false)
		{
			if (float.IsNaN(expected)) Assert.That(float.IsNaN(actual), Is.True);
			else if (exact || float.IsInfinity(expected)) Assert.That(actual, Is.EqualTo(expected));
			else Assert.That(actual, Is.EqualTo(expected).Within(Math.Max(1e-5f, Math.Abs(expected) * 1e-5f)));
			Assert.That(actual < 0f, Is.EqualTo(expected < 0f), "The negative-speed rendering branch must remain unchanged.");
			Assert.That(Mathf.Approximately(actual, 0f), Is.EqualTo(Mathf.Approximately(expected, 0f)), "The zero-speed branch must remain unchanged.");
		}
	}
}
#endif
