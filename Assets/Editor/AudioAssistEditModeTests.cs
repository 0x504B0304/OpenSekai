#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Sekai.Live;
using Sekai.MusicScoreMaker.Ingame.AudioAssist;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Presenters;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AudioAssistEditModeTests
{
    [Test]
    public void AlignmentEndTimesSurviveLoadingAndSessionSaveWithoutFillingSilentGaps()
    {
        var output=JsonUtility.FromJson<AssistAnalysisOutput>("{\"lyrics\":[{\"seconds\":12,\"text\":\"テト\",\"syllables\":[{\"seconds\":12.1,\"end\":12.18,\"label\":\"テ\"},{\"seconds\":12.4,\"end\":12.7,\"label\":\"ト\"}]}]}");
        var state=new AssistState();state.AcceptAlignment(output.lyrics,9);
        state=JsonUtility.FromJson<AssistState>(JsonUtility.ToJson(state));
        var point=state.lyrics[0].syllables[0];
        Assert.AreEqual(12.18,AudioAssistAlgorithms.SyllableEnd(point,12.4,30),1e-8);
        Assert.AreEqual(.08,point.end-point.seconds,1e-8,"Do not stretch an aligned syllable over the following silence.");
        state.lyricOffset=.5;
        Assert.AreEqual(12.68,AudioAssistAlgorithms.SyllableEnd(point,12.4,30)+state.lyricOffset,1e-8);
    }
    [Test]
    public void LegacyDurationRecoveryMatchesTimeAndLabelAndNeverReplacesKnownEnds()
    {
        var lyrics=AudioAssistAlgorithms.ParseLrc("[00:12]<00:12>ラ");
        var point=lyrics[0].syllables[0];
        Assert.AreEqual(12.25,AudioAssistAlgorithms.SyllableEnd(point,0,30));
        Assert.AreEqual(12.1,AudioAssistAlgorithms.SyllableEnd(point,12.1,30));
        var cache=new System.Collections.Generic.List<AssistLyric>{new AssistLyric{syllables=new System.Collections.Generic.List<AssistDraft>{
            new AssistDraft{seconds=11,end=11.5,label="ラ"},new AssistDraft{seconds=12,end=12.8,label="ヤ"},new AssistDraft{seconds=12,end=12.14,label="ラ"}}}};
        Assert.IsTrue(AudioAssistAlgorithms.RestoreSyllableEnds(lyrics,cache));
        Assert.AreEqual(12.14,point.end);
        cache[0].syllables[2].end=12.9;
        Assert.IsFalse(AudioAssistAlgorithms.RestoreSyllableEnds(lyrics,cache));Assert.AreEqual(12.14,point.end);
        Assert.AreEqual(12.12,AudioAssistAlgorithms.SyllableEnd(point,13,12.12));
    }
    [Test]
    public void EnhancedLrcHonorsExplicitTrailingEndTimestampAndRepeatedLineOffset()
    {
        var lyrics=AudioAssistAlgorithms.ParseLrc("[offset:250]\n[00:10][00:20]<00:10>テ<00:10.120>ト<00:10.350>");
        Assert.AreEqual(2,lyrics[0].syllables.Count);
        Assert.AreEqual(10.6,lyrics[0].syllables[1].end,1e-8);
        Assert.AreEqual(20.6,lyrics[1].syllables[1].end,1e-8);
    }
    [Test]
    public void WaveDividerOwnsRendererWhenCreatedOnAnEmptyRect()
    {
        var go=new GameObject("WaveDivider",typeof(RectTransform));
        try
        {
            go.AddComponent<AudioAssistSplitter>();
            Assert.IsNotNull(go.GetComponent<CanvasRenderer>());
            go.SetActive(false);go.SetActive(true);
        }
        finally{Object.DestroyImmediate(go);}
    }
    [Test]
    public void MagnifiedWaveformSeparatesTransientsInsideOneOldTenMillisecondBin()
    {
        const int hz=48000;var pcm=new float[hz*2];
        pcm[60*2]=.9f;pcm[90*2+1]=-.6f;
        var analysis=AudioAssistAlgorithms.Analyze(pcm,2,hz,CancellationToken.None);
        Assert.AreEqual(.9f,analysis.peaks[0]);
        Assert.AreEqual(.9f,analysis.waveform.Peak(59.1/hz,60.9/hz));
        Assert.AreEqual(0,analysis.waveform.Peak(70.1/hz,80.9/hz),"A transient must not smear across the old 10 ms bucket.");
        Assert.AreEqual(.6f,analysis.waveform.Peak(89.1/hz,90.9/hz),"Keep either stereo channel, including negative samples.");
        Assert.AreEqual(.9f,analysis.waveform.Peak(0,1),"Zooming out must retain a single-sample transient.");
    }

    [Test]
    public void WaveformPyramidMatchesRawPcmAtAllZoomLevelsAndFileEdges()
    {
        const int hz=44100,frames=8197;var random=new System.Random(711);
        var pcm=Enumerable.Range(0,frames*2).Select(_=>(float)(random.NextDouble()*2-1)).ToArray();
        var waveform=new AssistWaveformPeaks(pcm,2,hz,CancellationToken.None);
        for(int iteration=0;iteration<600;iteration++)
        {
            int from=random.Next(-100,frames+100),to=from+random.Next(1,frames);
            float expected=0;
            for(int i=Math.Max(0,from)*2;i<Math.Min(frames,to)*2;i++)expected=Math.Max(expected,Math.Abs(pcm[i]));
            Assert.AreEqual(expected,waveform.Peak((from+.1)/hz,(to-.1)/hz),$"Range {from}..{to}");
        }
        Assert.AreEqual(0,waveform.Peak(-1,-.1));Assert.AreEqual(0,waveform.Peak(1,2));
        Assert.Throws<OperationCanceledException>(()=>new AssistWaveformPeaks(pcm,2,hz,new CancellationToken(true)));
    }

    [Test]
    public void WaveformTimeMappingPreservesSubTickDetailAcrossTempoChanges()
    {
        Editor();
        var infos=Sekai.MusicScoreMaker.Ingame.Utilities.MusicScoreMakerUtility.ConvertMusicScoreInfo(model.MusicScoreMakerData.MusicScoreEventDataList);
        var seconds=Sekai.MusicScoreMaker.Ingame.Utilities.MusicScoreMakerUtility.CreatePreciseTimeConverter(infos);
        Assert.AreEqual(.25/960,seconds(.25),1e-10);
        Assert.AreEqual(2,seconds(1920),1e-10);
        Assert.AreEqual(2+.25/1440,seconds(1920.25),1e-10);
        Assert.AreEqual(2-.25/960,seconds(1919.75),1e-10);
    }

    [Test]
    public void LyricOffsetSurvivesReanalysisAndSessionRestore()
    {
        var state=new AssistState{lyricOffset=9};
        var aligned=AudioAssistAlgorithms.ParseLrc("[00:12.000]<00:12.000>ラ");
        state.AcceptAlignment(aligned,state.AlignmentOffset);
        Assert.AreEqual(0,state.lyricOffset);Assert.AreEqual(9,state.AlignmentOffset);
        state=JsonUtility.FromJson<AssistState>(JsonUtility.ToJson(state));
        state.AcceptAlignment(aligned,state.AlignmentOffset);
        Assert.AreEqual(9,state.AlignmentOffset,"Repeated analysis must retain the padding offset of the source LRC.");
        state.lyricOffset=.125;
        state.AcceptAlignment(aligned,state.AlignmentOffset);
        Assert.AreEqual(9.125,state.AlignmentOffset);Assert.AreEqual(0,state.lyricOffset);
    }
    [Test]
    public void LrcHandlesOffsetsRepeatedLinesAndEnhancedSyllables()
    {
        var lines = AudioAssistAlgorithms.ParseLrc("[ar:test]\n[offset:+250]\n[00:10.10][00:20.10]<00:10.100>ヤ<00:10.220>ラ<00:10.340>ラ<00:10.460>ラ\n[00:30]普通歌词");
        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual("ヤラララ", lines[0].text);
        Assert.AreEqual(10.35, lines[0].seconds, 1e-8);
        CollectionAssert.AreEqual(new[] { 20.35, 20.47, 20.59, 20.71 }, lines[1].syllables.Select(s => Math.Round(s.seconds, 2)).ToArray());
        Assert.IsEmpty(lines[2].syllables, "Line timestamps must never become fabricated syllable timings.");
    }

    [TestCase(.5)] [TestCase(.75)]
    public void SlowAudioPreservesPitchDurationAndStereoPhase(double rate)
    {
        const int hz=16000, frames=32000;
        var input=new float[frames*2];
        for(int n=0;n<frames;n++){input[2*n]=(float)(.4*Math.Sin(2*Math.PI*440*n/hz));input[2*n+1]=-input[2*n];}
        var result=AudioAssistAlgorithms.Stretch(input,2,hz,rate,CancellationToken.None);
        Assert.AreEqual((int)Math.Ceiling(frames/rate)*2,result.Length);
        int crossings=0, start=hz/2, end=result.Length/2-hz/2;
        for(int n=start+1;n<end;n++)
        {
            if(result[n*2-2]<=0&&result[n*2]>0)crossings++;
            Assert.AreEqual(-result[n*2],result[n*2+1],1e-6);
        }
        Assert.AreEqual(440,crossings*hz/(double)(end-start),4);
        Assert.Throws<OperationCanceledException>(()=>AudioAssistAlgorithms.Stretch(input,2,hz,rate,new CancellationToken(true)));
    }

    [Test]
    public void OnsetsUseSignalRatherThanEvenlySpacedPoints()
    {
        const int hz=16000;var pcm=new float[hz*3];
        foreach(double at in new[]{.31,1.17,2.45})
            for(int n=0;n<800;n++)pcm[(int)(at*hz)+n]=(float)(Math.Cos(n*.8)*Math.Exp(-n/100.0));
        var analyzed=AudioAssistAlgorithms.Analyze(pcm,1,hz,CancellationToken.None);
        foreach(double at in new[]{.31,1.17,2.45})Assert.Less(analyzed.onsets.Min(t=>Math.Abs(t-at)),.021);
        Assert.IsEmpty(AudioAssistAlgorithms.Analyze(new float[hz],1,hz,CancellationToken.None).onsets);
    }

    [TestCase(1)] [TestCase(.75)] [TestCase(.5)]
    public void LoopClockRetainsRemainderAcrossManyIterations(double rate)
    {
        Assert.AreEqual(3,AudioAssistAlgorithms.LoopPosition(3,-.07,rate,true,2,4));
        for(int i=1;i<=100;i++)
            Assert.AreEqual(2.125,AudioAssistAlgorithms.LoopPosition(3,(1+2*i+.125)/rate,rate,true,2,4),1e-8);
    }

    private GameObject root;
    private MusicScoreMakerPresenter presenter;
    private MusicScoreMakerModel model;
    private AudioAssistController controller;
    private WheelTestDispatcher dispatcher;
    private void Editor()
    {
        root=new GameObject("AudioAssistTest");
        dispatcher=root.AddComponent<WheelTestDispatcher>();dispatcher.SetupInstance();
        model=new MusicScoreMakerModel(null){MusicScoreMakerData=new MusicScoreMakerData(),FillerSec=3.25f,MasterMusicSec=30,MusicLength=30000};
        model.MusicScoreMakerData.MusicScoreEventDataList.Add(new MusicScoreEventData { id=100, ticks=0,eventType=MusicScoreEventType.BPM,changeValue=120 });
        model.MusicScoreMakerData.MusicScoreEventDataList.Add(new MusicScoreEventData { id=101, ticks=1920,eventType=MusicScoreEventType.BPM,changeValue=180 });
        presenter=MusicScoreMakerPresenter.Create(model,null,CancellationToken.None,null).GetAwaiter().GetResult();
        controller=root.AddComponent<AudioAssistController>();controller.enabled=false;
        Set(controller,"Presenter",presenter);
        var transport=root.AddComponent<AudioAssistTransport>();transport.State=controller.State;transport.Duration=30;
        Set(controller,"Transport",transport);presenter.AudioAssist=controller;
    }
    private static void Set(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
    private bool Place(int lane)=>(bool)typeof(MusicScoreMakerPresenter).GetMethod("TryPlaceAssistDraft",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(presenter,new object[]{lane});
    [TearDown] public void TearDown(){if(root!=null)Object.DestroyImmediate(root);}

    [Test]
    public void AudioTimeMappingUsesFillerAndTempoChanges()
    {
        Editor();
        Assert.AreEqual(3.25,presenter.AssistSeconds(0),1e-6);
        Assert.AreEqual(5.25,presenter.AssistSeconds(1920),1e-6);
        Assert.AreEqual(5.25+1.0/3,presenter.AssistSeconds(2400),1e-5);
        foreach(long tick in new long[]{0,120,1920,2400,6000})Assert.AreEqual(tick,presenter.AssistTicks(presenter.AssistSeconds(tick)),1);
    }
    [Test]
    public void ManualSyllableEditsAndDeletionPersistAndShareUndoWithoutChangingPlacedNotes()
    {
        Editor();model.SelectedNoteCategory=NoteCategory.Normal;
        var draft=new AssistDraft{seconds=10.1,label="existing draft"};controller.State.drafts.Add(draft);controller.SelectedDraft=draft;Place(2);
        var note=model.MusicScoreMakerData.NoteList.Single();long noteTicks=note.ticks;
        var point=new AssistDraft{seconds=1.1,end=1.3,label="テ",confidence=.2f};
        var line=new AssistLyric{seconds=1,text="テト"};line.syllables.Add(point);controller.State.lyrics.Add(line);controller.State.lyricOffset=9;
        string folder=Path.Combine(Path.GetTempPath(),"OpenSekai-SyllableEdit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var field=typeof(AudioAssistController).GetField("directory",BindingFlags.NonPublic|BindingFlags.Instance);field.SetValue(controller,folder);
        try
        {
            Assert.IsTrue(controller.EditSyllable(point,"ト",10.15,10.6,out var error),error);
            Assert.AreEqual(1.15,point.seconds,1e-8);Assert.AreEqual(1.6,point.end,1e-8);Assert.AreEqual("ト",point.label);Assert.IsTrue(point.manuallyEdited);
            var saved=JsonUtility.FromJson<AssistState>(File.ReadAllText(Path.Combine(folder,"session.json")));
            Assert.AreEqual("ト",saved.lyrics[0].syllables[0].label);Assert.IsTrue(saved.lyrics[0].syllables[0].manuallyEdited);
            Assert.IsTrue(controller.DeleteSyllable(point,out error),error);Assert.IsEmpty(line.syllables);
            dispatcher.Undo();Assert.AreSame(point,line.syllables.Single());Assert.AreEqual("ト",point.label);
            dispatcher.Undo();Assert.AreEqual("テ",point.label);Assert.AreEqual(1.1,point.seconds);Assert.AreEqual(1.3,point.end);Assert.IsFalse(point.manuallyEdited);
            dispatcher.Redo();Assert.AreEqual("ト",point.label);dispatcher.Redo();Assert.IsEmpty(line.syllables);
            Assert.AreEqual(noteTicks,model.MusicScoreMakerData.NoteList.Single().ticks);Assert.AreEqual(10.1,draft.seconds);Assert.AreEqual("existing draft",draft.label);
            saved=JsonUtility.FromJson<AssistState>(File.ReadAllText(Path.Combine(folder,"session.json")));Assert.IsEmpty(saved.lyrics[0].syllables);
        }
        finally{field.SetValue(controller,null);Directory.Delete(folder,true);}
    }
    [TestCase(-1,2,"ラ")][TestCase(2,2,"ラ")][TestCase(3,2,"ラ")][TestCase(1,31,"ラ")]
    [TestCase(double.NaN,2,"ラ")][TestCase(1,double.PositiveInfinity,"ラ")][TestCase(1,2," ")]
    public void InvalidSyllableEditsDoNotMutateDataOrHistory(double start,double end,string text)
    {
        Editor();var point=new AssistDraft{seconds=1,end=2,label="ラ"};var line=new AssistLyric();line.syllables.Add(point);controller.State.lyrics.Add(line);
        Assert.IsFalse(controller.EditSyllable(point,text,start,end,out var error));Assert.IsNotEmpty(error);
        Assert.AreEqual(1,point.seconds);Assert.AreEqual(2,point.end);Assert.AreEqual("ラ",point.label);Assert.IsFalse(dispatcher.CanUndo);
    }
    [Test]
    public void UndoOfAnOldSyllableEditCannotReinsertLyricsReplacedByReanalysis()
    {
        Editor();var point=new AssistDraft{seconds=1,end=2,label="ラ"};var line=new AssistLyric();line.syllables.Add(point);controller.State.lyrics.Add(line);
        Assert.IsTrue(controller.DeleteSyllable(point,out _));
        controller.State.AcceptAlignment(AudioAssistAlgorithms.ParseLrc("[00:01]新歌词"),0);
        dispatcher.Undo();Assert.AreEqual("新歌词",controller.State.lyrics.Single().text);Assert.IsEmpty(controller.State.lyrics[0].syllables);
    }

    [Test]
    public async Task ReplacedSourceInvalidatesStemsButUnchangedSourceKeepsCancelledAttempt()
    {
        Editor();
        string folder=Path.Combine(Path.GetTempPath(),"OpenSekai-CacheTest-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var directoryField=typeof(AudioAssistController).GetField("directory",BindingFlags.NonPublic|BindingFlags.Instance);
        directoryField.SetValue(controller,folder);
        var check=typeof(AudioAssistController).GetMethod("CheckSource",BindingFlags.NonPublic|BindingFlags.Instance);
        try
        {
            File.WriteAllText(Path.Combine(folder,"source.wav"),"source A");
            controller.State.stems.Add(new AssistStem{key="original",path="source.wav",enabled=true});
            controller.State.stems.Add(new AssistStem{key="vocals"});
            await (Task)check.Invoke(controller,new object[]{CancellationToken.None});
            string first=controller.State.sourceFingerprint;
            controller.State.automaticAttempt=first;
            controller.State.stems[1].path="cached.wav";controller.State.stems[1].enabled=true;
            await (Task)check.Invoke(controller,new object[]{CancellationToken.None});
            Assert.AreEqual(first,controller.State.automaticAttempt,"Reopening after cancellation must not launch analysis again.");
            Assert.AreEqual("cached.wav",controller.State.stems[1].path);
            File.WriteAllText(Path.Combine(folder,"source.wav"),"source B");
            await (Task)check.Invoke(controller,new object[]{CancellationToken.None});
            Assert.AreNotEqual(first,controller.State.sourceFingerprint);
            Assert.IsNull(controller.State.automaticAttempt);Assert.IsNull(controller.State.stems[1].path);
            Assert.IsFalse(controller.State.stems[1].enabled);
            var saved=JsonUtility.FromJson<AssistState>(File.ReadAllText(Path.Combine(folder,"session.json")));
            Assert.AreEqual(controller.State.sourceFingerprint,saved.sourceFingerprint);
        }
        finally { directoryField.SetValue(controller,null);Directory.Delete(folder,true); }
    }

    [Test]
    public void PlayDuringAnalysisDoesNotLeaveTheEditorMarkedPlaying()
    {
        Editor();Set(controller,"Busy",true);
        var onPlay=typeof(MusicScoreMakerPresenter).GetMethod("OnPlayMusic",BindingFlags.Instance|BindingFlags.NonPublic);
        dispatcher.Register<PlayMusicEvent>(e=>onPlay.Invoke(presenter,new object[]{e}));
        var toggle=typeof(MusicScoreMakerPresenter).GetMethod("SwitchPlayPauseMusic",BindingFlags.Instance|BindingFlags.NonPublic);
        Assert.IsFalse((bool)toggle.Invoke(presenter,new object[]{new SwitchPlayPauseMusicEvent()}));
        Assert.IsFalse(model.IsMusicPlaying);Assert.IsFalse(controller.Transport.Playing);
    }

    [Test]
    public void DraftPlacementAndLongChainShareGameUndoHistory()
    {
        Editor();model.SelectedNoteCategory=NoteCategory.Normal;
        var tap=new AssistDraft{seconds=4.25,label="tap"};controller.State.drafts.Add(tap);controller.SelectedDraft=tap;
        Assert.IsTrue(Place(2));Assert.AreEqual(1,model.MusicScoreMakerData.NoteList.Count);Assert.IsTrue(tap.used);
        Assert.AreEqual(960,model.MusicScoreMakerData.NoteList[0].ticks);
        dispatcher.Undo();Assert.IsFalse(tap.used);Assert.IsEmpty(model.MusicScoreMakerData.NoteList);
        dispatcher.Redo();Assert.IsTrue(tap.used);Assert.AreEqual(1,model.MusicScoreMakerData.NoteList.Count);
        model.SelectedNoteCategory=NoteCategory.Long;
        var points=Enumerable.Range(0,4).Select(i=>new AssistDraft{seconds=6+i*.25}).ToArray();
        controller.State.drafts.AddRange(points);
        for(int i=0;i<4;i++){controller.SelectedDraft=points[i];controller.FinishHold=i==3;Assert.IsTrue(Place(i+3));}
        var chain=model.MusicScoreMakerData.NoteList.Where(n=>n.ticks>1920).OrderBy(n=>n.ticks).ToArray();
        Assert.AreEqual(4,chain.Length);
        for(int i=1;i<4;i++){Assert.AreEqual(chain[i].id,chain[i-1].nextConnectionId);Assert.AreEqual(chain[i-1].id,chain[i].previousConnectionId);}
        Assert.IsTrue(points.All(p=>p.used));
        dispatcher.Undo();Assert.AreEqual(1,model.MusicScoreMakerData.NoteList.Count);Assert.IsTrue(points.All(p=>!p.used));
        dispatcher.Redo();Assert.AreEqual(5,model.MusicScoreMakerData.NoteList.Count);
    }

    [Test]
    public void TempoCalibrationRestoresOriginalEventsOnUndo()
    {
        Editor();presenter.ApplyAssistBpm(5.25,150);
        Assert.AreEqual(150,model.MusicScoreMakerData.MusicScoreEventDataList.Single(e=>e.ticks==1920).changeValue);
        dispatcher.Undo();Assert.AreEqual(180,model.MusicScoreMakerData.MusicScoreEventDataList.Single(e=>e.ticks==1920).changeValue);
        dispatcher.Redo();Assert.AreEqual(150,model.MusicScoreMakerData.MusicScoreEventDataList.Single(e=>e.ticks==1920).changeValue);
    }
}
#endif
