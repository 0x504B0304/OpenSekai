using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Sekai;
using Sekai.Localization;
using Sekai.MenuUI;
using Sekai.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Diagnostic only: modifies instantiated objects, never saves prefabs or scenes.
public static class DialogTypographyAudit
{
    static readonly List<string> Rows = new List<string>();
    static string PathOf(Transform t, Transform root) => t == root ? t.name : PathOf(t.parent, root) + "/" + t.name;
    static string Clean(string s) => (s ?? "").Replace("\t", " ").Replace("\r", "").Replace("\n", "\\n");
    public static void Run()
    {
        Directory.CreateDirectory("Logs/DialogAudit");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string original = LocalizationManager.CurrentLanguage;
        var setter = typeof(LocalizationManager).GetMethod("SetLanguage", BindingFlags.Static | BindingFlags.NonPublic);
        Rows.Clear();
        Rows.Add("prefab\tlanguage\tsize\tcase\tobject\ttext\tfont\tfontSize\tlineSpacing\twidth\theight\tpreferredHeight\tlines\tminGlyphGap\ttruncated\twindowWidth\twindowHeight\twindowOutside\tlineBaselines");
        try
        {
            var paths = Directory.GetFiles("Assets/Resources/dialog", "*.prefab").Concat(new[] { "Assets/CustomMusicScoreManager/Resources/CustomMusicScoreManager/UI/VideoProcessingProgressDialog.prefab" }).Select(p=>p.Replace('\\','/'));
            foreach (string language in new[] { "zh-Hans", "en", "ja" })
            {
                setter.Invoke(null, new object[] { language, false });
                foreach (string path in paths)
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(3840, 2066), new Vector2Int(844, 390) })
                {
                    Capture(path, language, size, "current");
                    if (path.EndsWith("/LivePauseDialog.prefab") && size.x == 1920)
                        Capture(path, language, size, "spacing-zero");
                    if ((path.EndsWith("/Common1ButtonDialog.prefab") || path.EndsWith("/Common2ButtonDialog.prefab") || path.EndsWith("/SubWindowDialog.prefab")) && size.x == 1920)
                        Capture(path, language, size, "multiline-stress");
                    if (path.EndsWith("/Common2ButtonDialog.prefab"))
                        Capture(path, language, size, "scroll-stress");
                }
            }
        }
        finally
        {
            File.WriteAllLines("Logs/DialogAudit/text-metrics.tsv", Rows);
            setter.Invoke(null, new object[] { original, false });
        }
        Debug.Log("DIALOG AUDIT COMPLETE: " + (Rows.Count - 1) + " text measurements");
    }
    static void Capture(string path, string language, Vector2Int size, string variant)
    {
        var root = new GameObject("DialogAudit");
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.SetParent(root.transform); camera.transform.position = new Vector3(0,0,-10);
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(22,22,37,255);
        var rt = new RenderTexture(size.x, size.y, 24); camera.targetTexture = rt;
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(root.transform); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 2;
        canvas.scaleFactor = Mathf.Min(size.x/1920f, size.y/1080f); canvas.referencePixelsPerUnit = 1;
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), canvas.transform, false);
        try
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            var dialog = go.GetComponent<DialogBase>();
            if (dialog == null)
            {
                Debug.LogWarning("AUDIT NO DIALOG COMPONENT: " + path);
                return;
            }
            dialog.Initialize(DialogSize.Manual, false);
            if (dialog is Common1ButtonDialog one)
            {
                one.Initialize(null, "WORD_OK", null, DialogSize.Manual, false);
                one.SetMessageBodyText(LocalizationManager.Get("manager.status.settings_saved"));
            }
            if (dialog is Common2ButtonDialog two)
                two.Initialize(path.EndsWith("/Common2ButtonDialog.prefab") ? "MSG_PAUSE_LIVE_RETRY" : null,
                    "WORD_DECIDE", "WORD_CANCEL", null, null, DialogSize.Manual, false);
            if (dialog is Sekai.MusicScoreMaker.Ingame.MusicScoreMakerCustomQuantizeDialog quantize) quantize.Setup(16);
            if (dialog is AddMusicScoreEventDataDialog eventDialog) eventDialog.Setup(Sekai.MusicScoreMaker.Ingame.Models.MusicScoreEventType.BPM, 120m);
            if (dialog is SubWindowDialog sub)
                sub.Initialize(variant == "multiline-stress" ? string.Join("\n", Enumerable.Repeat(LocalizationManager.Get("live.paused"),4)) : LocalizationManager.Get("live.paused"), null, false);
            if (dialog.WindowRoot != null) dialog.WindowRoot.transform.localScale = Vector3.one;
            foreach (var text in go.GetComponentsInChildren<CustomTextMesh>(true)) text.UpdateWordingText();
            foreach (var text in go.GetComponentsInChildren<CustomText>(true)) text.UpdateWordingText();
            MenuTypography.BindTree(go.transform);
            foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
            {
                RuntimeLocalizationBootstrap.TryBind(text);
                if (variant == "spacing-zero") text.lineSpacing = 0;
                if (variant == "multiline-stress" && text.name == "MessageBody")
                    text.text = string.Join("\n", Enumerable.Repeat(LocalizationManager.Get("live.paused"), 4));
            }
            var repair = DialogTextLayout.Prepare(dialog);
            if (variant == "scroll-stress")
            {
                // Exercise message replacement after opening, without preparing the dialog again.
                ((Common2ButtonDialog)dialog).SetMessageBodyText(string.Join("\n",
                    Enumerable.Range(1, 40).Select(i => i + ": " + LocalizationManager.Get("live.paused"))));
            }
            for (int i=0;i<6;i++)
            {
                repair.SendMessage("LateUpdate");
                foreach (var label in go.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate(true,true);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)go.transform);
            }
            var window = dialog.WindowRoot != null ? (RectTransform)dialog.WindowRoot.transform : (RectTransform)go.transform;
            var corners = new Vector3[4]; window.GetWorldCorners(corners);
            var lo = camera.WorldToScreenPoint(corners[0]); var hi = camera.WorldToScreenPoint(corners[2]);
            bool outside = lo.x < -1 || lo.y < -1 || hi.x > size.x+1 || hi.y > size.y+1;
            Require(!outside, "Window exceeds canvas: " + path);
            if (dialog is Sekai.MusicScoreMaker.Ingame.MusicScoreMakerCustomQuantizeDialog)
            {
                var row = window.Find("ContentRoot/Content/Title");
                var items = row.GetComponentsInChildren<TMP_Text>().OrderBy(t => t.transform.position.x).ToArray();
                for (int i = 1; i < items.Length; i++)
                {
                    var a = new Vector3[4]; var b = new Vector3[4];
                    items[i-1].rectTransform.GetWorldCorners(a); items[i].rectTransform.GetWorldCorners(b);
                    Require(a[2].x <= b[0].x, "Quantize label overlaps value");
                }
            }
            if (variant == "scroll-stress")
            {
                var scroll = go.GetComponentInChildren<ScrollRect>();
                Require(scroll != null && scroll.vertical && scroll.content.rect.height > scroll.viewport.rect.height,
                    "Long message must scroll");
                var viewportCorners = new Vector3[4]; var footerCorners = new Vector3[4];
                scroll.viewport.GetWorldCorners(viewportCorners);
                ((RectTransform)window.Find("FooterButtons")).GetWorldCorners(footerCorners);
                Require(viewportCorners[0].y >= footerCorners[2].y, "Message overlaps footer");
                scroll.verticalNormalizedPosition = 0;
                Canvas.ForceUpdateCanvases();
                var bodyCorners = new Vector3[4]; scroll.content.GetWorldCorners(bodyCorners);
                Require(Mathf.Abs(bodyCorners[0].y - viewportCorners[0].y) < .01f,
                    "Last message line cannot be reached");
                scroll.verticalNormalizedPosition = 1;
            }
            foreach (var text in go.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate(true,true);
                var lines = text.textInfo.lineInfo.Take(text.textInfo.lineCount).ToArray();
                float gap = float.PositiveInfinity;
                for(int i=1;i<lines.Length;i++)
                {
                    var previous = text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.isVisible && c.lineNumber==i-1).ToArray();
                    var current = text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.isVisible && c.lineNumber==i).ToArray();
                    if(previous.Length>0 && current.Length>0) gap = Mathf.Min(gap, previous.Min(c=>c.bottomLeft.y)-current.Max(c=>c.topRight.y));
                }
                Rows.Add(string.Join("\t", new object[] {path,language,size.x+"x"+size.y,variant,PathOf(text.transform,go.transform),Clean(text.text),text.font.name,text.fontSize,text.lineSpacing,text.rectTransform.rect.width,text.rectTransform.rect.height,text.preferredHeight,lines.Length,gap,text.isTextTruncated,window.rect.width,window.rect.height,outside,string.Join(",",lines.Select(l=>l.baseline.ToString("F2")))}));
                Require(gap >= -.1f, "Overlapping glyph lines: " + PathOf(text.transform, go.transform));
                if (text.name == "MessageBody")
                    Require(text.rectTransform.rect.height + 1 >= text.preferredHeight,
                        "Message body is smaller than text: " + path);
            }
            foreach (var text in go.GetComponentsInChildren<Text>())
                Rows.Add(string.Join("\t",new object[]{path,language,size.x+"x"+size.y,variant,PathOf(text.transform,go.transform),Clean(text.text),text.font.name,text.fontSize,text.lineSpacing,text.rectTransform.rect.width,text.rectTransform.rect.height,text.preferredHeight,"legacy","","",window.rect.width,window.rect.height,outside,""}));
            typeof(TMP_FontAsset).GetMethod("UpdateFontAssetsInUpdateQueue", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,null);
            camera.Render(); RenderTexture.active=rt;
            var png = new Texture2D(size.x,size.y,TextureFormat.RGB24,false); png.ReadPixels(new Rect(0,0,size.x,size.y),0,0); png.Apply(); RenderTexture.active=null;
            File.WriteAllBytes($"Logs/DialogAudit/{System.IO.Path.GetFileNameWithoutExtension(path)}-{language}-{size.x}-{variant}.png",png.EncodeToPNG()); Object.DestroyImmediate(png);
        }
        catch(Exception e) { Debug.LogError("AUDIT FAILED "+path+": "+e); throw; }
        finally { Object.DestroyImmediate(root); rt.Release(); Object.DestroyImmediate(rt); }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
