#if UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Input;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Presenters;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using Sekai.MusicScoreMaker.Ingame.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RightDragAreaSelectEditModeTests
{
	private GameObject _eventSystemObject;
	private EventSystem _eventSystem;
	private MusicScoreMakerModel _model;
	private MusicScoreMakerPresenter _presenter;

	[SetUp]
	public void SetUp()
	{
		_eventSystemObject = new GameObject("RightDragAreaSelectEventSystem", typeof(EventSystem));
		_eventSystem = _eventSystemObject.GetComponent<EventSystem>();
		_model = new MusicScoreMakerModel(null)
		{
			MusicScoreMakerData = new MusicScoreMakerData()
		};
		_model.MusicScoreMakerData.AddNote(new MusicScoreNoteBase { id = 1, ticks = 100, laneStart = 1, laneEnd = 2 });
		_model.MusicScoreMakerData.AddNote(new MusicScoreNoteBase { id = 2, ticks = 200, laneStart = 3, laneEnd = 4 });
		_presenter = (MusicScoreMakerPresenter)Activator.CreateInstance(
			typeof(MusicScoreMakerPresenter),
			BindingFlags.Instance | BindingFlags.NonPublic,
			null,
			new object[] { _model, null, null },
			null);
	}

	[TearDown]
	public void TearDown()
	{
		UnityEngine.Object.DestroyImmediate(_eventSystemObject);
	}

	[Test]
	public void DesktopRightDragAlwaysStartsAreaSelectionRegardlessOfDirection()
	{
		PointerEventData pointer = Pointer(PointerEventData.InputButton.Right, Vector2.zero, new Vector2(0f, 30f));
		PointerDown(pointer);
		Drag(pointer);

		Assert.AreEqual("AreaSelect", GetField("_areaSelectDragMode").ToString());
		Assert.IsTrue((bool)GetField("_isRightButtonAreaSelectionActive"));
		Assert.IsFalse(_model.AreaSelectMode, "Temporary right-drag selection must not toggle the persistent mode.");
	}

	[Test]
	public void RightDragFromNoteUsesTheSameAreaSelectionPath()
	{
		PointerEventData pointer = Pointer(PointerEventData.InputButton.Right, Vector2.zero, new Vector2(0f, 30f));
		SelectedTargetOperation operationBeforeDrag = _model.MusicScoreMakerData.SelectedTargetOperation;
		Invoke("OnNotePreviewDrag", new OnNotePreviewDragEvent
		{
			NoteId = 1,
			PointerEventData = pointer,
			NoteTapPosition = SelectedTargetOperation.NoteTapPosition.center
		});

		Assert.AreEqual("AreaSelect", GetField("_areaSelectDragMode").ToString());
		Assert.AreSame(operationBeforeDrag, _model.MusicScoreMakerData.SelectedTargetOperation, "Right-drag must not create a note movement operation.");
		Assert.IsEmpty(_model.MusicScoreMakerData.SelectedTemporaryNoteIdList, "Right-drag must not put the pressed note into the move selection.");
	}

	[Test]
	public void RightDragFromResizeHandleUsesTheSameAreaSelectionPath()
	{
		PointerEventData pointer = Pointer(PointerEventData.InputButton.Right, Vector2.zero, new Vector2(0f, 30f));
		object leftExpandBeforeDrag = _model.MusicScoreMakerData.LeftExpandOperation;
		Invoke("OnExpandInputDrag", new OnExpandInputDragEvent
		{
			PointerEventData = pointer,
			PressPosition = pointer.pressPosition,
			NoteTapPosition = SelectedTargetOperation.NoteTapPosition.left
		});

		Assert.AreEqual("AreaSelect", GetField("_areaSelectDragMode").ToString());
		Assert.AreSame(leftExpandBeforeDrag, _model.MusicScoreMakerData.LeftExpandOperation, "Right-drag must not create a resize operation.");
	}

	[Test]
	public void RightClickDoesNotPlaceOrChangeNoteSelection()
	{
		MusicScoreMakerData data = _model.MusicScoreMakerData;
		data.AddSelectedNote(1);
		PointerEventData pointer = Pointer(PointerEventData.InputButton.Right, Vector2.zero, Vector2.zero);

		Invoke("OnNotePreviewClick", new OnNotePreviewClickEvent { NoteId = 2, PointerEventData = pointer });
		Invoke("OnMusicScorePreviewClick", new OnMusicScorePreviewClickEvent { EventData = pointer });

		CollectionAssert.AreEqual(new[] { 1 }, data.SelectedNoteIdList);
	}

	[Test]
	public void LeftDragKeepsExistingScrollAndAreaSelectionRules()
	{
		PointerEventData vertical = Pointer(PointerEventData.InputButton.Left, Vector2.zero, new Vector2(0f, 30f));
		PointerDown(vertical);
		Drag(vertical);
		Assert.AreEqual("Undecided", GetField("_areaSelectDragMode").ToString(), "Normal mode keeps left drag assigned to viewport scrolling.");

		_model.AreaSelectMode = true;
		PointerDown(vertical);
		Drag(vertical);
		Assert.AreEqual("Scroll", GetField("_areaSelectDragMode").ToString(), "Vertical left drag still scrolls while persistent area select is enabled.");

		PointerEventData horizontal = Pointer(PointerEventData.InputButton.Left, Vector2.zero, new Vector2(30f, 0f));
		PointerDown(horizontal);
		Drag(horizontal);
		Assert.AreEqual("AreaSelect", GetField("_areaSelectDragMode").ToString());
	}

	[Test]
	public void TouchPlatformsDoNotTreatRightButtonDataAsAreaSelection()
	{
		MethodInfo method = typeof(MusicScoreMakerPresenter).GetMethod("IsRightButtonAreaSelectionForPlatform", BindingFlags.Static | BindingFlags.NonPublic);
		Assert.IsNotNull(method);
		PointerEventData pointer = Pointer(PointerEventData.InputButton.Right, Vector2.zero, new Vector2(30f, 0f));
		Assert.IsTrue((bool)method.Invoke(null, new object[] { pointer, true }));
		Assert.IsFalse((bool)method.Invoke(null, new object[] { pointer, false }));
	}

	[Test]
	public void PointerUpAppliesSelectionAndClearsTemporaryRightDragState()
	{
		MusicScoreMakerData data = _model.MusicScoreMakerData;
		data.AddSelectedTemporaryNote(2);
		SetField("_isTemporaryAreaSelectionActive", true);
		SetField("_isRightButtonAreaSelectionActive", true);
		SetAreaSelectDragMode("AreaSelect");
		PointerEventData pointer = Pointer(PointerEventData.InputButton.Right, Vector2.zero, new Vector2(30f, 0f));

		Invoke("OnMusicScorePreviewPointerUp", new OnMusicScorePreviewPointerUpEvent
		{
			EventData = pointer,
			IsDragging = true
		});

		CollectionAssert.AreEqual(new[] { 2 }, data.SelectedNoteIdList);
		Assert.IsEmpty(data.SelectedTemporaryNoteIdList);
		Assert.IsFalse((bool)GetField("_isTemporaryAreaSelectionActive"));
		Assert.IsFalse((bool)GetField("_isRightButtonAreaSelectionActive"));
		Assert.AreEqual("Undecided", GetField("_areaSelectDragMode").ToString());
	}

	[Test]
	public void ArtMoveHandleCombinesHorizontalAndVerticalArrowLayers()
	{
		var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(ToolInputHandler));
		var horizontalObject = new GameObject("IconImage (1)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		horizontalObject.transform.SetParent(handleObject.transform, false);
		try
		{
			Image horizontal = horizontalObject.GetComponent<Image>();
			horizontal.raycastTarget = false;
			MethodInfo method = typeof(SelectedObjectEditUIView).GetMethod("AddVerticalMoveArrow", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(method);
			method.Invoke(null, new object[] { handleObject.GetComponent<ToolInputHandler>() });

			Transform vertical = handleObject.transform.Find("VerticalMoveArrow");
			Assert.IsNotNull(vertical);
			Assert.AreEqual(90f, vertical.localEulerAngles.z, 0.01f);
			Assert.IsFalse(vertical.GetComponent<Image>().raycastTarget);
			Assert.AreEqual(horizontalObject.transform.GetSiblingIndex() + 1, vertical.GetSiblingIndex());
		}
		finally { UnityEngine.Object.DestroyImmediate(handleObject); }
	}

	private PointerEventData Pointer(PointerEventData.InputButton button, Vector2 press, Vector2 position)
	{
		return new PointerEventData(_eventSystem)
		{
			button = button,
			pressPosition = press,
			position = position,
			delta = position - press
		};
	}

	private void PointerDown(PointerEventData pointer)
	{
		Invoke("OnMusicScorePreviewPointerDown", new OnMusicScorePreviewPointerDownEvent { EventData = pointer });
	}

	private void Drag(PointerEventData pointer)
	{
		Invoke("OnMusicScorePreviewDrag", new OnMusicScorePreviewDragEvent { EventData = pointer });
	}

	private void Invoke(string methodName, object argument)
	{
		MethodInfo method = typeof(MusicScoreMakerPresenter).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.IsNotNull(method, methodName);
		method.Invoke(_presenter, new[] { argument });
	}

	private object GetField(string name)
	{
		FieldInfo field = typeof(MusicScoreMakerPresenter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.IsNotNull(field, name);
		return field.GetValue(_presenter);
	}

	private void SetField(string name, object value)
	{
		FieldInfo field = typeof(MusicScoreMakerPresenter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.IsNotNull(field, name);
		field.SetValue(_presenter, value);
	}

	private void SetAreaSelectDragMode(string value)
	{
		FieldInfo field = typeof(MusicScoreMakerPresenter).GetField("_areaSelectDragMode", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.IsNotNull(field);
		field.SetValue(_presenter, Enum.Parse(field.FieldType, value));
	}
}
#endif
