using System;
using System.Collections.Generic;
using System.Threading;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    // Shares the transport's PCM. Coarse views query a peak pyramid, while
    // magnified views query the original samples without temporal smearing.
    public sealed class AssistWaveformPeaks
    {
        private const int BlockSize = 64;
        private readonly float[] pcm;
        private readonly int channels, sampleRate, frames;
        private readonly List<float[]> levels = new List<float[]>();
        public AssistWaveformPeaks(float[] samples, int channels, int sampleRate, CancellationToken token)
        {
            if (samples == null || channels < 1 || sampleRate < 1) throw new ArgumentException("Invalid audio format");
            pcm = samples;this.channels = channels;this.sampleRate = sampleRate;frames = samples.Length / channels;
            var level = new float[(frames + BlockSize - 1) / BlockSize];
            for (int i = 0; i < level.Length; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                level[i] = SamplePeak(i * BlockSize, Math.Min(frames, (i + 1) * BlockSize));
            }
            levels.Add(level);
            while (level.Length > 1)
            {
                token.ThrowIfCancellationRequested();
                var parent = new float[(level.Length + 1) / 2];
                for (int i = 0; i < parent.Length; i++) parent[i] = Math.Max(level[i * 2], i * 2 + 1 < level.Length ? level[i * 2 + 1] : 0);
                levels.Add(parent);level = parent;
            }
        }
        public float Peak(double fromSeconds, double toSeconds)
        {
            if (double.IsNaN(fromSeconds) || double.IsNaN(toSeconds) || toSeconds <= fromSeconds || frames == 0) return 0;
            int from = (int)Math.Max(0, Math.Min(frames, Math.Floor(fromSeconds * sampleRate)));
            int to = (int)Math.Max(0, Math.Min(frames, Math.Ceiling(toSeconds * sampleRate)));
            if (to <= from) return 0;
            int firstBlock = (from + BlockSize - 1) / BlockSize, lastBlock = to / BlockSize;
            if (firstBlock >= lastBlock) return SamplePeak(from, to);
            float result = Math.Max(SamplePeak(from, firstBlock * BlockSize), SamplePeak(lastBlock * BlockSize, to));
            int level = 0;
            while (firstBlock < lastBlock)
            {
                if ((firstBlock & 1) != 0) result = Math.Max(result, levels[level][firstBlock++]);
                if ((lastBlock & 1) != 0) result = Math.Max(result, levels[level][--lastBlock]);
                firstBlock /= 2;lastBlock /= 2;level++;
            }
            return result;
        }
        private float SamplePeak(int from, int to)
        {
            float result = 0;
            for (int i = from * channels; i < to * channels; i++)
            {
                float value = Math.Abs(pcm[i]);
                if (value > result && !float.IsInfinity(value)) result = value;
            }
            return result;
        }
    }
}
