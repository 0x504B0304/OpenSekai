using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Sekai.CustomMusicScoreManager
{
	public sealed class VideoCompletionHandler : MonoBehaviour
	{
		private static VideoCompletionHandler instance;
		public static VideoCompletionHandler Instance
		{
			get
			{
				if (instance == null)
				{
					var go = new GameObject("[VideoCompletionHandler]");
					instance = go.AddComponent<VideoCompletionHandler>();
					DontDestroyOnLoad(go);
				}
				return instance;
			}
		}

		private VideoExportOverlay overlay;
		private bool processing, saving, previousRunInBackground;
		private int previousSleepTimeout;
		private string pendingVideo;
		private string scoreTitle;
		private DateTime startTime;

		public void StartCompletionFlow()
		{
			if (processing) return;
			if (overlay != null) Destroy(overlay.gameObject);
			overlay = VideoExportOverlay.Create();
			if (!VideoGenerationController.Instance.HasCompleteRecordingData())
			{
				overlay.ShowResult(false, "录制数据不完整，无法生成视频。");
				return;
			}
			processing = true;
			previousRunInBackground = Application.runInBackground;
			previousSleepTimeout = Screen.sleepTimeout;
			Screen.sleepTimeout = SleepTimeout.NeverSleep;
			Application.runInBackground = true;
			scoreTitle = VideoGenerationController.Instance.ScoreTitle;
			startTime = VideoGenerationController.Instance.StartTime;
			// Allow at least one rendered frame for the progress UI before probing/encoding.
			StartCoroutine(BeginProcessing());
		}

		private IEnumerator BeginProcessing()
		{
			yield return null;
			if (!processing) yield break;
			try
			{
				VideoPostProcessor.Instance.StartProcessing(
					onComplete: Save,
					onError: Fail,
					onProgress: (value, message) => { if (overlay != null) overlay.UpdateProgress(value * 0.95f, message); });
			}
			catch (Exception ex) { Fail(ex.Message); }
		}

		private void Save(string temporaryVideo)
		{
			saving = true;
			pendingVideo = temporaryVideo;
			if (overlay != null) overlay.UpdateProgress(0.97f, "正在保存视频…");
			VideoSaveHelper.Instance.SaveVideo(temporaryVideo, scoreTitle, startTime,
				onComplete: finalPath =>
				{
					Finish();
					bool inGallery = VideoSaveHelper.IsContentUri(finalPath);
					if (overlay != null) overlay.ShowResult(true, inGallery ? "已保存到相册：OpenSekai_Rec" : "保存路径：\n" + finalPath, () => OpenFile(finalPath));
					// Only this successfully saved intermediate file belongs to this operation.
					if (inGallery || !string.Equals(Path.GetFullPath(temporaryVideo), Path.GetFullPath(finalPath), StringComparison.OrdinalIgnoreCase))
					{
						try { File.Delete(temporaryVideo); }
						catch (IOException ex) { Debug.LogWarning(ex.Message); }
					}
					VideoGenerationController.ClearRecordingData();
					pendingVideo = null;
				},
				onError: Fail,
				onProgress: value => { if (overlay != null) overlay.UpdateProgress(0.95f + value * 0.05f, "正在保存到相册…"); });
		}

		public void ShowRecordingError(string message)
		{
			if (overlay == null) overlay = VideoExportOverlay.Create();
			Fail(message);
		}

		private void Fail(string message)
		{
			bool canRetrySave = saving && !string.IsNullOrEmpty(pendingVideo) && File.Exists(pendingVideo);
			bool canRetryEncoding = !saving && VideoGenerationController.Instance.HasCompleteRecordingData();
			Finish();
			if (overlay == null) overlay = VideoExportOverlay.Create();
			overlay.ShowResult(false, message,
				canRetrySave ? (Action)RetrySave : canRetryEncoding ? StartCompletionFlow : (Action)null,
				canRetrySave ? "重试保存" : "重试生成");
			Debug.LogError("[VideoCompletionHandler] " + message);
			// Preserve the recording and encoded file when saving fails, so they can be recovered.
		}

		private void Finish()
		{
			if (processing) { Application.runInBackground = previousRunInBackground; Screen.sleepTimeout = previousSleepTimeout; }
			processing = false;
			saving = false;
		}

		private void RetrySave()
		{
			if (processing) return;
			previousRunInBackground = Application.runInBackground;
			previousSleepTimeout = Screen.sleepTimeout;
			Application.runInBackground = true; Screen.sleepTimeout = SleepTimeout.NeverSleep;
			processing = true;
			Save(pendingVideo);
		}

		private void OnApplicationPause(bool paused)
		{
			// Recording requires the foreground framebuffer; permission/share dialogs during
			// saving are allowed. Encoding cancellation retains source frames for recovery.
			if (paused && Application.platform == RuntimePlatform.Android && processing && !saving)
			{
				VideoPostProcessor.Instance.CancelProcessing();
				Fail("应用已切到后台，视频生成已中断。录制素材已保留，请保持应用在前台完成导出。");
			}
		}

		private void OpenFile(string path)
		{
			try
			{
#if UNITY_ANDROID && !UNITY_EDITOR
				using (var helper = new AndroidJavaClass("com.opensekai.ShareExportHelper"))
					helper.CallStatic("ShareVideo", path);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
				{
					FileName = "explorer.exe",
					Arguments = "/select,\"" + path.Replace('/', '\\') + "\"",
					UseShellExecute = true
				});
#else
				Application.OpenURL(new Uri(Path.GetDirectoryName(path)).AbsoluteUri);
#endif
			}
			catch (Exception ex) { overlay?.ShowResult(false, ex.Message); }
		}

		private void OnDestroy()
		{
			if (processing) VideoPostProcessor.Instance.CancelProcessing();
			Finish();
			if (overlay != null) Destroy(overlay.gameObject);
			if (instance == this) instance = null;
		}
	}
}
