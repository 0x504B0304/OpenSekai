using UnityEngine;
using UnityEngine.UI;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    // A standalone editor affordance, with the note palette but no shared note
    // sprite/material changes. Geometry stays crisp at the native canvas PPU of 1.
    public sealed class AudioAssistFoldArrowGraphic : MaskableGraphic
    {
        private bool pointsLeft=true;
        public bool PointsLeft
        {
            get=>pointsLeft;
            set { if(pointsLeft==value)return;pointsLeft=value;SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect=rectTransform.rect;
            float scale=Mathf.Min(1,Mathf.Min(rect.width/44,rect.height/68));
            float direction=pointsLeft?1:-1;
            Vector2 center=rect.center;
            Vector2 upper=center+new Vector2(8*direction,19)*scale;
            Vector2 tip=center+new Vector2(-9*direction,0)*scale;
            Vector2 lower=center+new Vector2(8*direction,-19)*scale;
            // Soft outer glow, a lavender/cyan rim, and a pale luminous core.
            Stroke(mesh,upper,tip,lower,8*scale,new Color(.60f,.43f,1,.05f),new Color(.15f,1,1,.07f));
            Stroke(mesh,upper,tip,lower,6*scale,new Color(.65f,.47f,1,.11f),new Color(.15f,1,1,.15f));
            Stroke(mesh,upper,tip,lower,4.6f*scale,new Color(.73f,.55f,1,.45f),new Color(.12f,1,1,.55f));
            Stroke(mesh,upper,tip,lower,3.4f*scale,new Color(.76f,.63f,1,1),new Color(.26f,1,1,1));
            Stroke(mesh,upper,tip,lower,2.1f*scale,new Color(.97f,.94f,1,1),new Color(.79f,1,1,1));
        }

        private void Stroke(VertexHelper mesh,Vector2 upper,Vector2 tip,Vector2 lower,float radius,Color top,Color bottom)
        {
            Capsule(mesh,upper,tip,radius,top,bottom,lower.y,upper.y);
            Capsule(mesh,tip,lower,radius,top,bottom,lower.y,upper.y);
        }

        private void Capsule(VertexHelper mesh,Vector2 from,Vector2 to,float radius,Color top,Color bottom,float low,float high)
        {
            const int steps=12;
            int start=mesh.currentVertCount;
            float angle=Mathf.Atan2(to.y-from.y,to.x-from.x);
            AddVertex(mesh,(from+to)*.5f,top,bottom,low,high);
            for(int end=0;end<2;end++)
            {
                Vector2 origin=end==0?from:to;
                for(int i=0;i<=steps;i++)
                {
                    float theta=angle+Mathf.PI*(.5f+end+i/(float)steps);
                    AddVertex(mesh,origin+new Vector2(Mathf.Cos(theta),Mathf.Sin(theta))*radius,top,bottom,low,high);
                }
            }
            int count=2*(steps+1);
            for(int i=0;i<count;i++)mesh.AddTriangle(start,start+1+i,start+1+(i+1)%count);
        }

        private void AddVertex(VertexHelper mesh,Vector2 point,Color top,Color bottom,float low,float high)
        {
            var vertex=UIVertex.simpleVert;
            vertex.position=point;
            vertex.color=Color.Lerp(bottom,top,Mathf.InverseLerp(low,high,point.y))*color;
            mesh.AddVert(vertex);
        }
    }
}
