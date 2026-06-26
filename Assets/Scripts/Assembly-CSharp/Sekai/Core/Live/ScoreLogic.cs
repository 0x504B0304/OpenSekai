using Sekai.Live;

namespace Sekai.Core.Live
{
	public class ScoreLogic
	{
		private const int DefaultTotalScore = 1000000;

		private readonly LiveBundleBuildData liveBundleBuildData;
		private MasterPlayLevelScore scoreInfo;

		public LiveScore score;

		public int BaseNoteScore { get; set; }

		public bool IsPerfectCombo
		{
			get
			{
				if (score.badCount != 0 || score.missCount != 0 || score.goodCount != 0)
				{
					return false;
				}
				// If all notes are Auto, it's not a perfect combo
				if (score.autoCount == score.totalComboCount)
				{
					return false;
				}
				return true;
			}
		}

		public bool IsAllPerfectCombo
		{
			get
			{
				if (score.badCount != 0 || score.missCount != 0 || score.goodCount != 0 || score.greatCount != 0)
				{
					return false;
				}
				// If all notes are Auto, it's not an all perfect combo
				if (score.autoCount == score.totalComboCount)
				{
					return false;
				}
				return score.IsAllPerfect;
			}
		}

		public ScoreLogic(LiveBundleBuildData data)
		{
			liveBundleBuildData = data;
			score.life = 1000;
			score.rank = ScoreRank.D;
		}

		public virtual void Setup(LiveBootDataBase bootData, MusicScore musicScore)
		{
			score = default;
			scoreInfo = bootData?.MusicData?.Score;
			int totalCombo = LiveUtility.CalculateTotalComboCount(musicScore);
			if (totalCombo <= 0)
			{
				totalCombo = bootData?.MusicData?.TotalNoteCount ?? 0;
			}
			score.totalComboCount = totalCombo;
			score.life = liveBundleBuildData != null && liveBundleBuildData.Life > 0 ? liveBundleBuildData.Life : 1000;
			score.rank = ScoreRank.D;
			BaseNoteScore = totalCombo > 0 ? UnityEngine.Mathf.Max(1, DefaultTotalScore / totalCombo) : 0;
		}

		public virtual void ExcuteEvent(EventBase eventBase)
		{
		}

		public virtual void UpdateCombo(NoteBase note)
		{
			// Decoration notes are auto-judged and should not affect combo
			if (note == null || note.Result == NoteResult.None || note.IsDecoration)
			{
				return;
			}

			NoteResult effectiveResult = note.Result;
			// In Fake Perfect mode, treat Auto as Perfect for combo counting
			if (effectiveResult == NoteResult.Auto && LiveSettingData.LoadFromStorage()?.UsesAutoFakePerfectMode == true)
			{
				effectiveResult = NoteResult.Perfect;
			}

			score.combo = effectiveResult < NoteResult.Great ? 0 : score.combo + 1;
			if (score.combo > score.maxCombo)
			{
				score.maxCombo = score.combo;
			}
		}

		public virtual void UpdateNoteResult(NoteBase note)
		{
			// Decoration notes are auto-judged and should not affect score counts
			if (note != null && note.IsDecoration)
			{
				return;
			}

			switch (note?.Result)
			{
				case NoteResult.JustPerfect:
					score.justPerfectCount++;
					score.perfectCount++;
					break;
				case NoteResult.Perfect:
					score.perfectCount++;
					break;
				case NoteResult.Great:
					score.greatCount++;
					break;
				case NoteResult.Good:
					score.goodCount++;
					break;
				case NoteResult.Auto:
					score.autoCount++;
					break;
				case NoteResult.Bad:
					score.badCount++;
					break;
				case NoteResult.Miss:
					score.missCount++;
					break;
			}

			if (note != null && note.Description == NoteResultDescription.Fast)
			{
				score.fastCount++;
			}
			else if (note != null && note.Description == NoteResultDescription.Late)
			{
				score.lateCount++;
			}
			else if (note != null && note.Description == NoteResultDescription.FlickMiss)
			{
				score.flickCount++;
			}
		}

		public virtual int CalculateAddScore(NoteBase note, float factor = 1f)
		{
			// Decoration notes do not contribute to score
			if (note == null || note.IsDecoration)
			{
				return 0;
			}

			int addScore = note.Result switch
			{
				NoteResult.JustPerfect => BaseNoteScore,
				NoteResult.Perfect => UnityEngine.Mathf.RoundToInt(BaseNoteScore * 0.9f),
				NoteResult.Great => UnityEngine.Mathf.RoundToInt(BaseNoteScore * 0.7f),
				NoteResult.Good => UnityEngine.Mathf.RoundToInt(BaseNoteScore * 0.5f),
				NoteResult.Auto => BaseNoteScore,
				NoteResult.Pass => 0,
				_ => 0
			};
			int actualAddScore = UnityEngine.Mathf.RoundToInt(addScore * factor);
			score.totalScore += actualAddScore;
			UpdateScoreRank();
			return actualAddScore;
		}

		public virtual void Damage(NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			if (LiveConfig.Damages.TryGetValue(note.Result, out int damage))
			{
				score.life = UnityEngine.Mathf.Max(0, score.life - damage);
			}
		}

		public virtual void UpdateScoreRank()
		{
			if (scoreInfo != null && (scoreInfo.s > 0 || scoreInfo.a > 0 || scoreInfo.b > 0 || scoreInfo.c > 0))
			{
				score.rank = ScoreGaugeCalculator.GetScoreRank(scoreInfo, score.totalScore);
				return;
			}

			// OjskCommunity fallback for custom scores: keep rank thresholds aligned with ScoreGaugeCalculator.Create(1000000).
			int rankS = UnityEngine.Mathf.FloorToInt(ScoreGaugeCalculator.RankRateS * DefaultTotalScore);
			int rankA = UnityEngine.Mathf.FloorToInt(ScoreGaugeCalculator.RankRateA * DefaultTotalScore);
			int rankB = UnityEngine.Mathf.FloorToInt(ScoreGaugeCalculator.RankRateB * DefaultTotalScore);
			int rankC = UnityEngine.Mathf.FloorToInt(ScoreGaugeCalculator.RankRateC * DefaultTotalScore);
			score.rank = score.totalScore >= rankS ? ScoreRank.S
				: score.totalScore >= rankA ? ScoreRank.A
				: score.totalScore >= rankB ? ScoreRank.B
				: score.totalScore >= rankC ? ScoreRank.C
				: ScoreRank.D;
		}
	}
}
