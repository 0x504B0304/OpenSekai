using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AudioAssistSplitter : MaskableGraphic, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IInitializePotentialDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action BeginResize, EndResize;
        public Action<float> Resize;
        private int? pointer;
        private bool hover;
        private float pressX;
        private RectTransform coordinates;
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold=false; }
        public void OnPointerEnter(PointerEventData e) { hover=true;SetVerticesDirty(); }
        public void OnPointerExit(PointerEventData e) { hover=false;SetVerticesDirty(); }
        public void OnPointerDown(PointerEventData e)
        {
            if(pointer.HasValue||e.button!=PointerEventData.InputButton.Left)return;
            coordinates=canvas.rootCanvas.transform as RectTransform;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(coordinates,e.position,e.pressEventCamera,out var p))return;
            pointer=e.pointerId;pressX=p.x;BeginResize?.Invoke();SetVerticesDirty();e.Use();
        }
        public void OnDrag(PointerEventData e)
        {
            if(pointer!=e.pointerId)return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(coordinates,e.position,e.pressEventCamera,out var p))Resize?.Invoke(p.x-pressX);
            e.Use();
        }
        public void OnPointerUp(PointerEventData e)
        {
            if(pointer!=e.pointerId)return;pointer=null;EndResize?.Invoke();SetVerticesDirty();e.Use();
        }
        protected override void OnDisable()
        {
            if(pointer.HasValue){pointer=null;EndResize?.Invoke();}hover=false;base.OnDisable();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float scale=canvas!=null?Mathf.Max(.1f,canvas.scaleFactor):1;
            var r=rectTransform.rect;float thickness=(pointer.HasValue?6:hover?3:1.3f)/scale;
            Color line=pointer.HasValue?new Color(.65f,1,1):hover?new Color(.65f,.9f,1):new Color(.7f,.76f,.88f,.4f);
            if(pointer.HasValue)Quad(vh,r.center.x-7/scale,r.yMin,14/scale,r.height,new Color(.4f,1,1,.14f));
            Quad(vh,r.center.x-thickness*.5f,r.yMin,thickness,r.height,line);
            for(int i=-1;i<=1;i++)Quad(vh,r.center.x-3/scale,r.center.y+i*8/scale,6/scale,3/scale,line);
        }
        private static void Quad(VertexHelper vh,float x,float y,float w,float h,Color tint)
        {
            int i=vh.currentVertCount;var v=UIVertex.simpleVert;v.color=tint;
            v.position=new Vector3(x,y);vh.AddVert(v);v.position=new Vector3(x,y+h);vh.AddVert(v);
            v.position=new Vector3(x+w,y+h);vh.AddVert(v);v.position=new Vector3(x+w,y);vh.AddVert(v);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
