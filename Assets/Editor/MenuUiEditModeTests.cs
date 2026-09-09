#if UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using Sekai.CustomMusicScoreManager;
using Sekai.Localization;
using Sekai.MusicScoreMaker.Common;
using Sekai.MenuUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using Object=UnityEngine.Object;

public sealed class MenuUiEditModeTests
{
    [Test]
    public void HiddenSettingsKeepTabStyleAndSongTitlesSurviveLocalization()
    {
        var root=new GameObject("MenuRegressionCanvas",typeof(RectTransform),typeof(Canvas));
        try
        {
            root.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            var go=new GameObject("Library",typeof(RectTransform));go.transform.SetParent(root.transform,false);
            MenuControls.Stretch(go.GetComponent<RectTransform>());
            var manager=go.AddComponent<ScreenLayerCustomMusicScoreManager>();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            object Field(string name)=>typeof(ScreenLayerCustomMusicScoreManager).GetField(name,flags).GetValue(manager);
            if(Field("_menuRoot")==null)typeof(ScreenLayerCustomMusicScoreManager).GetMethod("BuildView",flags).Invoke(manager,null);
            var tabs=(MenuSegments)Field("_menuSettingsTabs");
            Assert.IsFalse(tabs.gameObject.activeInHierarchy,"Exercise the hidden settings construction path.");
            foreach(var button in tabs.GetComponentsInChildren<Button>(true))
            {
                Assert.AreSame(MenuControls.Tab,button.image.sprite,"Generic button styling must not replace tab geometry.");
                Assert.AreEqual(Selectable.Transition.None,button.transition,"The extension must not retint a selected tab.");
            }
            var body=(RectTransform)Field("_menuSettingsScroll");
            Assert.AreEqual(MenuTheme.TabActive,body.GetComponent<Image>().color);
            var manifest=new CustomMusicScoreManifest{id="ui-regression",title="保存",scoreTitle="Test",userName="UI",musicDifficultyType="expert"};manifest.Normalize();
            var entry=new CustomMusicScoreEntry(System.IO.Path.GetFullPath("Logs/UiRegressionFixture"),manifest);
            var item=new CustomMusicScoreManagerItem(entry,DateTime.UtcNow,false,false,false,false);
            typeof(ScreenLayerCustomMusicScoreManager).GetMethod("UpdateSelection",flags).Invoke(manager,new object[]{item});
            var title=(TMP_Text)Field("_detailTitle");
            RuntimeLocalizationBootstrap.TryBind(title);
            title.GetComponent<LocalizedTextBinding>().Refresh();
            Assert.AreEqual("保存",title.text,"Song data matching UI copy must remain literal, not revert to the empty-selection prompt.");
        }
        finally{Object.DestroyImmediate(root);}
    }
    [Test]
    public void NearbyLoopEndpointsKeepSeparateTouchTargets()
    {
        var root=new GameObject("LoopHitTest",typeof(RectTransform));
        try
        {
            var range=MenuControls.Range(root.transform,null,56,(_,__)=>{});
            ((RectTransform)range.transform).sizeDelta=new Vector2(320,124);
            range.SetWithoutNotify(180,0,4);
            var a=new Vector3[4];var b=new Vector3[4];range.LowHandleRect.GetWorldCorners(a);range.HighHandleRect.GetWorldCorners(b);
            Assert.GreaterOrEqual(range.LowHandleRect.rect.height,44);
            Assert.GreaterOrEqual(range.HighHandleRect.rect.height,44);
            Assert.Greater(a[0].y,b[2].y,"Close times must not stack the A and B hit areas on top of each other.");
            Assert.AreEqual(0,range.LowValue);Assert.AreEqual(4,range.HighValue);
        }
        finally{Object.DestroyImmediate(root);}
    }
    [TestCase(1f)]
    [TestCase(100f)]
    public void MenuCornersKeepTheirSizeOnNativeAndDefaultCanvases(float reference)
    {
        var root=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));
        try
        {
            var canvas=root.GetComponent<Canvas>();canvas.referencePixelsPerUnit=reference;
            var button=MenuControls.Button(root.transform,"Menu",null,null);
            var panel=MenuControls.Rect("Panel",root.transform,MenuTheme.Panel).GetComponent<Image>();
            foreach(var image in new[]{button.image,panel})
            {
                Assert.AreEqual(1,image.pixelsPerUnit*image.pixelsPerUnitMultiplier,.001f);
                Assert.Greater(image.sprite.border.x,10);
                Assert.AreEqual(0,image.sprite.texture.GetPixel(0,0).a);
            }
            // Reparenting cannot inherit the previous canvas scale.
            var other=new GameObject("OtherCanvas",typeof(RectTransform),typeof(Canvas));other.transform.SetParent(root.transform);
            other.GetComponent<Canvas>().referencePixelsPerUnit=reference==1?100:1;
            button.transform.SetParent(other.transform,false);
            Assert.AreEqual(1,button.image.pixelsPerUnit*button.image.pixelsPerUnitMultiplier,.001f);
        }
        finally{Object.DestroyImmediate(root);}
    }
    [Test]
    public void RestoringRangeIsSilentAndUserChangeNotifies()
    {
        var root=new GameObject("RangeTest",typeof(RectTransform));
        try
        {
            int changes=0;var range=MenuControls.Range(root.transform,null,56,(a,b)=>changes++);
            range.SetWithoutNotify(90,30,36);Assert.AreEqual(0,changes);Assert.AreEqual(30,range.LowValue);Assert.AreEqual(36,range.HighValue);
            range.LowValue=31;Assert.AreEqual(1,changes);
            range.SetWithoutNotify(10,1,5);Assert.AreEqual(1,changes);Assert.AreEqual(5,range.HighValue);Assert.AreEqual(1,range.LowValue);
        }
        finally{Object.DestroyImmediate(root);}
    }
    [Test]
    public void CollapsedSegmentsRestoreWithoutDispatchAndPointerSelects()
    {
        var root=new GameObject("SegmentTest",typeof(RectTransform));root.SetActive(false);
        try
        {
            int changes=0,selected=-1;var tabs=MenuControls.Segments(root.transform,new[]{"A","B","C"},1,null,56,i=>{changes++;selected=i;});
            tabs.SetWithoutNotify(2);root.SetActive(true);Assert.AreEqual(0,changes);
            var segments=tabs.GetComponentsInChildren<SegmentedControlSegment>();
            segments[0].OnPointerClick(new PointerEventData(null){button=PointerEventData.InputButton.Left});
            Assert.AreEqual(1,changes);Assert.AreEqual(0,selected);
            root.SetActive(false);root.SetActive(true);
            Assert.AreEqual(1,changes);Assert.AreEqual(0,tabs.GetComponent<SegmentedControl>().selectedSegmentIndex);
            tabs.SetWithoutNotify(2);Assert.AreEqual(1,changes);Assert.AreEqual(2,tabs.GetComponent<SegmentedControl>().selectedSegmentIndex);
        }
        finally{Object.DestroyImmediate(root);}
    }
    [Test]
    public void ThemeChangesOnlyOptedInGraphicsIncludingInactiveControls()
    {
        bool original=MenuTheme.IsDark;var root=new GameObject("ThemeTest");
        try
        {
            var untouched=new GameObject("OriginalNote",typeof(RectTransform),typeof(Image));untouched.transform.SetParent(root.transform);var image=untouched.GetComponent<Image>();image.color=Color.magenta;
            var button=MenuControls.Button(root.transform,"Menu",null,null);MenuControls.Selected(button,true);
            root.SetActive(false);MenuTheme.SetDark(false,false);root.SetActive(true);
            Assert.AreEqual(Color.magenta,image.color);Assert.AreEqual(MenuTheme.PrimaryFace,button.image.color);
            Assert.AreEqual(MenuTheme.PrimaryInk,button.GetComponentInChildren<TMP_Text>().color);
            MenuControls.Selected(button,false);MenuTheme.SetDark(true,false);
            Assert.AreEqual(MenuTheme.ButtonFace,button.image.color);Assert.AreEqual(Color.magenta,image.color);
        }
        finally{Object.DestroyImmediate(root);MenuTheme.SetDark(original,false);}
    }
}
#endif
