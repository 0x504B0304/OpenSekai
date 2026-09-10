using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Sekai.Live;
using Sekai.MusicScoreMaker.Ingame.AudioAssist;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Presenters;
using Sekai.MusicScoreMaker.Ingame.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Sekai;
using Object=UnityEngine.Object;

// Offline prefab render: does not open gameplay, import a package, or save a chart.
public static class AudioAssistUiValidation
{
    private static bool vocalReview;
    private static bool editSyllableReview;
    public static void CaptureSyllableEditorReview()
    {
        editSyllableReview=true;
        try{CaptureVocalReview();}
        finally{editSyllableReview=false;}
    }
    public static void CaptureVocalReview()
    {
        vocalReview=true;
        try
        {
            foreach(bool dark in new[]{true,false})
            {
                Sekai.MenuUI.MenuTheme.SetDark(dark,false);
                Capture();
            }
        }
        finally{vocalReview=false;}
    }
    public static void CaptureLight()
    {
        Sekai.MenuUI.MenuTheme.SetDark(false,false);
        Capture();
    }
    public static void CaptureAndBuild()
    {
        Capture();
        Sekai.EditorTools.OpenSekaiAssetBundleBuildPipeline.BuildWindowsPlayer();
    }
    public static void Capture()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1024,768),new Vector2Int(844,390),new Vector2Int(3840,2160)})Capture(size);
    }
    private static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
    private static void Set(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
    private static void Capture(Vector2Int size)
    {
        var sceneRoot=new GameObject("AssistValidation");
        sceneRoot.AddComponent<AudioAssistValidationDispatcher>().SetupInstance();
        var camera=new GameObject("Camera").AddComponent<Camera>();camera.transform.SetParent(sceneRoot.transform);camera.orthographic=true;
        camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(29,27,49,255);
        var rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;
        var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.transform.SetParent(sceneRoot.transform);
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=2;canvas.scaleFactor=size.y/1080f;
        canvas.referencePixelsPerUnit=1;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/screen/prefabs/ScreenLayerMusicScoreMaker.prefab");
        var screen=Object.Instantiate(prefab,canvas.transform,false);var view=screen.GetComponentInChildren<MusicScoreMakerView>(true);
        foreach(var window in screen.GetComponentsInChildren<SubWindowSlideAnimationController>(true))window.Setup();
        Sekai.MenuUI.MenuTypography.BindTree(screen.transform);
        Canvas.ForceUpdateCanvases();
        var model=new MusicScoreMakerModel(null){MusicScoreMakerData=new MusicScoreMakerData(),MasterMusicSec=30};
        model.MusicScoreMakerData.MusicScoreEventDataList.Add(new MusicScoreEventData{id=100,ticks=0,eventType=MusicScoreEventType.BPM,changeValue=150});
        var presenter=MusicScoreMakerPresenter.Create(model,view,CancellationToken.None,null).GetAwaiter().GetResult();
        Call(presenter,"SetupEventDispatcher");
        var controller=view.gameObject.AddComponent<AudioAssistController>();controller.enabled=false;
        var transport=view.gameObject.AddComponent<AudioAssistTransport>();transport.enabled=false;transport.State=controller.State;transport.Duration=10;
        Set(controller,"Presenter",presenter);Set(controller,"View",view);Set(controller,"Transport",transport);presenter.AudioAssist=controller;
        foreach(string key in AudioAssistController.Keys)controller.State.stems.Add(new AssistStem{key=key,enabled=true});
        var samples=new float[441000];for(int i=0;i<samples.Length;i++)samples[i]=(float)(Math.Sin(i*.08)*(.2+.45*Math.Pow(Math.Max(0,Math.Sin(i/44100.0*18)),8)));
        // Valid silent banks allow click-audition to exercise the real transport
        // without emitting audio or indexing an incomplete capture fixture.
        foreach(string key in AudioAssistController.Keys)transport.Tracks[key]=new AudioAssistTransport.Track{sources=new[]{view.gameObject.AddComponent<AudioSource>(),view.gameObject.AddComponent<AudioSource>()},state=controller.State.stems.First(s=>s.key==key),analysis=AudioAssistAlgorithms.Analyze(samples,1,44100,CancellationToken.None)};
        foreach(string key in AudioAssistController.Keys)if(!controller.Visible.Contains(key))controller.Visible.Add(key);controller.Status="采音辅助 · 六轨布局验证";
        if(vocalReview)
        {
            controller.Visible.Clear();controller.Visible.Add("vocals");controller.Visible.Add("original");
            controller.State.lyricOffset=.125;
            controller.State.lyrics.Add(new AssistLyric{seconds=.2,text="采音辅助验证",syllables=new System.Collections.Generic.List<AssistDraft>{
                new AssistDraft{seconds=.2,end=.45,label="采"},new AssistDraft{seconds=.55,end=.68,label="音",confidence=.2f},
                new AssistDraft{seconds=.85,end=1.2,label="辅"},new AssistDraft{seconds=1.3,end=1.6,label="助"},
                new AssistDraft{seconds=1.8,end=2.5,label="验"},new AssistDraft{seconds=2.7,end=3.3,label="证"}}});
            Set(controller,"AnalysisStatus",AssistAnalysisStatus.Running);Set(controller,"AnalysisProgress",.72f);
            Set(controller,"AnalysisStage","对齐乐句 18/30");Set(controller,"Busy",true);
            controller.Status="音节时长 / 分析进度 · 离线界面验证";
        }
        var panel=view.gameObject.AddComponent<AudioAssistPanel>();Set(controller,"Panel",panel);
        var tools=(GameObject)typeof(MusicScoreMakerView).GetField("_toolWindowObject",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
        Call(view,"InitializeToolWindowViews");Call(view,"SetScoreDisplayScale");
        panel.Build(controller,tools.transform);
        if(vocalReview)
        {
            var button=canvas.GetComponentsInChildren<Button>(true).First(b=>b.name=="Assist_分轨并对齐歌词");
            button.transform.parent.parent.Find("Header").GetComponent<Toggle>().SetIsOnWithoutNotify(true);
            button.transform.parent.gameObject.SetActive(true);
        }
        Sekai.MenuUI.MenuTypography.BindTree(canvas.transform);
        view.GetComponentInChildren<LaneLinePreview>(true).UpdateView(1,0);
        panel.ToggleTools(true);panel.Toggle(false);
        Vector2 originalSize=view.RectTransform.rect.size,originalPosition=view.RectTransform.anchoredPosition;
        try
        {
            for(int n=0;n<3;n++){panel.Toggle(true);panel.ToggleTools(false);panel.Toggle(false);panel.ToggleTools(true);}
            if(Vector2.Distance(view.RectTransform.rect.size,originalSize)>.01||Vector2.Distance(view.RectTransform.anchoredPosition,originalPosition)>.01)throw new Exception("Fold changed original editor geometry.");
            foreach(bool expanded in new[]{false,true})
            {
                panel.ToggleTools(true);panel.Toggle(expanded);Canvas.ForceUpdateCanvases();
                var baselineCorners=new Vector3[4];view.NotesViewRectTransform.GetWorldCorners(baselineCorners);
                float baselineWidth=view.NotesViewRectTransform.rect.width;
                foreach(bool toolsExpanded in new[]{true,false})
                {
                panel.ToggleTools(toolsExpanded);
                panel.Toggle(expanded);Canvas.ForceUpdateCanvases();Call(panel,"LateUpdate");
                if(vocalReview&&expanded)
                {
                    var button=canvas.GetComponentsInChildren<Button>(true).First(b=>b.name=="Assist_分轨并对齐歌词");
                    button.transform.parent.gameObject.SetActive(true);
                    Canvas.ForceUpdateCanvases();Canvas.ForceUpdateCanvases();Call(panel,"LateUpdate");
                    var scroll=button.GetComponentInParent<ScrollRect>();
                    var br=(RectTransform)button.transform;var bc=new Vector3[4];br.GetWorldCorners(bc);
                    float target=-scroll.content.InverseTransformPoint(bc[1]).y-90;
                    scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(target,0,Math.Max(0,scroll.content.rect.height-scroll.viewport.rect.height)));
                    foreach(var overlay in canvas.GetComponentsInChildren<AudioAssistLyricOverlay>(true))overlay.RefreshLayout();
                }
                view.GetComponentInChildren<LaneLinePreview>(true).UpdateView(1,0);
                var wave=canvas.GetComponentInChildren<AudioAssistWaveform>(true);foreach(var renderedWave in canvas.GetComponentsInChildren<AudioAssistWaveform>(true))Call(renderedWave,"Update");
                Canvas.ForceUpdateCanvases();
                Sekai.MenuUI.MenuTypography.BindTree(canvas.transform);
                foreach(var label in canvas.GetComponentsInChildren<TMP_Text>(true))
                {
                    if(label.font==null||label.font.material==null||label.fontSharedMaterial==null)
                        throw new Exception("Missing font material: "+label.transform.parent.name+"/"+label.name+" font="+label.font+" menu="+Sekai.MenuUI.MenuTypography.IsMenu(label));
                    label.ForceMeshUpdate(true,true);
                }
                typeof(TMP_FontAsset).GetMethod("UpdateFontAssetsInUpdateQueue",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                if(editSyllableReview&&expanded)
                {
                    Set(controller,"Busy",false);
                    var point=controller.State.lyrics[0].syllables[0];
                    VerifySyllableGestures(controller,canvas,camera,point);
                    VerifyInlineEditing(controller,canvas,camera,point,size,toolsExpanded,rt);
                    Canvas.ForceUpdateCanvases();
                }
                Canvas.ForceUpdateCanvases();camera.Render();
                RenderTexture.active=rt;var png=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,size.x,size.y),0,0);png.Apply();RenderTexture.active=null;
                Directory.CreateDirectory("Logs/AudioAssistValidation");File.WriteAllBytes($"Logs/AudioAssistValidation/{(editSyllableReview?"syllable-editor":vocalReview?"vocal":"ui")}-{(Sekai.MenuUI.MenuTheme.IsDark?"dark":"light")}-{size.x}x{size.y}-{(expanded?"open":"closed")}-tools-{(toolsExpanded?"open":"closed")}.png",png.EncodeToPNG());Object.DestroyImmediate(png);
                if(editSyllableReview)AudioAssistSyllableDialog.CloseFor(controller);
                var noteCorners=new Vector3[4];view.NotesViewRectTransform.GetWorldCorners(noteCorners);
                if(!toolsExpanded)
                {
                    if(view.NotesViewRectTransform.rect.width<baselineWidth-1)throw new Exception("Collapsed tools did not release track width at "+size);
                    Debug.Log($"Tool fold {size}, assist={expanded}: lane width {baselineWidth:F1} -> {view.NotesViewRectTransform.rect.width:F1}");
                }
                if(expanded)
                {
                    var mesh=wave.canvasRenderer.GetMesh();
                    {
                        if(mesh.vertexCount<8)throw new Exception("Waveform did not render.");
                        var vertices=mesh.vertices;
                        float stripPixels=Math.Abs(camera.WorldToScreenPoint(wave.rectTransform.TransformPoint(vertices[5])).y-camera.WorldToScreenPoint(wave.rectTransform.TransformPoint(vertices[4])).y);
                        if(stripPixels<.9f||stripPixels>1.05f)throw new Exception("Waveform strip must resolve one physical pixel: "+stripPixels+" at "+size);
                        Debug.Log($"Waveform resolution {size}: {stripPixels:F3} pixels/strip, {mesh.vertexCount} vertices");
                    }
                    var rect=(RectTransform)typeof(AudioAssistPanel).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
                    var corners=new Vector3[4];rect.GetWorldCorners(corners);var left=camera.WorldToScreenPoint(corners[0]);var right=camera.WorldToScreenPoint(corners[2]);
                    Debug.Log($"Assist layout {size}: panel={left}..{right}, notes={view.NotesViewRectTransform.rect}, canvas={canvas.GetComponent<RectTransform>().rect}");
                    if(left.x<0||right.x>size.x+1||left.y< -1||right.y>size.y+1)throw new Exception("Assist panel outside viewport at "+size);
                }
                }
            }
            panel.ToggleTools(true);panel.Toggle(false);
            if(Vector2.Distance(view.RectTransform.rect.size,originalSize)>.01||Vector2.Distance(view.RectTransform.anchoredPosition,originalPosition)>.01)throw new Exception("Tool fold did not restore editor geometry.");
        }
        finally{transport.Tracks.Clear();Object.DestroyImmediate(sceneRoot);rt.Release();Object.DestroyImmediate(rt);}
        Debug.Log("Assist UI capture passed: "+size);
    }
    private static void VerifySyllableGestures(AudioAssistController controller,Canvas canvas,Camera camera,AssistDraft point)
    {
        var wave=canvas.GetComponentsInChildren<AudioAssistWaveform>().First(w=>w.StemKey=="vocals");
        wave.Lyrics.RefreshLayout();
        Vector3 local=new Vector3(wave.rectTransform.rect.xMax-wave.Lyrics.LabelWidth*.5f,
            wave.PositionY((point.seconds+point.end)*.5+controller.State.lyricOffset));
        var position=RectTransformUtility.WorldToScreenPoint(camera,wave.rectTransform.TransformPoint(local));
        var press=new PointerEventData(null){pointerId=-2,button=PointerEventData.InputButton.Right,position=position,
            pointerPressRaycast=new RaycastResult{module=canvas.GetComponent<GraphicRaycaster>()}};
        wave.OnPointerDown(press);
        if(!AudioAssistSyllableDialog.IsOpen)throw new Exception("Right-click did not open the syllable editor.");
        // Dispatch to the menu's transparent blocker, the live right-click
        // recipient. Calling the waveform would bypass the interception bug.
        var menuEditor=canvas.GetComponentInChildren<AudioAssistSyllableDialog>();
        foreach(var next in new[]{controller.State.lyrics[0].syllables[1],point})
        {
            Canvas.ForceUpdateCanvases();wave.Lyrics.RefreshLayout();
            if(!wave.Lyrics.TryGetTagRect(next,out var tag))throw new Exception("Switch target is not visible.");
            press.position=RectTransformUtility.WorldToScreenPoint(camera,tag.TransformPoint(tag.rect.center));
            if(!ExecuteEvents.Execute(menuEditor.gameObject,press,ExecuteEvents.pointerDownHandler))
                throw new Exception("Menu surface did not handle right-click.");
            if(controller.SelectedSyllable!=next||!AudioAssistSyllableDialog.IsOpen
                ||canvas.GetComponentsInChildren<AudioAssistSyllableDialog>().Length!=1||controller.Transport.Playing)
                throw new Exception("Right-click must retarget the existing menu without deselecting or auditioning.");
        }
        press.position=position;
        wave.OnPointerUp(press);AudioAssistSyllableDialog.CloseFor(controller);
        press.pointerId=11;press.button=PointerEventData.InputButton.Left;
        wave.OnPointerDown(press);press.position+=new Vector2(20,0);wave.OnDrag(press);
        Call(wave,"UpdateLongPress",Time.unscaledTimeAsDouble+1,true,1);
        if(AudioAssistSyllableDialog.IsOpen)throw new Exception("Dragging opened the long-press editor.");
        wave.OnPointerUp(press);press.position=position;
        wave.OnPointerDown(press);
        // Batch-mode has no focused native window; inject focus and elapsed time
        // into the same recognizer used by the live pointer update.
        Call(wave,"UpdateLongPress",Time.unscaledTimeAsDouble+1,true,1);
        if(!AudioAssistSyllableDialog.IsOpen)throw new Exception("Long press did not open the syllable editor.");
        wave.OnPointerUp(press);
        if(!AudioAssistSyllableDialog.IsOpen||controller.Transport.Playing)throw new Exception("Releasing a long press triggered audition or dismissed the editor.");
        Debug.Log("Syllable gestures passed: right click, direct switching through menu surface events, drag cancellation, touch long press and release suppression.");
    }
    private static void VerifyInlineEditing(AudioAssistController controller,Canvas canvas,Camera camera,AssistDraft point,Vector2Int size,bool tools,RenderTexture rt)
    {
        var wave=canvas.GetComponentsInChildren<AudioAssistWaveform>().First(w=>w.StemKey=="vocals");
        var editor=canvas.GetComponentInChildren<AudioAssistSyllableDialog>();
        var menu=editor.transform.Find("SyllableContextMenu");
        var captions=menu.GetComponentsInChildren<Button>().Select(b=>b.GetComponentInChildren<TMP_Text>().text).ToArray();
        if(!captions.SequenceEqual(new[]{"调整时间","编辑内容",Sekai.Localization.LocalizationManager.Get("common.delete")}))throw new Exception("Context menu must contain exactly three edit actions.");
        void CaptureMode(string name)
        {
            Canvas.ForceUpdateCanvases();wave.Lyrics.RefreshLayout();editor.Layout();Canvas.ForceUpdateCanvases();
            // The live localization scan runs every 750 ms, after the initial
            // menu frame. Exercise those delayed refreshes before checking ink.
            if(name=="menu")
            {
                var label=menu.GetComponentsInChildren<Button>().Last().GetComponentInChildren<TMP_Text>();
                for(int refresh=0;refresh<3;refresh++)Sekai.Localization.RuntimeLocalizationBootstrap.TryBind(label);
                label.ForceMeshUpdate(true,true);
                if(!label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c=>c.isVisible))
                    throw new Exception("Delete caption became invisible after runtime localization refresh.");
            }
            foreach(var text in editor.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate(true,true);
            typeof(TMP_FontAsset).GetMethod("UpdateFontAssetsInUpdateQueue",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            foreach(var button in editor.GetComponentsInChildren<Button>())
            {
                if(button.transform==editor.transform)continue;
                var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                if(b.x-a.x<43||b.y-a.y<43||a.x< -1||a.y< -1||b.x>size.x+1||b.y>size.y+1)
                    throw new Exception("Inline action is too small or outside viewport: "+button.name+" "+a+" "+b);
            }
            camera.Render();RenderTexture.active=rt;
            var png=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,size.x,size.y),0,0);png.Apply();RenderTexture.active=null;
            Directory.CreateDirectory("Logs/AudioAssistValidation");
            File.WriteAllBytes($"Logs/AudioAssistValidation/inline-{name}-{(Sekai.MenuUI.MenuTheme.IsDark?"dark":"light")}-{size.x}x{size.y}-tools-{tools}.png",png.EncodeToPNG());Object.DestroyImmediate(png);
        }
        CaptureMode("menu");
        editor.SetMode(AudioAssistSyllableDialog.EditMode.Time);CaptureMode("time");
        var other=controller.State.lyrics.SelectMany(l=>l.syllables).First(p=>p!=point);
        if(!AudioAssistSyllableDialog.IsDimming(controller,other)||AudioAssistSyllableDialog.IsDimming(controller,point))throw new Exception("Time mode must dim only the other tags.");
        double beforeStart=point.seconds,beforeEnd=point.end;
        var end=editor.GetComponentsInChildren<AudioAssistSyllableEdgeHandle>().First(h=>h.name=="SyllableEndEdge");
        var press=new PointerEventData(null){pointerId=21,button=PointerEventData.InputButton.Left,
            position=RectTransformUtility.WorldToScreenPoint(camera,end.Rect.TransformPoint(end.Rect.rect.center)),
            pointerPressRaycast=new RaycastResult{module=canvas.GetComponent<GraphicRaycaster>()}};
        end.OnPointerDown(press);
        // Two fingers cannot take ownership of the opposite edge mid-drag.
        if(editor.BeginEdge(false,press))throw new Exception("A second edge acquired an active drag.");
        press.position+=new Vector2(0,24);end.OnDrag(press);
        if(point.end!=beforeEnd)throw new Exception("Dragging must preview before release.");
        end.OnPointerUp(press);
        if(point.end<=beforeEnd||point.seconds!=beforeStart)throw new Exception("End edge did not change only the end timestamp.");
        MusicScoreMakerEventDispatcher.Instance.Undo();
        wave.Lyrics.RefreshLayout();
        if(point.end!=beforeEnd||!AudioAssistSyllableDialog.Preview(point,out _,out var preview)||Math.Abs(preview-beforeEnd-controller.State.lyricOffset)>.00001)
            throw new Exception("One undo must restore the entire edge drag and its preview.");
        editor.SetMode(AudioAssistSyllableDialog.EditMode.Text);CaptureMode("text");
        var field=editor.GetComponentInChildren<TMP_InputField>();string original=point.label;
        field.text="取消此内容";field.onEndEdit.Invoke(field.text);
        if(point.label!=original)throw new Exception("Deselecting the text field committed before Cancel.");
        editor.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<TMP_Text>()?.text=="×").onClick.Invoke();
        if(point.label!=original)throw new Exception("Cancel changed the label.");
        wave.Lyrics.Edit(point,beforeEnd);editor=canvas.GetComponentInChildren<AudioAssistSyllableDialog>();editor.SetMode(AudioAssistSyllableDialog.EditMode.Text);
        field=editor.GetComponentInChildren<TMP_InputField>();field.text="就地编辑";
        if(AudioAssistSyllableDialog.TryDeleteSelection())throw new Exception("Delete must remain inside the text field.");
        field.onSubmit.Invoke(field.text);
        if(point.label!="就地编辑")throw new Exception("Inline submit did not save text.");
        MusicScoreMakerEventDispatcher.Instance.Undo();
        if(point.label!=original)throw new Exception("Inline text undo did not restore text.");
        wave.Lyrics.RefreshLayout();
        var local=new Vector2(wave.rectTransform.rect.xMax-wave.Lyrics.LabelWidth*.5f,wave.PositionY((beforeStart+beforeEnd)*.5+controller.State.lyricOffset));
        if(!wave.Lyrics.TrySelect(local)||!AudioAssistSyllableDialog.TryDeleteSelection())throw new Exception("Click selection did not support label Delete.");
        if(controller.State.lyrics.Any(l=>l.syllables.Contains(point)))throw new Exception("Selected label was not deleted.");
        MusicScoreMakerEventDispatcher.Instance.Undo();
        if(!controller.State.lyrics.Any(l=>l.syllables.Contains(point)))throw new Exception("Delete undo did not restore label.");
        wave.Lyrics.RefreshLayout();wave.Lyrics.Edit(point,beforeEnd);
        Debug.Log("Inline editing passed: menu, edge preview/commit/undo, pointer ownership, text cancel/submit/undo, selection Delete/undo.");
    }
}
public sealed class AudioAssistValidationDispatcher : MusicScoreMakerEventDispatcher
{
    public override bool IsDontDestroy()=>false;
}
