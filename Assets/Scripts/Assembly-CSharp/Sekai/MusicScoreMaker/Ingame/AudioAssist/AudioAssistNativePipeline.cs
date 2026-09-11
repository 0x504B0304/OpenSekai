using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    internal static class AudioAssistNativePipeline
    {
        private const int Rate=44100, Segment=343980, Step=257985;
        private static readonly string[] Keys={"drums","bass","other","vocals","instrumental"};
        // Windowed-sinc resampling also handles non-44.1kHz original files. Sample
        // positions are absolute so independently read chunks share one clock.
        private static float Sample(float[] pcm,int channels,int sourceRate,int channel,double position,int targetRate)
        {
            int frames=pcm.Length/channels;
            if(sourceRate==targetRate)return pcm[Math.Min(frames-1,Math.Max(0,(int)position))*channels+Math.Min(channels-1,channel)];
            int center=(int)position;double sum=0,weight=0,cutoff=Math.Min(1.0,(double)targetRate/sourceRate);
            for(int i=center-24;i<=center+24;i++)
            {
                double d=i-position;if(Math.Abs(d)>=24)continue;
                double x=Math.PI*d*cutoff;
                double w=(Math.Abs(x)<1e-12?1:Math.Sin(x)/x)*(.5+.5*Math.Cos(Math.PI*d/24))*cutoff;
                sum+=pcm[Math.Min(frames-1,Math.Max(0,i))*channels+Math.Min(channels-1,channel)]*w;weight+=w;
            }
            return (float)(sum/weight);
        }
        private sealed class WaveWriter : IDisposable
        {
            private readonly BinaryWriter writer;
            public WaveWriter(string path,int frames)
            {
                writer=new BinaryWriter(File.Create(path));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+frames*4);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
                writer.Write((short)1);writer.Write((short)2);writer.Write(Rate);writer.Write(Rate*4);writer.Write((short)4);writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(frames*4);
            }
            public void Write(float left,float right){writer.Write(Encode(left));writer.Write(Encode(right));}
            private static short Encode(float value)
            {
                if(float.IsNaN(value)||float.IsInfinity(value))throw new IOException("模型输出包含无效采样");
                return (short)Math.Round(Math.Max(-1,Math.Min(32767f/32768,value))*32768);
            }
            public void Dispose()=>writer.Dispose();
        }
        public static AssistAnalysisOutput Run(float[] pcm,int channels,int hz,string lrc,string output,string models,string language,double offset,int threads,Action<string,float> progress,CancellationToken token)
        {
            if(pcm==null||channels<1||channels>8||hz<8000||hz>192000||pcm.Length%channels!=0)
                throw new ArgumentException("无效的音频采样格式");
            token.ThrowIfCancellationRequested();
            int total=checked((int)Math.Round((double)(pcm.Length/channels)*Rate/hz));
            if(total<1||total>1200*Rate)throw new IOException("分析音频须在 0–20 分钟范围内");
            var result=new AssistAnalysisOutput();
            Directory.CreateDirectory(output);
            // Only a mono 16k vocal track is retained for alignment. Stem output
            // is streamed through one overlapping window, never a full-song tensor.
            var writers=new List<WaveWriter>();
            progress("载入内置 Demucs 模型",.02f);
            try
            {
                foreach(string key in Keys)
                {
                    string path=Path.Combine(output,key+".wav");writers.Add(new WaveWriter(path,total));
                    result.stems.Add(new AssistStem{key=key,path=path});
                }
                using(var model=new AudioAssistNative(Path.Combine(models,"htdemucs.onnx"),threads,token))
                {
                    double mean=0,m2=0;int count=pcm.Length/channels;
                    for(int i=0;i<count;i++)
                    {
                        if((i&65535)==0)token.ThrowIfCancellationRequested();
                        for(int c=0;c<channels;c++)if(float.IsNaN(pcm[i*channels+c])||float.IsInfinity(pcm[i*channels+c]))
                            throw new IOException("原曲包含无效采样");
                        double value=(pcm[i*channels]+pcm[i*channels+Math.Min(1,channels-1)])*.5;
                        double d=value-mean;mean+=d/(i+1);m2+=d*(value-mean);
                    }
                    float std=(float)Math.Max(1e-6,Math.Sqrt(m2/Math.Max(1,count-1)));
                    var wave=new float[Segment*2];var separated=new float[Segment*8];
                    var pending=new float[Segment*8];var weights=new float[Segment];
                    var window=new float[Segment];
                    for(int i=0;i<Segment;i++)window[i]=(float)Math.Min(i+1,Segment-i)/(Segment/2);
                    for(int start=0;start<total;start+=Step)
                    {
                        token.ThrowIfCancellationRequested();
                        int valid=Math.Min(Segment,total-start),pad=(Segment-valid)/2;
                        Array.Clear(wave,0,wave.Length);
                        for(int i=0;i<valid;i++)for(int c=0;c<2;c++)
                            wave[c*Segment+pad+i]=(Sample(pcm,channels,hz,c,(start+i)*(double)hz/Rate,Rate)-(float)mean)/std;
                        model.Separate(wave,separated);
                        for(int i=0;i<valid;i++)
                        {
                            weights[i]+=window[i];
                            for(int c=0;c<8;c++)pending[c*Segment+i]+=separated[c*Segment+pad+i]*window[i];
                        }
                        int flush=Math.Min(Step,total-start);
                        for(int i=0;i<flush;i++)
                        {
                            if((i&8191)==0)token.ThrowIfCancellationRequested();
                            for(int s=0;s<4;s++)writers[s].Write(pending[s*2*Segment+i]/weights[i]*std+(float)mean,pending[(s*2+1)*Segment+i]/weights[i]*std+(float)mean);
                            float l=Sample(pcm,channels,hz,0,(start+i)*(double)hz/Rate,Rate);
                            float r=Sample(pcm,channels,hz,1,(start+i)*(double)hz/Rate,Rate);
                            writers[4].Write(l-(pending[6*Segment+i]/weights[i]*std+(float)mean),r-(pending[7*Segment+i]/weights[i]*std+(float)mean));
                        }
                        for(int c=0;c<8;c++){Array.Copy(pending,c*Segment+Step,pending,c*Segment,Segment-Step);Array.Clear(pending,c*Segment+Segment-Step,Step);}
                        Array.Copy(weights,Step,weights,0,Segment-Step);Array.Clear(weights,Segment-Step,Step);
                        progress("正在分离人声、鼓组、贝斯和旋律",.04f+.5f*Math.Min(1f,(float)(start+flush)/total));
                    }
                }
            }
            finally {foreach(var writer in writers)writer.Dispose();}
            if(!string.IsNullOrEmpty(lrc))
                Align(ReadVocals(Path.Combine(output,"vocals.wav"),total,token),lrc,models,language,offset,threads,result,progress,token);
            progress("分析完成",1);return result;
        }
        public static AssistAnalysisOutput AlignOnly(float[] pcm,int channels,int hz,string lrc,string models,string language,double offset,int threads,Action<string,float> progress,CancellationToken token)
        {
            if (pcm == null || channels < 1 || hz < 8000 || hz > 192000 || pcm.Length % channels != 0)
                throw new ArgumentException("无效的人声采样格式");
            if (string.IsNullOrWhiteSpace(lrc)) throw new IOException("请先导入 LRC 歌词");
            int frames = pcm.Length / channels;
            int count = checked((int)Math.Round((double)frames * 16000 / hz));
            if (count < 1 || count > 1200 * 16000) throw new IOException("对齐音频须在 0–20 分钟范围内");
            var mono = new float[count];
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                double source = i * (double)hz / 16000;
                int left = Math.Min(frames - 1, Math.Max(0, (int)source));
                int right = Math.Min(frames - 1, left + 1);
                float fraction = (float)(source - left), value = 0;
                for (int c = 0; c < channels; c++) value += pcm[left * channels + c] * (1 - fraction) + pcm[right * channels + c] * fraction;
                mono[i] = value / channels;
            }
            progress?.Invoke("载入内置多语言音节对齐模型", .08f);
            var result = new AssistAnalysisOutput();
            Align(mono, lrc, models, language, offset, threads, result, progress, token);
            progress?.Invoke("对齐完成", 1); return result;
        }
        // Read the completed stem with real left/right filter context. This avoids
        // resampling discontinuities at overlap flush boundaries and stale tail data.
        private static float[] ReadVocals(string path,int total,CancellationToken token)
        {
            var result=new float[(int)Math.Ceiling(total*16000.0/Rate)];
            using var reader=new BinaryReader(File.OpenRead(path));
            for(int at=0;at<result.Length;at+=16000)
            {
                token.ThrowIfCancellationRequested();
                int end=Math.Min(result.Length,at+16000);
                int from=Math.Max(0,(int)(at*(double)Rate/16000)-24);
                int to=Math.Min(total,(int)Math.Ceiling(end*(double)Rate/16000)+24);
                var chunk=new float[to-from];reader.BaseStream.Position=44+from*4L;
                for(int i=0;i<chunk.Length;i++)chunk[i]=(reader.ReadInt16()+reader.ReadInt16())/65536f;
                for(int i=at;i<end;i++)result[i]=Sample(chunk,1,Rate,0,i*(double)Rate/16000-from,16000);
            }
            return result;
        }
        private static void Align(float[] vocals,string lrc,string models,string language,double offset,int threads,AssistAnalysisOutput result,Action<string,float> progress,CancellationToken token)
        {
            var lines=AudioAssistAlgorithms.ParseLrc(lrc);
            foreach(var line in lines){line.seconds+=offset;line.syllables.Clear();}
            lines=lines.Where(l=>l.seconds>=-.9&&l.seconds<vocals.Length/16000.0).ToList();
            if(lines.Count==0)throw new IOException("LRC 没有落在音频范围内的歌词，请检查整体偏移");
            var reading=new AudioAssistPronunciation(models,language);
            progress("载入内置多语言音节对齐模型",.56f);
            using var model=new AudioAssistNative(Path.Combine(models,"mms_fa.int8.onnx"),threads,token);
            for(int first=0;first<lines.Count;)
            {
                token.ThrowIfCancellationRequested();int last=first;
                // Each LRC line anchors its own acoustic window. Grouping repeated
                // refrains can otherwise move one line onto a later repetition.
                double start=Math.Max(0,lines[first].seconds-.9),end=Math.Min(vocals.Length/16000.0,last+1<lines.Count?lines[last+1].seconds+.35:lines[last].seconds+5);
                // Very long LRC gaps must not allocate unbounded attention tensors.
                if(end>start+22)result.warnings.Add("乐句 "+(first+1)+" 超过 22 秒，请拆分 LRC 并核对末尾音节");
                end=Math.Min(end,start+22);
                try
                {
                    var units=new List<AudioAssistPronunciation.Unit>();
                    for(int i=first;i<=last;i++)units.AddRange(reading.Convert(lines[i].text,i));
                    if(units.Count==0)throw new IOException("该乐句没有可用读音");
                    var tokens=new List<int>{28};var ranges=new List<(int first,int count)>();
                    foreach(var unit in units)
                    {int at=tokens.Count;foreach(char c in unit.roman)tokens.Add(AudioAssistForcedAlignment.Token(c));ranges.Add((at,tokens.Count-at));}
                    tokens.Add(28);
                    int from=(int)(start*16000),count=Math.Min(vocals.Length-from,(int)(end*16000)-from);
                    if(count<400)throw new IOException("乐句音频太短");
                    var wave=new float[count];Array.Copy(vocals,from,wave,0,count);
                    var emissions=model.Emissions(wave,out int frames);
                    var spans=AudioAssistForcedAlignment.Align(emissions,frames,tokens.ToArray(),token);
                    double ratio=count/16000.0/frames;
                    for(int i=0;i<units.Count;i++)
                    {
                        var range=ranges[i];float score=0;int duration=0;
                        for(int j=range.first;j<range.first+range.count;j++){int d=spans[j].end-spans[j].start;score+=spans[j].confidence*d;duration+=d;}
                        lines[units[i].line].syllables.Add(new AssistDraft{label=units[i].label,seconds=from/16000.0+spans[range.first].start*ratio,end=from/16000.0+spans[range.first+range.count-1].end*ratio,confidence=score/Math.Max(1,duration)});
                    }
                }
                catch(IOException ex){result.warnings.Add("乐句 "+(first+1)+"："+ex.Message);}
                first=last+1;progress("对齐乐句 "+first+"/"+lines.Count,.58f+.4f*first/lines.Count);
            }
            if(!lines.Any(l=>l.syllables.Count>0))throw new IOException("未能对齐歌词，请核对歌词、语言、读音和时间偏移");
            result.lyrics=lines;
        }
    }
}
