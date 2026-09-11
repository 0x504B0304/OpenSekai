using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    internal sealed class AudioAssistPronunciation
    {
        internal sealed class Unit {public string label, roman; public int line;}
        private readonly Dictionary<string,string> readings=new Dictionary<string,string>();
        private readonly Dictionary<string,string> roman=new Dictionary<string,string>();
        private int maxKey=1;
        private readonly string language;
        public AudioAssistPronunciation(string root,string language)
        {
            this.language=language;
            if(language=="ja"||language=="zh")
            {
                Load(Path.Combine(root,language+"-reading.tsv"),readings);
                foreach(string k in readings.Keys)maxKey=Math.Max(maxKey,k.Length);
                if(language=="ja")Load(Path.Combine(root,"ja-roman.tsv"),roman);
            }
        }
        private static void Load(string file,Dictionary<string,string> target)
        {
            foreach(string line in File.ReadLines(file))
            {int at=line.IndexOf('\t');if(at>0)target[line.Substring(0,at)]=line.Substring(at+1);}
        }
        private bool Reading(string text,int start,out string value,out int length)
        {
            for(length=Math.Min(maxKey,text.Length-start);length>0;length--)
                if(readings.TryGetValue(text.Substring(start,length),out value))return true;
            value=null;length=1;return false;
        }
        public List<Unit> Convert(string text,int line)
        {
            text=text.Normalize(NormalizationForm.FormKC);var result=new List<Unit>();
            if(language=="en")
            {
                foreach(Match word in Regex.Matches(text,@"[A-Za-z]+(?:'[A-Za-z]+)?"))
                    result.Add(new Unit{label=word.Value,roman=Regex.Replace(word.Value.ToLowerInvariant(),"[^a-z]",""),line=line});
                return result;
            }
            if(language=="zh")
            {
                for(int i=0;i<text.Length;)
                {
                    if(Reading(text,i,out string value,out int n))
                    {
                        var parts=value.Split(' ');
                        for(int j=0;j<n&&j<parts.Length;j++)
                            if(parts[j].Length>0)result.Add(new Unit{label=text.Substring(i+j,1),roman=parts[j],line=line});
                        i+=n;
                    }
                    else if(IsLatin(text[i]))
                    {
                        int first=i++;while(i<text.Length&&IsLatin(text[i]))i++;
                        result.Add(new Unit{label=text.Substring(first,i-first),roman=text.Substring(first,i-first).ToLowerInvariant(),line=line});
                    }
                    else {if(char.IsLetter(text[i]))throw new IOException("缺少歌词读音，请使用拼音或修改歌词："+text[i]);i++;}
                }
                return result;
            }
            var reading=new StringBuilder();
            for(int i=0;i<text.Length;)
            {
                if(text[i]>=0x3400&&text[i]<=0x9fff)
                {
                    if(!Reading(text,i,out var value,out int n))throw new IOException("缺少日文读音，请在 LRC 中使用假名："+text[i]);
                    reading.Append(value);i+=n;
                }
                else {char c=text[i++];reading.Append(c>=0x30a1&&c<=0x30f6?(char)(c-0x60):c);}
            }
            string previous="a";
            foreach(Match match in Regex.Matches(reading.ToString(),@"[ぁ-ん][ゃゅょぁぃぅぇぉ]?|ー|[A-Za-z]+"))
            {
                string label=match.Value;
                string value=label=="ー"?previous:roman.TryGetValue(label,out var r)?r:label.ToLowerInvariant();
                value=Regex.Replace(value,"[^a-z]","");if(value.Length==0)continue;
                foreach(char c in value)if("aeiou".IndexOf(c)>=0)previous=c.ToString();
                result.Add(new Unit{label=label,roman=value,line=line});
            }
            return result;
        }
        private static bool IsLatin(char c)=>(c>='a'&&c<='z')||(c>='A'&&c<='Z');
    }
}
