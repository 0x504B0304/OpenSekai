#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Views;
using UnityEngine;
using UnityEngine.UI;

public sealed class PreviewPoolLifecycleEditModeTests
{
    private GameObject root, templates;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("PreviewLifecycleTest", typeof(RectTransform));
        templates = new GameObject("Templates", typeof(RectTransform));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var note in root.GetComponentsInChildren<NotePreview>(true)) note.Dispose();
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(templates);
    }

    [Test]
    public void RepeatedNotesSetupReusesPoolAndResetHidesEveryInstance()
    {
        var view = root.AddComponent<NotesPreview>();
        var template = Child<NotePreview>(templates.transform, "Note");
        Set(template, "_rectTransform", template.GetComponent<RectTransform>());
        Set(view, "_notePreviewPrefab", template);
        var data = new MusicScoreMakerData();
        data.AddNote(new MusicScoreNoteBase { id = 1, ticks = 100, laneStart = 2, laneEnd = 3 });
        view.Setup(2, 0);
        var pool = Get<NotePreviewPool>(view, "_notePreviewPool");
        for (int i = 0; i < 3; i++)
        {
            view.UpdateView(data, 0, 1000, 1f);
            Assert.AreEqual(1, ActiveNotes());
            view.Setup(2, 0);
            Assert.AreSame(pool, Get<NotePreviewPool>(view, "_notePreviewPool"));
            Assert.AreEqual(0, ActiveNotes(), "Re-entry must hide previous chart visuals immediately.");
            Assert.AreEqual(2, root.GetComponentsInChildren<NotePreview>(true).Length);
        }
        view.UpdateView(data, 0, 1000, 1f);
        var note = view.NoteDict[1];
        Vector2 before = note.RectTransform.anchoredPosition;
        view.UpdateView(data, 50, 1050, 1f);
        Assert.AreNotEqual(before, note.RectTransform.anchoredPosition);
        data.ClearNotesAndSpeedEvents();
        view.Refresh();
        view.UpdateView(data, 0, 1000, 1f);
        Assert.AreEqual(0, ActiveNotes());
        Assert.IsEmpty(view.NoteDict);
    }

    [Test]
    public void RepeatedLineSetupRetainsOwnershipAndHidesStaleHitTargets()
    {
        var view = Child<LongNoteLinesPreview>(root.transform, "Lines");
        var template = Child<LongNoteLinePreview>(templates.transform, "Line");
        Set(view, "_longNoteLinePreviewPrefab", template);
        view.Setup(2);
        var acquire = typeof(LongNoteLinesPreview).GetMethod("GetOrCreateLine", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < 3; i++)
        {
            var line = (LongNoteLinePreview)acquire.Invoke(view, new object[] { i });
            line.SetActive(true);
            view.Setup(2);
            Assert.IsFalse(line.gameObject.activeSelf);
            Assert.AreEqual(2, view.GetComponentsInChildren<LongNoteLinePreview>(true).Length);
            Assert.IsEmpty(Get<Dictionary<int, LongNoteLinePreview>>(view, "_activeLines"));
        }
        var last = (LongNoteLinePreview)acquire.Invoke(view, new object[] { 10 });
        last.SetActive(true);
        view.HideAllLines();
        Assert.IsFalse(last.gameObject.activeSelf);
        Assert.AreEqual(2, Get<List<LongNoteLinePreview>>(view, "_linePool").Count);
        view.Setup(4);
        Assert.AreEqual(4, view.GetComponentsInChildren<LongNoteLinePreview>(true).Length);
        view.Setup(1);
        Assert.AreEqual(4, view.GetComponentsInChildren<LongNoteLinePreview>(true).Length);
    }

    [Test]
    public void RepeatedMinimapSetupKeepsOneFrameAndMaterial()
    {
        Assert.IsFalse(MusicScoreMakerEventDispatcher.ExistsInstance);
        var dispatcher = root.AddComponent<WheelTestDispatcher>();
        dispatcher.SetupInstance();
        var view = root.AddComponent<MusicScoreMinimapView>();
        var raw = Child<RawImage>(root.transform, "Map");
        var material = new Material(Shader.Find("UI/Default"));
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
        var allocated = new HashSet<Material>();
        Set(view, "_rawImage", raw);
        Set(view, "_minimapMaterial", material);
        Set(view, "_viewportFrameSprite", sprite);
        try
        {
            for (int i = 0; i < 3; i++)
            {
                view.Setup();
                allocated.Add(raw.material);
            }
            Assert.AreEqual(1, raw.GetComponentsInChildren<Image>(true).Length);
            Assert.AreEqual(1, allocated.Count);
        }
        finally
        {
            raw.material = null;
            foreach (var item in allocated) Object.DestroyImmediate(item);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(sprite);
        }
    }

    private int ActiveNotes() => root.GetComponentsInChildren<NotePreview>(true).Count(n => n.gameObject.activeSelf);
    private static T Child<T>(Transform parent, string name) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
}
#endif
