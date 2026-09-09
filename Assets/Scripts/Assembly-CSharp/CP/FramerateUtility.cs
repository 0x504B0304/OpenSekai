using UnityEngine;

namespace CP
{
	public static class FramerateUtility
	{
		public const int DefaultFrameRate = 60;
		public static readonly System.Collections.Generic.IReadOnlyList<int> FrameRateOptions =
			System.Array.AsReadOnly(new[] { 0, 30, 60, 90, 120, 144, 165, 240 });

		public static int GetConfiguredFrameRate(Sekai.LiveSettingData settings)
		{
			int value = settings.MaxFrameRate ?? (settings.Use120FPS ? 120 : 0);
			foreach (int option in FrameRateOptions)
				if (value == option) return value;
			return 0;
		}

		public static void SetFrameRate(int targetFrameRate = -1)
		{
			int configured = targetFrameRate > 0 ? targetFrameRate : GetConfiguredFrameRate(Sekai.LiveSettingData.LoadFromStorage());
			int resolved = configured > 0 ? configured : GetFeasibleFramerate();
#if UNITY_ANDROID && !UNITY_EDITOR
			try
			{
				using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
				int requested = activity.Call<int>("setMaximumFrameRate", configured);
				if (configured == 0 && requested > 0) resolved = requested;
			}
			catch (System.Exception exception)
			{
				Debug.LogWarning("Could not request Android refresh rate: " + exception.Message);
			}
#endif
			// Desktop vSync otherwise overrides Application.targetFrameRate.
			QualitySettings.vSyncCount = 0;
			Application.targetFrameRate = resolved;
		}

		private static int GetFeasibleFramerate()
		{
#if UNITY_2022_2_OR_NEWER
			RefreshRate refreshRateRatio = Screen.currentResolution.refreshRateRatio;
			if (refreshRateRatio.numerator > 0 && refreshRateRatio.denominator > 0)
			{
				double refreshRate = (double)refreshRateRatio.numerator / refreshRateRatio.denominator;
				return Mathf.Max(1, Mathf.RoundToInt((float)refreshRate));
			}
#endif

#pragma warning disable CS0618
			int legacyRefreshRate = Screen.currentResolution.refreshRate;
#pragma warning restore CS0618
			return legacyRefreshRate > 0 ? legacyRefreshRate : DefaultFrameRate;
		}
	}
}
