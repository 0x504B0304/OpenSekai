#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Sekai.MusicScoreMaker.Ingame.AudioAssist;

public sealed class AudioAssistNativeTests
{
    private static float[] Emissions(params int[] labels)
    {
        var values = Enumerable.Repeat(-20f, labels.Length * 28).ToArray();
        for (int i = 0; i < labels.Length; i++) values[i * 28 + labels[i]] = 20;
        return values;
    }
    [Test] public void RepeatedPhonemesKeepSeparateSpansAndLeaveSilenceBetweenThem()
    {
        int a = AudioAssistForcedAlignment.Token('a');
        var spans = AudioAssistForcedAlignment.Align(Emissions(0, a, a, 0, 0, a, 0), 7, new[] { a, a }, CancellationToken.None);
        Assert.AreEqual(1, spans[0].start); Assert.AreEqual(3, spans[0].end);
        Assert.AreEqual(5, spans[1].start); Assert.AreEqual(6, spans[1].end);
        Assert.Greater(spans[0].confidence, .99);
    }
    [Test] public void ImpossibleRepeatedPhonemesAreRejectedInsteadOfInventingTiming()
    {
        int a = AudioAssistForcedAlignment.Token('a');
        Assert.Throws<IOException>(() => AudioAssistForcedAlignment.Align(Emissions(a, a), 2, new[] { a, a }, CancellationToken.None));
    }
    [Test] public void WildcardsAbsorbUntranscribedAudioAtPhraseEdges()
    {
        int a = AudioAssistForcedAlignment.Token('a'), b = AudioAssistForcedAlignment.Token('b');
        var spans = AudioAssistForcedAlignment.Align(Emissions(b, b, a, a, b, b), 6, new[] { 28, a, 28 }, CancellationToken.None);
        Assert.AreEqual(2, spans[1].start); Assert.AreEqual(3, spans[1].end);
    }
    [Test] public void AlignmentCancelsAndRejectsOutOfRangeTokens()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.Throws<OperationCanceledException>(() => AudioAssistForcedAlignment.Align(Emissions(1, 1), 2, new[] { 1 }, source.Token));
        Assert.Throws<ArgumentException>(() => AudioAssistForcedAlignment.Align(Emissions(1), 1, new[] { 29 }, CancellationToken.None));
    }
    [Test] public void ManifestCannotEscapeModelCacheOrOmitModels()
    {
        Assert.Throws<IOException>(() => AudioAssistLocalAnalysis.ParseManifest("{\"schema\":1,\"version\":\"../outside\",\"files\":[]}"));
        Assert.Throws<IOException>(() => AudioAssistLocalAnalysis.ParseManifest("{\"schema\":1,\"version\":\"v1\",\"files\":[]}"));
        Assert.Throws<IOException>(() => AudioAssistLocalAnalysis.ParseManifest("{\"schema\":1,\"version\":\"v1\",\"files\":[{\"path\":\"../outside\",\"bytes\":1,\"sha256\":\"bad\"}]}"));
    }
}
#endif
