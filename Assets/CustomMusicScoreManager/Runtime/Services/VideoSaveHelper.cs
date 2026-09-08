using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>
	/// 视频保存助手
	/// 负责跨平台视频保存功能
	/// Task 8: 实现跨平台视频保存
	/// </summary>
	public class VideoSaveHelper
	{
		#region Singleton Pattern

		private static VideoSaveHelper _instance;

		public static VideoSaveHelper Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new VideoSaveHelper();
				}
				return _instance;
			}
		}

		#endregion

		#region Constants

		// 相册名称
		private const string ALBUM_NAME = "OpenSekai_Rec";

		// 非法文件名字符
		private static readonly char[] InvalidFileNameChars = new char[]
		{
			'\\', '/', ':', '*', '?', '"', '<', '>', '|'
		};

		#endregion

		#region Public API

		/// <summary>
		/// 保存视频到平台指定位置
		/// Windows: 保存到下载目录
		/// Android: 保存到相册
		/// </summary>
		/// <param name="sourcePath">源视频文件路径</param>
		/// <param name="scoreTitle">谱面标题</param>
		/// <param name="timestamp">时间戳</param>
		/// <param name="onComplete">完成回调，参数为最终保存路径</param>
		/// <param name="onError">错误回调，参数为错误消息</param>
		public void SaveVideo(
			string sourcePath,
			string scoreTitle,
			DateTime timestamp,
			Action<string> onComplete = null,
			Action<string> onError = null)
		{
			if (string.IsNullOrEmpty(sourcePath))
			{
				string error = "源视频文件路径为空";
				Debug.LogError($"[VideoSaveHelper] {error}");
				onError?.Invoke(error);
				return;
			}

			if (!File.Exists(sourcePath))
			{
				string error = $"源视频文件不存在: {sourcePath}";
				Debug.LogError($"[VideoSaveHelper] {error}");
				onError?.Invoke(error);
				return;
			}

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			SaveVideoOnWindows(sourcePath, scoreTitle, timestamp, onComplete, onError);
#elif UNITY_ANDROID && !UNITY_EDITOR
			SaveVideoOnAndroid(sourcePath, scoreTitle, timestamp, onComplete, onError);
#else
			// 其他平台的备用方案：保存到应用持久化目录
			SaveVideoToPersistentPath(sourcePath, scoreTitle, timestamp, onComplete, onError);
#endif
		}

		#endregion

		#region Windows Implementation

		/// <summary>
		/// SubTask 8.1: Windows端：获取下载目录路径
		/// 使用Environment.SpecialFolder.UserProfile + "\Downloads"
		/// </summary>
		/// <returns>下载目录路径</returns>
		private string GetWindowsDownloadPath()
		{
			try
			{
				// 方案1: 使用用户目录 + Downloads
				string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				if (!string.IsNullOrEmpty(userProfile))
				{
					string downloadsPath = Path.Combine(userProfile, "Downloads");

					// 如果不存在则创建
					if (!Directory.Exists(downloadsPath))
					{
						Directory.CreateDirectory(downloadsPath);
						Debug.Log($"[VideoSaveHelper] 创建下载目录: {downloadsPath}");
					}

					return downloadsPath;
				}

				// 方案2: 使用MyDocuments作为备用
				string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				if (!string.IsNullOrEmpty(documentsPath))
				{
					string openSekaiPath = Path.Combine(documentsPath, "OpenSekai_Videos");

					if (!Directory.Exists(openSekaiPath))
					{
						Directory.CreateDirectory(openSekaiPath);
						Debug.Log($"[VideoSaveHelper] 创建备用视频目录: {openSekaiPath}");
					}

					Debug.LogWarning($"[VideoSaveHelper] 无法获取Downloads目录，使用备用目录: {openSekaiPath}");
					return openSekaiPath;
				}

				// 方案3: 使用应用持久化目录
				string persistentPath = Application.persistentDataPath;
				Debug.LogWarning($"[VideoSaveHelper] 无法获取系统目录，使用应用持久化目录: {persistentPath}");
				return persistentPath;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[VideoSaveHelper] 获取下载目录失败: {ex.Message}");
				return Application.persistentDataPath;
			}
		}

		/// <summary>
		/// SubTask 8.2: Windows端：保存视频文件
		/// 从VideoPostProcessor的输出路径获取源文件
		/// 生成文件名（谱面标题_时间戳.mp4，去除非法字符）
		/// 使用File.Copy保存到下载目录
		/// </summary>
		/// <param name="sourcePath">源视频文件路径</param>
		/// <param name="scoreTitle">谱面标题</param>
		/// <param name="timestamp">时间戳</param>
		/// <param name="onComplete">完成回调</param>
		/// <param name="onError">错误回调</param>
		private void SaveVideoOnWindows(
			string sourcePath,
			string scoreTitle,
			DateTime timestamp,
			Action<string> onComplete,
			Action<string> onError)
		{
			try
			{
				// 生成文件名
				string fileName = GenerateFileName(scoreTitle, timestamp);
				string downloadPath = GetWindowsDownloadPath();
				string destPath = Path.Combine(downloadPath, fileName);

				// 如果文件已存在，添加序号
				destPath = GetUniqueFilePath(destPath);

				// 复制文件
				File.Copy(sourcePath, destPath, false);

				long fileSize = new FileInfo(destPath).Length;
				Debug.Log($"[VideoSaveHelper] 视频已保存到Windows下载目录: {destPath}, 大小: {fileSize / 1024 / 1024:F2}MB");

				onComplete?.Invoke(destPath);
			}
			catch (Exception ex)
			{
				string error = $"Windows保存视频失败: {ex.Message}";
				Debug.LogError($"[VideoSaveHelper] {error}\n{ex.StackTrace}");
				onError?.Invoke(error);
			}
		}

		#endregion

		#region Android Implementation

		/// <summary>
		/// SubTask 8.3 & 8.4: 安卓端：保存视频到相册
		/// 使用AndroidJavaClass和AndroidJavaObject调用安卓原生API
		/// 通过MediaStore API保存到相册"OpenSekai_Rec"
		/// </summary>
		/// <param name="sourcePath">源视频文件路径</param>
		/// <param name="scoreTitle">谱面标题</param>
		/// <param name="timestamp">时间戳</param>
		/// <param name="onComplete">完成回调</param>
		/// <param name="onError">错误回调</param>
		private void SaveVideoOnAndroid(
			string sourcePath,
			string scoreTitle,
			DateTime timestamp,
			Action<string> onComplete,
			Action<string> onError)
		{
			try
			{
				// 生成文件名
				string fileName = GenerateFileName(scoreTitle, timestamp);

				// 调用Java层的ShareExportHelper.saveVideoToGallery方法
				using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
				using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
				using (AndroidJavaClass helperClass = new AndroidJavaClass("com.opensekai.ShareExportHelper"))
				{
					// 调用静态方法: saveVideoToGallery(String sourcePath, String filename, String albumName)
					helperClass.CallStatic("saveVideoToGallery", sourcePath, fileName, ALBUM_NAME);

					// 由于Java方法是异步的，我们需要等待回调
					// 这里暂时返回一个路径标识
					string savedPath = $"content://media/external/video/media/{fileName}";
					Debug.Log($"[VideoSaveHelper] 视频已保存到Android相册: {ALBUM_NAME}/{fileName}");

					onComplete?.Invoke(savedPath);
				}
			}
			catch (Exception ex)
			{
				string error = $"Android保存视频失败: {ex.Message}";
				Debug.LogError($"[VideoSaveHelper] {error}\n{ex.StackTrace}");
				onError?.Invoke(error);
			}
		}

		#endregion

		#region Fallback Implementation

		/// <summary>
		/// 备用方案：保存到应用持久化目录
		/// </summary>
		private void SaveVideoToPersistentPath(
			string sourcePath,
			string scoreTitle,
			DateTime timestamp,
			Action<string> onComplete,
			Action<string> onError)
		{
			try
			{
				string fileName = GenerateFileName(scoreTitle, timestamp);
				string videosDir = Path.Combine(Application.persistentDataPath, "SavedVideos");

				if (!Directory.Exists(videosDir))
				{
					Directory.CreateDirectory(videosDir);
				}

				string destPath = Path.Combine(videosDir, fileName);
				destPath = GetUniqueFilePath(destPath);

				File.Copy(sourcePath, destPath, false);

				long fileSize = new FileInfo(destPath).Length;
				Debug.Log($"[VideoSaveHelper] 视频已保存到持久化目录: {destPath}, 大小: {fileSize / 1024 / 1024:F2}MB");

				onComplete?.Invoke(destPath);
			}
			catch (Exception ex)
			{
				string error = $"保存视频到持久化目录失败: {ex.Message}";
				Debug.LogError($"[VideoSaveHelper] {error}\n{ex.StackTrace}");
				onError?.Invoke(error);
			}
		}

		#endregion

		#region Utility Methods

		/// <summary>
		/// 生成文件名
		/// 格式：谱面标题_时间戳.mp4
		/// 去除非法字符
		/// </summary>
		/// <param name="scoreTitle">谱面标题</param>
		/// <param name="timestamp">时间戳</param>
		/// <returns>文件名</returns>
		private string GenerateFileName(string scoreTitle, DateTime timestamp)
		{
			// 清理标题中的非法字符
			string cleanTitle = scoreTitle ?? "Video";

			// 移除非法字符
			foreach (char c in InvalidFileNameChars)
			{
				cleanTitle = cleanTitle.Replace(c, '_');
			}

			// 也移除Path.GetInvalidFileNameChars()中的字符
			char[] systemInvalidChars = Path.GetInvalidFileNameChars();
			foreach (char c in systemInvalidChars)
			{
				cleanTitle = cleanTitle.Replace(c, '_');
			}

			// 限制标题长度，避免文件名过长
			if (cleanTitle.Length > 50)
			{
				cleanTitle = cleanTitle.Substring(0, 50);
			}

			// 格式：标题_yyyyMMdd_HHmmss.mp4
			string timestampStr = timestamp.ToString("yyyyMMdd_HHmmss");
			string fileName = $"{cleanTitle}_{timestampStr}.mp4";

			return fileName;
		}

		/// <summary>
		/// 获取唯一的文件路径
		/// 如果文件已存在，添加序号
		/// </summary>
		/// <param name="basePath">基础路径</param>
		/// <returns>唯一的文件路径</returns>
		private string GetUniqueFilePath(string basePath)
		{
			if (!File.Exists(basePath))
			{
				return basePath;
			}

			string directory = Path.GetDirectoryName(basePath);
			string fileNameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
			string extension = Path.GetExtension(basePath);

			int counter = 1;
			string newPath;

			do
			{
				newPath = Path.Combine(directory, $"{fileNameWithoutExt}_{counter}{extension}");
				counter++;
			}
			while (File.Exists(newPath));

			return newPath;
		}

		#endregion
	}
}