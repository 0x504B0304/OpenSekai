using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    public static class AudioAssistForcedAlignment
    {
        public sealed class Span {public int start,end;public float confidence;}
        // MMS_FA vocabulary; 0 is CTC blank, 28 is the zero-cost wildcard.
        public static int Token(char c)
        {
            const string exact="-aienoutsrmkldghybpwcvjzf'qx";
            int token=exact.IndexOf(c);if(token<1)throw new ArgumentException("Unsupported phoneme: "+c);return token;
        }
        // Standard CTC Viterbi with blank states. Repeated labels must cross a
        // blank; all supplied characters must be visited (never uniformly split).
        public static Span[] Align(float[] logits,int frames,int[] tokens,CancellationToken cancel)
        {
            if(logits==null||tokens==null||frames<=0||tokens.Length==0||logits.Length<(long)frames*28)throw new ArgumentException("Empty alignment input");
            foreach(int t in tokens)if(t<1||t>28)throw new ArgumentException("Invalid CTC token");
            int states=checked(tokens.Length*2+1);
            if((long)states*frames>32000000)throw new IOException("乐句过长，请在 LRC 中拆成更短的行");
            var trace=new byte[frames*states];var previous=new float[states];var next=new float[states];
            for(int s=0;s<states;s++)previous[s]=float.NegativeInfinity;
            previous[0]=0;
            for(int t=0;t<frames;t++)
            {
                cancel.ThrowIfCancellationRequested();
                float max=float.NegativeInfinity;
                for(int c=0;c<28;c++)max=Math.Max(max,logits[t*28+c]);
                double sum=0;for(int c=0;c<28;c++)sum+=Math.Exp(logits[t*28+c]-max);
                float normalization=max+(float)Math.Log(sum);
                for(int c=0;c<28;c++)logits[t*28+c]-=normalization;
                for(int s=0;s<states;s++)
                {
                    int label=s%2==0?0:tokens[s/2];
                    float best=previous[s];byte step=0;
                    if(s>0&&previous[s-1]>best){best=previous[s-1];step=1;}
                    if(s>1&&s%2!=0&&label!=tokens[s/2-1]&&previous[s-2]>best){best=previous[s-2];step=2;}
                    next[s]=best+(label==28?0:logits[t*28+label]);trace[t*states+s]=step;
                }
                var swap=previous;previous=next;next=swap;
            }
            int state=previous[states-1]>previous[states-2]?states-1:states-2;
            if(float.IsNegativeInfinity(previous[state]))throw new IOException("歌词长度或重复音节与音频不匹配，无法对齐");
            var result=new Span[tokens.Length];
            for(int i=0;i<result.Length;i++)result[i]=new Span{start=frames,end=0};
            for(int t=frames-1;t>=0;t--)
            {
                if(state%2!=0)
                {
                    var span=result[state/2];span.start=t;span.end=Math.Max(span.end,t+1);
                    span.confidence+=tokens[state/2]==28?0:(float)Math.Exp(logits[t*28+tokens[state/2]]);
                }
                state-=trace[t*states+state];
            }
            foreach(var span in result)
            {
                if(span.end<=span.start)throw new IOException("对齐路径缺少音节");
                span.confidence/=span.end-span.start;
            }
            return result;
        }
    }
}
