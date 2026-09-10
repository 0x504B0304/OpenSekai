using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.MenuUI;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    // Shares the waveform's rect and time conversion. Labels never introduce a
    // separate scrolling clock or inflate short syllables to button height.
    public sealed class AudioAssistLyricOverlay : MonoBehaviour
    {
        private sealed class Tag
        {
            public RectTransform rect;
            public Image face, edge;
            public TMP_Text text;
            public AssistDraft point;
            public double end;
        }
        private AudioAssistController owner;
        private AudioAssistWaveform wave;
        private TMP_FontAsset font;
        private readonly List<Tag> pool = new List<Tag>();
        private AssistDraft[] points = Array.Empty<AssistDraft>();
        private readonly Vector3[] corners = new Vector3[4];
        public bool HasLabels => points.Length > 0;
        public float LabelWidth => HasLabels ? wave.rectTransform.rect.width * .46f : 0;
        public void Build(AudioAssistController controller, AudioAssistWaveform waveform, TMP_FontAsset textFont)
        {
            owner=controller;wave=waveform;font=textFont;
            owner.Changed+=Refresh;Refresh();
        }
        private void Refresh()
        {
            points=owner.State.lyrics.SelectMany(l=>l.syllables).OrderBy(p=>p.seconds).ToArray();
            wave.SetVerticesDirty();
        }
        private Tag CreateTag()
        {
            var go=new GameObject("Syllable",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            go.transform.SetParent(transform,false);
            var tag=new Tag{rect=(RectTransform)go.transform,face=go.GetComponent<Image>()};
            tag.face.raycastTarget=false;
            // A square duration surface preserves both timestamp edges exactly.
            var edge=new GameObject("ConfidenceEdge",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            edge.transform.SetParent(go.transform,false);tag.edge=edge.GetComponent<Image>();tag.edge.raycastTarget=false;
            var er=(RectTransform)edge.transform;er.anchorMin=Vector2.zero;er.anchorMax=new Vector2(0,1);er.pivot=new Vector2(0,.5f);er.sizeDelta=new Vector2(2,0);
            var label=new GameObject("SyllableLabel",typeof(RectTransform));label.transform.SetParent(go.transform,false);
            tag.text=label.AddComponent<TextMeshProUGUI>();tag.text.font=font;tag.text.raycastTarget=false;
            Sekai.MenuUI.MenuTypography.Bind(tag.text,Sekai.MenuUI.MenuTextRole.Body);
            tag.text.richText=false;tag.text.textWrappingMode=TextWrappingModes.NoWrap;tag.text.overflowMode=TextOverflowModes.Ellipsis;
            tag.text.alignment=TextAlignmentOptions.Center;
            var tr=tag.text.rectTransform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(3,0);tr.offsetMax=new Vector2(-2,0);
            pool.Add(tag);return tag;
        }
        public void RefreshLayout()
        {
            if(owner==null||wave.canvas==null)return;
            var rect=wave.rectTransform.rect;
            if(rect.height<=0)return;
            // Only allocate labels for the visible portion of the tall timeline.
            ((RectTransform)transform.parent).GetWorldCorners(corners);
            float bottom=wave.rectTransform.InverseTransformPoint(corners[0]).y;
            float top=wave.rectTransform.InverseTransformPoint(corners[1]).y;
            double min=wave.SecondsAtY(bottom),max=wave.SecondsAtY(top);
            float scale=Math.Max(.1f,wave.canvas.scaleFactor),fontSize=Math.Max(18,12/scale);
            int used=0;
            for(int i=0;i<points.Length;i++)
            {
                var point=points[i];double offset=owner.State.lyricOffset;
                double end=AudioAssistAlgorithms.SyllableEnd(point,i+1<points.Length?points[i+1].seconds:0,owner.Transport.Duration-offset);
                double displayedStart=point.seconds+offset,displayedEnd=end+offset;
                if(AudioAssistSyllableDialog.Preview(point,out var previewStart,out var previewEnd))
                {displayedStart=previewStart;displayedEnd=previewEnd;}
                if(displayedStart>max||displayedEnd<min||displayedEnd<=displayedStart)continue;
                float a=wave.PositionY(displayedStart),b=wave.PositionY(displayedEnd);
                if(b<=a)continue;
                var tag=used<pool.Count?pool[used]:CreateTag();used++;
                tag.point=point;tag.end=end;tag.rect.gameObject.SetActive(true);
                tag.rect.anchorMin=tag.rect.anchorMax=new Vector2(.5f,.5f);tag.rect.pivot=Vector2.zero;
                tag.rect.anchoredPosition=new Vector2(rect.xMax-LabelWidth,a);
                tag.rect.sizeDelta=new Vector2(Math.Max(1,LabelWidth-3),b-a);
                bool selected=point==owner.SelectedSyllable;
                Color tint=AudioAssistStemColors.For("vocals");
                tag.face.color=Color.Lerp(MenuTheme.Panel,tint,selected?.6f:MenuTheme.IsDark?.24f:.3f);
                tag.edge.color=point.manuallyEdited?MenuTheme.Accent:!AudioAssistAlgorithms.HasSyllableEnd(point)||point.confidence<.45f?new Color(1,.78f,.32f):tint;
                tag.text.color=selected?new Color(.14f,.13f,.24f):MenuTheme.Text;
                if(AudioAssistSyllableDialog.IsDimming(owner,point))
                {
                    tag.face.color=Color.Lerp(MenuTheme.Panel,Color.black,.22f);
                    tag.edge.color=Color.Lerp(tag.face.color,tag.edge.color,.22f);
                    tag.text.color=Color.Lerp(tag.face.color,tag.text.color,.3f);
                }
                else if(selected)tag.edge.color=MenuTheme.Accent;
                tag.text.text=point.label;tag.text.fontSize=fontSize;
                // Small intervals stay exact; zooming in reveals their text.
                tag.text.enabled=b-a>=fontSize*1.05f;
            }
            for(int i=used;i<pool.Count;i++){pool[i].point=null;pool[i].rect.gameObject.SetActive(false);}
        }
        public bool TrySelect(Vector2 local)
        {
            if(!TryGetPoint(local,out var point,out var end))return false;
            AudioAssistSyllableDialog.Select(owner,this,wave,font,point,end);
            owner.SelectSyllable(point,end);return true;
        }
        public void Edit(AssistDraft point,double end)=>AudioAssistSyllableDialog.Open(owner,this,wave,font,point,end);
        public bool TryGetTagRect(AssistDraft point,out RectTransform rect)
        {
            rect=pool.FirstOrDefault(t=>t.point==point&&t.rect.gameObject.activeInHierarchy)?.rect;
            return rect!=null;
        }
        public bool TryGetPoint(Vector2 local,out AssistDraft point,out double end)
        {
            point=null;end=0;
            if(!HasLabels||local.x<wave.rectTransform.rect.xMax-LabelWidth)return false;
            // Prefer the actual interval; nearby short marks get a touch-friendly
            // nearest hit without changing their rendered duration.
            Tag nearest=null;float best=22/Math.Max(.1f,wave.canvas.scaleFactor);
            foreach(var tag in pool)
            {
                if(tag.point==null||!tag.rect.gameObject.activeSelf)continue;
                float a=tag.rect.anchoredPosition.y,b=a+tag.rect.rect.height;
                float distance=local.y<a?a-local.y:local.y>b?local.y-b:0;
                if(distance<=best){nearest=tag;best=distance;if(distance==0)break;}
            }
            if(nearest==null)return false;
            point=nearest.point;end=nearest.end;return true;
        }
        private void OnDestroy(){if(owner!=null)owner.Changed-=Refresh;}
    }
}
