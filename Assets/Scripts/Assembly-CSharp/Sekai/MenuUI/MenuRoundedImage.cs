using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sekai.MenuUI
{
    // The native canvas uses one reference pixel/unit, while menu sprites use 100.
    // Normalize only opted-in menu images, leaving the native canvas and note skins alone.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed class MenuRoundedImage : UIBehaviour
    {
        private Image image;
        public static void Set(Image target, Sprite sprite)
        {
            target.sprite=sprite;target.overrideSprite=null;target.type=Image.Type.Sliced;
            var binding=target.GetComponent<MenuRoundedImage>()??target.gameObject.AddComponent<MenuRoundedImage>();
            binding.Refresh();
        }
        public void Refresh()
        {
            if(image==null)image=GetComponent<Image>();
            if(image.sprite==null)return;
            var currentCanvas=GetComponentInParent<Canvas>();
            float reference=currentCanvas!=null?currentCanvas.referencePixelsPerUnit:100;
            float multiplier=reference/image.sprite.pixelsPerUnit;
            if(!Mathf.Approximately(image.pixelsPerUnitMultiplier,multiplier))image.pixelsPerUnitMultiplier=multiplier;
        }
        protected override void OnEnable(){base.OnEnable();Refresh();}
        protected override void OnCanvasHierarchyChanged(){base.OnCanvasHierarchyChanged();Refresh();}
        protected override void OnTransformParentChanged(){base.OnTransformParentChanged();Refresh();}
        private void LateUpdate(){Refresh();}
    }
}
