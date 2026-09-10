using System;
using System.Linq;
using Sekai.MenuUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    // Attached to a waveform tag. Only the context menu captures outside clicks.
    public sealed class AudioAssistSyllableDialog : MonoBehaviour, IPointerDownHandler
    {
        public enum EditMode { Selected, Menu, Time, Text }
        private static AudioAssistSyllableDialog active;
        private static int backKeyFrame=-1;
        public static bool IsOpen => active!=null&&active.mode!=EditMode.Selected;
        public static bool IsTimeEditing => active!=null&&active.mode==EditMode.Time;
        public static bool IsDragging => active!=null&&active.dragging;
        private EditMode mode;
        private AudioAssistController owner;
        private AudioAssistLyricOverlay overlay;
        private AudioAssistWaveform wave;
        private AssistDraft point;
        private Canvas canvas;
        private TMP_FontAsset font;
        private RectTransform menu,inputRect,actions;
        private TMP_InputField input;
        private AudioAssistSyllableEdgeHandle startHandle,endHandle;
        private double previewStart,previewEnd,dragStart,dragEnd;
        private float dragPointerY;
        private bool dragging,draggingEnd,closing;
        private int interactionFrame;
        private readonly Vector3[] corners=new Vector3[4];
        private float Unit => Math.Max(1,1/Math.Max(.1f,canvas.scaleFactor));
        public static void Select(AudioAssistController owner,AudioAssistLyricOverlay overlay,AudioAssistWaveform wave,TMP_FontAsset font,AssistDraft point,double end)
        {
            if(active!=null&&active.point==point&&active.mode==EditMode.Selected){active.interactionFrame=Time.frameCount;return;}
            if(active!=null)active.Close();
            var root=MenuControls.Rect("SyllableInlineEditor",wave.canvas.transform);MenuControls.Stretch(root);
            active=root.gameObject.AddComponent<AudioAssistSyllableDialog>();
            active.owner=owner;active.overlay=overlay;active.wave=wave;active.canvas=wave.canvas;active.font=font;active.point=point;
            active.previewStart=point.seconds+owner.State.lyricOffset;active.previewEnd=end+owner.State.lyricOffset;
            active.interactionFrame=Time.frameCount;
        }
        public static void Open(AudioAssistController owner,AudioAssistLyricOverlay overlay,AudioAssistWaveform wave,TMP_FontAsset font,AssistDraft point,double end)
        {
            if(owner.Busy)return;
            Select(owner,overlay,wave,font,point,end);
            owner.Presenter.PauseAssist();owner.FocusSyllable(point,end);
            active.SetMode(EditMode.Menu);
        }
        public static void CloseFor(AudioAssistController owner){if(active!=null&&active.owner==owner)active.Close();}
        public static bool TryHandleBackKey()
        {
            // TMP may process Escape before ScreenManager in the same frame.
            if(backKeyFrame==Time.frameCount)return true;
            if(active==null)return false;
            bool consumed=IsOpen;active.Close();
            if(consumed)backKeyFrame=Time.frameCount;
            return consumed;
        }
        public static bool TryDeleteSelection()
        {
            if(active==null||active.mode==EditMode.Text||!active.wave.isActiveAndEnabled)return false;
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected!=null&&(selected.GetComponentInParent<TMP_InputField>()!=null||selected.GetComponentInParent<InputField>()!=null))return false;
            active.Delete();return true;
        }
        public static bool IsDimming(AudioAssistController owner,AssistDraft point)=>active!=null&&active.owner==owner&&active.mode==EditMode.Time&&active.point!=point;
        public static bool Preview(AssistDraft point,out double start,out double end)
        {
            start=end=0;
            if(active==null||active.point!=point||active.mode!=EditMode.Time)return false;
            if(!active.dragging)
            {active.previewStart=point.seconds+active.owner.State.lyricOffset;active.previewEnd=active.owner.SyllableEnd(point)+active.owner.State.lyricOffset;}
            start=active.previewStart;end=active.previewEnd;return true;
        }
        private void ClearControls()
        {
            // Detach before destroying so input deselection cannot commit a mode change.
            if(input!=null){input.onSubmit.RemoveAllListeners();input.onEndEdit.RemoveAllListeners();}
            var children=new Transform[transform.childCount];for(int i=0;i<children.Length;i++)children[i]=transform.GetChild(i);
            foreach(var child in children){child.gameObject.SetActive(false);if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);}
            var blocker=GetComponent<Image>();if(blocker!=null)blocker.raycastTarget=false;
            var outside=GetComponent<Button>();if(outside!=null)outside.enabled=false;
            menu=inputRect=actions=null;input=null;startHandle=endHandle=null;
        }
        private Button SmallButton(Transform parent,string text,Action click,bool danger=false)
        {
            var b=MenuControls.Button(parent,text,click,font,44);b.navigation=new Navigation{mode=Navigation.Mode.None};
            if(danger)MenuControls.Style(b,danger:true);
            if(parent==menu)MenuRoundedImage.SetProportional(b.image,.16f);
            // Register before the periodic localization scan. Its default
            // ellipsis policy can hide the entire line inside a 44 px button.
            if(Sekai.Localization.RuntimeLocalizationBootstrap.TryGetLocalizationKey(text,out var key))
            {
                var label=b.GetComponentInChildren<TMP_Text>();
                var binding=label.gameObject.AddComponent<Sekai.Localization.LocalizedTextBinding>();
                binding.PreserveLayout=true;binding.Key=key;
            }
            return b;
        }
        public void SetMode(EditMode next)
        {
            ClearControls();mode=next;interactionFrame=Time.frameCount;
            if(next==EditMode.Menu)
            {
                var shade=GetComponent<Image>()??gameObject.AddComponent<Image>();shade.color=Color.clear;shade.raycastTarget=true;
                var outside=GetComponent<Button>()??gameObject.AddComponent<Button>();outside.targetGraphic=shade;outside.transition=Selectable.Transition.None;
                outside.enabled=true;
                outside.onClick.RemoveAllListeners();outside.onClick.AddListener(Close);outside.navigation=new Navigation{mode=Navigation.Mode.None};
                menu=MenuControls.Rect("SyllableContextMenu",transform,MenuTheme.Panel);
                var layout=MenuControls.Vertical(menu,4);layout.padding=new RectOffset(6,6,6,6);
                SmallButton(menu,"调整时间",()=>SetMode(EditMode.Time));
                SmallButton(menu,"编辑内容",()=>SetMode(EditMode.Text));
                SmallButton(menu,"删除",Delete,danger:true);
            }
            else if(next==EditMode.Time)
            {
                owner.Presenter.PauseAssist();
                previewStart=point.seconds+owner.State.lyricOffset;previewEnd=owner.SyllableEnd(point)+owner.State.lyricOffset;
                startHandle=CreateHandle(false);endHandle=CreateHandle(true);BuildActions(false);
                owner.Status="拖动音节上下边沿调整时间；每次松手保存，点击空白或 ✓ 结束";owner.Refresh();
            }
            else if(next==EditMode.Text)
            {
                owner.Presenter.PauseAssist();
                inputRect=MenuControls.Rect("SyllableInlineText",transform,MenuTheme.TabField);
                MenuThemeBinding.Bind(inputRect.GetComponent<Image>(),MenuColor.TabField);
                var viewport=MenuControls.Rect("TextViewport",inputRect);MenuControls.Stretch(viewport,4);viewport.gameObject.AddComponent<RectMask2D>();
                var text=MenuControls.Text(viewport,point.label,font,20);MenuControls.Stretch(text.rectTransform);
                MenuThemeBinding.Bind(text,MenuColor.TabFieldInk);text.richText=false;text.textWrappingMode=TextWrappingModes.NoWrap;
                input=inputRect.gameObject.AddComponent<TMP_InputField>();input.textComponent=text;input.textViewport=viewport;
                input.targetGraphic=inputRect.GetComponent<Image>();input.lineType=TMP_InputField.LineType.SingleLine;
                input.SetTextWithoutNotify(point.label);
                input.onSubmit.AddListener(_=>CommitText());
                // Deselecting to click × must not commit first. Outside clicks
                // are handled explicitly in Update, Enter by onSubmit.
                input.onEndEdit.AddListener(_=>{if(!closing&&mode==EditMode.Text&&input.wasCanceled)TryHandleBackKey();});
                BuildActions(true);owner.Status="就地输入标签内容；Enter 或 ✓ 保存，Esc 或 × 取消";owner.Refresh();
            }
            overlay.RefreshLayout();Layout();
            if(next==EditMode.Text&&Application.isPlaying){input.Select();input.ActivateInputField();}
        }
        private void BuildActions(bool cancel)
        {
            actions=MenuControls.Rect("SyllableInlineActions",transform);
            var row=actions.gameObject.AddComponent<HorizontalLayoutGroup>();row.spacing=4;row.childControlWidth=row.childControlHeight=true;row.childForceExpandWidth=true;
            SmallButton(actions,"✓",()=>{if(mode==EditMode.Text)CommitText();else Close();});
            if(cancel)SmallButton(actions,"×",Close);
        }
        private AudioAssistSyllableEdgeHandle CreateHandle(bool end)
        {
            var r=MenuControls.Rect(end?"SyllableEndEdge":"SyllableStartEdge",transform);
            var handle=r.gameObject.AddComponent<AudioAssistSyllableEdgeHandle>();handle.Build(this,end);return handle;
        }
        private void Delete()
        {
            if(owner.DeleteSyllable(point,out var error))Close();else{owner.Status=error;owner.Refresh();}
        }
        public void CommitText()
        {
            if(closing||input==null)return;
            if(string.IsNullOrWhiteSpace(input.text)){owner.Status="标签内容不能为空";owner.Refresh();input.ActivateInputField();return;}
            if(input.text.Trim()==point.label){Close();return;}
            if(owner.EditSyllable(point,input.text,point.seconds+owner.State.lyricOffset,owner.SyllableEnd(point)+owner.State.lyricOffset,out var error))Close();
            else{owner.Status=error;owner.Refresh();}
        }
        public bool BeginEdge(bool end,PointerEventData e)
        {
            if(dragging||mode!=EditMode.Time||owner.Busy)return false;
            dragging=true;draggingEnd=end;dragStart=previewStart;dragEnd=previewEnd;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(wave.rectTransform,e.position,e.pressEventCamera,out var local);dragPointerY=local.y;
            return true;
        }
        public void DragEdge(PointerEventData e)
        {
            if(!dragging)return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(wave.rectTransform,e.position,e.pressEventCamera,out var local);
            double target=wave.SecondsAtY(wave.PositionY(draggingEnd?dragEnd:dragStart)+local.y-dragPointerY);
            if(draggingEnd)previewEnd=Math.Max(previewStart+.001,Math.Min(owner.Transport.Duration,target));
            else previewStart=Math.Max(0,Math.Min(previewEnd-.001,target));
            owner.Status=$"{point.label}  {previewStart:0.000} – {previewEnd:0.000} 秒";owner.Refresh();overlay.RefreshLayout();Layout();
        }
        public void EndEdge()
        {
            if(!dragging)return;
            if(Math.Abs(previewStart-dragStart)<.000001&&Math.Abs(previewEnd-dragEnd)<.000001){dragging=false;return;}
            if(!owner.EditSyllable(point,point.label,previewStart,previewEnd,out var error))
            {previewStart=dragStart;previewEnd=dragEnd;owner.Status=error;owner.Refresh();}
            dragging=false;overlay.RefreshLayout();Layout();
        }
        private Rect TagRect()
        {
            if(!overlay.TryGetTagRect(point,out var tag))return Rect.zero;
            tag.GetWorldCorners(corners);var root=(RectTransform)transform;
            var min=root.InverseTransformPoint(corners[0]);var max=root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        private void Place(RectTransform rect,Vector2 position,Vector2 size,bool clamp)
        {
            var bounds=((RectTransform)transform).rect;float margin=6*Unit;
            if(clamp){position.x=Mathf.Clamp(position.x,bounds.xMin+margin,bounds.xMax-size.x-margin);position.y=Mathf.Clamp(position.y,bounds.yMin+margin,bounds.yMax-size.y-margin);}
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;
        }
        public void Layout()
        {
            if(canvas==null)return;
            Rect tag=TagRect();
            foreach(Transform child in transform)child.gameObject.SetActive(tag.width>0);
            if(tag.width<=0)return;float u=Unit;var bounds=((RectTransform)transform).rect;
            if(menu!=null)
            {
                menu.localScale=Vector3.one*u;float w=176*u,h=152*u;
                float x=tag.xMax+6*u;if(x+w>bounds.xMax-6*u)x=tag.xMin-w-6*u;
                Place(menu,new Vector2(x,tag.center.y-h*.5f),new Vector2(w,h),true);menu.sizeDelta/=u;
            }
            if(mode==EditMode.Time)
            {
                bool shortTag=tag.height<44*u;float width=shortTag?Math.Max(44*u,tag.width/2):Math.Max(44*u,tag.width);
                startHandle.Position(new Vector2(shortTag?tag.center.x-width:tag.center.x-width/2,tag.yMin-22*u),new Vector2(width,44*u),u);
                endHandle.Position(new Vector2(shortTag?tag.center.x:tag.center.x-width/2,tag.yMax-22*u),new Vector2(width,44*u),u);
            }
            if(inputRect!=null)
            {
                // Only the editor gets a minimum input height; the duration
                // surface underneath retains its real start and end positions.
                Place(inputRect,new Vector2(tag.xMin,tag.center.y-Math.Max(tag.height,44*u)/2),new Vector2(Math.Max(tag.width,44*u),Math.Max(tag.height,44*u)),true);
                input.textComponent.fontSize=20*u;
            }
            if(actions!=null)
            {
                float width=(mode==EditMode.Text?92:44)*u;
                float x=tag.xMax+6*u;if(x+width>bounds.xMax-6*u)x=tag.xMin-width-6*u;
                Place(actions,new Vector2(x,tag.center.y-22*u),new Vector2(width,44*u),true);
                foreach(var text in actions.GetComponentsInChildren<TMP_Text>())text.fontSize=22*u;
            }
        }
        private bool Inside(RectTransform rect,Vector2 position)
        {
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            return rect!=null&&RectTransformUtility.RectangleContainsScreenPoint(rect,position,camera);
        }
        public void OnPointerDown(PointerEventData e)
        {
            // The menu's outside-click surface receives this event instead of
            // the waveform. Retarget in place so it does not swallow a right click.
            if(mode!=EditMode.Menu||e.button!=PointerEventData.InputButton.Right||owner.Busy
                ||Inside(menu,e.position)||!Inside((RectTransform)wave.transform.parent,e.position))return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(wave.rectTransform,e.position,e.pressEventCamera,out var local)
                ||!overlay.TryGetPoint(local,out var next,out var end)||next==point)return;
            owner.ClearSyllableSelection(point);point=next;
            previewStart=point.seconds+owner.State.lyricOffset;previewEnd=end+owner.State.lyricOffset;
            owner.FocusSyllable(point,end);SetMode(EditMode.Menu);
        }
        private void Update()
        {
            if(owner==null||!wave.isActiveAndEnabled||!owner.State.lyrics.Any(l=>l.syllables.Contains(point))
                ||mode==EditMode.Selected&&owner.SelectedSyllable!=point){Close();return;}
            if(Time.frameCount==interactionFrame||mode==EditMode.Menu||dragging)return;
            bool down=UnityEngine.Input.GetMouseButtonDown(0);Vector2 position=UnityEngine.Input.mousePosition;
            if(UnityEngine.Input.touchCount>0&&UnityEngine.Input.GetTouch(0).phase==TouchPhase.Began){down=true;position=UnityEngine.Input.GetTouch(0).position;}
            if(!down)return;
            if(Inside(inputRect,position)||overlay.TryGetTagRect(point,out var tag)&&Inside(tag,position))return;
            if(Inside(actions,position)||Inside(startHandle?.Rect,position)||Inside(endHandle?.Rect,position))return;
            if(mode==EditMode.Text)CommitText();else Close();
        }
        private void LateUpdate(){if(!closing)Layout();}
        public void Close()
        {
            if(closing)return;closing=true;dragging=false;
            if(active==this)active=null;
            if(owner!=null){owner.ClearSyllableSelection(point);owner.Refresh();}
            if(overlay!=null)overlay.RefreshLayout();
            gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
        }
        private void OnApplicationFocus(bool focused){if(!focused&&mode==EditMode.Time)Close();}
        private void OnDestroy(){if(active==this)active=null;}
    }

    public sealed class AudioAssistSyllableEdgeHandle : MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        private AudioAssistSyllableDialog editor;
        private bool end;
        private int? pointer;
        private RectTransform line;
        public RectTransform Rect=>(RectTransform)transform;
        public void Build(AudioAssistSyllableDialog editor,bool end)
        {
            this.editor=editor;this.end=end;var hit=gameObject.AddComponent<Image>();hit.color=Color.clear;
            line=MenuControls.Rect("EdgeGrip",transform,MenuTheme.Accent);line.GetComponent<Image>().raycastTarget=false;
            line.anchorMin=new Vector2(0,.5f);line.anchorMax=new Vector2(1,.5f);
        }
        public void Position(Vector2 position,Vector2 size,float unit)
        {
            Rect.anchorMin=Rect.anchorMax=new Vector2(.5f,.5f);Rect.pivot=Vector2.zero;Rect.anchoredPosition=position;Rect.sizeDelta=size;
            line.sizeDelta=new Vector2(0,(pointer.HasValue?6:3)*unit);
        }
        public void OnPointerDown(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left||pointer.HasValue)return;if(editor.BeginEdge(end,e))pointer=e.pointerId;}
        public void OnDrag(PointerEventData e){if(pointer==e.pointerId)editor.DragEdge(e);}
        public void OnPointerUp(PointerEventData e){if(pointer!=e.pointerId)return;pointer=null;editor.EndEdge();}
    }
}
