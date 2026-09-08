using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sekai.MusicScoreMaker.Common;
using Sekai.MusicScoreMaker.Ingame.Models;
using UnityEngine;

namespace Sekai.CustomMusicScoreManager
{
	public static class BackupService
	{
		private const string BackupDirectoryName = "backup";
		private const string ScoresDirectoryName = "scores";
		private const string SettingsDirectoryName = "settings";

		public delegate void ProgressCallback(string progress);
		public delegate void CompleteCallback(string result);

		private static ProgressCallback _progressCallback;
		private static CompleteCallback _completeCallback;

		public static void StartBackup(string callbackObject, string callbackMethod, string progressCallbackMethod)
		{
			UnityEngine.Debug.Log("[BackupService] StartBackup called");
			UniTask.Create(async () =>
			{
				try
				{
					UnityEngine.Debug.Log("[BackupService] Starting backup process");
					string backupDir = Path.Combine(Application.persistentDataPath, BackupDirectoryName);
					string uuid = Guid.NewGuid().ToString();
					string tempDir = Path.Combine(backupDir, uuid);
					string scoresDir = Path.Combine(tempDir, ScoresDirectoryName);
					string settingsDir = Path.Combine(tempDir, SettingsDirectoryName);

					Directory.CreateDirectory(scoresDir);
					Directory.CreateDirectory(settingsDir);

					// Export all scores
					CustomMusicScoreEntry[] entries = CustomMusicScoreStorage.LoadAllEntries();
					int total = entries.Length;
					UnityEngine.Debug.Log($"[BackupService] Found {total} scores to export");
					for (int i = 0; i < total; i++)
					{
						string progress = $"正在导出谱面... ({i + 1}/{total})";
						UnityEngine.Debug.Log($"[BackupService] Sending progress: {progress}");
						_progressCallback?.Invoke(progress);

						string zipPath = Path.Combine(scoresDir, entries[i].Manifest.id + ".zip");
						CustomMusicScoreManagerService.ExportZip(entries[i], zipPath);
						await UniTask.Yield();
					}

					// Export settings
					_progressCallback?.Invoke("正在导出设置...");

					// Save ApplicationLocalSettings
					ApplicationLocalSettings appSettings = ApplicationLocalSettings.LoadFromStorage();
					string appSettingsPath = Path.Combine(settingsDir, "ApplicationLocalSettings.json");
					string appSettingsJson = JsonConvert.SerializeObject(appSettings, Formatting.Indented);
					File.WriteAllText(appSettingsPath, appSettingsJson);

					// Save LiveSettingData
					LiveSettingData liveSettings = LiveSettingData.LoadFromStorage();
					string liveSettingsPath = Path.Combine(settingsDir, "LiveSettingData.json");
					string liveSettingsJson = JsonConvert.SerializeObject(liveSettings, Formatting.Indented);
					File.WriteAllText(liveSettingsPath, liveSettingsJson);

					// Compress to zip
					_progressCallback?.Invoke("正在压缩备份...");
					string zipFilePath = Path.Combine(backupDir, uuid + ".zip");
					if (File.Exists(zipFilePath))
					{
						File.Delete(zipFilePath);
					}
					ZipFile.CreateFromDirectory(tempDir, zipFilePath, System.IO.Compression.CompressionLevel.Optimal, false);

					// Delete temp directory
					Directory.Delete(tempDir, true);

					UnityEngine.Debug.Log($"[BackupService] Backup complete: {zipFilePath}");
					_completeCallback?.Invoke("success:" + zipFilePath);
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogError("Backup failed: " + ex.Message);
					_completeCallback?.Invoke("error:" + ex.Message);
				}
			}).Forget();
		}

		public static void SetCallbacks(ProgressCallback onProgress, CompleteCallback onComplete)
		{
			_progressCallback = onProgress;
			_completeCallback = onComplete;
		}

		public static void StartRestore(string backupZipPath, bool restoreAll, string callbackObject, string callbackMethod, string progressCallbackMethod)
		{
			UniTask.Create(async () =>
			{
				try
				{
					string tempDir = Path.Combine(Application.persistentDataPath, "backup_restore_temp_" + Guid.NewGuid().ToString("N"));
					Directory.CreateDirectory(tempDir);

					try
					{
						// Extract backup
						_progressCallback?.Invoke("正在解压备份...");
						ZipFile.ExtractToDirectory(backupZipPath, tempDir);

						string scoresDir = Path.Combine(tempDir, ScoresDirectoryName);
						string settingsDir = Path.Combine(tempDir, SettingsDirectoryName);

						// Restore settings if needed
						if (restoreAll && Directory.Exists(settingsDir))
						{
							_progressCallback?.Invoke("正在恢复设置...");

							// Restore ApplicationLocalSettings
							string appSettingsPath = Path.Combine(settingsDir, "ApplicationLocalSettings.json");
							if (File.Exists(appSettingsPath))
							{
								string json = File.ReadAllText(appSettingsPath);
								ApplicationLocalSettings appSettings = JsonConvert.DeserializeObject<ApplicationLocalSettings>(json);
								if (appSettings != null)
								{
									ApplicationLocalSettings.SaveToStorage(appSettings);
								}
							}

							// Restore LiveSettingData
							string liveSettingsPath = Path.Combine(settingsDir, "LiveSettingData.json");
							if (File.Exists(liveSettingsPath))
							{
								string json = File.ReadAllText(liveSettingsPath);
								LiveSettingData liveSettings = JsonConvert.DeserializeObject<LiveSettingData>(json);
								if (liveSettings != null)
								{
									LiveSettingData.SaveToStorage(liveSettings);
								}
							}
						}

						// Restore scores
						if (Directory.Exists(scoresDir))
						{
							string[] zipFiles = Directory.GetFiles(scoresDir, "*.zip");
							int total = zipFiles.Length;
							for (int i = 0; i < total; i++)
							{
								string progress = $"正在恢复谱面... ({i + 1}/{total})";
								_progressCallback?.Invoke(progress);

								try
								{
									CustomMusicScoreManagerService.ImportZip(zipFiles[i]);
								}
								catch (Exception ex)
								{
									UnityEngine.Debug.LogError($"Failed to import score: {zipFiles[i]}, error: {ex.Message}");
								}
								await UniTask.Yield();
							}
						}

						_completeCallback?.Invoke("success");
					}
					finally
					{
						if (Directory.Exists(tempDir))
						{
							Directory.Delete(tempDir, true);
						}
					}
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogError("Restore failed: " + ex.Message);
					_completeCallback?.Invoke("error:" + ex.Message);
				}
			}).Forget();
		}

		public static void ShareBackup(string backupZipPath)
		{
#if UNITY_ANDROID
			using (AndroidJavaClass helper = new AndroidJavaClass("com.opensekai.ShareExportHelper"))
			{
				helper.CallStatic("ShareFile", backupZipPath);
			}
#elif UNITY_STANDALONE || UNITY_EDITOR
			try
			{
				UnityEngine.Debug.Log("[BackupService] ShareBackup path: " + backupZipPath);
				// Use explorer.exe with /select, to open file explorer and highlight the file
				string directory = Path.GetDirectoryName(backupZipPath);
				UnityEngine.Debug.Log("[BackupService] ShareBackup directory: " + directory);
				if (!string.IsNullOrEmpty(directory))
				{
					// explorer.exe /select, works with either slash style
					System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + backupZipPath + "\"");
				}
				else
				{
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
					{
						FileName = backupZipPath,
						UseShellExecute = true
					});
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError("Share failed: " + ex.Message);
			}
#endif
		}

	}
}
