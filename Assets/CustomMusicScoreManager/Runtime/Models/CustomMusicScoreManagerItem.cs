using System;
using Sekai.Localization;
using Sekai.MusicScoreMaker.Common;

namespace Sekai.CustomMusicScoreManager
{
	public sealed class CustomMusicScoreManagerItem
	{
		public CustomMusicScoreEntry Entry { get; }

		public DateTime LastWriteTime { get; }

		public bool HasManifest { get; }

		public bool HasScore { get; }

		public bool HasAudio { get; }

		public bool HasJacket { get; }

		public string StatusText
		{
			get
			{
				if (!HasManifest)
				{
					return LocalizationManager.Get("manager.item.missing_config");
				}
				if (!HasScore)
				{
					return LocalizationManager.Get("manager.item.missing_score");
				}
				if (!HasAudio)
				{
					return LocalizationManager.Get("manager.item.missing_audio");
				}
				if (!HasJacket)
				{
					return LocalizationManager.Get("manager.item.missing_jacket");
				}
				return LocalizationManager.Get("manager.item.ready");
			}
		}

		public bool IsReadyForEdit => HasManifest;

		public CustomMusicScoreManagerItem(
			CustomMusicScoreEntry entry,
			DateTime lastWriteTime,
			bool hasManifest,
			bool hasScore,
			bool hasAudio,
			bool hasJacket)
		{
			Entry = entry;
			LastWriteTime = lastWriteTime;
			HasManifest = hasManifest;
			HasScore = hasScore;
			HasAudio = hasAudio;
			HasJacket = hasJacket;
		}
	}
}
