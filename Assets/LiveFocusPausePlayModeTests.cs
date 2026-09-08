#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Sekai.Core.Live;
using UnityEngine;

public sealed class LiveFocusPausePlayModeTests
{
	private GameObject root;
	private FocusPauseControllerProbe controller;
	private FocusPauseViewProbe view;

	[SetUp]
	public void SetUp()
	{
		root = new GameObject("FocusPauseTest");
		controller = root.AddComponent<FocusPauseControllerProbe>();
		view = root.AddComponent<FocusPauseViewProbe>();
		typeof(SoloLiveController).GetField("liveViews", BindingFlags.NonPublic | BindingFlags.Instance)
			.SetValue(controller, new LiveViewBase[] { view });
		controller.SetState("Playing");
	}

	[TearDown]
	public void TearDown() => Object.DestroyImmediate(root);

	[Test]
	public void FocusLossPausesViewsAndDoesNotResumeOnFocusGain()
	{
		controller.Focus(false);
		Assert.AreEqual("Pause", controller.State);
		Assert.AreEqual(1, view.PauseCount);
		controller.Focus(true);
		Assert.AreEqual("Pause", controller.State);
		Assert.AreEqual(0, view.ResumeCount);
	}

	[TestCase(true)]
	[TestCase(false)]
	public void FocusAndSuspendMustBothRecoverBeforeExplicitResume(bool regainFocusFirst)
	{
		controller.Focus(false);
		controller.Suspend(true);
		if (regainFocusFirst) controller.Focus(true); else controller.Suspend(false);
		controller.ResumeNoCountDown();
		Assert.IsTrue(controller.SystemPaused);
		Assert.AreEqual("Pause", controller.State);
		if (regainFocusFirst) controller.Suspend(false); else controller.Focus(true);
		Assert.IsFalse(controller.SystemPaused);
		Assert.AreEqual("Pause", controller.State);
		Assert.AreEqual(1, view.PauseCount);
		controller.ResumeNoCountDown();
		Assert.AreEqual("Playing", controller.State);
		Assert.AreEqual(1, view.ResumeCount);
	}

	[Test]
	public void SuspendWithoutFocusEventPausesAndDoesNotAutoResume()
	{
		controller.Suspend(true);
		controller.Suspend(false);
		Assert.AreEqual("Pause", controller.State);
		Assert.AreEqual(1, view.PauseCount);
	}

	[Test]
	public void DuplicateNotificationsDoNotPauseTwice()
	{
		controller.Focus(false);
		controller.Focus(false);
		controller.Suspend(true);
		Assert.AreEqual(1, view.PauseCount);
	}

	[Test]
	public void LosingFocusWhileLoadingStillRequiresResumeAfterLoading()
	{
		controller.SetState("None");
		controller.Focus(false);
		controller.Focus(true);
		controller.FinishLoading();
		Assert.AreEqual("Pause", controller.State);
		Assert.AreEqual(1, view.PauseCount);
	}

	[Test]
	public void FinishedLiveIsNotReopenedByFocusLoss()
	{
		controller.SetState("Finish");
		controller.Focus(false);
		Assert.AreEqual("Finish", controller.State);
		Assert.AreEqual(0, view.PauseCount);
	}

	[Test]
	public void LosingFocusCancelsPendingResumeCountdown()
	{
		controller.Pause();
		controller.Resume();
		Assert.AreEqual("ResumeCountDown", controller.State);
		var staleCountdown = GetCountdown();
		Assert.IsTrue(staleCountdown.MoveNext());
		controller.Focus(false);
		controller.Focus(true);
		Assert.IsNull(typeof(SoloLiveController).GetField("resumeCoroutine", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller));
		Assert.IsFalse(staleCountdown.MoveNext(), "An already scheduled continuation must not restart playback.");
		Assert.AreEqual("Pause", controller.State);
		Assert.AreEqual(0, view.ResumeCount);
	}

	[Test]
	public void ExplicitResumeStillCompletesCountdown()
	{
		controller.Focus(false);
		controller.Focus(true);
		controller.Resume();
		var countdown = GetCountdown();
		Assert.IsTrue(countdown.MoveNext());
		Assert.IsFalse(countdown.MoveNext());
		Assert.AreEqual("Playing", controller.State);
		Assert.AreEqual(1, view.ResumeCount);
	}

	private IEnumerator GetCountdown() => (IEnumerator)typeof(SoloLiveController)
		.GetMethod("ResumeCoroutine", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
}

public sealed class FocusPauseControllerProbe : SoloLiveController
{
	protected override void OnAwake() { }
	protected override void OnUpdate() { }
	protected override void OnExit() { }
	public string State => state.ToString();
	public bool SystemPaused => IsPause;
	public void SetState(string value) => state = (LiveControllerState)System.Enum.Parse(typeof(LiveControllerState), value);
	public void Focus(bool value) => OnApplicationFocus(value);
	public void Suspend(bool value) => OnApplicationPause(value);
	public void FinishLoading() => OnRhythmGameStart();
}

public sealed class FocusPauseViewProbe : LiveViewBase
{
	public int PauseCount;
	public int ResumeCount;
	public override void Pause() => PauseCount++;
	public override void Resume(float time) => ResumeCount++;
}
#endif
