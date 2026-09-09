using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Presenters;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using Sekai.MusicScoreMaker.Ingame.Views;
using SFB;
using UnityEngine;
using UnityEngine.Networking;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    public sealed class AudioAssistController : MonoBehaviour
    {
        public static readonly string[] Keys = { "original", "vocals", "instrumental", "drums", "bass", "other" };
        public static readonly string[] Names = { "原曲", "人声", "伴奏", "鼓组", "贝斯", "旋律" };
        public AssistState State { get; private set; } = new AssistState();
        public AudioAssistTransport Transport { get; private set; }
        public MusicScoreMakerPresenter Presenter { get; private set; }
        public MusicScoreMakerView View { get; private set; }
        public AudioAssistPanel Panel { get; private set; }
        public bool Ready => Transport != null && Transport.Tracks.ContainsKey("original") && !Transport.Busy;
        public AssistDraft SelectedDraft;
        public bool FinishHold, RegionMode, ShowOnsets = true;
        public bool PreviewBeats;
        public double PreviewBpm = 120, PreviewBeatOrigin;
        public readonly List<string> Visible = new List<string> { "original" };
        public string InspectedStem = "original";
        public double SelectedSeconds;
        public string Status = "点击“采音”展开辅助面板";
        public bool Busy { get; private set; }
        public bool Analyzing { get; private set; }
        public bool BlocksPlayback => Busy && !Analyzing;
        public event Action Changed;
        private string directory, lrcPath;
        private CancellationTokenSource lifetime = new CancellationTokenSource(), job;
        private double previousTime, stopAt = -1;
        private AudioSource cues;
        private AudioClip click;
        private bool originalNoteSound;
        private bool shuttingDown;
        public void Setup(MusicScoreMakerPresenter presenter, MusicScoreMakerView view, Transform tools)
        {
            if (Presenter != null) return;
            Presenter = presenter;View = view;presenter.AudioAssist = this;
            originalNoteSound = MusicScoreMakerSettingsManager.PlayMusicSEEnabled;
            State.noteSound = originalNoteSound;
            directory = Path.Combine(Application.persistentDataPath, "AudioAssist", presenter.Model.MusicId.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);lrcPath = Path.Combine(directory, "lyrics.lrc");
            try
            {
                string path = Path.Combine(directory, "session.json");
                if (File.Exists(path)) State = JsonUtility.FromJson<AssistState>(File.ReadAllText(path)) ?? new AssistState();
            }
            catch (Exception ex) { Debug.LogWarning("Audio assist state could not be restored: " + ex.Message); }
            State.stems ??= new List<AssistStem>();State.drafts ??= new List<AssistDraft>();State.lyrics ??= new List<AssistLyric>();
            State.drafts.RemoveAll(d => d == null || double.IsNaN(d.seconds) || double.IsInfinity(d.seconds));
            var savedIds = new HashSet<int>(presenter.Model.MusicScoreMakerData.NoteList.Select(n => n.id));
            foreach (var draft in State.drafts)
            {
                if (string.IsNullOrEmpty(draft.id)) draft.id = Guid.NewGuid().ToString("N");
                // Draft sidecars can be saved before the user saves the chart itself.
                if (draft.used && (draft.placedNoteIds == null || draft.placedNoteIds.Count == 0 || !draft.placedNoteIds.All(savedIds.Contains))) draft.used = false;
            }
            State.stems.RemoveAll(s => s == null || !Keys.Contains(s.key));
            State.stems = State.stems.GroupBy(s => s.key).Select(g => g.First()).ToList();
            State.lyrics.RemoveAll(l => l == null || double.IsNaN(l.seconds) || double.IsInfinity(l.seconds));
            foreach (var line in State.lyrics) { line.syllables ??= new List<AssistDraft>();line.syllables.RemoveAll(d => d == null || double.IsNaN(d.seconds) || double.IsInfinity(d.seconds)); }
            foreach (string key in Keys) if (!State.stems.Any(s => s.key == key)) State.stems.Add(new AssistStem { key = key, enabled = key == "original" });
            Transport = gameObject.AddComponent<AudioAssistTransport>();Transport.State = State;
            cues = gameObject.AddComponent<AudioSource>();cues.playOnAwake = false;
            float[] data = new float[2205];for (int n = 0; n < data.Length; n++) data[n] = (float)(Math.Sin(n * 2 * Math.PI * 1200 / 44100) * Math.Exp(-n / 250.0) * .22);
            click = AudioClip.Create("AssistCue", data.Length, 1, 44100, false);click.SetData(data, 0);
            Panel = gameObject.AddComponent<AudioAssistPanel>();Panel.Build(this, tools);
            Run(Initialize);
        }
        private async Task Initialize(CancellationToken token)
        {
            var entry = Presenter.Model.CustomMusicScoreEntry;
            if (entry != null && entry.RegisteredAudioClip == null) await entry.RegisterAudioAsync(token);
            if (entry?.RegisteredAudioClip != null) await LoadClip("original", entry.RegisteredAudioClip, false, token);
            await CheckSource(token);
            foreach (var state in State.stems.ToArray())
            {
                token.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(state.path) && File.Exists(Path.Combine(directory, Path.GetFileName(state.path))))
                    await LoadFile(state.key, Path.Combine(directory, Path.GetFileName(state.path)), token);
            }
            Status = Ready ? "已读取原曲与可用分轨" : "请先导入原曲音频";Refresh();
            await AutoSeparate(token);
        }
        public void Run(Func<CancellationToken, Task> operation)
        {
            if (Busy) return;
            RunJob(operation);
        }
        private async void RunJob(Func<CancellationToken, Task> operation)
        {
            Busy = true;job = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);Refresh();
            try { await operation(job.Token); }
            catch (OperationCanceledException) { if (this != null) Status = "已取消，已有谱面不变"; }
            catch (Exception ex) { if (this != null) { Status = "处理失败：" + ex.Message;Debug.LogWarning("Audio assist: " + ex); } }
            finally { job?.Dispose();job = null;Busy = false;if (this != null) Refresh(); }
        }
        public void Cancel() => job?.Cancel();
        public void Refresh() { if (!shuttingDown) Changed?.Invoke(); }
        public void ClearAuditionEnd() => stopAt = -1;
        public void Save()
        {
            if (string.IsNullOrEmpty(directory)) return;
            try
            {
                string path = Path.Combine(directory, "session.json"), temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(State, true));
                if (File.Exists(path)) File.Replace(temp, path, null);else File.Move(temp, path);
            }
            catch (Exception ex) { Status = "采音草稿保存失败：" + ex.Message; }
            Refresh();
        }
        public static void Pick(string[] extensions, Action<string> callback)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (NativeFilePicker.IsFilePickerBusy()) return;
            NativeFilePicker.PickFile(path => { if (!string.IsNullOrEmpty(path)) callback(path); }, Array.Empty<string>());
