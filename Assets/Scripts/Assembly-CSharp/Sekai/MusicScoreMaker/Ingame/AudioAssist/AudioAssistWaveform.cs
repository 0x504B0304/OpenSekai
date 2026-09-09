using System;
using System.Linq;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AudioAssistWaveform : MaskableGraphic, IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler
    {
        public AudioAssistController Controller;
        public string StemKey;
        private long start, end;
        private Vector2 press;
        private double pressSeconds, dragSeconds;
        private AssistDraft dragged;
        private string handle;
        private bool moved;
        private int? pointer;
        private double[] edgeTimes = Array.Empty<double>();
        private float lastPixelHeight;
        private float Width => rectTransform.rect.width;
        private float Height => rectTransform.rect.height;
        private double AtY(float y)
        {
            long tick = start + (long)((y / Height + .5) * (end - start));
            return Controller.Presenter.AssistSeconds(tick);
        }
        private float Y(double seconds) => MusicScoreMakerUtility.CalcPreviewPositionYFromTicks(start, end, rectTransform.rect.size, Vector2.zero, Controller.Presenter.AssistTicks(seconds));
        private void Update()
        {
            if (Controller == null) return;
            long s = MusicScoreMakerUtility.GetPreviewStartTicks(), e = MusicScoreMakerUtility.GetPreviewEndTicks();
            float pixels = canvas != null ? canvas.pixelRect.height : 0;
            if (s != start || e != end || pixels != lastPixelHeight || Controller.Transport.Playing) SetVerticesDirty();
            lastPixelHeight = pixels;
            start = s;end = e;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if (Controller == null || end <= start || Height <= 0 || canvas == null) return;
            var keys = (string.IsNullOrEmpty(StemKey)?Controller.Visible:new[]{StemKey}.ToList()).Where(k => Controller.Transport.Tracks.ContainsKey(k)).ToList();
            if (keys.Count == 0) return;
            float left = -Width / 2, bottom = -Height / 2;
            var infos = MusicScoreMakerUtility.ConvertMusicScoreInfo(Controller.Presenter.Model.MusicScoreMakerData.MusicScoreEventDataList);
            var toSeconds = MusicScoreMakerUtility.CreatePreciseTimeConverter(infos);
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var screenBottom = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(new Vector3(0,bottom)));
            var screenTop = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(new Vector3(0,-bottom)));
            float pixelsPerUnit = Math.Abs(screenTop.y - screenBottom.y) / Height;
            if (pixelsPerUnit <= 0) return;
            float drawFrom = Math.Max(0,(canvas.pixelRect.yMin-screenBottom.y)/pixelsPerUnit);
            float drawTo = Math.Min(Height,(canvas.pixelRect.yMax-screenBottom.y)/pixelsPerUnit);
            if (drawTo <= drawFrom) return;
            // One strip per visible physical pixel; bound mesh size on very tall
            // displays and never draw the offscreen portion of the editor.
            int rows = Math.Max(1,Math.Min(Math.Min(4096,14000/keys.Count),(int)Math.Ceiling((drawTo-drawFrom)*pixelsPerUnit)));
            float rowHeight = (drawTo-drawFrom)/rows;
            if (edgeTimes.Length < rows+1) edgeTimes = new double[Mathf.NextPowerOfTwo(rows+1)];
            for (int row=0;row<=rows;row++)
                edgeTimes[row] = toSeconds(start+((drawFrom+(double)row*rowHeight)/Height)*(end-start))+Controller.Presenter.Model.FillerSec;
            for (int column = 0; column < keys.Count; column++)
            {
                var track = Controller.Transport.Tracks[keys[column]];var analysis = track.analysis;
                float w = Width / keys.Count, center = left + (column + .5f) * w;
                Color color = AudioAssistStemColors.For(keys[column]);color.a=.8f;
                Quad(vh, center, bottom, 1, Height, new Color(1,1,1,.08f));
                if (analysis?.waveform == null) continue;
                for (int row = 0; row < rows; row++)
                {
                    double from = edgeTimes[row]-track.state.offset, to = edgeTimes[row+1]-track.state.offset;
                    if (to <= 0 || from >= analysis.duration) continue;
                    float amplitude = analysis.waveform.Peak(from,to) * Math.Max(0,w*.46f-8);
                    Quad(vh, center-amplitude, bottom+drawFrom+row*rowHeight, Math.Max(1,amplitude*2), rowHeight, color);
                }
            }
            double min = Controller.Presenter.AssistSeconds(start), max = Controller.Presenter.AssistSeconds(end);
            if (Controller.PreviewBeats && Controller.PreviewBpm >= 20 && Controller.PreviewBpm <= 1000)
            {
                double step=60/Controller.PreviewBpm;
                double first=Controller.PreviewBeatOrigin+Math.Ceiling((min-Controller.PreviewBeatOrigin)/step)*step;
                for(double t=first;t<=max;t+=step)for(float x=left;x<Width/2;x+=16)
                    Quad(vh,x,Y(t),Math.Min(9,Width/2-x),2,new Color(1,.82f,.35f,.65f));
            }
            if (Controller.ShowOnsets && Controller.Transport.Tracks.TryGetValue(StemKey??Controller.InspectedStem, out var inspected))
            {
                for (int i = 0; i < inspected.analysis.onsets.Count; i++)
                {
                    double t = inspected.analysis.onsets[i] + inspected.state.offset;
                    if (t < min || t > max || inspected.analysis.strengths[i] < 1 - Controller.State.sensitivity) continue;
                    Quad(vh, Width / 2 - 25, Y(t) - 1, 23, 2, new Color(1,.82f,.35f));
                }
            }
            foreach (var draft in Controller.State.drafts)
            {
                if (draft.used || draft.seconds < min || draft.seconds > max) continue;
                float y = Y(draft == dragged && moved ? dragSeconds : draft.seconds);
                Quad(vh, left + 4, y - 5, 12, 10, draft == Controller.SelectedDraft ? Color.white : new Color(.75f,.55f,1));
            }
            if (Controller.State.loop || Controller.RegionMode)
            {
                float a = Y(Controller.State.loopA), b = Y(Controller.State.loopB);
                Quad(vh, left, a, Width, b - a, new Color(.4f,1,.8f,.07f));
                Quad(vh, left, a - 1, Width, 2, new Color(.4f,1,.8f));Quad(vh,left,b-1,Width,2,new Color(.4f,1,.8f));
                Quad(vh,left,a-7,20,14,new Color(.4f,1,.8f));Quad(vh,left,b-7,20,14,new Color(.4f,1,.8f));
            }
            double current = Controller.Transport.Playing ? Controller.Transport.Position : Controller.SelectedSeconds;
            if (current >= min && current <= max) Quad(vh,left,Y(current)-1,Width,2,new Color(1,.5f,.76f));
        }
        private static void Quad(VertexHelper vh, float x,float y,float w,float h,Color color)
        {
            if (w <= 0 || h <= 0) return;
            int i = vh.currentVertCount;var v = UIVertex.simpleVert;v.color = color;
            v.position = new Vector3(x,y);vh.AddVert(v);v.position = new Vector3(x,y+h);vh.AddVert(v);
            v.position = new Vector3(x+w,y+h);vh.AddVert(v);v.position = new Vector3(x+w,y);vh.AddVert(v);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
        private Vector2 Local(PointerEventData e) { RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out var p);return p; }
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || Controller.Busy || pointer.HasValue) return;
            pointer=e.pointerId;
            if(!string.IsNullOrEmpty(StemKey))Controller.InspectedStem=StemKey;
            Controller.Presenter.PauseAssist();press = Local(e);pressSeconds = AtY(press.y);moved = false;dragged = null;handle = null;
            float hit=22/Math.Max(.1f,canvas.scaleFactor);
            if (press.x < -Width/2 + hit*2) dragged = Controller.State.drafts.Where(d => !d.used && Math.Abs(Y(d.seconds)-press.y)<hit).OrderBy(d=>Math.Abs(Y(d.seconds)-press.y)).FirstOrDefault();
            if (dragged != null) { Controller.SelectedDraft = dragged;return; }
            if (Controller.State.loop || Controller.RegionMode)
            {
                if (Math.Abs(Y(Controller.State.loopA)-press.y)<hit) handle="a";
                else if (Math.Abs(Y(Controller.State.loopB)-press.y)<hit) handle="b";
                else if (Controller.RegionMode) handle="region";
            }
        }
        public void OnDrag(PointerEventData e)
        {
            if (Controller.Busy || pointer!=e.pointerId) return;Vector2 p = Local(e);moved |= Vector2.Distance(p,press)>4;
            if (!moved) return;dragSeconds=Math.Max(Controller.Presenter.Model.FillerSec,Math.Min(Controller.Transport.Duration,AtY(p.y)));
            if (dragged == null)
            {
                if (handle=="a") Controller.State.loopA=Math.Min(dragSeconds,Controller.State.loopB-.15);
                else if (handle=="b") Controller.State.loopB=Math.Max(dragSeconds,Controller.State.loopA+.15);
                else if (handle=="region") { Controller.State.loopA=Math.Min(pressSeconds,dragSeconds);Controller.State.loopB=Math.Max(pressSeconds,dragSeconds); }
                else
                {
                    long delta=(long)((p.y-press.y)/Height*(end-start));
                    MusicScoreMakerUtility.SetFocusTicks(Controller.Presenter.CurrentFocusTicks-delta);press=p;
                }
            }
            SetVerticesDirty();
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (pointer!=e.pointerId) return;
            pointer=null;
            if (Controller.Busy) { dragged=null;handle=null;return; }
            if (moved && dragged != null) Controller.EditDraft(dragged,dragSeconds);
            else if (moved && handle!=null) { Controller.State.loop=Controller.State.loopB-Controller.State.loopA>=.15;Controller.Save(); }
            else if (!moved)
            {
                double t = AtY(Local(e).y);
                if (dragged != null) { Controller.SelectedSeconds=dragged.seconds;Controller.Presenter.SeekAssist(dragged.seconds);Controller.Refresh(); }
                else
                {
                    if (Controller.ShowOnsets && Controller.Transport.Tracks.TryGetValue(Controller.InspectedStem,out var track))
                    {
                        double nearest = t;float distance=14;
                        for(int i=0;i<track.analysis.onsets.Count;i++)
                        {
                            if(track.analysis.strengths[i]<1-Controller.State.sensitivity)continue;
                            double candidate=track.analysis.onsets[i]+track.state.offset;float diff=Math.Abs(Y(candidate)-Local(e).y);
                            if(diff<distance){distance=diff;nearest=candidate;}
                        }
                        t=nearest;
                    }
                    Controller.SetPoint(t);
                }
            }
            dragged=null;handle=null;SetVerticesDirty();
        }
        public void OnScroll(PointerEventData e) => Controller.View.OnScroll(e);
        protected override void OnDisable() { pointer=null;dragged=null;handle=null;base.OnDisable(); }
    }
}
