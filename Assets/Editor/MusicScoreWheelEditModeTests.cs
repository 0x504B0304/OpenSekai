#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using Sekai.MusicScoreMaker.Ingame.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MusicScoreWheelEditModeTests
{
    private GameObject _root;
    private GameObject _eventSystemObject;
    private MusicScoreMakerView _view;
    private MusicScorePreview _preview;
    private GameObject _note;
    private MusicScoreMakerEventDispatcher _dispatcher;
    private bool _ownsDispatcher;
    private long _focus;
    private long _max;
    private float _scale;
    private bool _playing;
    private int _focusChanges;
    private int _scaleChanges;
    private float _oldStep, _oldMin, _oldMax;

    [SetUp]
    public void SetUp()
    {
        _ownsDispatcher = !MusicScoreMakerEventDispatcher.ExistsInstance;
        if (_ownsDispatcher)
        {
            _dispatcher = new GameObject("WheelTestDispatcher").AddComponent<WheelTestDispatcher>();
            _dispatcher.SetupInstance();
        }
        else _dispatcher = MusicScoreMakerEventDispatcher.Instance;
        _dispatcher.Register<GetFocusTicksEvent, long>(GetFocus);
        _dispatcher.Register<GetMusicScoreTicksMaxEvent, long>(GetMax);
        _dispatcher.Register<GetCurrentMusicScoreScaleEvent, float>(GetScale);
        _dispatcher.Register<IsMusicPlayingEvent, bool>(IsPlaying);
        _dispatcher.Register<SetFocusTicksEvent>(SetFocus);
        _dispatcher.Register<SetZoomTimelineScaleEvent>(SetScale);
        _oldStep = MusicScoreMakerSettingsManager.ZoomTimelineStep;
        _oldMin = MusicScoreMakerSettingsManager.ZoomTimelineScaleMin;
        _oldMax = MusicScoreMakerSettingsManager.ZoomTimelineScaleMax;
        MusicScoreMakerSettingsManager.ZoomTimelineStep = .1f;
        MusicScoreMakerSettingsManager.ZoomTimelineScaleMin = .1f;
        MusicScoreMakerSettingsManager.ZoomTimelineScaleMax = 8f;
        _focus = 1000; _max = 10000; _scale = 1; _playing = false;
        _focusChanges = _scaleChanges = 0;

        _eventSystemObject = new GameObject("WheelTestEventSystem", typeof(EventSystem));
        _root = new GameObject("WheelTestEditor", typeof(RectTransform));
        _view = _root.AddComponent<MusicScoreMakerView>();
        var previewObject = new GameObject("Preview", typeof(RectTransform));
        previewObject.transform.SetParent(_root.transform, false);
        _preview = previewObject.AddComponent<MusicScorePreview>();
        SetField(_preview, "_rectTransform", (RectTransform)previewObject.transform);
        SetField(_view, "_musicScorePreview", _preview);
        _note = new GameObject("Note", typeof(RectTransform));
        _note.transform.SetParent(previewObject.transform, false);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
        Object.DestroyImmediate(_eventSystemObject);
        _dispatcher.Remove<GetFocusTicksEvent, long>(GetFocus);
        _dispatcher.Remove<GetMusicScoreTicksMaxEvent, long>(GetMax);
        _dispatcher.Remove<GetCurrentMusicScoreScaleEvent, float>(GetScale);
        _dispatcher.Remove<IsMusicPlayingEvent, bool>(IsPlaying);
        _dispatcher.Remove<SetFocusTicksEvent>(SetFocus);
        _dispatcher.Remove<SetZoomTimelineScaleEvent>(SetScale);
        MusicScoreMakerSettingsManager.ZoomTimelineStep = _oldStep;
        MusicScoreMakerSettingsManager.ZoomTimelineScaleMin = _oldMin;
        MusicScoreMakerSettingsManager.ZoomTimelineScaleMax = _oldMax;
        if (_ownsDispatcher) Object.DestroyImmediate(_dispatcher.gameObject);
    }

    [Test]
    public void WheelBubblesFromNotesAndScrollsInBothDirections()
    {
        var input = Wheel(2);
        Assert.AreSame(_view.gameObject, ExecuteEvents.ExecuteHierarchy(_note, input, ExecuteEvents.scrollHandler));
        Assert.AreEqual(1384, _focus);
        Assert.IsTrue(input.used);
        _preview.HandleMouseWheel(Wheel(-2), false);
        Assert.AreEqual(1000, _focus);
        Assert.AreEqual(0, _scaleChanges);
    }

    [Test]
    public void ScrollStepTracksVisibleRangeAndRetainsSmallDeltas()
    {
        _scale = 2;
        _preview.HandleMouseWheel(Wheel(1), false);
        Assert.AreEqual(1384, _focus);
        _focus = 0; _scale = 1;
        for (int i = 0; i < 10; i++) _preview.HandleMouseWheel(Wheel(.001f), false);
        Assert.AreEqual(1, _focus);
    }

    [Test]
    public void ScrollingClampsAtBothEndsWithoutAccumulatingOverscroll()
    {
        _focus = 0;
        _preview.HandleMouseWheel(Wheel(-100), false);
        Assert.AreEqual(0, _focus);
        _preview.HandleMouseWheel(Wheel(1), false);
        Assert.AreEqual(192, _focus);
        _preview.HandleMouseWheel(Wheel(1000), false);
        Assert.AreEqual(_max, _focus);
        _preview.HandleMouseWheel(Wheel(-1), false);
        Assert.AreEqual(_max - 192, _focus);
    }

    [Test]
    public void ControlWheelZoomsInsteadOfScrollingAndUsesExistingLimits()
    {
        _preview.HandleMouseWheel(Wheel(1), true);
        Assert.AreEqual(.9f, _scale, .0001f);
        _preview.HandleMouseWheel(Wheel(-1), true);
        Assert.AreEqual(1f, _scale, .0001f);
        _preview.HandleMouseWheel(Wheel(1000), true);
        Assert.AreEqual(.1f, _scale);
        _preview.HandleMouseWheel(Wheel(-1000), true);
        Assert.AreEqual(8f, _scale);
        Assert.AreEqual(1000, _focus);
        Assert.AreEqual(0, _focusChanges);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PlaybackBlocksScrollAndZoom(bool zoom)
    {
        _playing = true;
        var input = Wheel(1);
        _preview.HandleMouseWheel(input, zoom);
        Assert.IsFalse(input.used);
        Assert.AreEqual(0, _focusChanges + _scaleChanges);
    }

    [TestCase(0f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidWheelDeltasAreIgnored(float delta)
    {
        _preview.HandleMouseWheel(Wheel(delta), false);
        Assert.AreEqual(0, _focusChanges + _scaleChanges);
    }

    [Test]
    public void PanelsControlsAndPointsOutsideThePreviewDoNotScrollChart()
    {
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(_root.transform, false);
        _preview.HandleMouseWheel(Wheel(1, panel), false);
        _note.AddComponent<Button>();
        _preview.HandleMouseWheel(Wheel(1), false);
        Object.DestroyImmediate(_note.GetComponent<Button>());
        var outside = Wheel(1);
        outside.position = new Vector2(1000, 1000);
        _preview.HandleMouseWheel(outside, false);
        Assert.AreEqual(0, _focusChanges);
    }

    [Test]
    public void SelectedGroupOverlayCanStillScrollTheViewport()
    {
        var selection = new GameObject("SelectedGroup", typeof(RectTransform));
        selection.transform.SetParent(_root.transform, false);
        selection.AddComponent<SelectedObjectEditUIView>();
        var input = Wheel(1, selection);
        ExecuteEvents.ExecuteHierarchy(selection, input, ExecuteEvents.scrollHandler);
        Assert.IsTrue(input.used);
        Assert.AreEqual(1192, _focus);
    }

    [Test]
    public void ConsumedInputIsNotAppliedTwice()
    {
        var input = Wheel(1);
        _preview.HandleMouseWheel(input, false);
        _preview.HandleMouseWheel(input, false);
        Assert.AreEqual(1, _focusChanges);
    }

    [Test]
    public void WheelIsIndependentOfTouchSwipeSetting()
    {
        bool oldSwipe = MusicScoreMakerSettingsManager.EnableSwipeScroll;
        try
        {
            MusicScoreMakerSettingsManager.EnableSwipeScroll = false;
            _preview.HandleMouseWheel(Wheel(1), false);
            Assert.AreEqual(1192, _focus);
        }
        finally { MusicScoreMakerSettingsManager.EnableSwipeScroll = oldSwipe; }
    }

    [Test]
    public void RealPrefabRoutesPreviewAndSelectionScrollToEditorView()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/screen/prefabs/ScreenLayerMusicScoreMaker.prefab");
        var preview = prefab.GetComponentInChildren<MusicScorePreview>(true);
        var selection = prefab.GetComponentInChildren<SelectedObjectEditUIView>(true);
        Assert.IsNotNull(preview);
        Assert.IsNotNull(selection);
        Assert.IsNotNull(preview.GetComponentInParent<MusicScoreMakerView>(true));
        Assert.AreSame(preview.GetComponentInParent<MusicScoreMakerView>(true), selection.GetComponentInParent<MusicScoreMakerView>(true));
        Assert.IsNotNull(preview.GetComponentInParent<IScrollHandler>(true));
        Assert.IsNotNull(selection.GetComponentInParent<IScrollHandler>(true));
    }

    private PointerEventData Wheel(float delta, GameObject target = null) => new PointerEventData(_eventSystemObject.GetComponent<EventSystem>())
    {
        position = Vector2.zero,
        scrollDelta = new Vector2(0, delta),
        pointerCurrentRaycast = new RaycastResult { gameObject = target ?? _note }
    };

    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private long GetFocus(GetFocusTicksEvent _) => _focus;
    private long GetMax(GetMusicScoreTicksMaxEvent _) => _max;
    private float GetScale(GetCurrentMusicScoreScaleEvent _) => _scale;
    private bool IsPlaying(IsMusicPlayingEvent _) => _playing;
    private void SetFocus(SetFocusTicksEvent e) { _focus = e.Ticks; _focusChanges++; }
    private void SetScale(SetZoomTimelineScaleEvent e) { _scale = e.Scale; _scaleChanges++; }
}

public sealed class WheelTestDispatcher : MusicScoreMakerEventDispatcher
{
    public override bool IsDontDestroy() => false;
}
#endif
