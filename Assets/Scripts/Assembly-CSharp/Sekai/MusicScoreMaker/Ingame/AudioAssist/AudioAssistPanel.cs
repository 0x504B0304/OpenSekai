using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sekai.Localization;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sekai.MenuUI;
using Sekai.MusicScoreMaker.Ingame.Views;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    public sealed class AudioAssistPanel : MonoBehaviour
    {
        private AudioAssistController owner;
        private RectTransform root, canvasRect;
        private RectTransform quantize, toolsList;
        private Vector2 quantizePosition;
        private Canvas canvas;
        private TMP_FontAsset font;
        private bool ownsFont;
        private float layoutScale;
        private readonly List<(TMP_Text text,float size)> labels=new List<(TMP_Text,float)>();
        private TMP_Text status, inspector;
        private Button stemAnalysisButton, alignButton;
        private Image stemAnalysisFill, alignFill;
        private TMP_Text stemAnalysisLabel, alignLabel;
        private Button languageDropdownButton;
        private GameObject languageDropdownMenu;
        private Button leftEntry;
        private EditorActionDock dock;
        private AudioAssistFoldArrowGraphic leftArrow;
        private MenuSegments rates;

        private readonly Dictionary<string, Button> stems = new Dictionary<string, Button>();
        private readonly List<GameObject> groups = new List<GameObject>();
        private readonly List<GameObject> leftTools = new List<GameObject>();
        private readonly Dictionary<string, (Button button,Func<bool> on,Color color)> aux = new Dictionary<string, (Button,Func<bool>,Color)>();
        private sealed class WaveLane
        {
            public RectTransform root;
            public AudioAssistWaveform wave;
            public AudioAssistLyricOverlay syllables;
            public TMP_Text title;
            public AudioAssistSplitter splitter;
            public float preferred, actual;
        }
        private readonly Dictionary<string,WaveLane> waveLanes=new Dictionary<string,WaveLane>();
        private RectTransform controlsViewport,waveDivider;
        private string[] visibleKeys=Array.Empty<string>();
        private float waveWidth, maxWaveWidth, minimumWaveWidth, resizeStartWidth;
        private string resizingKey;
        private Transform drafts, lyrics;
        private TMP_InputField draftTime, lyricOffset, bpmInput, beatsInput;
        private int language;
        private int lyricPage, draftPage;
        private bool open, leftOpen = true;
        private float width = 510, buttonHeight = 60;
        private double calibrationA, calibrationB;
        private readonly List<double> taps = new List<double>();
        private readonly List<(Button button, Func<bool> on)> switches = new List<(Button, Func<bool>)>();
        private bool refreshing;
        private Vector2 lastCanvasSize;
        private float lastHorizontalScale;
        private float lastToolScale;
        private static Color PanelColor => MenuTheme.Panel;
        private static Color Mint => MenuTheme.Accent;
        private static Color Muted => MenuTheme.Raised;
        public void Build(AudioAssistController controller, Transform tools)
        {
            owner = controller;canvas = GetComponentInParent<Canvas>();if(canvas==null)return;
            canvasRect = (RectTransform)canvas.transform;font=GetComponentInChildren<TMP_Text>(true)?.font;
            font=MenuTypography.Get();ownsFont=false;
            layoutScale=canvas.scaleFactor;
            buttonHeight=Math.Max(58,44/Math.Max(.25f,canvas.scaleFactor));
            if (tools != null) foreach(string name in new[]{"List","UndoRedo","UIPartsToggle","EditRestrictedToggleButton"})
            { var child=tools.Find(name);if(child!=null)leftTools.Add(child.gameObject); }
            quantize=tools!=null?tools.Find("QuantizeSettingsView") as RectTransform:null;
            toolsList=tools!=null?tools.Find("List") as RectTransform:null;
            if(quantize!=null)quantizePosition=quantize.anchoredPosition;
            dock=owner.View!=null?owner.View.EditorDock:null;
            if(dock==null)
            {
                var dockObject=new GameObject("EditorActionDock",typeof(RectTransform));dockObject.transform.SetParent(canvas.transform,false);
                dock=dockObject.AddComponent<EditorActionDock>();
            }
            dock.Build(canvasRect,canvas.scaleFactor,tools,font,()=>Toggle(!open),()=>{LayoutPanels();LateUpdate();});
            var arrowObject=new GameObject("AudioAssistToolsToggle",typeof(RectTransform));
            arrowObject.transform.SetParent(canvas.transform,false);
            leftArrow=arrowObject.AddComponent<AudioAssistFoldArrowGraphic>();
            leftEntry=arrowObject.AddComponent<Button>();leftEntry.targetGraphic=leftArrow;
            leftEntry.navigation=new Navigation{mode=Navigation.Mode.None};
            var arrowColors=ColorBlock.defaultColorBlock;
            arrowColors.normalColor=new Color(.86f,.92f,1,1);
            arrowColors.highlightedColor=Color.white;
            arrowColors.pressedColor=new Color(.6f,1,1,1);
            arrowColors.selectedColor=arrowColors.normalColor;
            arrowColors.fadeDuration=.1f;leftEntry.colors=arrowColors;
            leftEntry.onClick.AddListener(()=>ToggleTools(!leftOpen));
            root=Rect("AudioAssistPanel",canvas.transform,PanelColor);root.sizeDelta=new Vector2(width,900);
            // Stay hidden until Build completes; a mid-build failure must not leave a half-built panel on screen.
            root.gameObject.SetActive(false);
            root.gameObject.AddComponent<RectMask2D>();
            waveDivider=Rect("WaveControlsDivider",root,MenuTheme.Border);waveDivider.anchorMin=new Vector2(0,0);waveDivider.anchorMax=new Vector2(0,1);waveDivider.pivot=new Vector2(.5f,.5f);waveDivider.sizeDelta=new Vector2(2,0);waveDivider.GetComponent<Image>().raycastTarget=false;
            RectTransform viewport=Rect("AssistControls",root,Color.clear);controlsViewport=viewport;viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;scroll.viewport=viewport;
            RectTransform content=Rect("Content",viewport,Color.clear);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var contentLayout=Vertical(content,12);contentLayout.padding=new RectOffset(14,52,0,0);content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=content;
            Label(content,"采音辅助",27,42);
            // Status hugs the title; the first card starts right below it.
            status=Label(content,"读取音频…",18,48);status.alignment=TextAlignmentOptions.TopLeft;
            var sound=Section(content,"混音",true);
            Label(sound,"按钮亮起参与混音 · 拖动右侧调整音量",18,36);
            for(int i=0;i<KeysLength;i++)
            {
                string key=AudioAssistController.Keys[i];
                var row=MixRow(sound);
                stems[key]=Button(row,AudioAssistController.Names[i],()=>owner.ToggleStem(key));
                NarrowButton(stems[key]);
                Localize(stems[key],"editor.audio_assist.mix."+key);
                WideSlider(Slider(row,owner.State.stems.First(s=>s.key==key).volume,value=>{owner.State.stems.First(s=>s.key==key).volume=value;owner.Transport.ApplyMix();owner.Save();}));
            }
            AuxChannel(sound,"metronome","editor.audio_assist.mix.metronome",()=>owner.State.metronome,()=>{owner.State.metronome=!owner.State.metronome;owner.Save();},()=>owner.State.metronomeVolume,v=>{owner.State.metronomeVolume=v;owner.Save();});
            AuxChannel(sound,"draft","editor.audio_assist.mix.draft",()=>owner.State.draftSound,()=>{owner.State.draftSound=!owner.State.draftSound;owner.Save();},()=>owner.State.draftVolume,v=>{owner.State.draftVolume=v;owner.Save();});
            AuxChannel(sound,"note","editor.audio_assist.mix.note",()=>owner.State.noteSound,()=>{owner.State.noteSound=!owner.State.noteSound;owner.Save();},()=>owner.State.noteVolume,v=>{owner.State.noteVolume=v;owner.Save();});
            rates=MenuControls.Segments(sound,new[]{"1×","0.75×","0.5×"},0,font,buttonHeight,i=>owner.ChangeRate(new[]{1f,.75f,.5f}[i]));
            Label(sound,"保音高慢放；首次需准备音频",17,45);
            stemAnalysisButton=ProgressButton(sound,"运行分轨",()=>
            {
                if(owner.AnalysisStatus==AssistAnalysisStatus.Running&&owner.AnalysisKind==AssistAnalysisKind.Stems)owner.Cancel();
                else owner.AnalyzeStems(new[]{"ja","zh","en"}[language]);
            },out stemAnalysisFill,out stemAnalysisLabel);
            var lrcGroup=Fold(content,"歌词与音节");
            Button(lrcGroup,"导入 LRC",owner.ImportLyrics);
            languageDropdownButton=Button(lrcGroup,LanguageLabel(),null);
            languageDropdownButton.onClick.AddListener(ToggleLanguageDropdown);
            languageDropdownMenu=BuildLanguageDropdownMenu(lrcGroup);
            alignButton=ProgressButton(lrcGroup,"对齐歌词与音节",()=>
            {
                if(owner.AnalysisStatus==AssistAnalysisStatus.Running&&owner.AnalysisKind==AssistAnalysisKind.Alignment)owner.Cancel();
                else owner.AlignLyrics(new[]{"ja","zh","en"}[language]);
            },out alignFill,out alignLabel);
            lyricOffset=Number(lrcGroup,"歌词整体偏移（秒）",owner.State.lyricOffset,value=>{owner.State.lyricOffset=value;owner.Save();});
            Label(lrcGroup,"普通 LRC 按句；自动对齐或增强 LRC 可逐音节试听。",17,80);
            var lyricNavigation=Row(lrcGroup);
            Button(lyricNavigation,"上一句",()=>FocusLyric(lyricPage-1));
            Button(lyricNavigation,"下一句",()=>FocusLyric(lyricPage+1));
            Button(lrcGroup,"当前附近歌词",()=>{lyricPage=Math.Max(0,owner.State.lyrics.FindLastIndex(l=>l.seconds+owner.State.lyricOffset<=owner.SelectedSeconds));lyricSignature=null;Refresh();});
            lyrics=Container(lrcGroup);
            var marks=Section(content,"起音与标记");
            inspector=Label(marks,"点击波形定位",21,90);
            Switch(marks,"起音提示",()=>owner.ShowOnsets,()=>{owner.ShowOnsets=!owner.ShowOnsets;Refresh();});
            Label(marks,"提示灵敏度",18,36);Slider(marks,owner.State.sensitivity,v=>{owner.State.sensitivity=v;Refresh();});
            Button(marks,"试听附近",()=>owner.Audition(owner.SelectedSeconds-.3,owner.SelectedSeconds+.6,false));
            var loop=Section(content,"试听与循环",true);
            Switch(loop,"框选区间",()=>owner.RegionMode,()=>{owner.RegionMode=!owner.RegionMode;Refresh();});
            Switch(loop,"启用循环",()=>owner.State.loop,()=>{owner.Presenter.PauseAssist();owner.State.loop=!owner.State.loop;owner.Save();});
            Button(loop,"播放选区",()=>owner.Audition(owner.State.loopA,owner.State.loopB,true));
            var draftGroup=Section(content,"采音草稿");
            draftTime=Number(draftGroup,"选中草稿时间（秒）",0,v=>owner.EditDraft(owner.SelectedDraft,v));
            var nudge=Row(draftGroup);Button(nudge,"−1ms",()=>owner.EditDraft(owner.SelectedDraft,(owner.SelectedDraft?.seconds??0)-.001));Button(nudge,"＋1ms",()=>owner.EditDraft(owner.SelectedDraft,(owner.SelectedDraft?.seconds??0)+.001));
            Button(draftGroup,"删除选中",owner.DeleteDraft);
            Switch(draftGroup,"下次为长条尾点",()=>owner.FinishHold,()=>{owner.FinishHold=!owner.FinishHold;Refresh();});
            Button(draftGroup,"取消长条录入",()=>{owner.Presenter.CancelAssistHold();owner.FinishHold=false;owner.Status="已取消待指定的长条";Refresh();});
            Label(draftGroup,"选择草稿 → 选音符工具 → 点击轨道。长条依次指定头、中间点和尾。",17,95);
            var draftNavigation=Row(draftGroup);
            Button(draftNavigation,"上一页",()=>{draftPage=Math.Max(0,draftPage-1);draftSignature=null;Refresh();});
            Button(draftNavigation,"下一页",()=>{draftPage=Math.Min(Math.Max(0,(owner.State.drafts.Count-1)/20),draftPage+1);draftSignature=null;Refresh();});
            drafts=Container(draftGroup);
            var timing=Section(content,"节拍校准");
            bpmInput=Number(timing,"BPM",120,_=>{});
            Button(timing,"敲拍估算",()=>{double now=Time.realtimeSinceStartupAsDouble;if(taps.Count>0&&now-taps[taps.Count-1]>3)taps.Clear();taps.Add(now);if(taps.Count>1)bpmInput.SetTextWithoutNotify((60*(taps.Count-1)/(now-taps[0])).ToString("0.00",CultureInfo.InvariantCulture));});
            Button(timing,"记录第一个拍点",()=>{calibrationA=owner.SelectedSeconds;owner.Status="A = "+Format(calibrationA);Refresh();});
            Button(timing,"记录第二个拍点",()=>{calibrationB=owner.SelectedSeconds;owner.Status="B = "+Format(calibrationB);Refresh();});
            beatsInput=Number(timing,"两点间拍数",4,_=>{});
            Button(timing,"两点估算",()=>{if(calibrationB>calibrationA&&TryNumber(beatsInput.text,out var beats)&&beats>0)bpmInput.SetTextWithoutNotify((60*beats/(calibrationB-calibrationA)).ToString("0.00",CultureInfo.InvariantCulture));});
            Switch(timing,"预览候选拍线",()=>owner.PreviewBeats,()=>{if(TryNumber(bpmInput.text,out var bpm)&&bpm>=20&&bpm<=1000){owner.PreviewBeats=!owner.PreviewBeats;owner.PreviewBpm=bpm;long tick=owner.Presenter.AssistTicks(owner.SelectedSeconds);owner.PreviewBeatOrigin=owner.Presenter.AssistSeconds(tick)+(Math.Ceiling(tick/480d)*480-tick)*60/(bpm*480);Refresh();}});
            Button(timing,"在当前点应用 BPM",()=>{if(TryNumber(bpmInput.text,out var bpm)){owner.Presenter.PauseAssist();try{owner.Presenter.ApplyAssistBpm(owner.SelectedSeconds,bpm);owner.Status="已应用 BPM，可用原撤销按钮恢复";}catch(Exception ex){owner.Status=ex.Message;}Refresh();}});
            Button(content,"保存采音状态",()=>{owner.Save();owner.Status="采音草稿已保存；正式音符请用游戏保存按钮保存";Refresh();});
            // Keep mixing and looping adjacent; advanced groups follow them.
            loop.parent.SetSiblingIndex(sound.parent.GetSiblingIndex()+1);
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
                if(!labels.Any(item=>item.text==label)){labels.Add((label,label.fontSize));label.fontSize=Math.Max(label.fontSize,12/Math.Max(.1f,canvas.scaleFactor));}
            MenuThemeBinding.Capture(root);MenuTheme.Changed+=Refresh;
            owner.Changed+=Refresh;root.gameObject.SetActive(false);Refresh();
            ToggleTools(PlayerPrefs.GetInt("MenuUI.toolsOpen",1)==1);
            Toggle(PlayerPrefs.GetInt("MenuUI.assistOpen",0)==1);
        }
        private int KeysLength=>AudioAssistController.Keys.Length;
        public void Toggle(bool value)
        {
            if(root==null)return;open=value;
            owner.Transport.SetAssistMixActive(open);
            if(Application.isPlaying)PlayerPrefs.SetInt("MenuUI.assistOpen",value?1:0);
            LayoutPanels();root.gameObject.SetActive(open);
            dock.SetAssistOpen(open);
            LateUpdate();Refresh();
        }
        public void ToggleTools(bool value)
        {
            leftOpen=value;
            if(Application.isPlaying)PlayerPrefs.SetInt("MenuUI.toolsOpen",value?1:0);
            foreach(var tool in leftTools)if(tool!=null)tool.SetActive(leftOpen);
            leftArrow.PointsLeft=leftOpen;
            LayoutPanels();LateUpdate();Refresh();
        }
        private void LayoutPanels()
        {
            if(root==null||owner.View.RectTransform==null)return;
            lastCanvasSize=canvasRect.rect.size;
            lastHorizontalScale=MusicScoreMakerSettingsManager.ScoreDisplayScaleHorizontal;
            lastToolScale=MusicScoreMakerSettingsManager.ToolWindowChildScale;
            // Always compute from the restored editor width; repeated toggles must not drift.
            owner.View.SetAudioAssistLayout(0,0);
            dock.Relayout();
            var toggleRect=(RectTransform)leftEntry.transform;
            // The visible arrow is small; its invisible hit area remains at least
            // 44 physical pixels. Anchor to the actual tool list, not the screen top.
            float uiScale=Math.Max(.1f,canvas.scaleFactor);
            Position(toggleRect,new Vector2(0,.5f),new Vector2(Math.Max(44,44/uiScale),Math.Max(68,44/uiScale)));
            float arrowX=canvasRect.rect.xMin+toggleRect.rect.width*.5f+4;
            float arrowY=canvasRect.rect.center.y;
            if(leftOpen&&toolsList!=null)
            {
                var toolCorners=new Vector3[4];toolsList.GetWorldCorners(toolCorners);
                var bottomRight=canvasRect.InverseTransformPoint(toolCorners[3]);
                var topRight=canvasRect.InverseTransformPoint(toolCorners[2]);
                arrowX=topRight.x+12;
                arrowY=(bottomRight.y+topRight.y)*.5f;
            }
            arrowX=Mathf.Clamp(arrowX,canvasRect.rect.xMin+toggleRect.rect.width*.5f,canvasRect.rect.xMax-toggleRect.rect.width*.5f);
            arrowY=Mathf.Clamp(arrowY,canvasRect.rect.yMin+toggleRect.rect.height*.5f,canvasRect.rect.yMax-toggleRect.rect.height*.5f);
            toggleRect.anchoredPosition=new Vector2(arrowX-canvasRect.rect.xMin,arrowY-canvasRect.rect.center.y);
            var corners=new Vector3[4];toggleRect.GetWorldCorners(corners);
            float left=canvasRect.InverseTransformPoint(corners[3]).x+48;
            float right=dock.LeftBoundary-8;
            float available=Math.Max(1,right-left);
            minimumWaveWidth=Math.Max(64,44/uiScale);
            float chartMinimum=Math.Max(180,100/uiScale);
            float controlsWidth=Mathf.Clamp(available*.30f,320,420);
            controlsWidth=Math.Max(280,Math.Min(controlsWidth,available-chartMinimum-visibleKeys.Length*minimumWaveWidth-16));
            maxWaveWidth=Math.Max(0,available-controlsWidth-chartMinimum-16);
            float requested=visibleKeys.Sum(k=>Math.Max(minimumWaveWidth,waveLanes[k].preferred));
            float baseline=visibleKeys.Length>0?Math.Min(minimumWaveWidth,maxWaveWidth/visibleKeys.Length):0;
            float extraRequested=Math.Max(0,requested-visibleKeys.Length*minimumWaveWidth);
            float extraAvailable=Math.Max(0,maxWaveWidth-visibleKeys.Length*baseline);
            waveWidth=0;
            foreach(string key in visibleKeys)
            {
                var lane=waveLanes[key];float desired=Math.Max(minimumWaveWidth,lane.preferred);
                lane.actual=requested<=maxWaveWidth?desired:baseline+(extraRequested>0?extraAvailable*(desired-minimumWaveWidth)/extraRequested:0);
                waveWidth+=lane.actual;
            }
            width=controlsWidth+waveWidth;
            owner.View.FitEditorLanes(canvasRect,left,right-(open?width+16:0));
            if(quantize!=null&&owner.View.NotesViewRectTransform!=null)
            {
                quantize.anchoredPosition=quantizePosition;
                owner.View.NotesViewRectTransform.GetWorldCorners(corners);
                float noteRight=canvasRect.InverseTransformPoint(corners[3]).x;
                quantize.GetWorldCorners(corners);
                float quantizeRight=canvasRect.InverseTransformPoint(corners[3]).x;
                quantize.position+=canvasRect.TransformVector(new Vector3(noteRight-quantizeRight-16,0,0));
            }
        }
        private void LateUpdate()
        {
            if(canvas!=null&&Math.Abs(layoutScale-canvas.scaleFactor)>.001f)
            {
                layoutScale=canvas.scaleFactor;buttonHeight=Math.Max(58,44/Math.Max(.1f,layoutScale));
                foreach(var item in labels)if(item.text!=null)item.text.fontSize=Math.Max(item.size,12/Math.Max(.1f,layoutScale));
                foreach(var button in root.GetComponentsInChildren<Button>(true))
                {var element=button.GetComponent<LayoutElement>();if(element!=null)element.minHeight=element.preferredHeight=buttonHeight;}
                foreach(var element in root.GetComponentsInChildren<LayoutElement>(true))
                    if(element.name=="Row"||element.name=="Volume"||element.name.StartsWith("Input_"))element.preferredHeight=buttonHeight;
                LayoutPanels();
            }
            if(root!=null&&(lastCanvasSize!=canvasRect.rect.size||Math.Abs(lastHorizontalScale-MusicScoreMakerSettingsManager.ScoreDisplayScaleHorizontal)>.001f||Math.Abs(lastToolScale-MusicScoreMakerSettingsManager.ToolWindowChildScale)>.001f))LayoutPanels();
            if(!open||root==null||owner.View.NotesViewRectTransform==null)return;
            var corners=new Vector3[4];owner.View.NotesViewRectTransform.GetWorldCorners(corners);
            Vector3 bottom=canvasRect.InverseTransformPoint(corners[3]),top=canvasRect.InverseTransformPoint(corners[2]);
            float visibleBottom=Math.Max(bottom.y,canvasRect.rect.yMin),visibleTop=Math.Min(top.y,canvasRect.rect.yMax);
            float center=(visibleBottom+visibleTop)*.5f;
            root.anchorMin=root.anchorMax=new Vector2(.5f,.5f);root.pivot=new Vector2(0,.5f);
            root.anchoredPosition=new Vector2(bottom.x+8,center);root.sizeDelta=new Vector2(width,visibleTop-visibleBottom);
            controlsViewport.anchorMin=Vector2.zero;controlsViewport.anchorMax=Vector2.one;
            controlsViewport.offsetMin=new Vector2(waveWidth+10,0);controlsViewport.offsetMax=new Vector2(-8,0);
            if(waveDivider!=null)waveDivider.anchoredPosition=new Vector2(waveWidth+5,0);
            float laneLeft=0;
            foreach(string key in visibleKeys)
            {
                var lane=waveLanes[key];var r=lane.root;
                r.anchorMin=new Vector2(0,0);r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,.5f);
                r.sizeDelta=new Vector2(lane.actual,0);r.anchoredPosition=new Vector2(laneLeft,0);
                // Each lane owns a mesh, retaining one strip per physical pixel
                // without exceeding UGUI's vertex limit when all six are visible.
                var wr=lane.wave.rectTransform;wr.anchorMin=new Vector2(0,.5f);wr.anchorMax=new Vector2(1,.5f);
                wr.sizeDelta=new Vector2(0,top.y-bottom.y);wr.anchoredPosition=new Vector2(0,(bottom.y+top.y)*.5f-center);
                if(lane.syllables!=null)lane.syllables.RefreshLayout();
                var separator=lane.splitter.rectTransform;
                separator.anchorMin=new Vector2(0,0);separator.anchorMax=new Vector2(0,1);separator.pivot=new Vector2(.5f,.5f);
                separator.sizeDelta=new Vector2(44/Math.Max(.1f,canvas.scaleFactor),0);separator.anchoredPosition=new Vector2(laneLeft,0);
                lane.splitter.transform.SetAsLastSibling();laneLeft+=lane.actual;
            }
        }
        private static void PaintStem(Button button,string key,bool on)
        {
            // These six controls own their colors; retain disabled bindings so
            // theme capture cannot replace the stem palette with the global mint.
            var face=button.image;var label=button.GetComponentInChildren<TMP_Text>(true);
            var faceBinding=face.GetComponent<MenuThemeBinding>();if(faceBinding!=null)faceBinding.enabled=false;
            var labelBinding=label.GetComponent<MenuThemeBinding>();if(labelBinding!=null)labelBinding.enabled=false;
            face.color=on?AudioAssistStemColors.For(key):MenuTheme.Background;
            label.color=on?new Color(.14f,.18f,.25f):MenuTheme.ButtonInk;
            var colors=button.colors;colors.selectedColor=Color.white;button.colors=colors;
        }
        // Non-waveform mix channels (metronome, draft cue, note SE): colored toggle + volume.
        private void AuxChannel(Transform parent,string key,string labelKey,Func<bool> state,Action toggle,Func<float> volume,Action<float> setVolume)
        {
            var row=MixRow(parent);
            var button=Button(row,LocalizationManager.Get(labelKey),toggle);
            NarrowButton(button);
            Localize(button,labelKey);
            aux[key]=(button,state,AudioAssistStemColors.For(key));
            WideSlider(Slider(row,volume(),setVolume));
        }
        // Mix channels keep a narrow button so the volume slider gets most of the row.
        private Transform MixRow(Transform parent)
        {
            var row=Rect("Row",parent,Color.clear);row.gameObject.AddComponent<LayoutElement>().preferredHeight=buttonHeight;
            var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=6;layout.childControlWidth=true;layout.childForceExpandWidth=false;layout.childControlHeight=true;layout.childForceExpandHeight=true;return row;
        }
        private static void NarrowButton(Button button)
        {
            var element=button.GetComponent<LayoutElement>();if(element==null)return;
            element.preferredWidth=0;element.flexibleWidth=.3f;
        }
        private static void WideSlider(Slider slider)
        {
            var element=slider.GetComponent<LayoutElement>();if(element==null)return;
            element.preferredWidth=0;element.flexibleWidth=.7f;
        }
        private static void Localize(Button button,string key)
        {
            var label=button!=null?button.GetComponentInChildren<TMP_Text>(true):null;
            Localize(label,key);
            if(label==null)return;
            // Narrow buttons: keep one line and shrink longer translations instead of clipping.
            label.textWrappingMode=TextWrappingModes.NoWrap;
            label.overflowMode=TextOverflowModes.Overflow;
            label.enableAutoSizing=true;
            label.fontSizeMin=11;
            label.fontSizeMax=23;
        }
        private static void Localize(TMP_Text label,string key)
        {
            if(label==null)return;
            var binding=label.GetComponent<LocalizedTextBinding>()??label.gameObject.AddComponent<LocalizedTextBinding>();
            binding.PreserveLayout=true;
            binding.Key=key;
        }
        private static void PaintAux(Button button,Color color,bool on)
        {
            var face=button.image;var label=button.GetComponentInChildren<TMP_Text>(true);
            var faceBinding=face.GetComponent<MenuThemeBinding>();if(faceBinding!=null)faceBinding.enabled=false;
            var labelBinding=label.GetComponent<MenuThemeBinding>();if(labelBinding!=null)labelBinding.enabled=false;
            face.color=on?color:MenuTheme.Background;
            label.color=on?new Color(.14f,.18f,.25f):MenuTheme.ButtonInk;
            var colors=button.colors;colors.selectedColor=Color.white;button.colors=colors;
        }
        private void SyncWaveLanes()
        {
            string[] next=AudioAssistController.Keys.Where(k=>owner.Visible.Contains(k)&&owner.Transport.Tracks.ContainsKey(k)).ToArray();
            bool changed=!visibleKeys.SequenceEqual(next);
            foreach(string key in next)
            {
                if(waveLanes.ContainsKey(key))continue;
                var lane=new WaveLane();
                var go=new GameObject("WaveLane_"+key,typeof(RectTransform));go.transform.SetParent(root,false);
                lane.root=(RectTransform)go.transform;go.AddComponent<RectMask2D>();
                var graph=new GameObject("Waveform_"+key,typeof(RectTransform));graph.transform.SetParent(go.transform,false);
                lane.wave=graph.AddComponent<AudioAssistWaveform>();lane.wave.Controller=owner;lane.wave.StemKey=key;
                if(key=="vocals")
                {
                    lane.syllables=graph.AddComponent<AudioAssistLyricOverlay>();
                    lane.syllables.Build(owner,lane.wave,font);lane.wave.Lyrics=lane.syllables;
                }
                var titleBack=Rect("StemHeader",go.transform,PanelColor);titleBack.GetComponent<Image>().raycastTarget=false;
                titleBack.anchorMin=new Vector2(0,1);titleBack.anchorMax=Vector2.one;titleBack.pivot=new Vector2(.5f,1);
                titleBack.sizeDelta=new Vector2(0,44);titleBack.anchoredPosition=Vector2.zero;
                lane.title=Label(titleBack,AudioAssistController.Names[Array.IndexOf(AudioAssistController.Keys,key)],18,44);
                Localize(lane.title,"editor.audio_assist.mix."+key);
                lane.title.GetComponent<MenuThemeBinding>().enabled=false;lane.title.color=AudioAssistStemColors.For(key);
                lane.title.alignment=TextAlignmentOptions.Center;lane.title.textWrappingMode=TextWrappingModes.NoWrap;lane.title.overflowMode=TextOverflowModes.Ellipsis;
                lane.title.rectTransform.anchorMin=Vector2.zero;lane.title.rectTransform.anchorMax=Vector2.one;
                lane.title.rectTransform.offsetMin=new Vector2(6,0);lane.title.rectTransform.offsetMax=new Vector2(-6,0);
                float stored=PlayerPrefs.GetFloat("MenuUI.waveWidth."+key,key=="vocals"?190:110);
                lane.preferred=float.IsNaN(stored)||float.IsInfinity(stored)?110:Mathf.Clamp(stored,64,500);
                var divider=new GameObject("WaveDivider_"+key,typeof(RectTransform));divider.transform.SetParent(root,false);
                lane.splitter=divider.AddComponent<AudioAssistSplitter>();
                lane.splitter.BeginResize=()=>
                {
                    if(resizingKey!=null)return;
                    resizingKey=key;
                    foreach(string visible in visibleKeys)waveLanes[visible].preferred=waveLanes[visible].actual;
                    resizeStartWidth=lane.actual;
                };
                lane.splitter.Resize=delta=>
                {
                    if(resizingKey!=key)return;
                    float otherWidth=visibleKeys.Where(k=>k!=key).Sum(k=>waveLanes[k].actual);
                    float upper=Math.Max(16,Math.Min(500,maxWaveWidth-otherWidth));
                    lane.preferred=Mathf.Clamp(resizeStartWidth-delta,Math.Min(minimumWaveWidth,upper),upper);
                    LayoutPanels();LateUpdate();
                };
                lane.splitter.EndResize=()=>
                {
                    if(resizingKey!=key)return;resizingKey=null;
                    if(Application.isPlaying)
                    {
                        foreach(var pair in waveLanes)PlayerPrefs.SetFloat("MenuUI.waveWidth."+pair.Key,pair.Value.preferred);
                        PlayerPrefs.Save();
                    }
                };
                waveLanes.Add(key,lane);
            }
            visibleKeys=next;
            foreach(var pair in waveLanes)
            {
                bool visible=visibleKeys.Contains(pair.Key);
                pair.Value.root.gameObject.SetActive(visible);pair.Value.splitter.gameObject.SetActive(visible);
                // Keep the add-marker "+" on the leftmost lane only.
                pair.Value.wave.ShowMarkerIcon=visibleKeys.Length>0&&pair.Key==visibleKeys[0];
            }
            if(changed){LayoutPanels();LateUpdate();}
        }
        private void Refresh()
        {
            if(root==null||refreshing)return;refreshing=true;
            try
            {
                status.text=owner.Status;
                RefreshAnalysisButton();
                rates.SetWithoutNotify(owner.Transport.Rate==1?0:owner.Transport.Rate==.75f?1:2);
                foreach(var pair in stems){var state=owner.State.stems.First(s=>s.key==pair.Key);bool ready=owner.Transport.Tracks.ContainsKey(pair.Key);pair.Value.interactable=ready;PaintStem(pair.Value,pair.Key,state.enabled&&ready);}
                foreach(var item in aux.Values)PaintAux(item.button,item.color,item.on());
                foreach(var item in switches){bool on=item.on();MenuControls.Selected(item.button,on,item.button.name.Substring("Assist_".Length));if(!on)MenuThemeBinding.Bind(item.button.image,MenuColor.Background);}
                if(!draftTime.isFocused)draftTime.SetTextWithoutNotify((owner.SelectedDraft?.seconds??owner.SelectedSeconds).ToString("0.000",CultureInfo.InvariantCulture));
                if(!lyricOffset.isFocused)lyricOffset.SetTextWithoutNotify(owner.State.lyricOffset.ToString("0.000",CultureInfo.InvariantCulture));
                long ticks=owner.Presenter.AssistTicks(owner.SelectedSeconds);long snapped=MusicScoreMakerUtility.CalculateSnapQuantizedTicks(0,ticks);
                inspector.text=Format(owner.SelectedSeconds)+"\n距拍线 "+((owner.SelectedSeconds-owner.Presenter.AssistSeconds(snapped))*1000).ToString("+0.0;-0.0;0",CultureInfo.InvariantCulture)+" ms";
                if(owner.SelectedSyllable!=null)
                {
                    var point=owner.SelectedSyllable;
                    inspector.text=point.label+"  "+Format(point.seconds+owner.State.lyricOffset)+"\n"+
                        ((owner.SelectedSyllableEnd-point.seconds)*1000).ToString("0",CultureInfo.InvariantCulture)+" ms"+
                        (point.manuallyEdited?" · 人工调整":!AudioAssistAlgorithms.HasSyllableEnd(point)?" · 时长估算":point.confidence<.45f?" · 待核对":"");
                }
                SyncWaveLanes();
                RebuildDrafts();RebuildLyrics();foreach(var lane in waveLanes.Values)lane.wave.SetVerticesDirty();
            }
            finally{refreshing=false;}
        }
        private string draftSignature,lyricSignature;
        private void FocusLyric(int index)
        {
            if(owner.State.lyrics.Count==0)return;
            lyricPage=Mathf.Clamp(index,0,owner.State.lyrics.Count-1);lyricSignature=null;
            if(owner.Transport.Tracks.ContainsKey("vocals")&&!owner.Visible.Contains("vocals"))owner.Visible.Add("vocals");
            owner.SetPoint(owner.State.lyrics[lyricPage].seconds+owner.State.lyricOffset);
        }
        private void RebuildDrafts()
        {
            string signature=string.Join("|",owner.State.drafts.Select(d=>d.id+d.seconds+d.used))+(owner.SelectedDraft?.id??"");if(signature==draftSignature)return;draftSignature=signature;
            Clear(drafts);
            draftPage=Math.Min(draftPage,Math.Max(0,(owner.State.drafts.Count-1)/20));
            Label(drafts,$"{owner.State.drafts.Count} 个草稿 · 第 {draftPage+1} 页",18,36);
            foreach(var point in owner.State.drafts.OrderBy(d=>d.seconds).Skip(draftPage*20).Take(20))
            {
                var d=point;var button=Button(drafts,(d.used?"✓ ":"")+d.label+" "+Format(d.seconds),()=>{owner.SelectedDraft=d;owner.SelectedSeconds=d.seconds;owner.Presenter.SeekAssist(d.seconds);Refresh();});
                button.image.color=d==owner.SelectedDraft?new Color(.55f,.42f,.72f):Muted;
            }
        }
        private void RebuildLyrics()
        {
            string signature=JsonUtility.ToJson(new AssistLyricPage{lines=owner.State.lyrics})+":"+owner.State.lyricOffset+":"+lyricPage;
            if(signature==lyricSignature)return;lyricSignature=signature;Clear(lyrics);
            lyricPage=Math.Max(0,Math.Min(lyricPage,owner.State.lyrics.Count-1));
            Label(lyrics,$"第 {Math.Min(owner.State.lyrics.Count,lyricPage+1)} / {owner.State.lyrics.Count} 句",18,36);
            foreach(var line in owner.State.lyrics.Skip(lyricPage).Take(1))
            {
                var lyric=line;double t=line.seconds+owner.State.lyricOffset;
                Label(lyrics,Format(t)+" "+line.text,19,70);
                var row=Row(lyrics);Button(row,"试听",()=>owner.Audition(t-.15,t+Math.Max(.6,NextLyricTime(lyric)-lyric.seconds),false));
                Button(row,"＋ 草稿",()=>owner.AddDrafts(lyric.syllables.Count>0?lyric.syllables.Select(s=>new AssistDraft{seconds=s.seconds+owner.State.lyricOffset,label=s.label,confidence=s.confidence}):new[]{new AssistDraft{seconds=t,label=lyric.text}}));
                Label(lyrics,line.syllables.Count>0?"点击人声音节试听；右键或长按修改文字、起止时间或删除。":"此句只有整句时间；对齐后显示音节标签。",17,80);
            }
        }
        private void RefreshAnalysisButton()
        {
            bool running=owner.AnalysisStatus==AssistAnalysisStatus.Running;
            RefreshProgressButton(stemAnalysisButton,stemAnalysisFill,stemAnalysisLabel,
                owner.AnalysisKind==AssistAnalysisKind.Stems,running,"运行分轨");
            RefreshProgressButton(alignButton,alignFill,alignLabel,
                owner.AnalysisKind==AssistAnalysisKind.Alignment,running,"对齐歌词与音节");
            if(languageDropdownButton!=null)
            {
                languageDropdownButton.interactable=!owner.Busy;
                var label=languageDropdownButton.GetComponentInChildren<TMP_Text>(true);
                if(label!=null)label.text=LanguageLabel();
            }
        }
        private void RefreshProgressButton(Button button,Image fill,TMP_Text label,bool ownsOperation,bool running,string idleText)
        {
            if(button==null||fill==null||label==null)return;
            bool active=running&&ownsOperation;
            button.interactable=!owner.Busy||active;
            fill.gameObject.SetActive(active||
                (ownsOperation&&owner.AnalysisStatus!=AssistAnalysisStatus.Idle));
            var binding=fill.GetComponent<MenuThemeBinding>();if(binding!=null)binding.enabled=false;
            fill.color=Color.Lerp(MenuTheme.ButtonFace,owner.AnalysisStatus==AssistAnalysisStatus.Failed?MenuTheme.Danger:MenuTheme.Accent,.32f);
            fill.rectTransform.anchorMax=new Vector2(ownsOperation?Mathf.Clamp01(owner.AnalysisProgress):0,1);
            fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
            label.fontSizeMin=12/Math.Max(.1f,canvas.scaleFactor);label.fontSizeMax=Math.Max(23,label.fontSizeMin);
            if(active)
            {
                string stage=owner.AnalysisStage.Replace("正在分离人声、鼓组、贝斯和旋律","正在分轨").Replace("载入内置多语言逐音节对齐模型","载入音节对齐模型").Replace("载入内置 Demucs 分轨模型","载入分轨模型");
                label.text=stage+"\n"+Mathf.FloorToInt(owner.AnalysisProgress*100)+"%";
            }
            else if(ownsOperation&&owner.AnalysisStatus==AssistAnalysisStatus.Completed)
                label.text="✓ "+idleText+"完成 100%";
            else if(ownsOperation&&owner.AnalysisStatus==AssistAnalysisStatus.Canceled)
                label.text="已取消 · "+idleText;
            else if(ownsOperation&&owner.AnalysisStatus==AssistAnalysisStatus.Failed)
                label.text="处理失败 · 点击重试";
            else label.text=idleText;
        }
        private string LanguageLabel()=>"语言："+new[]{"日语","中文","英语（词）"}[Mathf.Clamp(language,0,2)]+"  ▼";
        private void ToggleLanguageDropdown()
        {
            if(languageDropdownMenu!=null)languageDropdownMenu.SetActive(!languageDropdownMenu.activeSelf);
        }
        private void SelectLanguage(int value)
        {
            language=Mathf.Clamp(value,0,2);
            if(languageDropdownMenu!=null)languageDropdownMenu.SetActive(false);
            Refresh();
        }
        private GameObject BuildLanguageDropdownMenu(Transform parent)
        {
            var menu=Rect("LanguageDropdownMenu",parent,MenuTheme.Panel).gameObject;
            var layout=Vertical(menu.transform,4);layout.padding=new RectOffset(8,8,6,6);
            var le=menu.AddComponent<LayoutElement>();le.minHeight=buttonHeight*3+28;le.preferredHeight=buttonHeight*3+28;
            string[] labels={"日语","中文","英语（词）"};
            for(int i=0;i<labels.Length;i++){int index=i;Button(menu.transform,labels[i],()=>SelectLanguage(index));}
            menu.SetActive(false);return menu;
        }
        private double NextLyricTime(AssistLyric line)=>owner.State.lyrics.SkipWhile(l=>l!=line).Skip(1).Select(l=>l.seconds).DefaultIfEmpty(Math.Min(line.seconds+5,owner.Transport.Duration)).First();
        public void Dispose()
        {
            AudioAssistSyllableDialog.CloseFor(owner);
            if(owner!=null)owner.Changed-=Refresh;
            if(root!=null)Destroy(root.gameObject);if(dock!=null)dock.Dispose();if(leftEntry!=null)Destroy(leftEntry.gameObject);
            foreach(var tool in leftTools)if(tool!=null)tool.SetActive(true);
            if(quantize!=null)quantize.anchoredPosition=quantizePosition;
            if(owner?.View!=null)owner.View.SetAudioAssistLayout(0,0);Destroy(this);
        }
        private void OnDestroy(){MenuTheme.Changed-=Refresh;if(owner!=null)owner.Changed-=Refresh;if(ownsFont&&font!=null){if(Application.isPlaying)Destroy(font);else DestroyImmediate(font);}}
        private static bool TryNumber(string text,out double value)=>double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!double.IsNaN(value)&&!double.IsInfinity(value);
        private static string Format(double t)=>TimeSpan.FromSeconds(Math.Max(0,t)).ToString(@"mm\:ss\.fff",CultureInfo.InvariantCulture);
        private RectTransform Rect(string name,Transform parent,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var image=go.AddComponent<Image>();image.color=color;if(color.a>0){MenuRoundedImage.Set(image,MenuControls.Rounded);}MenuThemeBinding.Capture(go.transform);return (RectTransform)go.transform;
        }
        private void Position(RectTransform rect,Vector2 anchor,Vector2 size){rect.anchorMin=rect.anchorMax=anchor;rect.sizeDelta=size;rect.anchoredPosition=Vector2.zero;}
        private VerticalLayoutGroup Vertical(Transform parent,float spacing)
        {
            var layout=parent.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=spacing;layout.padding=new RectOffset(5,5,8,8);layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;return layout;
        }
        private Transform Container(Transform parent){var rect=Rect("Group",parent,Color.clear);Vertical(rect,8);return rect;}
        private Transform Row(Transform parent)
        {
            var row=Rect("Row",parent,Color.clear);row.gameObject.AddComponent<LayoutElement>().preferredHeight=buttonHeight;
            var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=6;layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=true;return row;
        }
        private TMP_Text Label(Transform parent,string text,float size,float height)
        {
            var go=new GameObject("Label",typeof(RectTransform));go.transform.SetParent(parent,false);
            var label=go.AddComponent<TextMeshProUGUI>();if(font!=null)label.font=font;label.text=text;label.fontSize=Math.Max(size,12/Math.Max(.1f,canvas.scaleFactor));label.color=MenuTheme.Text;MenuThemeBinding.Bind(label,MenuColor.Text);label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;label.alignment=TextAlignmentOptions.MidlineLeft;
            MenuTypography.Bind(label, size >= 28 ? MenuTextRole.Title : MenuTypography.Infer(label));
            labels.Add((label,size));
            var element=go.AddComponent<LayoutElement>();element.minHeight=height;element.flexibleWidth=1;return label;
        }
        private Button Button(Transform parent,string text,Action action)
        {
            var rect=Rect("Assist_"+text,parent,Muted);var element=rect.gameObject.AddComponent<LayoutElement>();element.minWidth=44/Math.Max(.1f,canvas.scaleFactor);element.minHeight=buttonHeight;element.preferredHeight=buttonHeight;element.flexibleWidth=1;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.onClick.AddListener(()=>{if(action!=null)action();});
            var label=Label(rect,text,23,buttonHeight);label.alignment=TextAlignmentOptions.Center;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(5,0);label.rectTransform.offsetMax=new Vector2(-5,0);MenuControls.Style(button);
            // Sit the buttons on the darker Background tone so they stand out from the Raised cards.
            MenuThemeBinding.Bind(button.image,MenuColor.Background);return button;
        }
        private Button ProgressButton(Transform parent,string text,Action action,out Image fill,out TMP_Text label)
        {
            var button=Button(parent,text,action);button.transition=Selectable.Transition.None;
            var mask=button.gameObject.GetComponent<Mask>()??button.gameObject.AddComponent<Mask>();mask.showMaskGraphic=true;
            var fillRect=Rect("Progress",button.transform,Color.clear);
            fillRect.anchorMin=Vector2.zero;fillRect.anchorMax=new Vector2(0,1);fillRect.offsetMin=fillRect.offsetMax=Vector2.zero;
            fillRect.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
            fill=fillRect.GetComponent<Image>();fill.color=Color.Lerp(MenuTheme.ButtonFace,MenuTheme.Accent,.32f);fill.raycastTarget=false;
            label=button.GetComponentInChildren<TMP_Text>(true);label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=23;label.transform.SetAsLastSibling();return button;
        }
        private void Switch(Transform parent,string text,Func<bool> state,Action action){var b=Button(parent,text,action);switches.Add((b,state));}
        private Transform Fold(Transform parent,string title)=>MenuControls.Fold(parent,title,font,buttonHeight,false,"assist."+title);
        private Transform Section(Transform parent,string title,bool visible=false)=>MenuControls.Fold(parent,title,font,buttonHeight,visible,"assist."+title);
        private TMP_InputField Number(Transform parent,string caption,double value,Action<double> changed)
        {
            Label(parent,caption,18,36);var rect=Rect("Input_"+caption,parent,MenuTheme.Raised);MenuThemeBinding.Bind(rect.GetComponent<Image>(),MenuColor.Raised);rect.gameObject.AddComponent<LayoutElement>().preferredHeight=buttonHeight;
            var input=rect.gameObject.AddComponent<TMP_InputField>();var text=Label(rect,value.ToString("0.###",CultureInfo.InvariantCulture),23,buttonHeight);
            MenuTypography.Bind(text,MenuTextRole.Numeric);
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(10,0);text.rectTransform.offsetMax=new Vector2(-10,0);input.targetGraphic=rect.GetComponent<Image>();input.textViewport=rect;input.textComponent=text;input.contentType=TMP_InputField.ContentType.DecimalNumber;input.SetTextWithoutNotify(value.ToString("0.###",CultureInfo.InvariantCulture));
            input.onEndEdit.AddListener(v=>{if(TryNumber(v,out var number))changed(number);else {owner.Status="请输入有效数字";Refresh();}});return input;
        }
        private Slider Slider(Transform parent,float value,Action<float> changed)=>MenuControls.Slider(parent,0,1,value,changed,buttonHeight);
        [Serializable] private sealed class AssistLyricPage { public List<AssistLyric> lines; }
        private void Clear(Transform transform){labels.RemoveAll(p=>p.text==null||p.text.transform.IsChildOf(transform));foreach(Transform child in transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
    }
}
