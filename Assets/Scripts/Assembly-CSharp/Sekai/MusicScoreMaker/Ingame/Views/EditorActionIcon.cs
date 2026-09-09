using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sekai.MusicScoreMaker.Ingame.Views
{
    public sealed class EditorActionIcon : MaskableGraphic, IPointerEnterHandler, IPointerExitHandler
    {
        public enum Kind { Test, Save, Audio, Map }
        public Kind Symbol;
        public Action<bool> Hover;
        private bool selected;
        public bool Selected { get=>selected;set {if(selected==value)return;selected=value;SetVerticesDirty();} }
        public void OnPointerEnter(PointerEventData e)=>Hover?.Invoke(true);
        public void OnPointerExit(PointerEventData e)=>Hover?.Invoke(false);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.47f;
            var c=rectTransform.rect.center;var background=selected?new Color(.43f,.94f,.87f):Color.white;
            Add(vh,c,background);const int segments=64;
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;Add(vh,c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,background);}
            for(int i=0;i<segments;i++)vh.AddTriangle(0,i+1,(i+1)%segments+1);
            float s=radius/25;
            void Line(float x1,float y1,float x2,float y2)=>Stroke(vh,c+new Vector2(x1,y1)*s,c+new Vector2(x2,y2)*s,2.4f*s);
            if(Symbol==Kind.Audio){for(int i=0;i<5;i++){float h=new[]{5,11,16,9,5}[i];Line((i-2)*6,-h,(i-2)*6,h);}}
            else if(Symbol==Kind.Save){Line(-12,-14,12,-14);Line(12,-14,12,9);Line(12,9,7,14);Line(7,14,-12,14);Line(-12,14,-12,-14);Line(-5,14,-5,4);Line(-5,4,6,4);Line(6,4,6,14);Line(-6,-13,-6,-5);Line(-6,-5,6,-5);Line(6,-5,6,-13);}
            else if(Symbol==Kind.Test){Line(-14,-10,-14,12);Line(-14,12,10,12);Line(-14,-10,-4,-10);Line(-8,2,-3,-3);Line(-3,-3,6,6);Line(5,-4,5,-15);Line(5,-15,15,-9);Line(15,-9,5,-4);}
            else {Line(-14,-13,-14,13);Line(-14,13,-5,9);Line(-5,9,5,13);Line(5,13,14,9);Line(14,9,14,-13);Line(14,-13,5,-9);Line(5,-9,-5,-13);Line(-5,-13,-14,-9);Line(-5,-13,-5,9);Line(5,-9,5,13);}
        }
        private void Stroke(VertexHelper vh,Vector2 a,Vector2 b,float width)
        {
            var delta=(b-a).normalized;var n=new Vector2(-delta.y,delta.x)*width*.5f;int start=vh.currentVertCount;
            var ink=new Color(.28f,.27f,.40f);Add(vh,a-n,ink);Add(vh,a+n,ink);Add(vh,b+n,ink);Add(vh,b-n,ink);
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
        private void Add(VertexHelper vh,Vector2 position,Color tint){var v=UIVertex.simpleVert;v.position=position;v.color=tint*color;vh.AddVert(v);}
    }
}
