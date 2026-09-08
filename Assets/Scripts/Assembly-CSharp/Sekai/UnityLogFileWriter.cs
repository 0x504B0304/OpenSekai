using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Sekai
{
	public static class UnityLogFileWriter
	{
		private const string LogDirectoryName = "OjskCommunityLogs";
		private static readonly object SyncRoot = new object();
		private static StreamWriter writer;
		private static bool initialized;

		public static string CurrentLogFilePath { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Initialize()
		{
			if (initialized)
			{
				return;
			}

			initialized = true;
			try
			{
				string directory = Path.Combine(Application.persistentDataPath, LogDirectoryName);
				Directory.CreateDirectory(directory);
				CurrentLogFilePath = Path.Combine(directory, $"unity_{DateTime.Now:yyyyMMdd_HHmmss}.log");
				writer = new StreamWriter(CurrentLogFilePath, append: false, Encoding.UTF8)
				{
					AutoFlush = true
				};
				writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Info] Unity log file started.");
				writer.WriteLine($"LogPath: {CurrentLogFilePath}");
				writer.WriteLine($"Unity: {Application.unityVersion}");
				writer.WriteLine($"Platform: {Application.platform}");
				writer.WriteLine($"Device: {SystemInfo.deviceModel} / {SystemInfo.operatingSystem}");
				writer.WriteLine();

				Application.logMessageReceivedThreaded += OnLogMessageReceived;
				Application.quitting += Close;
				Debug.Log($"Unity log file: {CurrentLogFilePath}");
			}
			catch
			{
				Close();
			}
		}

		private static void OnLogMessageReceived(string condition, string stackTrace, UnityEngine.LogType type)
		{
			lock (SyncRoot)
			{
				if (writer == null)
				{
					return;
				}

				writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{type}] {condition}");
				if (!string.IsNullOrEmpty(stackTrace) && type != UnityEngine.LogType.Log)
				{
					writer.WriteLine(stackTrace);
				}
			}
		}

		private static void Close()
		{
			lock (SyncRoot)
			{
				if (writer == null)
				{
					return;
				}

				writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Info] Unity log file closed.");
				writer.Dispose();
				writer = null;
			}
		}
	}
}
