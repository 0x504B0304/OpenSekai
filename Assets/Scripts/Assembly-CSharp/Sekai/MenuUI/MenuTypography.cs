using System;
using System.Collections.Generic;
using Sekai.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.MenuUI
{
    public enum MenuTextRole { Body, Caption, Control, Section, Title, SelectedTab, Numeric, Timecode }

    public static class MenuTypography
    {
        private const string Root = "Fonts/HarmonyOS/";
        private static readonly Dictionary<string, TMP_FontAsset> Fonts = new Dictionary<string, TMP_FontAsset>();

        public static string SourcePath(MenuTextRole role, string language)
        {
            bool bold = role == MenuTextRole.Title || role == MenuTextRole.SelectedTab;
            if (language == LocalizationManager.Japanese)
                return "font/FOT-RodinNTLGPro-" + (bold ? "EB" : "DB");
            if (role == MenuTextRole.Timecode) return Root + "HarmonyOS_Sans_Condensed_Medium";
            string weight = bold ? "Bold" : role == MenuTextRole.Body || role == MenuTextRole.Caption ? "Regular" : "Medium";
            bool sc = language == LocalizationManager.SimplifiedChinese && role != MenuTextRole.Numeric;
            return Root + "HarmonyOS_Sans_" + (sc ? "SC_" : "") + weight;
        }

        private static TMP_FontAsset Load(string path)
        {
            if (Fonts.TryGetValue(path, out var cached) && cached != null) return cached;
            var source = Resources.Load<Font>(path);
            if (source == null) throw new InvalidOperationException("Missing bundled menu font: " + path);
            return Fonts[path] = HighQualityDynamicFontProvider.GetFromSource(source);
        }

        public static TMP_FontAsset Get(MenuTextRole role = MenuTextRole.Body, string language = null)
        {
            language ??= LocalizationManager.CurrentLanguage;
            var font = Load(SourcePath(role, language));
            if (font.fallbackFontAssetTable != null && font.fallbackFontAssetTable.Count > 0) return font;
            var fallback = new List<TMP_FontAsset>();
            if (language != LocalizationManager.Japanese && !font.name.Contains("_SC_"))
            {
                string weight = role == MenuTextRole.Title || role == MenuTextRole.SelectedTab ? "Bold" :
                    role == MenuTextRole.Body || role == MenuTextRole.Caption ? "Regular" : "Medium";
                var sc = Load(Root + "HarmonyOS_Sans_SC_" + weight);
                sc.fallbackFontAssetTable = new List<TMP_FontAsset> { Load("Fonts/NotoSansCJKsc-Regular") };
                fallback.Add(sc);
            }
            fallback.Add(Load("Fonts/NotoSansCJKsc-Regular"));
            font.fallbackFontAssetTable = fallback;
            return font;
        }

        public static MenuTypographyBinding Bind(Component text, MenuTextRole role)
        {
            var binding = text.GetComponent<MenuTypographyBinding>() ?? text.gameObject.AddComponent<MenuTypographyBinding>();
            binding.Role = role;
            binding.Refresh();
            return binding;
        }

        // Legacy prefabs are classified once. Explicit factory bindings take precedence.
        public static MenuTextRole Infer(Component text)
        {
            string name = text.name.ToLowerInvariant();
            if (name == "playbacktime") return MenuTextRole.Timecode;
            if (name.Contains("hint") || name.Contains("placeholder") || name.Contains("status") || name.Contains("meta")) return MenuTextRole.Caption;
            var input = text.GetComponentInParent<TMP_InputField>(true);
            if (input != null)
                return input.contentType == TMP_InputField.ContentType.IntegerNumber || input.contentType == TMP_InputField.ContentType.DecimalNumber ? MenuTextRole.Numeric : MenuTextRole.Body;
            if (text.GetComponentInParent<InputField>(true) != null) return MenuTextRole.Body;
            if (text.GetComponentInParent<Selectable>(true) != null) return MenuTextRole.Control;
            if (name.Contains("title") || name.Contains("header")) return MenuTextRole.Title;
            if (name.Contains("bpm") || name.Contains("measure") || name.Contains("speedratio") || name.Contains("tick")) return MenuTextRole.Numeric;
            var tmp = text as TMP_Text;
            if (tmp != null && (tmp.fontStyle & FontStyles.Bold) != 0) return MenuTextRole.Section;
            return MenuTextRole.Body;
        }

        public static bool IsMenu(Component text)
        {
            for (Transform parent = text.transform; parent != null; parent = parent.parent)
            {
                foreach (var component in parent.GetComponents<MonoBehaviour>())
                {
                    if (component == null) continue;
                    if (component is DialogBase) return true;
                    if (component is Sekai.Core.Live.LiveViewBase) return false;
                    string name = component.GetType().Name;
                    if (name == "ScreenLayerLive" || name == "ScreenLayerMusicScoreMakerTestPlay" || name == "LiveUI") return false;
                    if (name == "ScreenLayerCustomMusicScoreManager" || name == "ScreenLayerMusicScoreMaker" || name == "MusicScoreMakerView" ||
                        name == "ScreenLayerLoading" || name == "ScreenLayerLiveLoading" || name == "LoadingView" ||
                        name == "ArtToolsRuntimePanel" || name == "AudioAssistPanel" || name == "AudioAssistLyricOverlay" ||
                        name == "AudioAssistSyllableDialog" || name == "EditorActionDock" ||
                        name == "VideoExportOverlay" || name == "VideoProcessingProgressDialog") return true;
                }
            }
            return false;
        }

        public static void TryBind(Component text)
        {
            var binding = text.GetComponent<MenuTypographyBinding>();
            if (binding != null) { binding.Refresh(); return; }
            if (IsMenu(text)) Bind(text, Infer(text));
        }

        public static void BindTree(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) TryBind(text);
            foreach (var text in root.GetComponentsInChildren<Text>(true)) TryBind(text);
        }
    }
}
