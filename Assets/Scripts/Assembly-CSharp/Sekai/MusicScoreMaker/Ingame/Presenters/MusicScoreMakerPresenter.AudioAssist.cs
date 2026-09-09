using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Live;
using Sekai.MusicScoreMaker.Ingame.AudioAssist;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Utilities;

namespace Sekai.MusicScoreMaker.Ingame.Presenters
{
    public partial class MusicScoreMakerPresenter
    {
        public AudioAssistController AudioAssist { get; set; }
        private bool _assistPlaybackActive;
        public double AssistSeconds(long ticks) => (_model?.MusicScoreMakerData?.GetTimeFromTicks(ticks) ?? 0) + (_model?.FillerSec ?? 0);
        public long AssistTicks(double seconds) => Math.Max(0, MusicScoreMakerUtility.GetTicksFromTime((float)Math.Max(0, seconds - (_model?.FillerSec ?? 0)), _model.MusicScoreMakerData.MusicScoreEventDataList));
        public void SeekAssist(double seconds)
        {
            AudioAssist?.ClearAuditionEnd();
            AudioAssist?.Transport?.Seek(seconds);
            SetFocusTicks(AssistTicks(seconds));
            NotifyMusicScoreAndTimelineChanged();
        }
        public void PauseAssist() { OnPauseMusic(new PauseMusicEvent()); }
        public void PlayAssist(double seconds) { PauseAssist();SeekAssist(seconds);OnPlayMusic(new PlayMusicEvent()); }
        public void AssistHistory(Action undo, Action redo)
        {
            if (_model == null || _model.IsEditRestricted || _model.IsMusicPlaying) return;
            PushUndoableAction(undo, redo, _model.FocusTicks, _model.FocusTicks);
        }
        public void ApplyAssistBpm(double seconds, double bpm)
        {
            if (double.IsNaN(bpm) || double.IsInfinity(bpm) || bpm < 20 || bpm > 1000) throw new ArgumentOutOfRangeException(nameof(bpm));
            var data = CurrentData();long ticks = AssistTicks(seconds);
            var previous = data.MusicScoreEventDataList.Where(e => e.eventType == MusicScoreEventType.BPM && e.ticks == ticks).ToList();
            var created = new MusicScoreEventData { id = data.GetNewId(), ticks = ticks, eventType = MusicScoreEventType.BPM, changeValue = (float)bpm };
            AssistHistory(() => { data.MusicScoreEventDataList.Remove(created);data.MusicScoreEventDataList.AddRange(previous);data.MusicScoreEventDataList.Sort((x,y)=>x.ticks.CompareTo(y.ticks));NotifyMusicScoreAndTimelineChanged(true); },
                () => { foreach (var e in previous) data.MusicScoreEventDataList.Remove(e);data.MusicScoreEventDataList.Add(created);data.MusicScoreEventDataList.Sort((x,y)=>x.ticks.CompareTo(y.ticks));NotifyMusicScoreAndTimelineChanged(true); });
        }
        private readonly List<(AssistDraft draft, int lane)> assistHold = new List<(AssistDraft, int)>();
        public void CancelAssistHold() { assistHold.Clear(); }
        private bool TryPlaceAssistDraft(int lane)
        {
            var controller = AudioAssist;var draft = controller?.SelectedDraft;
            if (draft == null || draft.used || _model.IsMusicPlaying || _model.IsEditRestricted) return false;
            var data = CurrentData();long ticks = AssistTicks(draft.seconds);
            if (lane < 0 || lane > MusicScoreMakerModel.LaneCountMinus1) return true;
            bool hold = IsLongRootCategory(_model.SelectedNoteCategory);
            if (!hold && assistHold.Count > 0) { assistHold.Clear();controller.FinishHold = false; }
            if (hold)
            {
                if (assistHold.Any(p => p.draft.id == draft.id) || (assistHold.Count > 0 && AssistTicks(assistHold[assistHold.Count - 1].draft.seconds) >= ticks))
                { controller.Status = "长条点必须按时间先后指定";return true; }
                assistHold.Add((draft, lane));
                if (!controller.FinishHold || assistHold.Count < 2)
                {
                    controller.SelectNextDraft(draft, assistHold.Select(p => p.draft.id));
                    controller.Status = $"已指定 {assistHold.Count} 个长条点；开启“下次为尾点”后指定终点";return true;
                }
            }
            var used = hold ? assistHold.Select(p => p.draft).ToList() : new List<AssistDraft> { draft };
            var notes = new List<MusicScoreNoteBase>();
            if (hold)
            {
                NoteCategory root = _model.SelectedNoteCategory;
                GetLongEndNoteCategoryAndBaseType(root, out var tail, out var tailBase);
                for (int i = 0; i < assistHold.Count; i++)
                {
                    var point = assistHold[i];var range = GetLaneRangeFromCenterLane(point.lane, Math.Max(1, _model.LastNoteWidth));
                    var category = i == 0 ? root : i == assistHold.Count - 1 ? tail : NoteCategory.Connection;
                    var baseType = i == 0 ? FindNoteBaseType(root) : i == assistHold.Count - 1 ? tailBase : FindNoteBaseType(NoteCategory.Connection);
                    var note = CreateMusicScoreNoteBaseWithCategory(AssistTicks(point.draft.seconds), data.GetNewId(), range.Item1, range.Item2, category, baseType);
                    if (notes.Count > 0) { note.previousConnectionId = notes[notes.Count - 1].id;notes[notes.Count - 1].nextConnectionId = note.id; }
                    notes.Add(note);
                }
            }
            else notes.AddRange(GenerateNote(lane, ticks));
            if (notes.Count == 0 || !MusicScoreMakerUtility.ValidateLongNoteTicks(notes)) return true;
            AssistHistory(() => { data.RemoveNoteRange(notes);foreach (var d in used) { d.used = false;d.placedNoteIds.Clear(); }controller.SelectedDraft = used[0];controller.Save();MarkEditedAndRefresh(notes); },
                () => { data.AddNoteRange(notes);foreach (var d in used) { d.used = true;d.placedNoteIds = notes.Select(n => n.id).ToList(); }data.UpdateConnectionNotes();controller.SelectNextDraft(used[used.Count - 1]);controller.Save();MarkEditedAndRefresh(notes); });
            assistHold.Clear();controller.FinishHold = false;controller.Status = "已按草稿时间落键；可以撤销";PlayToolTypeToNoteSe();return true;
        }
    }
}
