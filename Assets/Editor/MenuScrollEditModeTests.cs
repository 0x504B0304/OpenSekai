#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using Sekai.CustomMusicScoreManager;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MenuScrollEditModeTests
{
    [TestCase("Assets/Resources/screen/prefabs/ScreenLayerMusicScoreMaker.prefab", 3)]
    [TestCase("Assets/Resources/dialog/MusicScoreMakerCustomQuantizeDialog.prefab", 0)]
    [TestCase("Assets/Resources/dialog/AddMusicScoreEventDataDialog.prefab", 2)]
    public void EditorMenuPrefabsHaveUsableWheelSensitivity(string path, int expectedCount)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.IsNotNull(prefab);
        var scrolls = prefab.GetComponentsInChildren<ScrollRect>(true);
        Assert.AreEqual(expectedCount, scrolls.Length);
        foreach (var scroll in scrolls)
            Assert.AreEqual(48f, scroll.scrollSensitivity, path + "/" + scroll.name);
        foreach (var input in prefab.GetComponentsInChildren<TMPro.TMP_InputField>(true))
            Assert.AreEqual(1f, input.scrollSensitivity, "Text input scrolling must remain unchanged.");
    }

    [TestCase("CreateScrollRect", false)]
    [TestCase("CreateMaskedScrollRect", false)]
    [TestCase("CreateHorizontalScrollRect", true)]
    public void ManagerMenusScrollProportionallyInBothDirections(string factory, bool horizontal)
    {
        var root = new GameObject("MenuScrollTest", typeof(RectTransform));
        try
        {
            var scroll = CreateScroll(factory, root.transform);
            Assert.AreEqual(48f, scroll.scrollSensitivity);
            Assert.AreEqual(horizontal, scroll.horizontal);
            Assert.AreEqual(!horizontal, scroll.vertical);
            Assert.AreEqual(ScrollRect.MovementType.Clamped, scroll.movementType);
            Assert.IsTrue(scroll.inertia);
            Assert.AreEqual(0.135f, scroll.decelerationRate);

            scroll.movementType = ScrollRect.MovementType.Unrestricted;
            scroll.Rebuild(CanvasUpdate.PostLayout);
            var before = scroll.content.anchoredPosition;
            var input = new PointerEventData(null) { scrollDelta = new Vector2(0, -1) };
            scroll.OnScroll(input);
            var step = scroll.content.anchoredPosition - before;
            Assert.AreEqual(horizontal ? 48f : 0f, step.x, 0.01f);
            Assert.AreEqual(horizontal ? 0f : 48f, step.y, 0.01f);
            input.scrollDelta = new Vector2(0, 0.25f);
            scroll.OnScroll(input);
            var total = scroll.content.anchoredPosition - before;
            Assert.AreEqual(step.x * 0.75f, total.x, 0.01f);
            Assert.AreEqual(step.y * 0.75f, total.y, 0.01f);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase("CreateScrollRect")]
    [TestCase("CreateMaskedScrollRect")]
    [TestCase("CreateHorizontalScrollRect")]
    public void WheelSensitivityDoesNotChangePointerDragging(string factory)
    {
        var root = new GameObject("MenuDragTest", typeof(RectTransform));
        try
        {
            var scroll = CreateScroll(factory, root.transform);
            scroll.movementType = ScrollRect.MovementType.Unrestricted;
            scroll.Rebuild(CanvasUpdate.PostLayout);
            Vector2 original = Drag(scroll, 1f);
            Vector2 adjusted = Drag(scroll, 48f);
            Assert.Greater(original.magnitude, 0);
            Assert.Less((original - adjusted).magnitude, 0.01f);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static ScrollRect CreateScroll(string factory, Transform parent)
    {
        var method = typeof(ScreenLayerCustomMusicScoreManager).GetMethod(factory, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method);
        return (ScrollRect)method.Invoke(null, new object[] { "Scroll", parent, null });
    }

    private static Vector2 Drag(ScrollRect scroll, float sensitivity)
    {
        scroll.scrollSensitivity = sensitivity;
        scroll.content.anchoredPosition = Vector2.zero;
        var input = new PointerEventData(null) { button = PointerEventData.InputButton.Left, position = new Vector2(100, 100) };
        scroll.OnBeginDrag(input);
        input.position += new Vector2(60, 60);
        scroll.OnDrag(input);
        scroll.OnEndDrag(input);
        return scroll.content.anchoredPosition;
    }
}
#endif
