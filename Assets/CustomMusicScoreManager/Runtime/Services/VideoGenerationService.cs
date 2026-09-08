using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Sekai.CustomMusicScoreManager
{
	public sealed class VideoGenerationService : MonoBehaviour
	{
		private static VideoGenerationService instance;
		public static VideoGenerationService Instance
		{
			get
			{
				if (instance == null)
				{
					var go = new GameObject("[VideoGenerationService]");
					instance = go.AddComponent<VideoGenerationService>();
					DontDestroyOnLoad(go);
				}
				return instance;
			}
		}

		public enum VideoEncodingFormat { ImageSequence, MP4, WebM }
		public bool IsRecording { get; private set; }
		public int RecordedFrameCount { get; private set; }
		public float RecordingDuration => IsRecording ? Time.realtimeSinceStartup - startTime : duration;
		public string RecordedAudioPath { get; private set; }
		private int targetWidth = 1920, targetHeight = 1080, targetFrameRate = 30;
		private RenderTexture screenTexture, outputTexture;
		private Texture2D frameTexture;
		private AudioRecorder audioRecorder;
		private Coroutine recording;
		private string directory;
		private float startTime, duration;
		private byte[] lastFrame;
		private bool previousRunInBackground;
		private int previousSleepTimeout;

		public void Configure(int width, int height, int frameRate, VideoEncodingFormat format)
		{
			if (IsRecording) return;
			targetWidth = width; targetHeight = height; targetFrameRate = frameRate;
		}

		// Kept for callers; capture always uses the final framebuffer, including all cameras and UI.
		public void SetTargetCamera(Camera camera) { }

		public bool StartRecording(Camera camera = null, int? width = null, int? height = null, int? frameRate = null)
		{
			if (IsRecording) return false;
			targetWidth = width ?? targetWidth; targetHeight = height ?? targetHeight; targetFrameRate = frameRate ?? targetFrameRate;
			if (targetWidth <= 0 || targetHeight <= 0 || targetFrameRate <= 0 || targetFrameRate > 120) return false;
			try
			{
				string supportError = AndroidVideoEncoder.CheckSupport(targetWidth, targetHeight, targetFrameRate);
				if (!string.IsNullOrEmpty(supportError)) throw new InvalidOperationException(supportError);
				directory = Path.Combine(Application.temporaryCachePath, "VideoGeneration", Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(directory);
				outputTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
				outputTexture.Create();
				frameTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
				audioRecorder = GetComponent<AudioRecorder>() ?? gameObject.AddComponent<AudioRecorder>();
				RecordedAudioPath = Path.Combine(directory, "game_audio.wav");
				audioRecorder.StartRecording(RecordedAudioPath);
				RecordedFrameCount = 0; duration = 0; lastFrame = null;
				startTime = Time.realtimeSinceStartup;
				previousRunInBackground = Application.runInBackground;
				previousSleepTimeout = Screen.sleepTimeout;
				Screen.sleepTimeout = SleepTimeout.NeverSleep;
				Application.runInBackground = true;
				IsRecording = true;
				recording = StartCoroutine(RecordFrames());
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogError("[VideoGenerationService] Cannot start recording: " + ex.Message);
				ReleaseResources();
				return false;
			}
		}

		private IEnumerator RecordFrames()
		{
			var endOfFrame = new WaitForEndOfFrame();
			while (IsRecording)
			{
				yield return endOfFrame;
				if (!IsRecording) yield break;
				int expected = Mathf.FloorToInt(RecordingDuration * targetFrameRate) + 1;
				if (expected <= RecordedFrameCount) continue;
				try
				{
					// Preserve real time when rendering misses a deadline; never accelerate video
					// relative to the independently running audio engine.
					while (lastFrame != null && RecordedFrameCount + 1 < expected) WriteFrame(lastFrame);
					lastFrame = CaptureFinalFrame();
					WriteFrame(lastFrame);
				}
				catch (Exception ex)
				{
					VideoGenerationController.Instance.HandleRecordingFailure("画面捕获失败：" + ex.Message);
					yield break;
				}
			}
		}

		// Call only after WaitForEndOfFrame, after URP camera stacks and overlay canvases.
		private byte[] CaptureFinalFrame()
		{
			if (screenTexture == null || screenTexture.width != Screen.width || screenTexture.height != Screen.height)
			{
				if (screenTexture != null) { screenTexture.Release(); Destroy(screenTexture); }
				screenTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
				screenTexture.Create();
			}
			ScreenCapture.CaptureScreenshotIntoRenderTexture(screenTexture);
			RenderTexture previous = RenderTexture.active;
			try
			{
				// D3D screenshot textures use a top-left origin; ReadPixels/JPG use bottom-left.
				bool flipY = SystemInfo.graphicsUVStartsAtTop;
				Graphics.Blit(screenTexture, outputTexture, new Vector2(1, flipY ? -1 : 1), new Vector2(0, flipY ? 1 : 0));
				RenderTexture.active = outputTexture;
				frameTexture.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0, false);
				frameTexture.Apply(false, false);
				return frameTexture.EncodeToJPG(90);
			}
			finally { RenderTexture.active = previous; }
		}

		private void WriteFrame(byte[] frame)
		{
			File.WriteAllBytes(Path.Combine(directory, $"frame_{RecordedFrameCount + 1:D6}.jpg"), frame);
			RecordedFrameCount++;
		}

		public string StopRecording(string outputPath = null)
		{
			if (!IsRecording) return null;
			duration = RecordingDuration;
			IsRecording = false;
			if (recording != null) StopCoroutine(recording);
			recording = null;
			try
			{
				RecordedAudioPath = audioRecorder.StopRecording();
				if (RecordedAudioPath == null) throw new InvalidOperationException("未捕获到游戏音频，请重新录制。");
				while (lastFrame != null && RecordedFrameCount < Mathf.CeilToInt(duration * targetFrameRate)) WriteFrame(lastFrame);
				if (RecordedFrameCount == 0) throw new InvalidOperationException("未捕获到游戏画面。");
				File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(new VideoManifest
				{
					width = targetWidth, height = targetHeight, frameRate = targetFrameRate,
					frameCount = RecordedFrameCount, duration = RecordedFrameCount / (float)targetFrameRate,
					format = "ImageSequence", createdTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
				}, true));
				// Each session owns its directory; retain it on failure for diagnosis/retry.
				return directory;
			}
			catch (Exception ex)
			{
				Debug.LogError("[VideoGenerationService] Cannot finish recording: " + ex.Message);
				return null;
			}
			finally { Application.runInBackground = previousRunInBackground; Screen.sleepTimeout = previousSleepTimeout; ReleaseResources(); }
		}

		public void CancelRecording()
		{
			if (IsRecording) { Application.runInBackground = previousRunInBackground; Screen.sleepTimeout = previousSleepTimeout; }
			IsRecording = false;
			if (recording != null) StopCoroutine(recording);
			recording = null;
			ReleaseResources();
		}

		private void ReleaseResources()
		{
			audioRecorder?.ClearRecording();
			if (screenTexture != null) { screenTexture.Release(); Destroy(screenTexture); screenTexture = null; }
			if (outputTexture != null) { outputTexture.Release(); Destroy(outputTexture); outputTexture = null; }
			if (frameTexture != null) { Destroy(frameTexture); frameTexture = null; }
			lastFrame = null;
		}

		private void OnDestroy()
		{
			CancelRecording();
			if (instance == this) instance = null;
		}

		public static string GetDefaultOutputPath() => Path.Combine(Application.temporaryCachePath, "VideoRecordings", $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}");
		public static string[] GetSupportedFormats() => new[] { "ImageSequence", "MP4", "WebM" };
	}

	[Serializable]
	internal class VideoManifest
	{
		public int width, height, frameRate, frameCount;
		public float duration;
		public string format, createdTime;
	}
}
