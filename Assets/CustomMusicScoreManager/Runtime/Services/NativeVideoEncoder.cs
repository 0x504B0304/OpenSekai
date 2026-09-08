using System;
using System.Collections;
using UnityEngine;

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>
	/// 原生视频编码器抽象基类
	/// 提供跨平台视频编码接口，支持帧序列到视频文件的转换
	/// </summary>
	public abstract class NativeVideoEncoder : MonoBehaviour
	{
		#region Properties

		/// <summary>
		/// 编码器是否正在工作
		/// </summary>
		public bool IsEncoding { get; protected set; }

		/// <summary>
		/// 当前编码进度 (0-1)
		/// </summary>
		public float Progress { get; protected set; }

		/// <summary>
		/// 当前状态描述
		/// </summary>
		public string StatusMessage { get; protected set; }

		/// <summary>
		/// 编码器是否可用
		/// </summary>
		public abstract bool IsAvailable { get; }

		/// <summary>
		/// 编码器名称
		/// </summary>
		public abstract string EncoderName { get; }

		#endregion

		#region Configuration

		/// <summary>
		/// 输出帧率
		/// </summary>
		protected int outputFrameRate = 30;

		/// <summary>
		/// 视频比特率
		/// </summary>
		protected int videoBitrate = 8000000; // 8 Mbps

		/// <summary>
		/// 音频比特率
		/// </summary>
		protected int audioBitrate = 192000; // 192 kbps

		#endregion

		#region Public API

		/// <summary>
		/// 配置编码器参数
		/// </summary>
		/// <param name="frameRate">输出帧率</param>
		/// <param name="videoBitrate">视频比特率</param>
		/// <param name="audioBitrate">音频比特率</param>
		public virtual void Configure(int frameRate, int videoBitrate, int audioBitrate)
		{
			this.outputFrameRate = frameRate;
			this.videoBitrate = videoBitrate;
			this.audioBitrate = audioBitrate;
		}

		/// <summary>
		/// 编码视频
		/// </summary>
		/// <param name="frameSequencePath">帧序列目录路径</param>
		/// <param name="audioPath">音频文件路径（可选）</param>
		/// <param name="outputPath">输出视频文件路径</param>
		/// <param name="frameRate">帧率</param>
		/// <param name="width">视频宽度</param>
		/// <param name="height">视频高度</param>
		/// <param name="speedMultiplier">速度倍数（用于恢复原始速度）</param>
		/// <param name="onProgress">进度回调</param>
		/// <returns>编码结果</returns>
		public abstract IEnumerator EncodeVideo(
			string frameSequencePath,
			string audioPath,
			string outputPath,
			int frameRate,
			int width,
			int height,
			int speedMultiplier,
			Action<float, string> onProgress = null);

		/// <summary>
		/// 取消编码
		/// </summary>
		public virtual void CancelEncoding()
		{
			IsEncoding = false;
			Progress = 0f;
			StatusMessage = "已取消";
		}

		/// <summary>
		/// 获取编码器能力描述
		/// </summary>
		/// <returns>能力描述</returns>
		public abstract string GetCapabilities();

		#endregion

		#region Protected Methods

		/// <summary>
		/// 更新进度
		/// </summary>
		protected virtual void UpdateProgress(float progress, string status)
		{
			Progress = progress;
			StatusMessage = status;
			onProgress?.Invoke(progress, status);
			Debug.Log($"[{EncoderName}] 进度: {progress * 100:F1}% - {status}");
		}

		/// <summary>
		/// 进度回调
		/// </summary>
		protected Action<float, string> onProgress;

		/// <summary>
		/// 读取帧序列的manifest信息
		/// </summary>
		protected FrameSequenceInfo ReadFrameSequenceInfo(string frameSequencePath, int speedMultiplier)
		{
			FrameSequenceInfo info = new FrameSequenceInfo
			{
				DirectoryPath = frameSequencePath,
				SpeedMultiplier = speedMultiplier
			};

			// 读取manifest.json
			string manifestPath = System.IO.Path.Combine(frameSequencePath, "manifest.json");
			if (System.IO.File.Exists(manifestPath))
			{
				string manifestJson = System.IO.File.ReadAllText(manifestPath);
				VideoManifest manifest = JsonUtility.FromJson<VideoManifest>(manifestJson);

				info.Width = manifest.width;
				info.Height = manifest.height;
				info.OriginalFrameRate = manifest.frameRate;
				info.FrameCount = manifest.frameCount;
				info.OriginalDuration = manifest.duration;
			}
			else
			{
				Debug.LogWarning($"[{EncoderName}] manifest.json不存在，使用默认值");
				info.OriginalFrameRate = outputFrameRate;
				info.Width = 1920;
				info.Height = 1080;
			}

			// 获取所有帧文件
			string[] frameFiles = System.IO.Directory.GetFiles(frameSequencePath, "frame_*.jpg");
			System.Array.Sort(frameFiles);
			info.FrameFiles = new System.Collections.Generic.List<string>(frameFiles);

			if (info.FrameFiles.Count == 0)
			{
				Debug.LogError($"[{EncoderName}] 没有找到帧文件");
				return null;
			}

			// 如果manifest不存在，从帧文件推断帧数和时长
			if (!System.IO.File.Exists(manifestPath))
			{
				info.FrameCount = info.FrameFiles.Count;
				info.OriginalDuration = info.FrameCount / (float)info.OriginalFrameRate;
				Debug.Log($"[{EncoderName}] 从帧文件推断: {info.FrameCount}帧, 原始时长: {info.OriginalDuration:F2}秒");
			}

			// 新方案：正常速度录制，无需倍速调整
			// AdjustedDuration = OriginalDuration（录制时长就是实际游戏时长）
			info.AdjustedDuration = info.OriginalDuration;

			return info;
		}

		#endregion

		#region Data Structures

		/// <summary>
		/// 帧序列信息
		/// </summary>
		protected class FrameSequenceInfo
		{
			public string DirectoryPath { get; set; }
			public int Width { get; set; }
			public int Height { get; set; }
			public int OriginalFrameRate { get; set; }
			public int SpeedMultiplier { get; set; }
			public int FrameCount { get; set; }
			public float OriginalDuration { get; set; }
			public float AdjustedDuration { get; set; }
			public System.Collections.Generic.List<string> FrameFiles { get; set; } = new System.Collections.Generic.List<string>();
		}

		#endregion
	}
}