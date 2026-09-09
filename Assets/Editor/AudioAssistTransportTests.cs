#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sekai.MusicScoreMaker.Ingame.AudioAssist;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Collections;

public sealed class AudioAssistPlayModeTests
{
    private bool originalOptionsEnabled;
    private EnterPlayModeOptions originalOptions;
    [SetUp] public void Setup()
    {
        originalOptionsEnabled=EditorSettings.enterPlayModeOptionsEnabled;originalOptions=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }
    [TearDown] public void Restore(){EditorSettings.enterPlayModeOptionsEnabled=originalOptionsEnabled;EditorSettings.enterPlayModeOptions=originalOptions;}
    [UnityTest]
    public IEnumerator EditorWithoutUnityListenerProducesOriginalAndStemAudio()
    {
        yield return new EnterPlayMode(false);
        Assert.IsEmpty(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None), "Match the real CRI editor scene: no Unity listener supplied by the test.");
        var root=new GameObject("AssistOutputTest");
        var transport=root.AddComponent<AudioAssistTransport>();
        transport.State=new AssistState{music=true};transport.Duration=4;
        transport.SetAssistMixActive(true);
        var clips=new List<AudioClip>();
        int captureRate=Time.captureFramerate;
        bool recording=false;
        try
        {
            foreach(var pair in new[]{("original",440f),("vocals",880f)})
            {
                var state=new AssistStem{key=pair.Item1,enabled=pair.Item1=="original",volume=.5f};
                transport.State.stems.Add(state);
                float[] pcm=new float[44100*4];for(int i=0;i<pcm.Length;i++)pcm[i]=.3f*Mathf.Sin(2*Mathf.PI*pair.Item2*i/44100);
                var clip=AudioClip.Create(pair.Item1,pcm.Length,1,44100,false);clip.SetData(pcm,0);clips.Add(clip);
                transport.Add(pair.Item1,clip,pcm,new AssistAnalysis(),state,false);
            }
            Time.captureFramerate=60;recording=AudioRenderer.Start();Assert.IsTrue(recording);
            transport.Play(0);
            float[] output=null;
            yield return CaptureOutput(samples=>output=samples);
            Assert.Greater(Tone(output,440),.02,"Original must reach Unity's final mixer output, not merely advance its clock.");
            Assert.Less(Tone(output,880),.005);
            transport.State.stems[0].enabled=false;transport.State.stems[1].enabled=true;transport.ApplyMix();
            yield return CaptureOutput(samples=>output=samples);
            Assert.Greater(Tone(output,880),.02,"Selecting the vocal stem must produce that stem's actual signal.");
            Assert.Less(Tone(output,440),.005);
            transport.State.stems[1].enabled=false;transport.ApplyMix();
            yield return CaptureOutput(samples=>output=samples);
            Assert.Less(output.Max(v=>Mathf.Abs(v)),.001,"Turning every stem off must still intentionally mute the mix.");
            root.SetActive(false);
            Assert.IsEmpty(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l=>l.isActiveAndEnabled),"Closing the editor must release its listener.");
            root.SetActive(true);
            Object.Destroy(transport);yield return null;yield return null;
            Assert.IsEmpty(root.GetComponentsInChildren<AudioListener>(true),"Component-only shutdown must remove its listener from the retained editor view.");
        }
        finally {if(recording)AudioRenderer.Stop();Time.captureFramerate=captureRate;Object.Destroy(root);foreach(var clip in clips)Object.Destroy(clip);}
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator CollapsedPanelPlaysOriginalAndReopeningRestoresSavedMix()
    {
        yield return new EnterPlayMode(false);
        var root=new GameObject("AssistFoldMixTest");
        var transport=root.AddComponent<AudioAssistTransport>();
        transport.State=new AssistState{music=false};transport.Duration=12;
        var clips=new List<AudioClip>();
        int captureRate=Time.captureFramerate;
        bool recording=false;
        try
        {
            foreach(var pair in new[]{("original",440f),("vocals",880f)})
            {
                var state=new AssistStem{key=pair.Item1,enabled=pair.Item1=="vocals",volume=pair.Item1=="original"?0:.5f};
                transport.State.stems.Add(state);
                float[] pcm=new float[44100*12];for(int i=0;i<pcm.Length;i++)pcm[i]=.3f*Mathf.Sin(2*Mathf.PI*pair.Item2*i/44100);
                var clip=AudioClip.Create(pair.Item1,pcm.Length,1,44100,false);clip.SetData(pcm,0);clips.Add(clip);
                transport.Add(pair.Item1,clip,pcm,new AssistAnalysis(),state,false);
            }
            Time.captureFramerate=60;recording=AudioRenderer.Start();Assert.IsTrue(recording);
            Assert.IsFalse(transport.AssistMixActive,"Start collapsed even when restoring a muted assist session.");
            string saved=JsonUtility.ToJson(transport.State);
            transport.Play(0);
            float[] output=null;
            yield return CaptureOutput(samples=>output=samples);
            Assert.Greater(Tone(output,440),.02,"Collapsed startup must ignore assist music mute, original deselection and zero volume.");
            Assert.Less(Tone(output,880),.005,"Selected stems must remain inaudible while collapsed.");

            transport.SetAssistMixActive(true);
            yield return CaptureOutput(samples=>output=samples);
            Assert.Less(output.Max(v=>Mathf.Abs(v)),.001,"Reopening must restore the saved music mute.");
            Assert.AreEqual(saved,JsonUtility.ToJson(transport.State));
            transport.State.music=true;transport.ApplyMix();
            yield return CaptureOutput(samples=>output=samples);
            Assert.Greater(Tone(output,880),.02);
            Assert.Less(Tone(output,440),.005);

            saved=JsonUtility.ToJson(transport.State);
            double before=transport.Position;
            transport.SetAssistMixActive(false);
            Assert.IsTrue(transport.Playing);
            Assert.AreEqual(before,transport.Position,.025,"Folding must not seek or restart playback.");
            yield return CaptureOutput(samples=>output=samples);
            Assert.Greater(Tone(output,440),.02);
            Assert.Less(Tone(output,880),.005);
            transport.SetAssistMixActive(true);
            yield return CaptureOutput(samples=>output=samples);
            Assert.Greater(Tone(output,880),.02,"Reopening must restore the vocal mix at the current position.");
            Assert.Less(Tone(output,440),.005);
            Assert.AreEqual(saved,JsonUtility.ToJson(transport.State));

            transport.State.stems[1].enabled=false;transport.ApplyMix();
            yield return CaptureOutput(samples=>output=samples);
            Assert.Less(output.Max(v=>Mathf.Abs(v)),.001,"All-off still mutes the expanded panel.");
            saved=JsonUtility.ToJson(transport.State);
            transport.Pause();before=transport.Position;
            transport.SetAssistMixActive(false);
            Assert.IsFalse(transport.Playing,"Folding while paused must not autoplay.");
            Assert.AreEqual(before,transport.Position);
            transport.Play(before);
            yield return CaptureOutput(samples=>output=samples);
            Assert.Greater(Tone(output,440),.02,"Play after folding an all-off mix must resume original audio.");
            Assert.Less(Tone(output,880),.005);
            Assert.AreEqual(saved,JsonUtility.ToJson(transport.State));
        }
        finally {if(recording)AudioRenderer.Stop();Time.captureFramerate=captureRate;Object.Destroy(root);foreach(var clip in clips)Object.Destroy(clip);}
        yield return new ExitPlayMode();
    }
    private static int OutputChannels=>AudioSettings.speakerMode switch
    {
        AudioSpeakerMode.Mono=>1,AudioSpeakerMode.Quad=>4,AudioSpeakerMode.Surround=>5,
        AudioSpeakerMode.Mode5point1=>6,AudioSpeakerMode.Mode7point1=>8,_=>2
    };
    private static IEnumerator CaptureOutput(System.Action<float[]> done)
    {
        var samples=new List<float>();
        for(int frame=0;frame<30;frame++)
        {
            yield return null;
            int channels=OutputChannels;
            using var buffer=new NativeArray<float>(AudioRenderer.GetSampleCountForCaptureFrame()*channels,Allocator.Temp);
            Assert.IsTrue(AudioRenderer.Render(buffer));
            if(frame>=15)for(int i=0;i<buffer.Length;i+=channels)samples.Add(buffer[i]);
        }
        Assert.IsNotEmpty(samples);done(samples.ToArray());
    }
    private static double Tone(float[] samples,double hz)
    {
        double real=0,imaginary=0;
        for(int i=0;i<samples.Length;i++){double angle=2*System.Math.PI*hz*i/AudioSettings.outputSampleRate;real+=samples[i]*System.Math.Cos(angle);imaginary+=samples[i]*System.Math.Sin(angle);}
        return 2*System.Math.Sqrt(real*real+imaginary*imaginary)/samples.Length;
    }
    [UnityTest]
    public IEnumerator MixerAllowsAllOrNoneWithoutMovingTheCommonClock()
    {
        yield return new EnterPlayMode(false);
        var root=new GameObject("AssistTransportTest");
        root.AddComponent<AudioListener>();
        var transport=root.AddComponent<AudioAssistTransport>();
        transport.State=new AssistState{music=true,loop=true,loopA=.2,loopB=.5};transport.Duration=2;
        transport.SetAssistMixActive(true);
        var clip=AudioClip.Create("Silence",88200,1,44100,false);
        try
        {
            foreach(string key in new[]{"original","vocals","drums"})
            {
                var state=new AssistStem{key=key,enabled=false,volume=.6f};transport.State.stems.Add(state);
                transport.Add(key,clip,new float[88200],new AssistAnalysis(),state,false);
            }
            transport.Play(.2);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.isActiveAndEnabled),"Reuse the scene listener without adding a duplicate.");
            for(int i=0;i<8;i++)
            {
                yield return new WaitForSecondsRealtime(.12f);
                double before=transport.Position;
                foreach(var stem in transport.State.stems)stem.enabled=i%2==0;
                transport.ApplyMix();
                Assert.Less(System.Math.Abs(transport.Position-before),.025);
                Assert.That(transport.Position,Is.InRange(.2,.5));
                foreach(var source in transport.Tracks.Values.SelectMany(t=>t.sources))Assert.AreEqual(i%2==0?.6f:0,source.volume,.0001f);
            }
            transport.Seek(.35);Assert.AreEqual(.35,transport.Position,.015);
            transport.Pause();double parked=transport.Position;yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(parked,transport.Position);Assert.IsFalse(transport.Playing);
        }
        finally { Object.Destroy(root);Object.Destroy(clip); }
        yield return new ExitPlayMode();
    }
}
#endif
