using System;
using UnityEngine;

namespace Sekai.MenuUI
{
    // Scoped menu theme. Never modifies uPalette or shared PJSK assets.
    [Serializable]
    public sealed class MenuTheme
    {
        public string background="#171925", panel="#222536", raised="#2C3045", border="#3B4059";
        public string accent="#77EEDD", text="#F1F3FA", muted="#A4ADC7", danger="#FF7897", selected="#28514F";
        private static MenuTheme current;
        private static bool? dark;
        public static bool IsDark => dark ?? (dark=PlayerPrefs.GetInt("MenuUI.dark",1)==1).Value;
        public static event Action Changed;
        public static void SetDark(bool value,bool persist=true)
        {
            dark=value;current=null;
            if(persist){PlayerPrefs.SetInt("MenuUI.dark",value?1:0);PlayerPrefs.Save();}
            Changed?.Invoke();
        }
        public static MenuTheme Current => current ?? (current=Load());
        private static MenuTheme Load()
        {
            var asset=Resources.Load<TextAsset>(IsDark?"MenuUI/theme":"MenuUI/theme-light");
            return asset!=null ? JsonUtility.FromJson<MenuTheme>(asset.text) : new MenuTheme();
        }
        public static Color C(string hex) { UnityEngine.ColorUtility.TryParseHtmlString(hex,out var color);return color; }
        public static Color Background=>C(Current.background);
        public static Color Panel=>C(Current.panel);
        public static Color Raised=>C(Current.raised);
        public static Color Border=>C(Current.border);
        public static Color Accent=>C(Current.accent);
        public static Color Text=>C(Current.text);
        public static Color Muted=>C(Current.muted);
        public static Color Danger=>C(Current.danger);
        public static Color Selected=>C(Current.selected);
        public static Color ButtonFace=>IsDark?new Color32(73,73,101,255):Color.white;
        public static Color ButtonInk=>IsDark?Text:new Color32(68,68,102,255);
        public static Color PrimaryFace=>Accent;
        public static Color PrimaryInk=>new Color32(51,58,85,255);
        public static Color TabBar=>IsDark?new Color32(66,66,92,255):new Color32(185,184,205,255);
        // The selected tab and its content surface intentionally use a theme
        // specific tint instead of reusing Panel. This keeps the active section
        // legible in both themes while preserving the continuous tab silhouette.
        public static Color TabActive=>IsDark?new Color32(105,101,145,255):new Color32(211,243,238,255);
        public static Color TabActiveInk=>IsDark?new Color32(250,250,255,255):new Color32(52,60,91,255);
        public static Color TabInactiveInk=>IsDark?new Color32(204,204,224,255):new Color32(65,65,96,255);
        public static Color TabField=>new Color32(250,250,254,255);
        // Input values sit on TabField even while their surrounding tab surface
        // uses light ink in dark mode; keep those values dark for contrast.
        public static Color TabFieldInk=>new Color32(53,57,84,255);
        public static Color TabMutedInk=>new Color32(105,105,130,255);
    }
}
