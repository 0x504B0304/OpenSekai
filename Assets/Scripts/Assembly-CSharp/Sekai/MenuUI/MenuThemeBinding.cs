using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.MenuUI
{
    public enum MenuColor { Background,Panel,Raised,Border,Accent,Text,Muted,Danger,Selected,ButtonFace,ButtonInk,PrimaryFace,PrimaryInk,TabBar,TabActive,TabActiveInk,TabInactiveInk,TabField,TabFieldInk,TabMutedInk }
    [ExecuteAlways]
    public sealed class MenuThemeBinding:MonoBehaviour
    {
        public MenuColor Role;
        private Graphic graphic;
        private float opacity=1;
        public static Color Value(MenuColor role)
        {
            switch(role)
            {
                case MenuColor.Background:return MenuTheme.Background;case MenuColor.Panel:return MenuTheme.Panel;
                case MenuColor.Raised:return MenuTheme.Raised;case MenuColor.Border:return MenuTheme.Border;
                case MenuColor.Accent:return MenuTheme.Accent;case MenuColor.Muted:return MenuTheme.Muted;
                case MenuColor.Danger:return MenuTheme.Danger;case MenuColor.Selected:return MenuTheme.Selected;
                case MenuColor.ButtonFace:return MenuTheme.ButtonFace;case MenuColor.ButtonInk:return MenuTheme.ButtonInk;
                case MenuColor.PrimaryFace:return MenuTheme.PrimaryFace;case MenuColor.PrimaryInk:return MenuTheme.PrimaryInk;
                case MenuColor.TabBar:return MenuTheme.TabBar;case MenuColor.TabActive:return MenuTheme.TabActive;
                case MenuColor.TabActiveInk:return MenuTheme.TabActiveInk;case MenuColor.TabInactiveInk:return MenuTheme.TabInactiveInk;
                case MenuColor.TabField:return MenuTheme.TabField;case MenuColor.TabFieldInk:return MenuTheme.TabFieldInk;case MenuColor.TabMutedInk:return MenuTheme.TabMutedInk;
                default:return MenuTheme.Text;
            }
        }
        public static void Bind(Graphic graphic,MenuColor role)
        {
            if(graphic==null)return;
            var binding=graphic.GetComponent<MenuThemeBinding>()??graphic.gameObject.AddComponent<MenuThemeBinding>();
            binding.graphic=graphic;binding.Role=role;binding.opacity=graphic.color.a;binding.Apply();
        }
        public static void Capture(Transform root)
        {
            foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if(graphic.GetComponent<MenuThemeBinding>()!=null)continue;
                foreach(MenuColor role in Enum.GetValues(typeof(MenuColor)))
                {
                    var a=graphic.color;var b=Value(role);
                    if(Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)<.02f){Bind(graphic,role);break;}
                }
            }
        }
        private void OnEnable(){MenuTheme.Changed+=Apply;Apply();}
        private void OnDisable(){MenuTheme.Changed-=Apply;}
        private void Apply(){if(graphic!=null){var c=Value(Role);c.a=opacity;graphic.color=c;}}
    }
}
