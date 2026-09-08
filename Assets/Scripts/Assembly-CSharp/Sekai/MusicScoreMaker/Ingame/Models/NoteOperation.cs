namespace Sekai.MusicScoreMaker.Ingame.Models
{
	public struct NoteOperation
	{
		public int Id { get; }

		public int StartLane { get; }

		public int EndLane { get; }

		public long Ticks { get; }
		public float? GuideStartOffset { get; }
		public float? GuideEndOffset { get; }

		public NoteOperation(int id, int startLane, int endLane, long ticks, float? guideStartOffset = null, float? guideEndOffset = null)
		{
			Id = id;
			StartLane = startLane;
			EndLane = endLane;
			Ticks = ticks;
			GuideStartOffset = guideStartOffset;
			GuideEndOffset = guideEndOffset;
		}

		public static bool operator ==(NoteOperation left, NoteOperation right)
		{
			return left.Id == right.Id && left.StartLane == right.StartLane && left.EndLane == right.EndLane && left.Ticks == right.Ticks && left.GuideStartOffset == right.GuideStartOffset && left.GuideEndOffset == right.GuideEndOffset;
		}

		public static bool operator !=(NoteOperation left, NoteOperation right)
		{
			return !(left == right);
		}
	}
}
