using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    [Serializable] public sealed class AssistDraft
    {
        public string id = Guid.NewGuid().ToString("N");
        public double seconds;
        // Absolute audio end time from the aligner; zero in legacy sessions.
        public double end;
        public bool manuallyEdited;
        public string label = "";
        public bool used;
        public List<int> placedNoteIds = new List<int>();
        public float confidence = 1;
    }
    [Serializable] public sealed class AssistStem
    {
        public string key, path;
        public bool enabled;
        public float volume = .8f;
        // Audio file time zero occurs at this position in the chart package audio.
        public double offset;
    }
    [Serializable] public sealed class AssistLyric
    {
        public double seconds;
        public string text;
        public List<AssistDraft> syllables = new List<AssistDraft>();
    }
    [Serializable] public sealed class AssistState
    {
        public int version = 1;
        public string sourceFingerprint, automaticAttempt;
        public List<AssistStem> stems = new List<AssistStem>();
        public List<AssistDraft> drafts = new List<AssistDraft>();
        public List<AssistLyric> lyrics = new List<AssistLyric>();
        public double loopA, loopB = 4, lyricOffset;
        // The cached source LRC remains unchanged when aligned timestamps are displayed.
        public double sourceLyricOffset;
        public double AlignmentOffset => sourceLyricOffset + lyricOffset;
        public void AcceptAlignment(List<AssistLyric> aligned, double inputOffset)
        {
            lyrics = aligned;sourceLyricOffset = inputOffset;lyricOffset = 0;
        }
        public bool loop, music = true, metronome, draftSound, noteSound = true;
        public float sensitivity = .55f;
    }
    public sealed class AssistAnalysis
    {
        public AssistWaveformPeaks waveform;
        public float[] peaks;
        public double duration;
        public List<double> onsets = new List<double>();
        public List<float> strengths = new List<float>();
    }
    public static class AudioAssistAlgorithms
    {
        public static bool HasSyllableEnd(AssistDraft point) => point.end > point.seconds && !double.IsNaN(point.end) && !double.IsInfinity(point.end);
        public static double SyllableEnd(AssistDraft point, double nextStart, double duration)
        {
            double end = HasSyllableEnd(point) ? point.end :
                (nextStart > point.seconds ? nextStart : point.seconds + .25);
            return Math.Max(point.seconds, Math.Min(duration, end));
        }
        public static bool RestoreSyllableEnds(List<AssistLyric> lyrics, List<AssistLyric> cached)
        {
            if (cached == null) return false;
            var points = cached.Where(l => l?.syllables != null).SelectMany(l => l.syllables)
                .Where(p => p != null && HasSyllableEnd(p)).ToLookup(p => p.label);
            bool changed = false;
            foreach (var point in lyrics.SelectMany(l => l.syllables))
            {
                if (HasSyllableEnd(point)) continue;
                var match = points[point.label].FirstOrDefault(p => Math.Abs(p.seconds - point.seconds) < .00001);
                if (match == null) continue;
                point.end = match.end;changed = true;
            }
            return changed;
        }
        private static readonly Regex Stamp = new Regex(@"\[(\d+):(\d{2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
        private static readonly Regex Syllable = new Regex(@"<(\d+):(\d{2})(?:[.:](\d{1,3}))?>", RegexOptions.Compiled);
        private static double Time(Match match)
        {
            string fraction = match.Groups[3].Value;
            return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
                + (fraction.Length == 0 ? 0 : int.Parse(fraction, CultureInfo.InvariantCulture) / Math.Pow(10, fraction.Length));
        }
        public static List<AssistLyric> ParseLrc(string text)
        {
            var result = new List<AssistLyric>();
            double offset = 0;
            var tag = Regex.Match(text ?? "", @"\[offset:([+-]?\d+)\]", RegexOptions.IgnoreCase);
            if (tag.Success) offset = double.Parse(tag.Groups[1].Value, CultureInfo.InvariantCulture) / 1000;
            foreach (string line in (text ?? "").Split('\n'))
            {
                var stamps = Stamp.Matches(line);
                if (stamps.Count == 0) continue;
                string content = Stamp.Replace(line, "").Trim();
                var words = Syllable.Matches(content);
                foreach (Match stamp in stamps)
                {
                    var lyric = new AssistLyric { seconds = Time(stamp) + offset, text = Syllable.Replace(content, "") };
                    for (int i = 0; i < words.Count; i++)
                    {
                        int start = words[i].Index + words[i].Length;
                        int end = i + 1 < words.Count ? words[i + 1].Index : content.Length;
                        string word = content.Substring(start, end - start).Trim();
                        if (word.Length > 0) lyric.syllables.Add(new AssistDraft {
                            seconds = Time(words[i]) + offset + Time(stamp) - Time(stamps[0]),
                            end = i + 1 < words.Count ? Time(words[i + 1]) + offset + Time(stamp) - Time(stamps[0]) : 0,
                            label = word });
                    }
                    if (!string.IsNullOrWhiteSpace(lyric.text)) result.Add(lyric);
                }
            }
            return result.OrderBy(x => x.seconds).ToList();
        }
        public static double LoopPosition(double start, double elapsed, double rate, bool loop, double a, double b)
        {
            double t = start + Math.Max(0, elapsed) * rate;
            return loop && b > a && t >= b ? a + (t - b) % (b - a) : t;
        }
        public static AssistAnalysis Analyze(float[] samples, int channels, int sampleRate, CancellationToken token)
        {
            int hop = Math.Max(1, sampleRate / 100), frames = samples.Length / channels;
            int count = (frames + hop - 1) / hop;
            var result = new AssistAnalysis { duration = (double)frames / sampleRate, peaks = new float[count], waveform = new AssistWaveformPeaks(samples, channels, sampleRate, token) };
            var energy = new double[count];
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                double sum = 0; float peak = 0;
                int end = Math.Min(frames, (i + 1) * hop);
                for (int n = i * hop; n < end; n++)
                    for (int c = 0; c < channels; c++) { float v = samples[n * channels + c]; sum += v * v; peak = Math.Max(peak, Math.Abs(v)); }
                result.peaks[i] = peak;
                energy[i] = Math.Sqrt(sum / Math.Max(1, (end - i * hop) * channels));
            }
            var flux = new double[count];
            for (int i = 1; i < count; i++) flux[i] = Math.Max(0, energy[i] - energy[i - 1]);
            double max = flux.Length == 0 ? 1 : Math.Max(.00001, flux.Max());
            int last = -10;
            for (int i = 2; i < count - 2; i++)
            {
                if (i - last < 5 || flux[i] < max * .025 || flux[i] < flux[i - 1] || flux[i] <= flux[i + 1]) continue;
                double mean = 0; int a = Math.Max(0, i - 20), b = Math.Min(count, i + 20);
                for (int j = a; j < b; j++) mean += flux[j];
                if (flux[i] < mean / (b - a) * 1.5) continue;
                result.onsets.Add((double)i * hop / sampleRate);
                result.strengths.Add((float)Math.Sqrt(flux[i] / max)); last = i;
            }
            return result;
        }
        // Offline waveform-similarity overlap-add. Both stereo channels use one
        // alignment offset, preserving interchannel phase. Unity objects stay on main thread.
        public static float[] Stretch(float[] input, int channels, int sampleRate, double rate, CancellationToken token)
        {
            if (Math.Abs(rate - 1) < .001) return input;
            if (rate < .49 || rate > 1 || channels < 1) throw new ArgumentOutOfRangeException(nameof(rate));
            int frames = input.Length / channels, outputFrames = (int)Math.Ceiling(frames / rate);
            int window = Math.Max(128, sampleRate / 25), hop = window / 2, search = sampleRate / 160;
            var output = new float[checked(outputFrames * channels)];
            var weights = new float[outputFrames];
            int previous = 0;
            for (int dest = 0; dest < outputFrames; dest += hop)
            {
                token.ThrowIfCancellationRequested();
                int expected = (int)Math.Round(dest * rate), source = expected;
                if (dest > 0 && dest + hop < outputFrames && expected + window + search < frames)
                {
                    double best = double.NegativeInfinity;
                    for (int shift = -search; shift <= search; shift += 4)
                    {
                        int candidate = expected + shift;
                        if (candidate < 0) continue;
                        double cross = 0, power = 1e-12;
                        for (int n = 0; n < hop; n += 8)
                        {
                            int x = Math.Min(frames - 1, previous + hop + n);
                            double a = input[x * channels], b = input[(candidate + n) * channels];
                            cross += a * b; power += b * b;
                        }
                        double score = cross / Math.Sqrt(power);
                        if (score > best) { best = score; source = candidate; }
                    }
                }
                for (int n = 0; n < window && dest + n < outputFrames; n++)
                {
                    int src = source + n;
                    if (src >= frames) break;
                    float w = (float)(.5 - .5 * Math.Cos(2 * Math.PI * (n + .5) / window));
                    weights[dest + n] += w;
                    for (int c = 0; c < channels; c++) output[(dest + n) * channels + c] += input[src * channels + c] * w;
                }
                previous = source;
            }
            for (int n = 0; n < outputFrames; n++) if (weights[n] > 1e-6f)
                for (int c = 0; c < channels; c++) output[n * channels + c] /= weights[n];
            return output;
        }
    }
}
