using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

namespace Sekai
{
	// Unity's PerMonitorV2 player owns DPI changes; no OS bitmap scaling is requested.
	public sealed class DisplayFontSampling : MonoBehaviour
	{
		private Vector3 appliedDisplay;
		private Vector3 pendingDisplay;
		private float nextCheck;
		private float stableSince;

#if UNITY_6000_0_OR_NEWER
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Initialize()
		{
			var go = new GameObject(nameof(DisplayFontSampling));
			DontDestroyOnLoad(go);
			var monitor = go.AddComponent<DisplayFontSampling>();
			monitor.appliedDisplay = monitor.pendingDisplay = monitor.ReadDisplay();
			HighQualityDynamicFontProvider.Resample(HighQualityDynamicFontProvider.CalculateSamplingPointSize(
				monitor.appliedDisplay.z, Screen.width, Screen.height));
		}

		private void Update()
		{
			float now = Time.unscaledTime;
			if (now < nextCheck) return;
			nextCheck = now + 0.25f;
			Vector3 display = ReadDisplay();
			if (display != pendingDisplay)
			{
				pendingDisplay = display;
				stableSince = now;
				return;
			}
			if (display == appliedDisplay || now - stableSince < 0.5f) return;
			HighQualityDynamicFontProvider.Resample(HighQualityDynamicFontProvider.CalculateSamplingPointSize(
				display.z, (int)display.x, (int)display.y));
			appliedDisplay = display;
		}
#endif

		private Vector3 ReadDisplay()
		{
			float dpiScale = 1f;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
			// Cache our thread's window, not the foreground window of another application.
			IntPtr activeWindow = GetActiveWindow();
			if (activeWindow != IntPtr.Zero) playerWindow = activeWindow;
			if (playerWindow != IntPtr.Zero && IsWindow(playerWindow))
			{
				uint dpi = GetDpiForWindow(playerWindow);
				if (dpi > 0) dpiScale = dpi / 96f;
			}
#endif
			return new Vector3(Screen.width, Screen.height, dpiScale);
		}

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
		private IntPtr playerWindow;
		[DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
		[DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
		[DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsWindow(IntPtr hwnd);
#endif
	}
}
