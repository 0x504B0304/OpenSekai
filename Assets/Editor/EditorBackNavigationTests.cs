#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using CP;
using NUnit.Framework;
using Sekai;
using Sekai.MusicScoreMaker.Common;
using Sekai.MusicScoreMaker.Ingame.AudioAssist;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Presenters;
using Sekai.MusicScoreMaker.Ingame.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class EditorBackNavigationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
    private static void HardwareBack(ScreenManager manager)=>Call(manager,"OnBackKey",new List<RaycastResult>());
    private static void ResetBackFrame()=>typeof(AudioAssistSyllableDialog).GetField("backKeyFrame",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,-1);

    [Test]
    public void HardwareBackRoutesOnlyReadyEditorAndRespectsDialogsAndTransitions()
    {
        var root=new GameObject("BackRouting");
        var managerObject=new GameObject("InactiveNavigationHost");managerObject.SetActive(false);
        var manager=managerObject.AddComponent<ScreenManager>();
        var editor=root.AddComponent<ScreenLayerMusicScoreMaker>();
        var dispatcher=root.AddComponent<WheelTestDispatcher>();dispatcher.SetupInstance();
        int editorBack=0,genericBack=0;
        dispatcher.Register<BackKeyPressedEvent>(_=>editorBack++);
        manager.OnBackUIScreenOverride=_=>genericBack++;
        Set(manager,"currentUI",new ScreenLayerState{ScreenLayer=editor});
        ResetBackFrame();
        try
        {
            HardwareBack(manager);Assert.AreEqual(0,genericBack,"Loading must not fall through to a generic screen pop.");
            Set(editor,"_isSetupComplete",true);
            HardwareBack(manager);Assert.AreEqual(1,editorBack);Assert.AreEqual(0,genericBack);
            var inlineObject=new GameObject("InlineMenu");inlineObject.transform.SetParent(root.transform);
            var inline=inlineObject.AddComponent<AudioAssistSyllableDialog>();Set(inline,"mode",AudioAssistSyllableDialog.EditMode.Menu);
            typeof(AudioAssistSyllableDialog).GetField("active",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,inline);
            HardwareBack(manager);Assert.IsFalse(AudioAssistSyllableDialog.IsOpen);
            HardwareBack(manager);Assert.AreEqual(1,editorBack,"The same Escape must not both close a label menu and leave the editor.");
            ResetBackFrame();
            Set(manager,"changeUILayerCoroutine",new List<int>().GetEnumerator());
            HardwareBack(manager);Assert.AreEqual(1,editorBack);
            Set(manager,"changeUILayerCoroutine",null);
            var dialogLayer=new GameObject(DisplayLayerType.Layer_Dialog.ToString());dialogLayer.transform.SetParent(managerObject.transform);
            var dialog=new GameObject("Dialog");dialog.transform.SetParent(dialogLayer.transform);dialog.AddComponent<Common2ButtonDialog>();
            HardwareBack(manager);Assert.AreEqual(1,editorBack,"A modal owns Escape; the editor must remain open.");
            dialog.SetActive(false);
            HardwareBack(manager);Assert.AreEqual(2,editorBack);
            Set(manager,"currentUI",new ScreenLayerState());
            HardwareBack(manager);Assert.AreEqual(1,genericBack,"Other pages retain normal back navigation.");
        }
        finally{Object.DestroyImmediate(root);Object.DestroyImmediate(managerObject);ResetBackFrame();}
    }

    [UnityTest]
    public IEnumerator RepeatedHardwareAndButtonExitSaveAndDiscardTheCachedEditor()
    {
        bool previousOptionsEnabled=EditorSettings.enterPlayModeOptionsEnabled;
        var previousOptions=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode(false);
        var root=new GameObject("EditorExitRegression");
        var host=new GameObject("NavigationHost");host.SetActive(false);host.transform.SetParent(root.transform);
        var manager=host.AddComponent<ScreenManager>();ScreenManager.SetupInstance(manager);
        var editorData=ScriptableObject.CreateInstance<ScreenLayerData>();editorData.ScreenType=MenuScreenType.MusicScoreMaker;
        var topData=ScriptableObject.CreateInstance<ScreenLayerData>();topData.ScreenType=MenuScreenType.MusicScoreMakerTop;
        var top=new ScreenLayerState{Data=topData};
        var editorState=new ScreenLayerState{Data=editorData};
        Set(manager,"screenMap",new Dictionary<MenuScreenType,ScreenLayerState>{{MenuScreenType.MusicScoreMaker,editorState}});
        var stack=new LimitedStack<Tuple<ScreenLayerState,object,ScreenLayer.BootArgBase>>(4);
        stack.Push(Tuple.Create(top,(object)null,(ScreenLayer.BootArgBase)null));Set(manager,"uiScreenStack",stack);
        // Substitute only the home-page animation; production save, disposal,
        // cache removal and next editor instance creation are exercised below.
        manager.OnBackUIScreenOverride=_=>Set(manager,"currentUI",top);
        manager.IsAutoClearBackUIScreenOverride=false;
        string folder=Path.Combine(Path.GetTempPath(),"OpenSekai-Back-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var manifest=new CustomMusicScoreManifest{id="back-regression",title="Back regression",musicDifficultyType="expert"};manifest.Normalize();
        var entry=new CustomMusicScoreEntry(folder,manifest);
        try
        {
            for(int cycle=0;cycle<4;cycle++)
            {
                var go=new GameObject("Editor-"+cycle,typeof(RectTransform));go.transform.SetParent(root.transform);
                var screen=go.AddComponent<ScreenLayerMusicScoreMaker>();var view=go.AddComponent<MusicScoreMakerView>();view.enabled=false;
                var dispatcher=go.AddComponent<WheelTestDispatcher>();dispatcher.SetupInstance();
                var data=entry.LoadScore()??new MusicScoreMakerData();
                data.AddNote(new MusicScoreNoteBase{id=cycle+1,ticks=480*(cycle+1),laneStart=1,laneEnd=2});
                var model=new MusicScoreMakerModel(null){MusicScoreMakerData=data,CustomMusicScoreEntry=entry};
                var presenter=MusicScoreMakerPresenter.Create(model,view,CancellationToken.None,null).GetAwaiter().GetResult();
                Set(screen,"_presenter",presenter);Set(screen,"_MusicScoreMakerView",view);Set(screen,"_isSetupComplete",true);
                Set(presenter,"_fromScreenType",MenuScreenType.MusicScoreMakerTop);Call(presenter,"SetupBackKeyEventDispatcher");
                editorState.RefInstance=go;editorState.ScreenLayer=screen;Set(manager,"currentUI",editorState);ResetBackFrame();
                if(cycle%2==0)HardwareBack(manager);else Call(view,"OnBackButtonClicked");
                Assert.IsNull(editorState.RefInstance,"Exit must clear the cached instance so reopening constructs all controls.");
                Assert.IsNull(editorState.ScreenLayer);
                Assert.AreEqual(cycle+1,entry.LoadScore().NoteList.Count,"Both exit routes must save the edited chart.");
                yield return null;yield return null;
                Assert.IsTrue(go==null,"The old editor must be destroyed before re-entry.");
            }
        }
        finally
        {
            Object.Destroy(root);Object.Destroy(editorData);Object.Destroy(topData);ScreenManager.SetupInstance(null);
            Directory.Delete(folder,true);ResetBackFrame();
            EditorSettings.enterPlayModeOptionsEnabled=previousOptionsEnabled;EditorSettings.enterPlayModeOptions=previousOptions;
        }
        yield return new ExitPlayMode();
    }
}
#endif
