using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sekai.Localization;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.UI;

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
        private readonly List<CustomButton> buttons=new List<CustomButton>();
        private RectTransform canvasRect;
        private float canvasScale=1f;
        // These references are baked into the prefab by EditorChromePrefabBuilder so the
        // dock is static; runtime creation only happens as a fallback.
        [SerializeField] private RectTransform controller;
        [SerializeField] private RectTransform minimap;
        [SerializeField] private RectTransform play;
        [SerializeField] private TMP_Text nativeTime;
        private TMP_Text time,tooltip;
        private LocalizedTextBinding tooltipBinding;
        private Button tooltipOwner;
        private Action changed;
        private bool minimapOpen;
        public float LeftBoundary { get; private set; }
        public bool MinimapOpen=>minimapOpen;
        public CustomButton TestButton => buttons.Count>0?buttons[0]:null;
        public CustomButton SaveButton => buttons.Count>1?buttons[1]:null;
        public void Build(RectTransform targetCanvasRect,float scaleFactor,Transform tools,TMP_FontAsset font,Action toggleAssist,Action layoutChanged)
        {
            canvasRect=targetCanvasRect;canvasScale=Mathf.Max(.1f,scaleFactor);changed=layoutChanged;
            var root=(RectTransform)transform;root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            buttons.Clear();
            foreach(string name in new[]{"ZoomTimelineButtons","TimeSliderValueButtons"}) Hide(tools?.Find(name));
            AddButton("common.test_play",EditorActionIcon.Kind.Test,()=>Publish(new OnTestPlayEvent()));
            AddButton("common.save",EditorActionIcon.Kind.Save,()=>Publish(new QuickSaveMusicScoreEvent()));
            AddButton("editor.audio_assist",EditorActionIcon.Kind.Audio,toggleAssist);
            AddButton("editor.minimap",EditorActionIcon.Kind.Map,()=>SetMinimapOpen(!minimapOpen));
            if(minimap==null)
            {
                var candidate=tools?.Find("BG") as RectTransform;
                if(candidate!=null&&candidate.GetComponentInChildren<MusicScoreMinimapView>(true)!=null)minimap=candidate;
            }
            if(minimap!=null&&minimap.parent!=transform)Move(minimap);
            if(controller==null)controller=tools?.Find("MusicControllerView") as RectTransform;
            if(controller!=null)
            {
                if(controller.parent!=transform)Move(controller);
                play=controller.Find("PlayButton") as RectTransform;if(play!=null)saved.Add(new SavedRect(play));
                var oldTime=controller.Find("Time");nativeTime=oldTime?.GetComponentInChildren<TMP_Text>(true);Hide(oldTime);
            }
            time=Label("PlaybackTime",font);time.text="00:00:000";
            tooltip=Label("ActionHint",font);
            tooltipBinding=tooltip.GetComponent<LocalizedTextBinding>()??tooltip.gameObject.AddComponent<LocalizedTextBinding>();
            tooltip.gameObject.SetActive(false);
            minimapOpen=PlayerPrefs.GetInt("MenuUI.minimapOpen",1)==1;
            if(minimap!=null)minimap.gameObject.SetActive(minimapOpen);
            Relayout();
        }
        private static void Publish<T>(T evt) where T:MusicScoreMakerDispatcherEventBase
        {
            if(MusicScoreMakerEventDispatcher.ExistsInstance)MusicScoreMakerEventDispatcher.Instance.Publish(evt);
        }
        private void Hide(Transform t) { if(t is RectTransform r){saved.Add(new SavedRect(r));r.gameObject.SetActive(false);} }
        private void Move(RectTransform r) { saved.Add(new SavedRect(r));r.SetParent(transform,false);r.localScale=Vector3.one; }
        private TMP_Text Label(string name,TMP_FontAsset font)
        {
            var existing=transform.Find(name) as RectTransform;
            if(existing!=null)
            {
                var found=existing.GetComponent<TMP_Text>();
                if(found!=null)return found;
            }
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(transform,false);
            var label=go.AddComponent<TextMeshProUGUI>();label.font=font;label.color=Color.white;label.raycastTarget=false;
            label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;
            Sekai.MenuUI.MenuTypography.Bind(label,name=="PlaybackTime"?Sekai.MenuUI.MenuTextRole.Timecode:Sekai.MenuUI.MenuTextRole.Caption);
            return label;
        }
        private void AddButton(string localizationKey,EditorActionIcon.Kind kind,Action action)
        {
            var existing=transform.Find("Editor_"+kind) as RectTransform;
            CustomButton button;EditorActionIcon icon;
            if(existing!=null)
            {
                button=existing.GetComponent<CustomButton>();icon=existing.GetComponent<EditorActionIcon>();
            }
            else
            {
                var go=new GameObject("Editor_"+kind,typeof(RectTransform));go.transform.SetParent(transform,false);
                icon=go.AddComponent<EditorActionIcon>();button=go.AddComponent<CustomButton>();
            }
            if(button==null||icon==null)return;
            icon.Symbol=kind;button.targetGraphic=icon;button.navigation=new Navigation{mode=Navigation.Mode.None};
            var colors=ColorBlock.defaultColorBlock;colors.highlightedColor=new Color(.8f,1f,1f);colors.pressedColor=new Color(.5f,.85f,.9f);colors.fadeDuration=.1f;button.colors=colors;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(()=>{tooltipOwner=null;if(tooltip!=null)tooltip.gameObject.SetActive(false);action?.Invoke();});
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
                var r=(RectTransform)button.transform;
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
            if(canvasRect==null)return;
            float scale=canvasScale;
            // Match the header menu button (96x96) and keep equal gaps between every item.
            float size=Mathf.Max(96,44/scale),gap=Mathf.Round(size*.35f);
            float railWidth=Mathf.Max(112,size+24),x=canvasRect.rect.xMax-railWidth*.5f;
            float y=canvasRect.rect.yMax-Mathf.Max(160,size*1.8f);
            void Stack(RectTransform rect,float height,float width)
            {
                if(rect==null)return;
                Place(rect,x,y-height*.5f,width,height);
                y-=height+gap;
            }
            // Order: test, save, play, clock, audio, minimap. The clock stays under play.
            if(buttons.Count>0)Stack((RectTransform)buttons[0].transform,size,size);
            if(buttons.Count>1)Stack((RectTransform)buttons[1].transform,size,size);
            Stack(controller,size,railWidth);
            if(play!=null){play.anchorMin=play.anchorMax=new Vector2(.5f,.5f);play.anchoredPosition=Vector2.zero;play.localScale=Vector3.one*(size/142f);}
            if(time!=null){Stack(time.rectTransform,80,railWidth);time.fontSize=Mathf.Max(20,12/scale);}
            if(buttons.Count>2)Stack((RectTransform)buttons[2].transform,size,size);
            if(buttons.Count>3)Stack((RectTransform)buttons[3].transform,size,size);
            if(tooltip!=null)tooltip.fontSize=Mathf.Max(20,12/scale);
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
            if(nativeTime==null||time==null)return;
            string value=nativeTime.text;
            if(time.rectTransform.rect.width*canvasScale<100){int colon=value.LastIndexOf(':');if(colon>=0)value=value.Substring(0,colon)+"\n"+value.Substring(colon+1);}
            time.text=value;
        }
        public void Dispose()
        {
            // The dock is a static prefab object; unwire only and let the screen own its lifetime.
            for(int i=saved.Count-1;i>=0;i--)saved[i].Restore();saved.Clear();buttons.Clear();
        }
    }
}
