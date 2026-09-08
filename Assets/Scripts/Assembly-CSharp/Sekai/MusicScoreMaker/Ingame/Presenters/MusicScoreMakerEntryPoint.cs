using System;
using System.Reflection;
using CP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;

namespace Sekai.MusicScoreMaker.Ingame.Presenters
{
	public class MusicScoreMakerEntryPoint : SceneEntryPoint
	{
		private const string EventSystemPrefabPath = "Common/Input/EventSystem";

		public class MusicScoreMakerBootData
		{
			public ScreenLayerMusicScoreMaker.BootArg bootData { get; set; }

			public MusicScoreMakerBootData()
			{
			}
		}

		public static MusicScoreMakerBootData BootData { get; set; }

		// Separate field to store boot data when returning from editor to settings
		public static ScreenLayerMusicScoreMaker.BootArg BootDataForSettingsReturn { get; set; }

		public ApplicationLocalSettings LocalSettings { get; set; }

		protected override void Awake()
		{
			LogUtility.WriteLog(1, "\u8b5c\u9762\u30c4\u30fc\u30eb\u3078\u9077\u79fb\u3057\u307e\u3057\u305f", System.Array.Empty<object>());
			base.Awake();
			EnsureEventSystem();
			Sekai.TextMeshProUtility.SetupBuiltinFontAsset();
			FramerateUtility.SetFrameRate();
			GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;

			BootData ??= new MusicScoreMakerBootData();

			AssetBundleMetaManager.Instance.Initialize();
			AssetBundleManager.Instance.Initialize();
			SoundManager.Instance.Initialize();
			SoundUtility.SetupGlobalSeSettings(GlobalSeSettings.Normal);

			OtaChecker.Instance.CheckForUpdates();
		}

		protected override void Start()
		{
			LocalSettings = ApplicationLocalSettings.LoadFromStorage();
			if (LocalSettings.LiveVolume == null)
			{
				LocalSettings.LiveVolume = LocalSettings.SetupLiveVolume();
			}

			SoundManager.Instance.SetupVolume(
				1f,
				LocalSettings.SystemVolume.Bgm,
				LocalSettings.SystemVolume.Se,
				LocalSettings.SystemVolume.Voice);

			const string menuCommonSoundBundle = "sound/menu/menu_common";
			if (!SoundManager.Instance.IsLoadedSoundBundle(menuCommonSoundBundle))
			{
				SoundManager.Instance.LoadSoundBundle(menuCommonSoundBundle, true);
			}

			ScreenManager.Instance.CreateEmptyBaseCamera();

			SceneManager sceneManager = SceneManager.Instance;
			bool isEnteredFromTransitionBlank =
				sceneManager.CurrentScene == SceneManager.Scene.TransitionBlank ||
				sceneManager.PrevScene == SceneManager.Scene.TransitionBlank;
			if (isEnteredFromTransitionBlank)
			{
				ScreenManager.Instance.SetScreenCoverDirect(Color.black);
				ScreenManager.Instance.FadeIn(0f, 0.3f, null);
			}
			else
			{
				ScreenManager.Instance.FadeIn(0f, 0f, null);
			}

			ScreenManager.Instance.AddScreen(MenuScreenType.Header);
			ScreenManager.Instance.AddScreen(MenuScreenType.TouchEffect);
			ScreenManager.Instance.AddScreen(MenuScreenType.InsertNoti);

			var bootArg = BootData?.bootData;
			if (bootArg?.FinishTransitionCallback == null)
			{
				LiveTransitioner.SafeForceFinish(null);
			}

			// Check if we need to open settings (returning from editor)
			bool shouldOpenSettings = MusicScoreMakerPresenter._isReturningToEditorWithSettings;
			if (shouldOpenSettings)
			{
				MusicScoreMakerPresenter._isReturningToEditorWithSettings = false;
			}

			if (bootArg == null)
			{
				ScreenManager.Instance.PushUIScreen(MenuScreenType.MusicScoreMakerTop, false);
				// If returning from editor with saved state, open settings overlay
				if (shouldOpenSettings)
				{
					// Delay opening settings to allow the screen to initialize
					ScheduleOpenSettingsAfterPush();
				}
				return;
			}

			ScreenManager.Instance.PushUIScreen(MenuScreenType.MusicScoreMaker, bootArg, false);
		}

		private static void ScheduleOpenSettingsAfterPush()
		{
			// Delay the call to allow the screen to fully initialize
			// Use a coroutine-like approach via MonoBehaviour
			var presenter = new GameObject("SettingsOpener").AddComponent<SettingsOpenerHelper>();
			UnityEngine.Object.DontDestroyOnLoad(presenter);
			presenter.OpenSettingsAfterDelay();
		}

		private class SettingsOpenerHelper : UnityEngine.MonoBehaviour
		{
			public void OpenSettingsAfterDelay()
			{
				StartCoroutine(OpenSettingsCoroutine());
			}

			private System.Collections.IEnumerator OpenSettingsCoroutine()
			{
				UnityEngine.Debug.Log("SettingsOpenerHelper: Starting coroutine");
				yield return null; // Wait one frame
				yield return null; // Wait another frame for the screen to initialize
				UnityEngine.Debug.Log("SettingsOpenerHelper: After delay, trying to find ScreenLayerCustomMusicScoreManager");

				// Use FindObjectsOfTypeAll to find the screen layer regardless of type name
				var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Component>();
				foreach (var obj in allObjects)
				{
					string typeName = obj.GetType().Name;
					if (typeName == "ScreenLayerCustomMusicScoreManager" || typeName.Contains("CustomMusicScore") && typeName.Contains("Manager"))
					{
						UnityEngine.Debug.Log("Found potential manager: " + typeName + " on object: " + obj.name);
						// Check if it has the method we need
						var method = obj.GetType().GetMethod("OpenSettingsAfterReturnFromEditor", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
						if (method != null)
						{
							method.Invoke(null, null);
							UnityEngine.Debug.Log("Successfully invoked OpenSettingsAfterReturnFromEditor via component");
							break;
						}
					}
				}

				Destroy(gameObject);
			}
		}

		protected override void ExitScene()
		{
			base.ExitScene();
		}

		private static void EnsureEventSystem()
		{
			if (EventSystem.current != null || FindObjectOfType<EventSystem>() != null)
			{
				return;
			}

			// Original boot flow creates this in SekaiSingletonManager.OnInitialize.
			// MusicScoreMaker.unity is entered directly in OjskCommunity, so mirror just that required input setup here.
			GameObject prefab = Resources.Load<GameObject>(EventSystemPrefabPath);
			GameObject eventSystemObject = prefab != null
				? Instantiate(prefab)
				: new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
			eventSystemObject.name = "EventSystem";
			DontDestroyOnLoad(eventSystemObject);
		}

		public void OnApplicationPause(bool pause)
		{
			if (pause)
			{
				SoundManager.Instance.Pause();
			}
			else
			{
				SoundManager.Instance.Resume();
			}
		}

		public MusicScoreMakerEntryPoint()
		{
		}
	}
}
