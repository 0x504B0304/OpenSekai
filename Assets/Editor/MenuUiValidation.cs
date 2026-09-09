using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Sekai.CustomMusicScoreManager;
using Sekai.MusicScoreMaker.Common;
using Sekai.MenuUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Renders menu instances in an empty offline scene. Never imports songs or starts Live.
public static class MenuUiValidation
{
    public static void CaptureReview()
    {
        Capture();
        MenuTheme.SetDark(true,false);AudioAssistUiValidation.Capture();
        MenuTheme.SetDark(false,false);AudioAssistUiValidation.Capture();
    }
    public static void Capture()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Directory.CreateDirectory("Logs/MenuUI");
        foreach(bool dark in new[]{true,false}){MenuTheme.SetDark(dark,false);foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1024,768),new Vector2Int(844,390),new Vector2Int(3840,2160)})CaptureMenu(size);}

        Debug.Log("MENU UI VALIDATION PASSED");
    }
    private static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
    private static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    private static void CaptureMenu(Vector2Int size)
    {
        var root=new GameObject("MenuValidation");
        var camera=new GameObject("Camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=MenuTheme.Background;
        var rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;
        var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.transform.SetParent(root.transform);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=2;canvas.scaleFactor=size.y/1080f;
        canvas.referencePixelsPerUnit=1; // Match MusicScoreMaker.unity, not Unity's default 100.
        var go=new GameObject("Library",typeof(RectTransform));go.transform.SetParent(canvas.transform,false);MenuControls.Stretch((RectTransform)go.transform);
        var manager=go.AddComponent<ScreenLayerCustomMusicScoreManager>();
        if(Field<RectTransform>(manager,"_menuRoot")==null)Call(manager,"BuildView");
        var font=TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular"));
        foreach(var text in go.GetComponentsInChildren<TMP_Text>(true))text.font=font;
        var manifest=new CustomMusicScoreManifest{id="menu-validation",title="冲破穹顶",scoreTitle="EXPERT",userName="OpenSekai",musicDifficultyType="expert",playLevel=26,composer="塞壬唱片-MSR · PMP",fillerSec=9,secForMusicScoreMaker=180};manifest.Normalize();
        var item=new CustomMusicScoreManagerItem(new CustomMusicScoreEntry(Path.GetFullPath("Logs/MenuUI/Fixture"),manifest),DateTime.UtcNow,true,true,true,false);
        var row=Call(manager,"CreateRow",Field<RectTransform>(manager,"_listContent"),item);
        var rows=(System.Collections.IList)Field<object>(manager,"_rows");rows.Add(row);
        Field<TMP_Text>(manager,"_emptyText").gameObject.SetActive(false);
        Call(manager,"UpdateSelection",item);
        try
        {
            foreach(string screen in new[]{"library-basic","library-media","library-chart","settings-sound","settings-play","settings-display","settings-editor","settings-data"})
            {
                bool settingsScreen=screen.StartsWith("settings-");
                Field<RectTransform>(manager,"_settingsOverlay").gameObject.SetActive(settingsScreen);
                if(settingsScreen)Call(manager,"SelectMenuSettingsCategory",Array.IndexOf(new[]{"settings-sound","settings-play","settings-display","settings-editor","settings-data"},screen));
                else
                {
                    int category=Array.IndexOf(new[]{"library-basic","library-media","library-chart"},screen);
                    Field<RectTransform>(manager,"_menuFormTabs").GetComponentInChildren<MenuSegments>().SetWithoutNotify(category);
                    Call(manager,"SelectMenuFieldCategory",category);
                }
                for(int n=0;n<4;n++){Canvas.ForceUpdateCanvases();Call(manager,"UpdateMenuLayout");Call(manager,"UpdateManifestFieldLayout");}
                foreach(var label in go.GetComponentsInChildren<TMP_Text>(true))label.ForceMeshUpdate(true,true);
                typeof(TMP_FontAsset).GetMethod("UpdateFontAssetsInUpdateQueue",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                Canvas.ForceUpdateCanvases();
                foreach(var rounded in go.GetComponentsInChildren<MenuRoundedImage>(true))
                {
                    rounded.Refresh();var image=rounded.GetComponent<Image>();
                    if(Mathf.Abs(image.pixelsPerUnit*image.pixelsPerUnitMultiplier-1)>.001f)throw new Exception("Menu corner scale mismatch: "+image.name);
                }
                if(go.transform.Find("TopBar/Toolbar/MenuTheme")!=null)throw new Exception("Theme control must only appear in settings");
                File.WriteAllLines($"Logs/MenuUI/geometry-{screen}-{size.x}.txt",go.GetComponentsInChildren<TMP_Text>().Select(t=>$"{t.transform.parent.name}/{t.name}: {t.text} | rect={t.rectTransform.rect} pos={t.transform.position} color={t.color} verts={t.textInfo.characterCount}"));
                camera.Render();RenderTexture.active=rt;
                var png=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,size.x,size.y),0,0);png.Apply();RenderTexture.active=null;
                File.WriteAllBytes($"Logs/MenuUI/{screen}-{(MenuTheme.IsDark?"dark":"light")}-{size.x}x{size.y}.png",png.EncodeToPNG());Object.DestroyImmediate(png);
                var panel=Field<RectTransform>(manager,settingsScreen?"_menuSettingsDialog":"_detailPanel");var corners=new Vector3[4];panel.GetWorldCorners(corners);
                var lo=camera.WorldToScreenPoint(corners[0]);var hi=camera.WorldToScreenPoint(corners[2]);
                if(lo.x< -1||lo.y< -1||hi.x>size.x+1||hi.y>size.y+1)throw new Exception("Menu outside viewport: "+screen+" "+size);
                if(!settingsScreen&&Field<ScrollRect>(manager,"_manifestFormScroll").viewport.rect.height*canvas.scaleFactor<90)
                    throw new Exception("Library form cannot show a complete field at "+size);
                Debug.Log($"Menu capture {screen} {size}: {lo} .. {hi}");
            }
            Call(manager,"SelectMenuSettingsCategory",3);
            var settings=Field<RectTransform>(manager,"_menuSettingsScroll");
            if(!settings.GetComponentsInChildren<Transform>().Any(t=>t.name=="AutoSaveIntervalSelector"))throw new Exception("Settings category not selectable");
            Call(manager,"SelectMenuFieldCategory",1);
            if(!Field<TMP_InputField>(manager,"_audioInput").gameObject.activeInHierarchy)throw new Exception("Media fields lost");
        }
        finally{Object.DestroyImmediate(root);rt.Release();Object.DestroyImmediate(rt);}
    }
}
