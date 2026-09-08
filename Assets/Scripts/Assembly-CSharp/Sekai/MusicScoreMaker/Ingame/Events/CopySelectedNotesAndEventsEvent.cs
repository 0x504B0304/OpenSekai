namespace Sekai.MusicScoreMaker.Ingame.Events
{
	public class CopySelectedNotesAndEventsEvent : MusicScoreMakerDispatcherEventBase
	{
		public bool IsCut { get; set; }

		public CopySelectedNotesAndEventsEvent()
		{
		}
	}
}
