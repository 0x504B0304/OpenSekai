using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.MenuUI
{
    public static class MenuSurfaceStyle
    {
        public static T WithMenuTheme<T>(this T dialog) where T:Component
        {
            if(dialog!=null)Apply(dialog.transform,true);
            return dialog;
        }
        public static void Apply(Transform root,bool dialog=false)
        {
            // Only explicitly opted-in menu instances are changed. No global palette or prefab edits.
            foreach(var button in root.GetComponentsInChildren<Button>(true))
            {
                if(button.gameObject.GetComponent<MenuStyledMarker>()!=null)continue;
                button.gameObject.AddComponent<MenuStyledMarker>();
                var image=button.image;
                bool background=image!=null&&(image.sprite==null||image.sprite.name.ToLowerInvariant().Contains("btn")||image.sprite.name.ToLowerInvariant().Contains("button")||image.name.ToLowerInvariant().Contains("base"));
                if(background)MenuControls.Style(button);
                // Keep the original note/icon sprite when it is the button's target graphic.
                var outline=button.gameObject.GetComponent<Outline>()??button.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.47f,.93f,.87f,.16f);outline.effectDistance=new Vector2(1,-1);
            }
            foreach(var image in root.GetComponentsInChildren<Image>(true))
            {
                string name=image.name.ToLowerInvariant();
                if((name=="background"||name=="bg"||name=="base")&&image.rectTransform.rect.width>180&&image.GetComponentInParent<Selectable>()==null)
                {MenuRoundedImage.Set(image,MenuControls.Rounded);MenuThemeBinding.Bind(image,MenuColor.Panel);}
            }
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if(label.GetComponentInParent<Selectable>()==null)MenuThemeBinding.Bind(label,MenuColor.Text);
            }
        }
    }
    public sealed class MenuStyledMarker:MonoBehaviour { }
}
