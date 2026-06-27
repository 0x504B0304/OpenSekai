using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>
	/// 录制AudioListener输出的音频（游戏音效）
	/// 使用OnAudioFilterRead捕获音频数据并保存为WAV文件
	/// </summary>
	[RequireComponent(typeof(AudioListener))]
	public class AudioRecorder : MonoBehaviour
	{
		#region Public Properties

		public bool IsRecording { get; private set; }
		public int SampleRate { get; private set; } = 48000;
		public int Channels { get; private set; } = 2;
		public float RecordingDuration => _recordedSamples.Count / (float)(SampleRate * Channels);

		#endregion

		#region Private Fields

		private List<float> _recordedSamples = new List<float>();
		private string _outputPath;
		private bool _isInitialized = false;

		#endregion

		#region Public Methods

		/// <summary>
		/// 开始录制音频
		/// </summary>
		/// <param name="outputPath">输出WAV文件路径</param>
		public void StartRecording(string outputPath)
		{
			if (IsRecording)
			{
				Debug.LogWarning("[AudioRecorder] 已经在录制中");
				return;
			}

			_outputPath = outputPath;
			_recordedSamples.Clear();
			IsRecording = true;

			// 获取系统音频设置
			SampleRate = AudioSettings.outputSampleRate;
			Channels = 2; // 立体声

			Debug.Log($"[AudioRecorder] 开始录制音频: {SampleRate}Hz, {Channels}声道, 输出路径: {outputPath}");
		}

		/// <summary>
		/// 停止录制并保存WAV文件
		/// </summary>
		/// <returns>保存的WAV文件路径</returns>
		public string StopRecording()
		{
			if (!IsRecording)
			{
				Debug.LogWarning("[AudioRecorder] 当前没有在录制");
				return null;
			}

			IsRecording = false;

			if (_recordedSamples.Count == 0)
			{
				Debug.LogWarning("[AudioRecorder] 没有录制到任何音频数据");
				return null;
			}

			try
			{
				// 保存为WAV文件
				SaveWavFile(_outputPath, _recordedSamples.ToArray(), SampleRate, Channels);
				Debug.Log($"[AudioRecorder] 音频已保存: {_outputPath}, 时长: {RecordingDuration:F2}秒, 采样数: {_recordedSamples.Count}");
				return _outputPath;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[AudioRecorder] 保存音频失败: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// 清除录制的音频数据
		/// </summary>
		public void ClearRecording()
		{
			_recordedSamples.Clear();
			IsRecording = false;
		}

		#endregion

		#region Private Methods - Audio Capture

		/// <summary>
		/// Unity音频过滤器回调 - 捕获AudioListener的输出
		/// 这个方法会在每个音频帧被调用，捕获最终输出的音频数据
		/// </summary>
		void OnAudioFilterRead(float[] data, int channels)
		{
			if (!IsRecording)
			{
				return;
			}

			// 添加音频数据到录制缓冲区
			_recordedSamples.AddRange(data);
		}

		#endregion

		#region Private Methods - WAV File Saving

		/// <summary>
		/// 保存音频数据为WAV文件
		/// WAV格式: RIFF header + fmt chunk + data chunk
		/// </summary>
		private void SaveWavFile(string filepath, float[] samples, int sampleRate, int channels)
		{
			// 确保目录存在
			string directory = Path.GetDirectoryName(filepath);
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			using (FileStream fileStream = new FileStream(filepath, FileMode.Create))
			using (BinaryWriter writer = new BinaryWriter(fileStream))
			{
				// 将float样本转换为16位PCM
				short[] intData = new short[samples.Length];
				for (int i = 0; i < samples.Length; i++)
				{
					// 将[-1, 1]范围的float转换为[-32768, 32767]范围的short
					intData[i] = (short)(samples[i] * 32767f);
				}

				byte[] byteData = new byte[intData.Length * 2];
				Buffer.BlockCopy(intData, 0, byteData, 0, byteData.Length);

				// 写入WAV文件头
				WriteWavHeader(writer, sampleRate, channels, byteData.Length);

				// 写入音频数据
				writer.Write(byteData);
			}
		}

		/// <summary>
		/// 写入WAV文件头
		/// WAV文件格式:
		/// - RIFF header (12 bytes)
		/// - fmt chunk (24 bytes)
		/// - data chunk header (8 bytes)
		/// </summary>
		private void WriteWavHeader(BinaryWriter writer, int sampleRate, int channels, int dataLength)
		{
			// RIFF header
			writer.Write("RIFF".ToCharArray()); // ChunkID
			writer.Write(36 + dataLength); // ChunkSize (文件总大小 - 8)
			writer.Write("WAVE".ToCharArray()); // Format

			// fmt sub-chunk
			writer.Write("fmt ".ToCharArray()); // Subchunk1ID
			writer.Write(16); // Subchunk1Size (PCM格式固定为16)
			writer.Write((short)1); // AudioFormat (PCM = 1)
			writer.Write((short)channels); // NumChannels
			writer.Write(sampleRate); // SampleRate
			writer.Write(sampleRate * channels * 2); // ByteRate (SampleRate * NumChannels * BitsPerSample/8)
			writer.Write((short)(channels * 2)); // BlockAlign (NumChannels * BitsPerSample/8)
			writer.Write((short)16); // BitsPerSample

			// data sub-chunk
			writer.Write("data".ToCharArray()); // Subchunk2ID
			writer.Write(dataLength); // Subchunk2Size (音频数据大小)
		}

		#endregion

		#region Unity Lifecycle

		void Awake()
		{
			// 确保AudioListener存在
			if (GetComponent<AudioListener>() == null)
			{
				Debug.LogError("[AudioRecorder] 需要AudioListener组件");
			}
		}

		void OnDestroy()
		{
			// 如果还在录制，停止录制
			if (IsRecording)
			{
				Debug.LogWarning("[AudioRecorder] 组件被销毁时仍在录制，停止录制");
				StopRecording();
			}
		}

		#endregion
	}
}