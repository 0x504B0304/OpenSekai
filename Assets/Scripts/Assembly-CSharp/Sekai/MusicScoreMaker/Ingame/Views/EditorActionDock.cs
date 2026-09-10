using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sekai.Localization;

namespace Sekai.MusicScoreMaker.Ingame.Views
{
    public sealed class EditorActionDock : MonoBehaviour
    {
        private sealed class SavedRect
        {
            public RectTransform rect; public Transform parent; public int sibling;
            public Vector2 min,max,pivot,position,size; public Vector3 scale; public bool active;
            public SavedRect(RectTransform r) { rect=r;parent=r.parent;sibling=r.GetSiblingIndex();min=r.anchorMin;max=r.anchorMax;pivot=r.pivot;position=r.anchoredPosition;size=r.sizeDelta;scale=r.localScale;active=r.gameObject.activeSelf; }
            public void Restore() { if(rect==null||parent==null)return;rect.SetParent(parent,false);rect.SetSiblingIndex(sibling);rect.anchorMin=min;rect.anchorMax=max;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=scale;rect.gameObject.SetActive(active); }
        }
        private readonly List<SavedRect> saved=new List<SavedRect>();
        private readonly List<Button> buttons=new List<Button>();
        private Canvas canvas;
        private RectTransform canvasRect,minimap,play,controller;
        private Button nativeSave,nativeTest;
        private TMP_Text nativeTime,time,tooltip;
        private LocalizedTextBinding tooltipBinding;
        private Button tooltipOwner;
        private Action changed;
        private bool minimapOpen;
        public float LeftBoundary { get; private set; }
        public bool MinimapOpen=>minimapOpen;
        public void Build(Canvas targetCanvas,Transform tools,TMP_FontAsset font,Action toggleAssist,Action layoutChanged)
        {
            canvas=targetCanvas;canvasRect=(RectTransform)canvas.transform;changed=layoutChanged;
            var root=(RectTransform)transform;root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            foreach(string name in new[]{"ZoomTimelineButtons","TimeSliderValueButtons"}) Hide(tools?.Find(name));
            nativeTest=tools?.Find("OnTestPlayEventButton")?.GetComponent<Button>();
            nativeSave=tools?.Find("QuickSaveMusicScoreButton")?.GetComponent<Button>();
            Hide(nativeTest?.transform);Hide(nativeSave?.transform);
            AddButton("common.test_play",EditorActionIcon.Kind.Test,()=>{if(nativeTest!=null&&nativeTest.interactable)nativeTest.onClick.Invoke();});
            AddButton("common.save",EditorActionIcon.Kind.Save,()=>{if(nativeSave!=null&&nativeSave.interactable)nativeSave.onClick.Invoke();});
            AddButton("editor.audio_assist",EditorActionIcon.Kind.Audio,toggleAssist);
            AddButton("editor.minimap",EditorActionIcon.Kind.Map,()=>SetMinimapOpen(!minimapOpen));
            minimap=tools?.Find("BG") as RectTransform;
            if(minimap!=null&&minimap.GetComponentInChildren<MusicScoreMinimapView>(true)!=null) Move(minimap);else minimap=null;
            controller=tools?.Find("MusicControllerView") as RectTransform;
            if(controller!=null)
            {
                Move(controller);play=controller.Find("PlayButton") as RectTransform;if(play!=null)saved.Add(new SavedRect(play));
                var oldTime=controller.Find("Time");nativeTime=oldTime?.GetComponentInChildren<TMP_Text>(true);Hide(oldTime);
            }
            time=Label("PlaybackTime",font);time.text="00:00:000";
            tooltip=Label("ActionHint",font);
            tooltipBinding=tooltip.gameObject.AddComponent<LocalizedTextBinding>();
            tooltip.gameObject.SetActive(false);
            minimapOpen=PlayerPrefs.GetInt("MenuUI.minimapOpen",1)==1;
            if(minimap!=null)minimap.gameObject.SetActive(minimapOpen);
            Relayout();
        }
        private void Hide(Transform t) { if(t is RectTransform r){saved.Add(new SavedRect(r));r.gameObject.SetActive(false);} }
        private void Move(RectTransform r) { saved.Add(new SavedRect(r));r.SetParent(transform,false);r.localScale=Vector3.one; }
        private TMP_Text Label(string name,TMP_FontAsset font)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(transform,false);
            var label=go.AddComponent<TextMeshProUGUI>();label.font=font;label.color=Color.white;label.raycastTarget=false;
            label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;
            Sekai.MenuUI.MenuTypography.Bind(label,name=="PlaybackTime"?Sekai.MenuUI.MenuTextRole.Timecode:Sekai.MenuUI.MenuTextRole.Caption);
            return label;
        }
        private void AddButton(string localizationKey,EditorActionIcon.Kind kind,Action action)
        {
            var go=new GameObject("Editor_"+kind,typeof(RectTransform));go.transform.SetParent(transform,false);
            var icon=go.AddComponent<EditorActionIcon>();icon.Symbol=kind;
            var button=go.AddComponent<Button>();button.targetGraphic=icon;button.navigation=new Navigation{mode=Navigation.Mode.None};
            var colors=ColorBlock.defaultColorBlock;colors.highlightedColor=new Color(.8f,1,1);colors.pressedColor=new Color(.5f,.85f,.9f);colors.fadeDuration=.1f;button.colors=colors;
            button.onClick.AddListener(()=>{tooltipOwner=null;if(tooltip!=null)tooltip.gameObject.SetActive(false);action();});
            icon.Hover=on=>
            {
                if(tooltipBinding==null)return;
                if(!on)
                {
                    if(tooltipOwner==button){tooltipOwner=null;tooltip.gameObject.SetActive(false);}
                    return;
                }
                tooltipOwner=button;
                // Change the binding itself: the periodic localization scan would
                // overwrite a plain text assignment with the previous icon's key.
                tooltipBinding.Key=localizationKey;
                var r=(RectTransform)go.transform;
                Place(tooltip.rectTransform,r.anchoredPosition.x-100,r.anchoredPosition.y,140,36);
                tooltip.gameObject.SetActive(true);
            };
            buttons.Add(button);
        }
        public void SetAssistOpen(bool open) { if(buttons.Count>2)buttons[2].GetComponent<EditorActionIcon>().Selected=open; }
        public void SetMinimapOpen(bool open)
        {
            minimapOpen=open;if(Application.isPlaying)PlayerPrefs.SetInt("MenuUI.minimapOpen",open?1:0);
            if(minimap!=null)minimap.gameObject.SetActive(open);Relayout();changed?.Invoke();
        }
        public void Relayout()
        {
            if(canvas==null)return;
            float scale=Mathf.Max(.1f,canvas.scaleFactor),size=Mathf.Max(64,44/scale),gap=Mathf.Max(12,6/scale);
            float railWidth=Mathf.Max(112,size+24),x=canvasRect.rect.xMax-railWidth*.5f;
            float top=canvasRect.rect.yMax-Mathf.Max(140,size*1.8f);
            for(int i=0;i<buttons.Count;i++)Place((RectTransform)buttons[i].transform,x,top-i*(size+gap),size,size);
            float playY=top-4*(size+gap)-size*.2f;
            if(controller!=null)Place(controller,x,playY,railWidth,size);
            if(play!=null){play.anchorMin=play.anchorMax=new Vector2(.5f,.5f);play.anchoredPosition=Vector2.zero;play.localScale=Vector3.one*(size/142f);}
            Place(time.rectTransform,x,playY-size*.5f-44,railWidth,80);time.fontSize=Mathf.Max(20,12/scale);
            tooltip.fontSize=Mathf.Max(20,12/scale);
            LeftBoundary=canvasRect.rect.xMax-railWidth-12;
            if(minimap!=null)
            {
                float mapWidth=Mathf.Max(64,44/scale);
                Place(minimap,LeftBoundary-mapWidth*.5f-8,canvasRect.rect.center.y,mapWidth,canvasRect.rect.height-140);
                if(minimapOpen)LeftBoundary-=mapWidth+24;
            }
            if(buttons.Count>3)buttons[3].GetComponent<EditorActionIcon>().Selected=minimapOpen;
        }
        private void Place(RectTransform r,float x,float y,float width,float height)
        {
            r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(width,height);
            r.anchoredPosition=new Vector2(x-canvasRect.rect.center.x,y-canvasRect.rect.center.y);
        }
        private void LateUpdate()
        {
            if(buttons.Count<4)return;
            buttons[0].interactable=nativeTest!=null&&nativeTest.interactable;
            buttons[1].interactable=nativeSave!=null&&nativeSave.interactable;
            if(nativeTime!=null)
            {
                string value=nativeTime.text;
                if(time.rectTransform.rect.width*canvas.scaleFactor<100){int colon=value.LastIndexOf(':');if(colon>=0)value=value.Substring(0,colon)+"\n"+value.Substring(colon+1);}
                time.text=value;
            }
        }
        public void Dispose()
        {
            for(int i=saved.Count-1;i>=0;i--)saved[i].Restore();saved.Clear();Destroy(gameObject);
        }
    }
}
