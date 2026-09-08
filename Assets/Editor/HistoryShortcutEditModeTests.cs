#if UNITY_INCLUDE_TESTS
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using Sekai.MusicScoreMaker.Ingame.Views;
using Sekai.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class HistoryShortcutEditModeTests
{
	private GameObject root, eventRoot;
	private MusicScoreMakerView view;
	private CustomButton undo, redo;
	private MusicScoreMakerEventDispatcher dispatcher;
	private bool playing, restricted;
	private int value, undoEvents, redoEvents, saveEvents;
	private int copyEvents, cutEvents, pasteEvents;

	[SetUp]
	public void SetUp()
	{
		Assert.IsFalse(MusicScoreMakerEventDispatcher.ExistsInstance, "Tests require an isolated editor dispatcher.");
		root = new GameObject("HistoryShortcutTest", typeof(RectTransform));
		view = root.AddComponent<MusicScoreMakerView>();
		dispatcher = root.AddComponent<WheelTestDispatcher>();
		dispatcher.SetupInstance();
		dispatcher.Register<UndoEvent>(OnUndo);
		dispatcher.Register<RedoEvent>(OnRedo);
		dispatcher.Register<QuickSaveMusicScoreEvent>(OnSave);
		dispatcher.Register<CopySelectedNotesAndEventsEvent>(OnCopy);
		dispatcher.Register<PasteCopiedNotesAndEventsEvent>(OnPaste);
		dispatcher.Register<IsMusicPlayingEvent, bool>(IsPlaying);
		dispatcher.Register<IsEditRestrictedEvent, bool>(IsRestricted);
		undo = Button("Undo"); redo = Button("Redo");
		SetField("_undoButton", undo); SetField("_redoButton", redo);
		eventRoot = new GameObject("HistoryEventSystem", typeof(HistoryShortcutEventSystem));
		playing = restricted = false;
		value = undoEvents = redoEvents = saveEvents = 0;
		copyEvents = cutEvents = pasteEvents = 0;
		dispatcher.PushUndoableActionAndDoAction(() => value = 0, () => value = 1);
	}

	[TearDown]
	public void TearDown()
	{
		Object.DestroyImmediate(eventRoot);
		Object.DestroyImmediate(root);
	}

	[Test]
	public void UndoAndRedoUseExistingEventsAndHistory()
	{
		Assert.IsTrue(Handle(false));
		Assert.AreEqual(0, value);
		Assert.AreEqual(1, undoEvents);
		Assert.AreEqual(0, redoEvents);
		Assert.IsTrue(Handle(true));
		Assert.AreEqual(1, value);
		Assert.AreEqual(1, undoEvents);
		Assert.AreEqual(1, redoEvents);
	}

	[Test]
	public void EmptyHistoryDoesNotPublishCommands()
	{
		Assert.IsFalse(Handle(true));
		dispatcher.ClearUndoRedoStack();
		Assert.IsFalse(Handle(false));
		Assert.AreEqual(0, undoEvents + redoEvents);
	}

	[Test]
	public void SaveUsesQuickSaveWithoutChangingHistory()
	{
		Assert.IsTrue(HandleSave());
		Assert.AreEqual(1, saveEvents);
		Assert.AreEqual(1, value);
		Assert.AreEqual(0, undoEvents + redoEvents);
		Assert.IsTrue(dispatcher.CanUndo);
		Assert.IsFalse(dispatcher.CanRedo);
	}

	[Test]
	public void SaveDoesNotRequireUndoHistory()
	{
		dispatcher.ClearUndoRedoStack();
		Assert.IsTrue(HandleSave());
		Assert.AreEqual(1, saveEvents);
	}

	[TestCase(true, false)]
	[TestCase(false, true)]
	public void PlayingAndRestrictedEditingBlockHistory(bool isPlaying, bool isRestricted)
	{
		playing = isPlaying; restricted = isRestricted;
		Assert.IsFalse(Handle(false));
		Assert.IsFalse(HandleSave());
		AssertClipboardBlocked();
		Assert.AreEqual(0, saveEvents);
		Assert.AreEqual(1, value);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void DisabledButtonsAreRespected(bool redoCommand)
	{
		if (redoCommand) dispatcher.Undo();
		var button = redoCommand ? redo : undo;
		button.enabled = false;
		Assert.IsFalse(Handle(redoCommand));
		button.enabled = true; button.interactable = false;
		Assert.IsFalse(Handle(redoCommand));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void TextFieldsKeepTheirOwnUndo(bool useTmp)
	{
		var input = new GameObject("TextInput", typeof(RectTransform));
		input.transform.SetParent(root.transform, false);
		if (useTmp) input.AddComponent<TMPro.TMP_InputField>();
		else input.AddComponent<UnityEngine.UI.InputField>();
		EventSystem.current.SetSelectedGameObject(input);
		Assert.IsFalse(Handle(false));
		Assert.IsFalse(HandleSave());
		AssertClipboardBlocked();
		Assert.AreEqual(0, saveEvents);
		Assert.AreEqual(1, value);
	}

	[Test]
	public void HiddenEditorCannotConsumeHistoryShortcut()
	{
		view.enabled = false;
		Assert.IsFalse(Handle(false));
		Assert.IsFalse(HandleSave());
		AssertClipboardBlocked();
		Assert.AreEqual(0, saveEvents);
	}

	[Test]
	public void OpenSubWindowBlocksSaveAndHistory()
	{
		var panel = new GameObject("ShortcutBlockingPanel", typeof(RectTransform));
		panel.transform.SetParent(root.transform, false);
		panel.AddComponent<SubWindowSlideAnimationController>();
		Assert.IsFalse(HandleSave());
		Assert.IsFalse(Handle(false));
		AssertClipboardBlocked();
		Assert.AreEqual(0, saveEvents);
		panel.SetActive(false);
		Assert.IsTrue(HandleSave());
	}

	[TestCase(KeyCode.C, 1, 0, 0)]
	[TestCase(KeyCode.X, 0, 1, 0)]
	[TestCase(KeyCode.V, 0, 0, 1)]
	public void ClipboardShortcutsPublishExactlyOneCommand(KeyCode key, int copies, int cuts, int pastes)
	{
		Assert.IsTrue(HandleClipboard(key));
		Assert.AreEqual(copies, copyEvents);
		Assert.AreEqual(cuts, cutEvents);
		Assert.AreEqual(pastes, pasteEvents);
		Assert.AreEqual(1, value);
		Assert.AreEqual(0, undoEvents + redoEvents + saveEvents);
	}

	[Test]
	public void UnrelatedKeyDoesNotPublishClipboardCommand()
	{
		Assert.IsFalse(HandleClipboard(KeyCode.A));
		Assert.AreEqual(0, copyEvents + cutEvents + pasteEvents);
	}

	[Test]
	public void LatestClipboardUsesInsertionOrderEvenWithIdenticalTimestamps()
	{
		var manager = (ClipboardCacheManager)System.Activator.CreateInstance(typeof(ClipboardCacheManager), true);
		Assert.IsNull(manager.GetLatestCache());
		var caches = (List<ClipboardCacheData>)typeof(ClipboardCacheManager).GetField("_caches", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
		var first = new ClipboardCacheData { CreatedAt = "2026/09/07 12:00:00" };
		var latest = new ClipboardCacheData { CreatedAt = first.CreatedAt };
		caches.Add(first); caches.Add(latest);
		Assert.AreSame(latest, manager.GetLatestCache());
		manager.GetAllCaches();
		Assert.AreSame(latest, manager.GetLatestCache());
	}

	private void AssertClipboardBlocked()
	{
		foreach (var key in new[] { KeyCode.C, KeyCode.X, KeyCode.V }) Assert.IsFalse(HandleClipboard(key));
		Assert.AreEqual(0, copyEvents + cutEvents + pasteEvents);
	}
	private bool HandleClipboard(KeyCode key) => (bool)typeof(MusicScoreMakerView).GetMethod("TryHandleClipboardShortcut", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { key });
	private void OnCopy(CopySelectedNotesAndEventsEvent e) { if (e.IsCut) cutEvents++; else copyEvents++; }
	private void OnPaste(PasteCopiedNotesAndEventsEvent _) { pasteEvents++; }

	private CustomButton Button(string name)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(root.transform, false);
		return go.AddComponent<CustomButton>();
	}
	private void SetField(string name, object value) => typeof(MusicScoreMakerView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, value);
	private bool Handle(bool redoCommand) => (bool)typeof(MusicScoreMakerView).GetMethod("TryHandleHistoryShortcut", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { redoCommand });
	private bool HandleSave() => (bool)typeof(MusicScoreMakerView).GetMethod("TryHandleSaveShortcut", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
	private void OnSave(QuickSaveMusicScoreEvent e) { Assert.IsFalse(e.IsExitOnSave); saveEvents++; }
	private void OnUndo(UndoEvent _) { undoEvents++; dispatcher.Undo(); }
	private void OnRedo(RedoEvent _) { redoEvents++; dispatcher.Redo(); }
	private bool IsPlaying(IsMusicPlayingEvent _) => playing;
	private bool IsRestricted(IsEditRestrictedEvent _) => restricted;
}

[ExecuteAlways]
public sealed class HistoryShortcutEventSystem : EventSystem { }
#endif
