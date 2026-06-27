using MessagePack;

namespace Sekai
{
	[MessagePackObject(false)]
	public class VideoGenerationBootData : FreeLiveBootData
	{
		[Key("isVideoGenerationMode")]
		public bool IsVideoGenerationMode { get; set; } = true;

		[Key("videoGenerationSpeedMultiplier")]
		public int VideoGenerationSpeedMultiplier { get; set; } = 1; // 新方案：正常速度录制

		[Key("videoGenerationMuteAudio")]
		public bool VideoGenerationMuteAudio { get; set; } = true;

		[Key("videoGenerationDisablePause")]
		public bool VideoGenerationDisablePause { get; set; } = true;

		[Key("videoGenerationAudioPath")]
		public string VideoGenerationAudioPath { get; set; }

		public VideoGenerationBootData(int musicId, string difficulty, int vocalId, int deckId, int[] formationIndexes, LiveMusicData.CollaborationModeState collaboModeState, bool isOriginalMember = false, LiveScoreMode liveScoreMode = LiveScoreMode.Normal, LiveTournamentMode liveTournamentMode = LiveTournamentMode.None)
			: base(musicId, difficulty, vocalId, deckId, formationIndexes, collaboModeState, isOriginalMember, liveScoreMode, liveTournamentMode)
		{
			// Video Generation Mode always uses Auto play mode (not test Auto mode)
			IsAuto = true;
		}

		public VideoGenerationBootData(int musicId, string difficulty, int vocalId)
			: base(musicId, difficulty, vocalId)
		{
			// Video Generation Mode always uses Auto play mode (not test Auto mode)
			IsAuto = true;
		}

		public VideoGenerationBootData(int musicId, string difficulty, int vocalId, int deckId, LivePlayMode playMode, LiveMusicData.CollaborationModeState collaboModeState, MusicCategory category = MusicCategory.original)
			: base(musicId, difficulty, vocalId, deckId, playMode, collaboModeState, category)
		{
			// Video Generation Mode always uses Auto play mode (not test Auto mode)
			IsAuto = true;
		}
	}
}