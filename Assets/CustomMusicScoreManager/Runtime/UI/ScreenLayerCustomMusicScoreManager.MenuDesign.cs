using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sekai.MenuUI;
using Sekai.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.CustomMusicScoreManager
{
    public sealed partial class ScreenLayerCustomMusicScoreManager
    {
        private RectTransform _menuRoot,_menuList,_menuBody,_menuSettingsDialog,_menuSettingsNavigation,_menuSettingsScroll;
        private RectTransform _menuMore,_menuFormTabs;
        private Button _menuBack;
        private MenuSegments _menuSettingsTabs;
        private readonly List<(Transform item,int category)> _menuSettingsItems=new List<(Transform,int)>();
        private readonly List<(Transform item,int category)> _menuFields=new List<(Transform,int)>();
        private readonly List<(TMP_InputField input,Slider slider)> _menuSliders=new List<(TMP_InputField,Slider)>();
        private readonly Dictionary<TMP_Text,float> _menuFontSizes=new Dictionary<TMP_Text,float>();
        private readonly Dictionary<LayoutElement,Vector2> _menuHeights=new Dictionary<LayoutElement,Vector2>();
        private bool _menuDetailVisible;
        private MenuSegments _menuThemeChoice;
        private int _menuSettingsCategory;
        private float _menuLastScale=-1;
        private Vector2 _menuLastSize;

        private void BuildMenuDesign(RectTransform root)
        {
            _menuRoot=root;_menuBody=root.Find("Body") as RectTransform;_menuList=_menuBody.Find("ListPanel") as RectTransform;
            var top=root.Find("TopBar");
            MenuTheme.Changed+=RefreshMenuTheme;top.Find("Title").GetComponent<TMP_Text>().text="OpenSekai  /  本地曲库";
            top.Find("Title").GetComponent<TMP_Text>().fontSize=34;
            _menuBack=CreateButton("BackToList",top,"‹ 曲库",()=>{_menuDetailVisible=false;_menuLastSize=Vector2.zero;UpdateMenuLayout();},130,56);
            _menuBack.gameObject.SetActive(false);
            _menuMore=CreatePanel("MoreActions",_detailPanel,(Color32)MenuTheme.Raised);
            var moreLayout=MenuControls.Vertical(_menuMore,8);moreLayout.padding=new RectOffset(10,10,10,10);
            foreach(var button in new[]{_duplicateButton,_exportButton,_deleteButton}){button.transform.SetParent(_menuMore,false);var le=button.GetComponent<LayoutElement>();le.minHeight=le.preferredHeight=56;le.flexibleWidth=1;}
            var more=CreateButton("MoreButton",_actionButtonsContent,"更多 ···",()=>{_menuMore.gameObject.SetActive(!_menuMore.gameObject.activeSelf);_menuMore.SetAsLastSibling();},150,54);
            _menuMore.gameObject.SetActive(false);

            // Keep the original fields and callbacks; only their navigation is changed.
            var formLayout=_manifestFormScroll.content.GetComponent<VerticalLayoutGroup>();formLayout.childControlHeight=true;formLayout.childForceExpandHeight=false;
            _manifestFieldGrid.GetComponent<ContentSizeFitter>().enabled=false;
            foreach(Transform field in _manifestFieldGrid)
            {
                string n=field.name;int category=0;
                if(new[]{"音频Input","封面Input","谱面Input","2DMVInput","生成视频ButtonField"}.Contains(n))category=1;
                if(new[]{"前置空白秒Input","编辑时长秒Input","等级Input","DifficultyInput"}.Contains(n))category=2;
                _menuFields.Add((field,category));
            }
            // Keep navigation above the scrolling body so their shared surface stays joined.
            _menuFormTabs=CreateRect("ConfigurationTabs",_detailPanel);
            var tabs=MenuControls.Segments(_menuFormTabs,new[]{"基本信息","媒体资源","谱面参数"},0,GetOriginalFontAsset(FontStyles.Normal),56,SelectMenuFieldCategory);
            tabs.UseTabs();MenuControls.Stretch((RectTransform)tabs.transform);_menuFormTabs.gameObject.AddComponent<LayoutElement>().preferredHeight=60;
            SelectMenuFieldCategory(0);

            _menuSettingsDialog=_settingsOverlay.Find("SettingsDialog") as RectTransform;
            var settingsLayout=_menuSettingsDialog.GetComponent<VerticalLayoutGroup>();settingsLayout.enabled=false;
            _menuSettingsScroll=_menuSettingsDialog.Find("SettingsScroll") as RectTransform;
            var content=_menuSettingsScroll.GetComponent<ScrollRect>().content;
            var themeRow=CreateRect("MenuThemeSelector",content);themeRow.SetAsFirstSibling();
            MenuControls.Vertical(themeRow,8);themeRow.gameObject.AddComponent<LayoutElement>().preferredHeight=110;
            var themeCaption=MenuControls.Text(themeRow,"菜单主题（立即生效）",GetOriginalFontAsset(FontStyles.Normal),24);
            themeCaption.gameObject.AddComponent<LayoutElement>().preferredHeight=40;BindMenuLabel(themeCaption,"theme");
            _menuThemeChoice=MenuControls.Segments(themeRow,new[]{"浅色","深色"},MenuTheme.IsDark?1:0,GetOriginalFontAsset(FontStyles.Normal),56,i=>MenuTheme.SetDark(i==1));
            var themeLabels=_menuThemeChoice.GetComponentsInChildren<TMP_Text>(true);
            BindMenuLabel(themeLabels[0],"light");BindMenuLabel(themeLabels[1],"dark");
            foreach(Transform item in content)_menuSettingsItems.Add((item,SettingsCategory(item.name)));
            _menuSettingsNavigation=CreateRect("CategoryNavigation",_menuSettingsDialog);
            _menuSettingsTabs=MenuControls.Segments(_menuSettingsNavigation,new[]{"声音","游玩","显示","制谱","语言与数据"},0,GetOriginalFontAsset(FontStyles.Normal),56,SelectMenuSettingsCategory);
            _menuSettingsTabs.UseTabs();MenuControls.Stretch((RectTransform)_menuSettingsTabs.transform);
            // Same inputs remain the source of truth for SaveSettings and Cancel.
            AddMenuSlider(_settingLiveBgmInput,0,1);AddMenuSlider(_settingLiveSeInput,0,1);
            AddMenuSlider(_settingNoteSpeedInput,1,12);AddMenuSlider(_settingTimingAdjustInput,-20,20);
            AddMenuSlider(_settingNoteShowRateInput,0,100);AddMenuSlider(_settingBackgroundBrightnessInput,0,100);
            AddMenuSlider(_settingNoteLineAlphaInput,10,100);AddMenuSlider(_settingGuideLineAlphaInput,10,100);AddMenuSlider(_settingJudgeLineAlphaInput,0,100);
            SelectMenuSettingsCategory(PlayerPrefs.GetInt("MenuUI.settingsCategory",0));
            foreach(var image in root.GetComponentsInChildren<Image>(true))
                if(image.sprite==null&&image.name!="Jacket"&&image.color.a>0){MenuRoundedImage.Set(image,MenuControls.Rounded);}
            foreach(var b in root.GetComponentsInChildren<Button>(true))if(b.GetComponentInParent<MenuSegments>(true)==null)MenuControls.Style(b,b.name=="EditButton"||b.name=="SaveButton"||b.name=="ImportButton"||b.name=="SaveManifestButton",b.name=="DeleteButton");
            var menuKeys=new Dictionary<string,string>{
                {"OpenSekai  /  本地曲库","library"},{"‹ 曲库","back"},{"更多 ···","more"},
                {"基本信息","basic"},{"媒体资源","media"},{"谱面参数","chart"},
                {"声音","sound"},{"游玩","play"},{"显示","display"},{"制谱","editor"},{"语言与数据","data"}};
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
                if(menuKeys.TryGetValue(label.text,out var key))BindMenuLabel(label,key);
            StyleTabBody(_manifestFormScroll);
            StyleTabBody(_menuSettingsScroll.GetComponent<ScrollRect>());
            RefreshMenuTheme();
            MenuThemeBinding.Capture(root);
            _menuMore.SetAsLastSibling();UpdateMenuLayout();
        }
        private static void BindMenuLabel(TMP_Text label,string key)
        {
            var binding=label.GetComponent<LocalizedTextBinding>()??label.gameObject.AddComponent<LocalizedTextBinding>();
            binding.Key="menuui."+key;
        }
        private static void StyleTabBody(ScrollRect scroll)
        {
            var background=scroll.GetComponent<Image>();
            MenuRoundedImage.Set(background,MenuControls.TabBody);
            MenuThemeBinding.Bind(background,MenuColor.TabActive);
            // Scope readable light-surface ink/fields to this tab body, including hidden categories.
            foreach(var label in scroll.content.GetComponentsInChildren<TMP_Text>(true))
                if(label.GetComponentInParent<MenuSegments>(true)==null)MenuThemeBinding.Bind(label,MenuColor.TabActiveInk);
            foreach(var input in scroll.content.GetComponentsInChildren<TMP_InputField>(true))
            {
                MenuThemeBinding.Bind(input.image,MenuColor.TabField);
                if(input.textComponent!=null)MenuThemeBinding.Bind(input.textComponent,MenuColor.TabFieldInk);
                if(input.placeholder!=null)
                {
                    input.placeholder.color=Color.white;
                    MenuThemeBinding.Bind(input.placeholder,MenuColor.TabMutedInk);
                }
            }
            foreach(var button in scroll.content.GetComponentsInChildren<Button>(true))
                if(button.GetComponentInParent<MenuSegments>(true)==null)MenuThemeBinding.Bind(button.image,MenuColor.TabField);
        }
        private static void SetMenuLiteral(TMP_Text label,string value)
        {
            // An explicit empty key prevents the periodic localizer from treating song data as UI copy.
            var binding=label.GetComponent<LocalizedTextBinding>()??label.gameObject.AddComponent<LocalizedTextBinding>();
            binding.Key=string.Empty;label.text=value;
            label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
        }
        private void RefreshMenuTheme()
        {
            _menuThemeChoice?.SetWithoutNotify(MenuTheme.IsDark?1:0);
            _menuSettingsTabs?.SetWithoutNotify(_menuSettingsCategory);
            foreach(var row in _rows)MenuThemeBinding.Bind(row.Background,row.Item==_selected?MenuColor.Selected:MenuColor.Raised);
        }
        private void OnDestroy(){MenuTheme.Changed-=RefreshMenuTheme;}
        private void AddMenuSlider(TMP_InputField input,float min,float max)
        {
            var field=(RectTransform)input.transform;var root=(RectTransform)field.parent;
            var vertical=root.GetComponent<VerticalLayoutGroup>();vertical.enabled=false;
            var slider=MenuControls.Slider(root,min,max,min,v=>input.SetTextWithoutNotify(v.ToString("0.##",CultureInfo.InvariantCulture)),56);
            var label=root.Find("Label") as RectTransform;SetStretchTop(label,0,0,0,30);
            SetAnchor(field,new Vector2(1,0),Vector2.one,new Vector2(1,.5f),new Vector2(0,-20),new Vector2(145,-40));
            SetStretchOffsets((RectTransform)slider.transform,0,4,161,40);
            _menuSliders.Add((input,slider));
        }
        private static int SettingsCategory(string name)
        {
            if(name.Contains("音乐音量")||name.Contains("音效音量")||name=="NoteSeSelector"||name=="MaimaiMusicInfoSeSelector")return 0;
            if(name.Contains("音符流速")||name.Contains("判定偏移")||name.Contains("上隐挡板")||name=="FastLateFlickSelector"||name=="AutoFakePerfectModeSelector"||name=="AutoResultAnimationSelector")return 1;
            if(name=="AutoSaveIntervalSelector"||name=="ScoreMakerPreviewModeSelector")return 3;
            if(name=="LanguageSelector"||name=="BackupSelector"||name=="RestoreSelector")return 4;
            return 2;
        }
        private void SelectMenuFieldCategory(int category)
        {
            foreach(var field in _menuFields)field.item.gameObject.SetActive(field.category==category);
            if(_manifestFormScroll!=null)_manifestFormScroll.verticalNormalizedPosition=1;
        }
        private void SelectMenuSettingsCategory(int category)
        {
            _menuSettingsCategory=Mathf.Clamp(category,0,4);
            foreach(var item in _menuSettingsItems)item.item.gameObject.SetActive(item.category==_menuSettingsCategory);
            _menuSettingsTabs?.SetWithoutNotify(_menuSettingsCategory);
            if(_menuSettingsScroll!=null)_menuSettingsScroll.GetComponent<ScrollRect>().verticalNormalizedPosition=1;
            if(Application.isPlaying)PlayerPrefs.SetInt("MenuUI.settingsCategory",_menuSettingsCategory);
        }
        private void UpdateMenuLayout()
        {
            if(_menuRoot==null)return;
            var canvas=_menuRoot.GetComponentInParent<Canvas>();float scale=canvas!=null?canvas.scaleFactor:1;
            float touch=44/Mathf.Max(.1f,scale),fontMin=14/Mathf.Max(.1f,scale);
            float w=_menuRoot.rect.width,h=_menuRoot.rect.height;bool narrow=w*scale<1000;
            foreach(var pair in _menuSliders)if(!pair.input.isFocused&&float.TryParse(pair.input.text,NumberStyles.Float,CultureInfo.InvariantCulture,out var v))pair.slider.SetValueWithoutNotify(v);
            if(_menuLastSize==_menuRoot.rect.size&&Mathf.Abs(scale-_menuLastScale)<.001f)return;
            // New list rows can be added asynchronously, so collect only new elements.
            foreach(var t in _menuRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if(!_menuFontSizes.TryGetValue(t,out float original)){original=t.fontSize;_menuFontSizes[t]=original;}
                t.fontSize=Mathf.Max(original,fontMin);t.enableAutoSizing=true;t.fontSizeMax=Mathf.Max(original,fontMin);t.fontSizeMin=Mathf.Min(original,fontMin);
            }

            if(_selected!=null){var m=_selected.Entry.Manifest;_detailTitle.text=m.title;_detailMeta.text=string.Join(" · ",new[]{m.composer,m.singer}.Where(v=>!string.IsNullOrWhiteSpace(v)))+"\n"+m.musicDifficultyType.ToUpperInvariant()+"  "+m.playLevel+"   /   "+m.scoreTitle;}
            _menuLastSize=_menuRoot.rect.size;_menuLastScale=scale;
            float button=Mathf.Max(56,touch),margin=Mathf.Max(20,12/scale);
            var bar=(RectTransform)_menuRoot.Find("TopBar");SetStretchTop(bar,0,0,0,button+margin*2);
            var title=bar.Find("Title") as RectTransform;SetStretchTop(title,margin,margin,w*.65f,button);
            var toolbar=bar.Find("Toolbar") as RectTransform;toolbar.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;SetStretchTop(toolbar,w*.35f,margin,margin,button);
            foreach(Transform child in toolbar){var le=child.GetComponent<LayoutElement>();if(le!=null){le.preferredWidth=Mathf.Max(105,touch*1.5f);le.preferredHeight=button;((RectTransform)child).sizeDelta=new Vector2(le.preferredWidth,button);}}
            _menuBack.gameObject.SetActive(narrow&&_menuDetailVisible);title.gameObject.SetActive(!narrow||!_menuDetailVisible);
            SetStretchTop((RectTransform)_menuBack.transform,margin,margin,w*.70f,button);
            SetStretchOffsets(_menuBody,margin,margin,margin,button+margin*3);
            float listWidth=Mathf.Min(520,w*.30f);
            if(narrow)
            {
                _menuList.gameObject.SetActive(!_menuDetailVisible);_detailPanel.gameObject.SetActive(_menuDetailVisible);
                SetStretchOffsets(_menuList,0,0,0,0);SetStretchOffsets(_detailPanel,0,0,0,0);
            }
            else
            {
                _menuList.gameObject.SetActive(true);_detailPanel.gameObject.SetActive(true);
                SetAnchor(_menuList,Vector2.zero,new Vector2(0,1),new Vector2(0,.5f),Vector2.zero,new Vector2(listWidth,0));
                SetStretchOffsets(_detailPanel,listWidth+margin,0,0,0);
            }
            foreach(var row in _rows)
            {
                var r=(RectTransform)row.Root.transform;var le=r.GetComponent<LayoutElement>();le.minHeight=le.preferredHeight=Mathf.Max(116,80/scale);
                var titleRect=r.Find("Title") as RectTransform;SetStretchTop(titleRect,22,Mathf.Max(14,10/scale),22,Mathf.Max(36,26/scale));
                float statusWidth=Mathf.Max(178,100/scale),metaHeight=Mathf.Max(30,24/scale);
                SetStretchBottom(r.Find("Meta") as RectTransform,22,Mathf.Max(16,8/scale),statusWidth+32,metaHeight);
                SetAnchor(r.Find("Status") as RectTransform,new Vector2(1,0),new Vector2(1,0),new Vector2(1,0),new Vector2(-22,Mathf.Max(16,8/scale)),new Vector2(statusWidth,metaHeight));
            }
            // Header remains compact on short landscape screens; the form scrolls independently.
            bool shortScreen=h*scale<540;
            bool showBest=!shortScreen&&_selected!=null&&_detailPanel.rect.width>=1100;
            _detailMeta.gameObject.SetActive(!shortScreen);_detailStatus.gameObject.SetActive(!shortScreen);
            float titleHeight=Mathf.Max(56,fontMin*2.1f),headerMetaHeight=Mathf.Max(64,fontMin*3.2f);
            float hero=shortScreen?Mathf.Max(48/scale,titleHeight):Mathf.Max(showBest?190:168,titleHeight+headerMetaHeight+48),heroTop=margin,actionTop=hero+margin*2;
            SetAnchor(_jacketImage.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),new Vector2(margin,-heroTop),new Vector2(hero,hero));
            SetStretchTop(_detailTitle.rectTransform,hero+margin*2,heroTop,margin,titleHeight);
            SetStretchTop(_detailMeta.rectTransform,hero+margin*2,heroTop+titleHeight+8,margin,headerMetaHeight);
            _detailMeta.textWrappingMode=TextWrappingModes.NoWrap;_detailMeta.overflowMode=TextOverflowModes.Ellipsis;
            SetStretchTop(_detailStatus.rectTransform,hero+margin*2,heroTop+hero-30,margin,30);
            if(_bestResultPanel!=null)
            {
                _bestResultPanel.gameObject.SetActive(showBest);
                if(showBest){SetAnchor(_bestResultPanel,Vector2.one,Vector2.one,Vector2.one,new Vector2(-margin,-margin),new Vector2(350,hero));
                var titleOffset=_detailTitle.rectTransform.offsetMax;titleOffset.x=-390;_detailTitle.rectTransform.offsetMax=titleOffset;
                var metaOffset=_detailMeta.rectTransform.offsetMax;metaOffset.x=-390;_detailMeta.rectTransform.offsetMax=metaOffset;}
            }
            // Short landscape screens share a header row so the form can show a complete field.
            if(shortScreen)
            {
                actionTop=heroTop;
                float actionLeft=_detailPanel.rect.width*.43f;
                SetStretchTop(_detailTitle.rectTransform,hero+margin*2,heroTop,_detailPanel.rect.width-actionLeft+margin,titleHeight);
                SetStretchTop(_actionButtonsContent,actionLeft,actionTop,margin,button);
            }
            else SetStretchTop(_actionButtonsContent,margin,actionTop,margin,button);
            foreach(Transform child in _actionButtonsContent){var le=child.GetComponent<LayoutElement>();if(le!=null){le.preferredHeight=le.minHeight=button;le.minWidth=1;}}
            float saveHeight=button+margin*2;
            var save=(RectTransform)_detailPanel.Find("SaveManifestRow");SetStretchBottom(save,margin,margin,margin,button);
            save.GetComponent<HorizontalLayoutGroup>().spacing=Mathf.Max(12,8/scale);
            foreach(Transform child in save){var le=child.GetComponent<LayoutElement>();if(le!=null){le.preferredHeight=button;if(child.GetComponent<Button>()!=null)le.preferredWidth=Mathf.Max(le.preferredWidth,120/scale);}}
            float formTop=actionTop+(shortScreen?Mathf.Max(hero,button):button)+margin;
            SetStretchTop(_menuFormTabs,margin,formTop,margin,button);
            SetStretchOffsets(_manifestFormScrollRect,margin,saveHeight,margin,formTop+button);
            _menuFormTabs.GetComponent<LayoutElement>().preferredHeight=button;_menuFormTabs.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,button);
            _manifestFieldLayout.cellSize=new Vector2(_manifestFieldLayout.cellSize.x,Mathf.Max(70,touch)+Mathf.Max(40,fontMin*1.65f)+12);
            foreach(var field in _menuFields)
            {
                var caption=field.item.Find("Label") as RectTransform;if(caption!=null)caption.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(40,fontMin*1.65f));
                var input=field.item.Find("Field") as RectTransform;if(input!=null)input.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(70,touch));
            }
            SetAnchor(_menuMore,new Vector2(1,1),new Vector2(1,1),new Vector2(1,1),new Vector2(-margin,-actionTop-button-8),new Vector2(Mathf.Max(240,touch*3),button*3+36));
            foreach(Transform child in _menuMore){var le=child.GetComponent<LayoutElement>();le.minHeight=le.preferredHeight=button;}
            // Settings dialog uses the available canvas rather than a fixed 960-unit height.
            SetAnchor(_menuSettingsDialog,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2((w*scale<1100?w-margin*2:Mathf.Min(w-margin*2,1500)),h-margin*2));
            float navHeight=button;
            SetStretchTop(_menuSettingsDialog.Find("Title") as RectTransform,margin,margin,margin,Mathf.Max(66,fontMin*1.8f));
            SetStretchTop(_menuSettingsNavigation,margin,margin+Mathf.Max(60,fontMin*2.2f),margin,navHeight);
            SetStretchOffsets(_menuSettingsScroll,margin,button+margin*3,margin,margin+Mathf.Max(60,fontMin*2.2f)+navHeight);
            var footer=_menuSettingsDialog.Find("ButtonRow") as RectTransform;SetStretchBottom(footer,margin,margin,margin,button);
            foreach(Transform child in footer){var le=child.GetComponent<LayoutElement>();if(le!=null){le.preferredWidth=Mathf.Max(150,touch*2);le.preferredHeight=button;((RectTransform)child).sizeDelta=new Vector2(le.preferredWidth,button);}}
            foreach(var le in _menuSettingsScroll.GetComponentsInChildren<LayoutElement>(true))
            {
                if(!_menuHeights.TryGetValue(le,out var original)){original=new Vector2(le.minHeight,le.preferredHeight);_menuHeights[le]=original;}
                if(original.y>0)le.preferredHeight=Mathf.Max(original.y,touch+fontMin+32);
                if(original.x>0)le.minHeight=Mathf.Max(original.x,touch);
            }
            foreach(var pair in _menuSliders)
            {
                var field=(RectTransform)pair.input.transform;var row=(RectTransform)field.parent;
                SetStretchTop(row.Find("Label") as RectTransform,0,0,0,Mathf.Max(40,fontMin*1.6f));
                SetAnchor(field,new Vector2(1,0),Vector2.one,new Vector2(1,.5f),new Vector2(0,-(fontMin+16)/2),new Vector2(Mathf.Max(145,80/scale),-(fontMin+16)));
                SetStretchOffsets((RectTransform)pair.slider.transform,0,0,Mathf.Max(161,90/scale),fontMin+16);
            }
            foreach(var item in _menuSettingsItems)
            {
                // Legacy selector rows used fixed-height children. Preserve their arrangement, enlarge hit targets.
                foreach(var b in item.item.GetComponentsInChildren<Button>(true))
                {
                    var rect=(RectTransform)b.transform;
                    if(Mathf.Approximately(rect.anchorMin.y,rect.anchorMax.y))rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(56,touch));
                }
            }
            if(_menuThemeChoice!=null)
            {
                float captionHeight=Mathf.Max(40,fontMin*1.7f);
                var themeRow=_menuThemeChoice.transform.parent;
                themeRow.GetComponent<LayoutElement>().preferredHeight=captionHeight+button+8;
                themeRow.Find("Label").GetComponent<LayoutElement>().preferredHeight=captionHeight;
                foreach(var le in _menuThemeChoice.GetComponentsInChildren<LayoutElement>(true))le.minHeight=le.preferredHeight=button;
            }
        }
    }
}
