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
using TMPro;
using Sekai;
using Object=UnityEngine.Object;

// Offline prefab render: does not open gameplay, import a package, or save a chart.
public static class AudioAssistUiValidation
{
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
        var fallback=TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular"));
        TMP_Settings.fallbackFontAssets.Add(fallback);
        // Offline captures use an owned CJK font; the prefab's shared fallback materials
        // can belong to the previous temporary scene in a multi-page capture run.
        foreach(var label in screen.GetComponentsInChildren<TMP_Text>(true)){label.font=fallback;label.fontSharedMaterial=fallback.material;}
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
        foreach(string key in AudioAssistController.Keys)transport.Tracks[key]=new AudioAssistTransport.Track{sources=Array.Empty<AudioSource>(),state=controller.State.stems.First(s=>s.key==key),analysis=AudioAssistAlgorithms.Analyze(samples,1,44100,CancellationToken.None)};
        foreach(string key in AudioAssistController.Keys)if(!controller.Visible.Contains(key))controller.Visible.Add(key);controller.Status="采音辅助 · 六轨布局验证";
        var panel=view.gameObject.AddComponent<AudioAssistPanel>();Set(controller,"Panel",panel);
        var tools=(GameObject)typeof(MusicScoreMakerView).GetField("_toolWindowObject",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
        Call(view,"InitializeToolWindowViews");Call(view,"SetScoreDisplayScale");
        panel.Build(controller,tools.transform);
        foreach(var label in canvas.GetComponentsInChildren<TMP_Text>(true)){label.font=fallback;label.fontSharedMaterial=fallback.material;}
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
                view.GetComponentInChildren<LaneLinePreview>(true).UpdateView(1,0);
                var wave=canvas.GetComponentInChildren<AudioAssistWaveform>(true);foreach(var renderedWave in canvas.GetComponentsInChildren<AudioAssistWaveform>(true))Call(renderedWave,"Update");
                Canvas.ForceUpdateCanvases();
                foreach(var label in canvas.GetComponentsInChildren<TMP_Text>(true))label.ForceMeshUpdate(true,true);
                typeof(TMP_FontAsset).GetMethod("UpdateFontAssetsInUpdateQueue",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                Canvas.ForceUpdateCanvases();camera.Render();
                RenderTexture.active=rt;var png=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,size.x,size.y),0,0);png.Apply();RenderTexture.active=null;
                Directory.CreateDirectory("Logs/AudioAssistValidation");File.WriteAllBytes($"Logs/AudioAssistValidation/ui-{(Sekai.MenuUI.MenuTheme.IsDark?"dark":"light")}-{size.x}x{size.y}-{(expanded?"open":"closed")}-tools-{(toolsExpanded?"open":"closed")}.png",png.EncodeToPNG());Object.DestroyImmediate(png);
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
        finally{transport.Tracks.Clear();Object.DestroyImmediate(sceneRoot);rt.Release();Object.DestroyImmediate(rt);TMP_Settings.fallbackFontAssets.Remove(fallback);Object.DestroyImmediate(fallback);}
        Debug.Log("Assist UI capture passed: "+size);
    }
}
public sealed class AudioAssistValidationDispatcher : MusicScoreMakerEventDispatcher
{
    public override bool IsDontDestroy()=>false;
}