#else
            string[] paths = StandaloneFileBrowser.OpenFilePanel("导入采音素材", "", new[] { new ExtensionFilter("Files", extensions) }, false);
            if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0])) callback(paths[0]);
#endif
        }
        public void Import(string key) => Pick(new[] { "wav", "ogg", "mp3" }, path => Run(async token =>
        {
            Presenter.PauseAssist();string dest = Path.Combine(directory, key + "_" + Guid.NewGuid().ToString("N") + Path.GetExtension(path));
            await Task.Run(() => File.Copy(path, dest), token);
            try { await LoadFile(key, dest, token);State.stems.First(s => s.key == key).path = Path.GetFileName(dest);Save(); }
            catch { File.Delete(dest);throw; }
            if (key == "original") { await CheckSource(token);await AutoSeparate(token); }
        }));
        private string SourcePath()
        {
            var original = State.stems.First(s => s.key == "original");
            return !string.IsNullOrEmpty(original.path)
                ? Path.Combine(directory, Path.GetFileName(original.path))
                : Presenter.Model.CustomMusicScoreEntry?.AudioPath;
        }
        private async Task CheckSource(CancellationToken token)
        {
            string path = SourcePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            string fingerprint = await Task.Run(() =>
            {
                using var sha = SHA256.Create();using var stream = File.OpenRead(path);
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }, token);
            token.ThrowIfCancellationRequested();
            if (State.sourceFingerprint == fingerprint) return;
            foreach (var stem in State.stems.Where(s => s.key != "original"))
            {
                stem.path = null;stem.enabled = false;stem.offset = 0;Transport.Remove(stem.key);
            }
            Visible.Clear();Visible.Add("original");InspectedStem = "original";
            State.sourceFingerprint = fingerprint;State.automaticAttempt = null;Save();
        }
        private async Task AutoSeparate(CancellationToken token)
        {
            if (!Ready || !AudioAssistLocalAnalysis.Available || string.IsNullOrEmpty(State.sourceFingerprint)) return;
            bool complete = Keys.Where(k => k != "original").All(k => Transport.Tracks.ContainsKey(k));
            if (complete || State.automaticAttempt == State.sourceFingerprint) return;
            // Persist before starting: cancellation/failure never creates a restart loop.
            State.automaticAttempt = State.sourceFingerprint;Save();
            await Analyze("ja", false, token);
        }
        private async Task LoadFile(string key, string path, CancellationToken token)
        {
            AudioType type = Path.GetExtension(path).ToLowerInvariant() == ".ogg" ? AudioType.OGGVORBIS : Path.GetExtension(path).ToLowerInvariant() == ".mp3" ? AudioType.MPEG : AudioType.WAV;
            using var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
            var operation = request.SendWebRequest();
            while (!operation.isDone) { if (token.IsCancellationRequested) { request.Abort();token.ThrowIfCancellationRequested(); }await Task.Yield(); }
            if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
            var clip = DownloadHandlerAudioClip.GetContent(request);
            try { await LoadClip(key, clip, true, token); }
            catch { if (clip != null) Destroy(clip);throw; }
        }
        private async Task LoadClip(string key, AudioClip clip, bool owns, CancellationToken token)
        {
            if (clip == null || clip.samples == 0) throw new IOException("音频为空");
            if (clip.length > 1200) throw new IOException("采音预览最多支持 20 分钟音频");
            Status = "正在读取波形与起音：" + key;Refresh();
            float[] pcm = new float[checked(clip.samples * clip.channels)];
            if (!clip.GetData(pcm, 0)) throw new IOException("音频不能解码为采样数据");
            int channels = clip.channels, hz = clip.frequency;
            var analysis = await Task.Run(() => AudioAssistAlgorithms.Analyze(pcm, channels, hz, token), token);
            token.ThrowIfCancellationRequested();
            Transport.Add(key, clip, pcm, analysis, State.stems.First(s => s.key == key), owns);
            if(State.stems.First(s=>s.key==key).enabled) { if(!Visible.Contains(key))Visible.Add(key); }
            else Visible.Remove(key);
            if (key == "original") Transport.Duration = clip.length;
            Status = "已载入 " + Names[Array.IndexOf(Keys, key)];Refresh();
        }
        public void ToggleStem(string key)
        {
            if (!Transport.Tracks.ContainsKey(key))
            {
                if (key != "original" && AudioAssistLocalAnalysis.Available) AnalyzeLocal("ja");
                else Import(key);
                return;
            }
            var state = State.stems.First(s => s.key == key);state.enabled = !state.enabled;
            if (state.enabled) { if(!Visible.Contains(key))Visible.Add(key);InspectedStem = key; }
            else { Visible.Remove(key);if(InspectedStem==key)InspectedStem=Visible.FirstOrDefault()??"original"; }
            Transport.ApplyMix();Save();
        }
        public void ChangeRate(float rate) => Run(async token =>
        {
            double position = Transport.Playing ? Transport.Position : Presenter.AssistSeconds(Presenter.CurrentFocusTicks);
            Presenter.PauseAssist();Status = "正在准备保音高慢放…";Refresh();await Transport.SetRate(rate, token);Transport.Seek(position);Status = "慢放已就绪";
        });
        public void SetPoint(double seconds)
        {
            SelectedDraft = null;SelectedSeconds = Math.Max(0, Math.Min(Transport.Duration, seconds));Presenter.SeekAssist(SelectedSeconds);Refresh();
        }
        public void AddDraft(double seconds, string label = "") => AddDrafts(new[] { new AssistDraft { seconds = seconds, label = label } });
        public void AddDrafts(IEnumerable<AssistDraft> points)
        {
            Presenter.PauseAssist();
            var added = points.Where(p => p.seconds >= Presenter.Model.FillerSec && p.seconds <= Transport.Duration).Select(p => new AssistDraft { seconds = p.seconds, label = p.label, confidence = p.confidence }).ToList();
            if (added.Count == 0) return;
            Presenter.AssistHistory(() => { foreach (var d in added) State.drafts.Remove(d);SelectedDraft = null;Save(); },
                () => { State.drafts.AddRange(added);SelectedDraft = added[0];SelectedSeconds = added[0].seconds;Save(); });
            Status = "选择音符工具后点击轨道，按草稿时间落键";Refresh();
        }
        public void EditDraft(AssistDraft draft, double seconds)
        {
            if (draft == null || draft.used || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            Presenter.PauseAssist();double before = draft.seconds, after = Math.Max(Presenter.Model.FillerSec, Math.Min(Transport.Duration, seconds));
            Presenter.CancelAssistHold();
            Presenter.AssistHistory(() => { draft.seconds = before;Save(); }, () => { draft.seconds = after;SelectedSeconds = after;Save(); });
        }
        public void DeleteDraft()
        {
            var draft = SelectedDraft;if (draft == null) return;Presenter.PauseAssist();Presenter.CancelAssistHold();
            Presenter.AssistHistory(() => { State.drafts.Add(draft);SelectedDraft = draft;Save(); }, () => { State.drafts.Remove(draft);SelectedDraft = null;Save(); });
        }
        public void SelectNextDraft(AssistDraft after, IEnumerable<string> excluded = null)
        {
            var skip = excluded == null ? new HashSet<string>() : new HashSet<string>(excluded);
            SelectedDraft = State.drafts.Where(d => !d.used && d.seconds > after.seconds && !skip.Contains(d.id)).OrderBy(d => d.seconds).FirstOrDefault();
            if (SelectedDraft != null) { SelectedSeconds = SelectedDraft.seconds;Presenter.SeekAssist(SelectedSeconds); }
            Refresh();
        }
        public void ImportLyrics() => Pick(new[] { "lrc" }, path =>
        {
            if (Busy) return;
            try { var lyrics = AudioAssistAlgorithms.ParseLrc(File.ReadAllText(path));if (lyrics.Count == 0) throw new IOException("LRC 中没有有效时间戳");File.Copy(path, lrcPath, true);State.lyrics = lyrics;State.sourceLyricOffset = 0;Status = "已导入歌词；普通 LRC 为逐句时间，可运行自动对齐";Save(); }
            catch (Exception ex) { Status = ex.Message;Refresh(); }
        });
        public void Audition(double from, double to, bool loop)
        {
            if (!Ready) return;
            State.loopA = Math.Max(0, from);State.loopB = Math.Min(Transport.Duration, Math.Max(from + .15, to));State.loop = loop;
            Presenter.PlayAssist(State.loopA);stopAt = loop ? -1 : State.loopB;Refresh();
        }
        public void AnalyzeLocal(string language)
        {
            Run(token => Analyze(language, true, token));
        }
        private async Task Analyze(string language, bool alignLyrics, CancellationToken token)
        {
                Presenter.PauseAssist();
                string audioPath = SourcePath();
                if (string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath)) throw new IOException("请先导入原曲文件");
                double alignmentOffset = State.AlignmentOffset;
                AssistAnalysisOutput output;
                Analyzing = true;Refresh();
                try
                {
                    output = await AudioAssistLocalAnalysis.Run(audioPath, alignLyrics && File.Exists(lrcPath) ? lrcPath : null, directory, language, alignmentOffset,
                        message => { Status = message + "（可继续原曲回听）";Refresh(); }, token);
                }
                finally { Analyzing = false;Refresh(); }
                token.ThrowIfCancellationRequested();Presenter.PauseAssist();
                foreach (var stem in output.stems)
                {
                    string dest = Path.Combine(directory, stem.key + "_" + Guid.NewGuid().ToString("N") + ".wav");
                    File.Copy(stem.path, dest);
                    await LoadFile(stem.key, dest, token);
                    var state = State.stems.First(s => s.key == stem.key);state.path = Path.GetFileName(dest);state.offset = 0;
                }
                if (output.lyrics != null && output.lyrics.Count > 0) State.AcceptAlignment(output.lyrics, alignmentOffset);
                Status = output.warnings != null && output.warnings.Count > 0
                    ? "分析完成，需核对：" + string.Join("；", output.warnings)
                    : "内置分析完成，分轨已缓存；点击声部按钮加入混音";Save();
        }
        private void Update()
        {
            if (shuttingDown || Presenter == null || Transport == null) return;
            MusicScoreMakerSettingsManager.PlayMusicSEEnabled = State.noteSound;
            if (!Transport.Playing) { previousTime = Transport.Position;return; }
            double now = Transport.Position;
            if (stopAt >= 0 && now >= stopAt) { stopAt = -1;Presenter.PauseAssist();return; }
            if (now > previousTime && now - previousTime < .15)
            {
                bool play = State.draftSound && State.drafts.Any(d => d.seconds > previousTime && d.seconds <= now);
                if (State.metronome) { long a = Presenter.AssistTicks(previousTime), b = Presenter.AssistTicks(now);play |= a / 480 != b / 480; }
                if (play) cues.PlayOneShot(click);
            }
            else if (now < previousTime && State.loop)
            {
                bool play = State.draftSound && State.drafts.Any(d =>
                    (d.seconds > previousTime && d.seconds < State.loopB) || (d.seconds >= State.loopA && d.seconds <= now));
                if (State.metronome)
                    play |= Presenter.AssistTicks(State.loopA - .001) / 480 != Presenter.AssistTicks(now) / 480;
                if (play) cues.PlayOneShot(click);
            }
            previousTime = now;
        }
        private void OnApplicationPause(bool paused) { if (paused && Presenter != null) Presenter.PauseAssist(); }
        private void OnDestroy()
        {
            lifetime.Cancel();job?.Cancel();Save();lifetime.Dispose();
            if (Presenter != null && Presenter.AudioAssist == this) { Presenter.CancelAssistHold();Presenter.AudioAssist = null; }
            if (click != null) Destroy(click);
            if (!shuttingDown) MusicScoreMakerSettingsManager.PlayMusicSEEnabled = originalNoteSound;
        }
        public void Shutdown()
        {
            if (shuttingDown) return;
            shuttingDown = true;
            MusicScoreMakerSettingsManager.PlayMusicSEEnabled = originalNoteSound;
            lifetime.Cancel();job?.Cancel();Transport?.Pause();Save();Panel?.Dispose();
            if (Transport != null) Destroy(Transport);
            if (cues != null) Destroy(cues);
            Destroy(this);
        }
    }
}
