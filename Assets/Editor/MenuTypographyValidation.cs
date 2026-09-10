using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Sekai;
using Sekai.Localization;
using Sekai.MenuUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class MenuTypographyValidation
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Typography: " + message);
    }

    public static void Validate()
    {
        string originalLanguage = LocalizationManager.CurrentLanguage;
        var setLanguage = typeof(LocalizationManager).GetMethod("SetLanguage", BindingFlags.Static | BindingFlags.NonPublic);
        var root = new GameObject("Typography validation", typeof(Canvas));
        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(root.transform, false);
        label.rectTransform.sizeDelta = new Vector2(1200, 90);
        label.fontSize = 24;
        var gameplayFont = Resources.Load<TMP_FontAsset>("font/FOT-RodinNTLGPro-DB SDF_Base");
        var gameplayMaterial = gameplayFont.material;
        var oldFallbacks = gameplayFont.fallbackFontAssetTable.ToArray();
        try
        {
            foreach (string language in new[] { "zh-Hans", "en", "ja" })
            {
                setLanguage.Invoke(null, new object[] { language, false });
                foreach (MenuTextRole role in Enum.GetValues(typeof(MenuTextRole)))
                {
                    var font = MenuTypography.Get(role);
                    Check(font == MenuTypography.Get(role), "font cache not shared");
                    Check(font != gameplayFont && font.material != gameplayMaterial, "gameplay asset reused");
                    MenuTypography.Bind(label, role);
                    string sample = role == MenuTextRole.Timecode ? "00:01:234" : "OpenSekai 设置 保存 編集 日本語 0123456789 ✓ ♯ ♭ ♪";
                    foreach (int sampling in new[] { 90, 135, 180, 90 })
                    {
                        HighQualityDynamicFontProvider.Resample(sampling);
                        label.text = sample;
                        label.ForceMeshUpdate(true, true);
                        Check(!label.isTextTruncated, "clipped " + language + " " + role);
                        Check(label.textInfo.characterCount == sample.Length, "missing geometry");
                        foreach (var character in label.textInfo.characterInfo.Take(label.textInfo.characterCount))
                            if (!char.IsWhiteSpace(character.character)) Check(character.isVisible, "invisible " + character.character);
                    }
                }
                var face = MenuTypography.Get();
                foreach (string value in LocalizationManager.GetTable(language).Values)
                    Check(face.HasCharacters(value, out uint[] missing, true, true), "missing localized glyphs: " + value);
            }
            MenuTypography.Bind(label, MenuTextRole.Title);
            root.SetActive(false);
            setLanguage.Invoke(null, new object[] { "en", false });
            root.SetActive(true);
            // Batch EditMode does not dispatch MonoBehaviour enable callbacks.
            label.GetComponent<MenuTypographyBinding>().SendMessage("OnEnable");
            Check(label.font.sourceFontFile.name == "HarmonyOS_Sans_Bold", "inactive label language refresh");
            Check(label.fontStyle == FontStyles.Normal, "synthetic bold remains");
            label.GetComponent<MenuTypographyBinding>().SendMessage("OnDisable");
            Check(gameplayFont.material == gameplayMaterial && gameplayFont.fallbackFontAssetTable.SequenceEqual(oldFallbacks), "gameplay resources modified");
            Check(!MenuTypography.IsMenu(label), "unscoped text classified as menu");
            Check(Resources.Load<TextAsset>("Fonts/HarmonyOS/LICENSE").text.Contains("Huawei"), "license not bundled");
            Debug.Log("HARMONYOS TYPOGRAPHY VALIDATION PASSED: three languages, eight roles, DPI 100/150/200, glyph coverage, hidden reopen, gameplay isolation.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            setLanguage.Invoke(null, new object[] { originalLanguage, false });
            HighQualityDynamicFontProvider.Resample(90);
        }
    }

    public static void Capture()
    {
        Validate();
        Directory.CreateDirectory("Logs/HarmonyOS");
        string original = LocalizationManager.CurrentLanguage;
        bool originalDark = MenuTheme.IsDark;
        var setter = typeof(LocalizationManager).GetMethod("SetLanguage", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            foreach (string language in new[] { "zh-Hans", "en", "ja" })
            {
                setter.Invoke(null, new object[] { language, false });
                foreach (bool dark in new[] { true, false })
                {
                MenuTheme.SetDark(dark, false);
                foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(3840,2160), new Vector2Int(1920,1080) })
                {
                    int sample = size.x == 3840 ? 180 : 90;
                    HighQualityDynamicFontProvider.Resample(sample);
                    var captureStarted = DateTime.UtcNow;
                    typeof(MenuUiValidation).GetMethod("CaptureMenu", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { size });
                    foreach (string file in Directory.GetFiles("Logs/MenuUI", "*.png"))
                        if(File.GetLastWriteTimeUtc(file)>=captureStarted)
                            File.Copy(file, "Logs/HarmonyOS/" + language + "-" + Path.GetFileName(file), true);
                }
                }
            }
        }
        finally
        {
            setter.Invoke(null, new object[] { original, false });
            MenuTheme.SetDark(originalDark, false);
            HighQualityDynamicFontProvider.Resample(90);
        }
    }

    public static void CaptureToolsAndBuildWindows()
    {
        AudioAssistUiValidation.CaptureSyllableEditorReview();
        Sekai.EditorTools.OpenSekaiAssetBundleBuildPipeline.BuildWindowsPlayer();
    }

    public static void ValidateAndBuildWindows()
    {
        Capture();
        Sekai.EditorTools.OpenSekaiAssetBundleBuildPipeline.BuildWindowsPlayer();
    }
}
