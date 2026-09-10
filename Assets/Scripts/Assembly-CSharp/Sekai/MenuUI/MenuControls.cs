using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

namespace Sekai.MenuUI
{
    public static class MenuControls
    {
        private static Sprite rounded, segmentSprite, capsule, tab, tabBody;
        public static Sprite TabBody
        {
            get
            {
                if(tabBody!=null)return tabBody;
                // Mirror the tab's upper corners for a body with a flush top edge.
                var source=Tab.texture;var pixels=source.GetPixels32();int size=source.width;
                var flipped=new Color32[pixels.Length];
                for(int y=0;y<size;y++)Array.Copy(pixels,y*size,flipped,(size-1-y)*size,size);
                var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
                tex.name="Menu tab body";tex.hideFlags=HideFlags.HideAndDontSave;tex.wrapMode=TextureWrapMode.Clamp;
                tex.SetPixels32(flipped);tex.Apply();
                tabBody=Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,1));
                tabBody.name="Menu tab body";tabBody.hideFlags=HideFlags.HideAndDontSave;return tabBody;
            }
        }
        // Only the upper corners curve: the selected tab meets the content edge squarely.
        public static Sprite Tab
        {
            get
            {
                if(tab!=null)return tab;
                const int size=64;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
                tex.name="Menu tab";tex.hideFlags=HideFlags.HideAndDontSave;tex.wrapMode=TextureWrapMode.Clamp;
                var pixels=new Color32[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float dx=Mathf.Max(12.5f-x,x-50.5f,0),dy=Mathf.Max(y-50.5f,0);
                    pixels[y*size+x]=new Color(1,1,1,y<51?1:Mathf.Clamp01(12.5f-Mathf.Sqrt(dx*dx+dy*dy)));
                }
                tex.SetPixels32(pixels);tex.Apply();
                tab=Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,1,16,16));
                tab.name="Menu tab";tab.hideFlags=HideFlags.HideAndDontSave;return tab;
            }
        }
        public static Sprite Capsule
        {
            get
            {
                if(capsule!=null)return capsule;
                var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);tex.hideFlags=HideFlags.HideAndDontSave;
                var p=new Color32[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++)p[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-Mathf.Sqrt((x-31.5f)*(x-31.5f)+(y-31.5f)*(y-31.5f))));
                tex.SetPixels32(p);tex.Apply();capsule=Sprite.Create(tex,new Rect(0,0,64,64),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(31,31,31,31));capsule.hideFlags=HideFlags.HideAndDontSave;return capsule;
            }
        }
        public static Sprite SegmentSprite => segmentSprite!=null ? segmentSprite : (segmentSprite=Sprite.Create(Rounded.texture,Rounded.rect,new Vector2(.5f,.5f),100));
        public static Sprite Rounded
        {
            get
            {
                if(rounded!=null)return rounded;
                const int size=64;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
                tex.name="Menu rounded rectangle";tex.hideFlags=HideFlags.HideAndDontSave;tex.wrapMode=TextureWrapMode.Clamp;
                var pixels=new Color32[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float dx=Mathf.Max(12.5f-x,x-50.5f,0),dy=Mathf.Max(12.5f-y,y-50.5f,0);
                    pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(12.5f-Mathf.Sqrt(dx*dx+dy*dy)));
                }
                tex.SetPixels32(pixels);tex.Apply();
                rounded=Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
                rounded.name="Menu rounded rectangle";rounded.hideFlags=HideFlags.HideAndDontSave;return rounded;
            }
        }
        public static RectTransform Rect(string name,Transform parent,Color? color=null)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            if(color.HasValue){var image=go.AddComponent<Image>();image.color=color.Value;MenuRoundedImage.Set(image,Rounded);MenuThemeBinding.Capture(go.transform);}
            return (RectTransform)go.transform;
        }
        public static void Stretch(RectTransform rect,float inset=0)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*inset;rect.offsetMax=-Vector2.one*inset;}
        public static VerticalLayoutGroup Vertical(Transform root,float spacing=10)
        {
            var v=root.gameObject.AddComponent<VerticalLayoutGroup>();v.spacing=spacing;v.childControlWidth=v.childControlHeight=true;v.childForceExpandWidth=true;v.childForceExpandHeight=false;return v;
        }
        public static TMP_Text Text(Transform root,string value,TMP_FontAsset font,float size=22)
        {
            var rect=Rect("Label",root);var t=rect.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=MenuTheme.Text;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode=TextWrappingModes.Normal;MenuThemeBinding.Bind(t,MenuColor.Text);MenuTypography.Bind(t,MenuTextRole.Body);return t;
        }
        public static Button Button(Transform root,string title,Action click,TMP_FontAsset font,float height=56)
        {
            var rect=Rect(title,root,MenuTheme.Raised);var le=rect.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=height;le.flexibleWidth=1;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
            var text=Text(rect,title,font);MenuTypography.Bind(text,MenuTextRole.Control);text.alignment=TextAlignmentOptions.Center;Stretch(text.rectTransform,8);
            if(click!=null)button.onClick.AddListener(()=>click());Style(button);return button;
        }
        public static void Style(Selectable selectable,bool primary=false,bool danger=false)
        {
            if(selectable.image!=null){MenuRoundedImage.Set(selectable.image,Capsule);MenuThemeBinding.Bind(selectable.image,primary?MenuColor.PrimaryFace:MenuColor.ButtonFace);}
            var shadow=selectable.GetComponent<Shadow>()??selectable.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.12f,.12f,.2f,.25f);shadow.effectDistance=new Vector2(1,-2);
            var c=selectable.colors;c.normalColor=Color.white;c.highlightedColor=new Color(.84f,.93f,1);c.pressedColor=new Color(.58f,.78f,.8f);c.selectedColor=c.highlightedColor;c.disabledColor=new Color(.5f,.53f,.62f,.48f);c.fadeDuration=.12f;selectable.colors=c;
            selectable.transition=Selectable.Transition.ColorTint;
            foreach(var t in selectable.GetComponentsInChildren<TMP_Text>(true))MenuThemeBinding.Bind(t,primary?MenuColor.PrimaryInk:danger?MenuColor.Danger:MenuColor.ButtonInk);
        }
        public static void Selected(Button button,bool on,string title=null)
        {
            MenuThemeBinding.Bind(button.image,on?MenuColor.PrimaryFace:MenuColor.ButtonFace);
            var label=button.GetComponentInChildren<TMP_Text>(true);if(label!=null){MenuThemeBinding.Bind(label,on?MenuColor.PrimaryInk:MenuColor.ButtonInk);if(title!=null)label.text=(on?"✓ ":"")+title;}
        }
        public static MenuSegments Segments(Transform parent,string[] titles,int selected,TMP_FontAsset font,float height,Action<int> changed)
        {
            var root=Rect("Segments",parent);var le=root.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=height;
            var layout=root.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=6;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;
            foreach(var title in titles)Button(root,title,null,font,height);
            var adapter=root.gameObject.AddComponent<MenuSegments>();adapter.Initialize(selected,changed);return adapter;
        }
        public static Transform Fold(Transform parent,string title,TMP_FontAsset font,float height,bool expanded,string preference=null)
        {
            var wrapper=Rect("Fold_"+title,parent);Vertical(wrapper,6);
            wrapper.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var accordion=wrapper.gameObject.AddComponent<Accordion>();accordion.transition=Accordion.Transition.Instant;
            var header=Rect("Header",wrapper,MenuTheme.Panel);var le=header.gameObject.AddComponent<LayoutElement>();le.minHeight=height;le.preferredHeight=height;
            var label=Text(header,title,font,22);MenuTypography.Bind(label,MenuTextRole.Section);Stretch(label.rectTransform,8);
            var body=Rect("Body",wrapper);Vertical(body,8).padding=new RectOffset(8,8,4,12);
            var toggle=header.gameObject.AddComponent<AccordionElement>();toggle.group=null;toggle.targetGraphic=header.GetComponent<Image>();
            if(preference!=null&&PlayerPrefs.HasKey("MenuUI.fold."+preference))expanded=PlayerPrefs.GetInt("MenuUI.fold."+preference)==1;
            void Apply(bool value){body.gameObject.SetActive(value);label.text=title+(value?"  −":"  ＋");MenuThemeBinding.Bind(label,value?MenuColor.Text:MenuColor.Muted);}
            toggle.SetIsOnWithoutNotify(expanded);Apply(expanded);
            toggle.onValueChanged.AddListener(value=>{Apply(value);if(preference!=null)PlayerPrefs.SetInt("MenuUI.fold."+preference,value?1:0);});return body;
        }
        public static Slider Slider(Transform parent,float min,float max,float value,Action<float> changed,float height=56)
        {
            var root=Rect("MenuSlider",parent,Color.clear);var le=root.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=height;
            var track=Rect("Track",root,MenuTheme.Border);Stretch(track);track.anchorMin=new Vector2(0,.5f);track.anchorMax=new Vector2(1,.5f);track.sizeDelta=new Vector2(-24,5);
            var area=Rect("HandleArea",root);Stretch(area,12);var fill=Rect("Fill",track,MenuTheme.Accent);Stretch(fill);
            var handle=Rect("Handle",area,Color.clear);handle.sizeDelta=new Vector2(height,0);var visible=Rect("Knob",handle,MenuTheme.Accent);visible.anchorMin=visible.anchorMax=new Vector2(.5f,.5f);visible.sizeDelta=new Vector2(18,24);
            var slider=root.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=visible.GetComponent<Image>();slider.minValue=min;slider.maxValue=max;slider.SetValueWithoutNotify(value);slider.onValueChanged.AddListener(v=>changed(v));return slider;
        }
        public static MenuRange Range(Transform parent,TMP_FontAsset font,float height,Action<float,float> changed)
        {
            var root=Rect("LoopRange",parent,MenuTheme.Background);var le=root.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=height*2+12;
            var area=Rect("RangeArea",root);Stretch(area);area.offsetMin=new Vector2(height/2,0);area.offsetMax=new Vector2(-height/2,0);
            var track=Rect("Track",area,MenuTheme.Border);Stretch(track);track.anchorMin=new Vector2(0,.5f);track.anchorMax=new Vector2(1,.5f);track.sizeDelta=new Vector2(0,5);
            var fill=Rect("Fill",area,MenuTheme.Accent);Stretch(fill);fill.offsetMin=new Vector2(0,height+4);fill.offsetMax=new Vector2(0,-height-4);
            // Each handle has its own vertical hit lane, so even equal/nearby times remain selectable.
            RectTransform Handle(string title,bool upper)
            {
                var lane=Rect(title+"HandleArea",area);Stretch(lane);
                lane.anchorMin=new Vector2(0,upper?.5f:0);lane.anchorMax=new Vector2(1,upper?1:.5f);
                lane.offsetMin=new Vector2(0,upper?6:0);lane.offsetMax=new Vector2(0,upper?0:-6);
                var r=Rect(title,lane,MenuTheme.Raised);r.sizeDelta=new Vector2(height,0);
                var t=Text(r,title,font,20);t.alignment=TextAlignmentOptions.Center;Stretch(t.rectTransform);return r;
            }
            var low=Handle("A",true);var high=Handle("B",false);var slider=root.gameObject.AddComponent<MenuRange>();slider.FillRect=fill;slider.LowHandleRect=low;slider.HighHandleRect=high;slider.targetGraphic=fill.GetComponent<Image>();slider.MinValue=0;slider.MaxValue=1;slider.HighValue=1;slider.OnValueChanged.AddListener((a,b)=>changed(a,b));return slider;
        }
    }
    // UI Extensions sends selection events while restoring state; callers receive user changes only.
    [ExecuteAlways]
    public sealed class MenuSegments : MonoBehaviour
    {
        private SegmentedControl control;private Button[] buttons;private bool syncing;private Action<int> changed;private int pending;private bool tabs;
        private GameObject[] tabIndicators;
        public void Initialize(int selected,Action<int> callback)
        {
            buttons=GetComponentsInChildren<Button>(true);
            foreach(var button in buttons){MenuRoundedImage.Set(button.image,MenuControls.Rounded);button.gameObject.AddComponent<SegmentedControlSegment>();}
            control=gameObject.AddComponent<MenuSegmentControl>();changed=callback;
            // The library's TMP colour swapping assumes a light button. Our adapter owns colour states.
            foreach(var button in buttons){button.transition=Selectable.Transition.None;button.image.CrossFadeColor(Color.white,0,true,true);}
            control.onValueChanged.AddListener(i=>{if(!syncing)pending=i;Paint(i);if(!syncing&&i>=0)changed?.Invoke(i);});SetWithoutNotify(selected);
        }
        public void SetWithoutNotify(int value)
        {
            pending=value;if(control==null)return;syncing=true;
            try
            {
                // The upstream setter caches programmatic indices separately from pointer selection.
                // Clear that cache before restoring to avoid silently retaining an old user selection.
                if(gameObject.activeInHierarchy){control.selectedSegmentIndex=-1;control.selectedSegmentIndex=value;}
                Paint(value);
            }
            finally{pending=value;syncing=false;}
        }
        private void OnEnable(){if(control!=null)SetWithoutNotify(pending);}
        public void UseTabs()
        {
            if(tabs)return;
            tabs=true;
            var bar=gameObject.AddComponent<Image>();bar.raycastTarget=false;
            MenuRoundedImage.Set(bar,MenuControls.Tab);MenuThemeBinding.Bind(bar,MenuColor.TabBar);
            // Preserve the full touch height and align the strip ends with its content body.
            var layout=GetComponent<HorizontalLayoutGroup>();layout.spacing=0;layout.padding=new RectOffset();
            tabIndicators=new GameObject[buttons.Length];
            for(int i=0;i<buttons.Length;i++)
            {
                MenuRoundedImage.Set(buttons[i].image,MenuControls.Tab);
                var edge=MenuControls.Rect("SelectedTabEdge",buttons[i].transform);
                edge.anchorMin=Vector2.zero;edge.anchorMax=new Vector2(1,0);edge.pivot=new Vector2(.5f,0);
                edge.offsetMin=Vector2.zero;edge.offsetMax=new Vector2(0,4);
                var image=edge.gameObject.AddComponent<Image>();image.raycastTarget=false;
                MenuThemeBinding.Bind(image,MenuColor.Accent);tabIndicators[i]=edge.gameObject;
            }
            Paint(pending);
        }
        private void Paint(int value)
        {
            for(int i=0;i<buttons.Length;i++)
            {
                buttons[i].image.CrossFadeColor(Color.white,0,true,true);
                if(!tabs){MenuControls.Selected(buttons[i],i==value);continue;}
                bool selected=i==value;
                MenuThemeBinding.Bind(buttons[i].image,selected?MenuColor.TabActive:MenuColor.TabBar);
                var label=buttons[i].GetComponentInChildren<TMP_Text>(true);
                MenuThemeBinding.Bind(label,selected?MenuColor.TabActiveInk:MenuColor.TabInactiveInk);
                if(label!=null)MenuTypography.Bind(label,selected?MenuTextRole.SelectedTab:MenuTextRole.Control);
                tabIndicators[i].SetActive(selected);
                var shadow=buttons[i].GetComponent<Shadow>();if(shadow!=null)shadow.enabled=false;
            }
        }
    }
    // Use UI Extensions selection behavior with Unity's layout group. The upstream
    // sprite-cutting layout conflicts with responsive groups and distorts nine-slice corners.
    public sealed class MenuSegmentControl:SegmentedControl
    {
        protected override void OnEnable() { }
        protected override void Start() { }
    }
    public sealed class MenuRange : RangeSlider
    {
        public void SetWithoutNotify(float duration,float low,float high)
        {
            // Range changes may clamp values, so reset in a valid order with the event detached.
            var evt=OnValueChanged;OnValueChanged=new RangeSliderEvent();
            try{MaxValue=Mathf.Max(.001f,duration);SetLow(0,false);SetHigh(Mathf.Clamp(high,0,MaxValue),false);SetLow(Mathf.Clamp(low,0,HighValue),false);}
            finally{OnValueChanged=evt;}
        }
    }
}
