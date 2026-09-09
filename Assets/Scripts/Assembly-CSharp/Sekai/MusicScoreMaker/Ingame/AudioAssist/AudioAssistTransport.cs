using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    public sealed class AudioAssistTransport : MonoBehaviour
    {
        public sealed class Track
        {
            public AssistStem state;
            public AudioClip original, stretched;
            public float[] pcm;
            public AssistAnalysis analysis;
            public bool ownsOriginal;
            public AudioSource[] sources = new AudioSource[2];
        }
        public readonly Dictionary<string, Track> Tracks = new Dictionary<string, Track>();
        public AssistState State { get; set; }
        public bool AssistMixActive { get; private set; }
        public bool Playing { get; private set; }
        public float Rate { get; private set; } = 1;
        public double Duration { get; set; }
        private double startDsp, startTime, parkedTime, nextDsp;
        private bool looping;
        private double a, b;
        private readonly double[] ends = new double[2];
        private int generation;
        private AudioListener ownedListener;
        public bool Busy { get; private set; }
        public double Position => Playing ? Math.Min(Duration, AudioAssistAlgorithms.LoopPosition(startTime, AudioSettings.dspTime - startDsp, Rate, looping, a, b)) : parkedTime;
        public bool Ended => Playing && !looping && Position >= Duration;

        private void OnEnable()
        {
            if (Application.isPlaying) EnsureAudioListener();
        }
        private void OnDisable()
        {
            Pause();
            if (ownedListener != null) ownedListener.enabled = false;
        }
        private void EnsureAudioListener()
        {
            // The chart editor uses CRI for its existing audio and has no Unity
            // listener. AudioSources can advance silently without one. Own a
            // listener only for this editor's lifetime, reusing scene listeners.
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (listener == ownedListener || !listener.isActiveAndEnabled) continue;
                if (ownedListener != null) ownedListener.enabled = false;
                return;
            }
            if (ownedListener == null)
            {
                var child = new GameObject("AssistAudioListener");
                child.transform.SetParent(transform, false);
                ownedListener = child.AddComponent<AudioListener>();
            }
            ownedListener.enabled = true;
        }

        public void Add(string key, AudioClip clip, float[] pcm, AssistAnalysis analysis, AssistStem state, bool ownsClip)
        {
            Pause();
            if (Tracks.TryGetValue(key, out var previous)) Release(previous);
            var track = new Track { state = state, original = clip, pcm = pcm, analysis = analysis, ownsOriginal = ownsClip };
            for (int i = 0; i < 2; i++)
            {
                var child = new GameObject("AssistAudio_" + key + "_" + i); child.transform.SetParent(transform, false);
                var source = child.AddComponent<AudioSource>();source.playOnAwake = false;source.spatialBlend = 0;
                track.sources[i] = source;
            }
            Tracks[key] = track;
            Rate = 1;
            foreach (var item in Tracks.Values) if (item.stretched != null) { Destroy(item.stretched); item.stretched = null; }
            ApplyMix();
        }
        public void SetAssistMixActive(bool active)
        {
            AssistMixActive = active;
            ApplyMix();
        }
        public void ApplyMix()
        {
            // Panel visibility is transient: never overwrite the saved mix to
            // restore ordinary editor playback. All tracks keep the same clock.
            foreach (var pair in Tracks)
            {
                var track = pair.Value;
                float volume = AssistMixActive
                    ? (State != null && State.music && track.state.enabled ? Mathf.Clamp01(track.state.volume) : 0)
                    : (pair.Key == "original" ? 1 : 0);
                foreach (var source in track.sources) source.volume = volume;
            }
        }
        public async Task SetRate(float rate, CancellationToken token)
        {
            if (Mathf.Abs(rate - Rate) < .001f) return;
            if (rate != 1 && rate != .75f && rate != .5f) throw new ArgumentOutOfRangeException(nameof(rate));
            Pause(); int serial = ++generation; Busy = true;
            var prepared = new Dictionary<Track, AudioClip>();
            try
            {
                if (rate != 1) foreach (var track in Tracks.Values)
                {
                    token.ThrowIfCancellationRequested();int channels = track.original.channels, hz = track.original.frequency;
                    float[] samples = await Task.Run(() => AudioAssistAlgorithms.Stretch(track.pcm, channels, hz, rate, token), token);
                    token.ThrowIfCancellationRequested();if (serial != generation) throw new OperationCanceledException();
                    var clip = AudioClip.Create("AssistStretch_" + track.state.key, samples.Length / channels, channels, hz, false);
                    clip.SetData(samples, 0);prepared.Add(track, clip);
                }
                foreach (var track in Tracks.Values)
                {
                    if (track.stretched != null) Destroy(track.stretched);
                    track.stretched = prepared.TryGetValue(track, out var clip) ? clip : null;
                }
                prepared.Clear();Rate = rate;
            }
            finally { foreach (var clip in prepared.Values) Destroy(clip);Busy = false; }
        }
        public void Play(double seconds)
        {
            if (Busy || Tracks.Count == 0 || Duration <= 0) return;
            EnsureAudioListener();
            Pause();startTime = Math.Max(0, Math.Min(Duration, seconds));parkedTime = startTime;
            looping = State.loop && State.loopB - State.loopA >= .15;
            a = Math.Max(0, State.loopA);b = Math.Min(Duration, State.loopB);
            looping &= b - a >= .15;
            if (looping && (startTime < a || startTime >= b)) startTime = a;
            startDsp = AudioSettings.dspTime + .07;
            double end = looping ? b : Duration;
            nextDsp = startDsp + (end - startTime) / Rate;
            Schedule(0, startDsp, startTime, end);
            if (looping) { Schedule(1, nextDsp, a, b);nextDsp += (b - a) / Rate; }
            Playing = true;ApplyMix();
        }
        private void Schedule(int bank, double dsp, double from, double to)
        {
            ends[bank] = dsp + (to - from) / Rate;
            foreach (var track in Tracks.Values)
            {
                var source = track.sources[bank];source.Stop();
                var clip = Rate == 1 ? track.original : track.stretched;
                if (clip == null) continue;
                double fileStart = Math.Max(0, from - track.state.offset);
                double audibleStart = Math.Max(from, track.state.offset);
                double audibleEnd = Math.Min(to, track.state.offset + track.original.length);
                if (audibleStart >= audibleEnd) continue;
                source.clip = clip;source.pitch = 1;
                source.timeSamples = Math.Min(clip.samples - 1, (int)Math.Round(fileStart / Rate * clip.frequency));
                source.PlayScheduled(dsp + (audibleStart - from) / Rate);
                source.SetScheduledEndTime(dsp + (audibleEnd - from) / Rate);
            }
        }
        private void Update()
        {
            if (!Playing || !looping) return;
            double now = AudioSettings.dspTime;
            for (int bank = 0; bank < 2; bank++) if (now >= ends[bank])
            {
                // A suspended app can miss several loops; restart rather than schedule in the past.
                if (nextDsp < now + .01) { Play(Position);return; }
                Schedule(bank, nextDsp, a, b);nextDsp += (b - a) / Rate;
            }
        }
        public void Pause()
        {
            parkedTime = Position;Playing = false;
            foreach (var track in Tracks.Values) foreach (var source in track.sources) source.Stop();
        }
        public void Seek(double seconds) { bool playing = Playing;Pause();parkedTime = Math.Max(0, Math.Min(Duration, seconds));if (playing) Play(parkedTime); }
        public void Remove(string key)
        {
            Pause();
            if (Tracks.TryGetValue(key, out var track)) { Release(track);Tracks.Remove(key); }
        }
        private void OnApplicationPause(bool paused) { if (paused) Pause(); }
        private void Release(Track track)
        {
            foreach (var source in track.sources) if (source != null) Destroy(source.gameObject);
            if (track.stretched != null) Destroy(track.stretched);
            if (track.ownsOriginal && track.original != null) Destroy(track.original);
        }
        private void OnDestroy()
        {
            generation++;Pause();foreach (var track in Tracks.Values) Release(track);Tracks.Clear();
            // Shutdown destroys this component while retaining the editor view.
            if (ownedListener != null) { ownedListener.enabled = false;Destroy(ownedListener.gameObject); }
        }
    }
}
