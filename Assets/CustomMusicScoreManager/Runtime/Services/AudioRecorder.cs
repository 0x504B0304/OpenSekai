using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using CriWare;
using UnityEngine;

namespace Sekai.CustomMusicScoreManager
{
	// CRI renders music and hit sounds outside Unity's AudioListener.
	public sealed class AudioRecorder : MonoBehaviour
	{
		public bool IsRecording { get; private set; }
		public int SampleRate => 48000; // SoundManager's CRI output rate.
		public int Channels => 2;
		public float RecordingDuration => sampleFrames / (float)SampleRate;
		public float PeakAmplitude { get; private set; }
		private CaptureBuffer capture;
		private static CaptureBuffer activeCapture;
		private static readonly BusFilterCallback busCallback = CaptureBus;
		private BinaryWriter writer;
		private string outputPath;
		private int sampleFrames;
		private readonly byte[] pcm = new byte[4096 * 4];

		[UnmanagedFunctionPointer(Common.pluginCallingConvention)]
		private delegate void BusFilterCallback(IntPtr context, int format, int channels, int count, IntPtr data);

		[DllImport(Common.pluginName, CallingConvention = Common.pluginCallingConvention)]
		private static extern void criAtomExAsr_SetBusFilterCallbackByName(string busName,
			BusFilterCallback before, BusFilterCallback after, IntPtr context);

		// Single producer (CRI audio thread), single consumer (Unity main thread).
		// Preallocated storage keeps disk I/O, allocations and render stalls off the audio thread.
		private sealed class CaptureBuffer
		{
			public const int Capacity = 48000 * 30;
			public readonly float[] Left = new float[Capacity], Right = new float[Capacity];
			public readonly short[] IntegerSamples = new short[4096];
			public readonly float[] MixSamples = new float[4096];
			public long Written, Read;
			public string Error;
		}

		[AOT.MonoPInvokeCallback(typeof(BusFilterCallback))]
		private static void CaptureBus(IntPtr context, int format, int channels, int count, IntPtr data)
		{
			CaptureBuffer buffer = Volatile.Read(ref activeCapture);
			if (buffer == null || buffer.Error != null || count <= 0) return;
			try
			{
				if ((channels != 1 && channels != 2 && channels != 6 && channels != 8) || (format != 0 && format != 1))
					throw new InvalidOperationException($"Unsupported CRI output: format={format}, channels={channels}, samples={count}.");
				long written = buffer.Written;
				if (written - Volatile.Read(ref buffer.Read) + count > CaptureBuffer.Capacity)
					throw new InvalidOperationException("Audio capture buffer overflow: recording cannot keep up.");
				CopyChannel(buffer, Marshal.ReadIntPtr(data), buffer.Left, format, count, written);
				CopyChannel(buffer, Marshal.ReadIntPtr(data, channels > 1 ? IntPtr.Size : 0), buffer.Right, format, count, written);
				if (channels >= 6)
				{
					// CRI 5.1/7.1 order: L, R, C, LFE, surround L/R, rear L/R.
					// Standard stereo fold-down; omit the dedicated subwoofer channel.
					MixChannel(buffer, Marshal.ReadIntPtr(data, 2 * IntPtr.Size), format, count, written, true, true);
					MixChannel(buffer, Marshal.ReadIntPtr(data, 4 * IntPtr.Size), format, count, written, true, false);
					MixChannel(buffer, Marshal.ReadIntPtr(data, 5 * IntPtr.Size), format, count, written, false, true);
					if (channels == 8)
					{
						MixChannel(buffer, Marshal.ReadIntPtr(data, 6 * IntPtr.Size), format, count, written, true, false);
						MixChannel(buffer, Marshal.ReadIntPtr(data, 7 * IntPtr.Size), format, count, written, false, true);
					}
				}
				Volatile.Write(ref buffer.Written, written + count);
			}
			catch (Exception ex) { Volatile.Write(ref buffer.Error, ex.Message); }
		}

		private static void MixChannel(CaptureBuffer buffer, IntPtr source, int format, int count, long written, bool left, bool right)
		{
			for (int copied = 0; copied < count;)
			{
				int length = Math.Min(count - copied, buffer.MixSamples.Length);
				if (format == 1) Marshal.Copy(IntPtr.Add(source, copied * 4), buffer.MixSamples, 0, length);
				else Marshal.Copy(IntPtr.Add(source, copied * 2), buffer.IntegerSamples, 0, length);
				for (int i = 0; i < length; i++)
				{
					float value = (format == 1 ? buffer.MixSamples[i] : buffer.IntegerSamples[i] / 32768f) * 0.70710678f;
					int index = (int)((written + copied + i) % CaptureBuffer.Capacity);
					if (left) buffer.Left[index] += value;
					if (right) buffer.Right[index] += value;
				}
				copied += length;
			}
		}

		private static void CopyChannel(CaptureBuffer buffer, IntPtr source, float[] target, int format, int count, long written)
		{
			int offset = (int)(written % CaptureBuffer.Capacity);
			if (format == 1) // CRIATOM_PCM_FORMAT_FLOAT32; planar, normalized samples.
			{
				int first = Math.Min(count, CaptureBuffer.Capacity - offset);
				Marshal.Copy(source, target, offset, first);
				if (first < count) Marshal.Copy(IntPtr.Add(source, first * 4), target, 0, count - first);
			}
			else // CRIATOM_PCM_FORMAT_SINT16.
			{
				for (int copied = 0; copied < count;)
				{
					int length = Math.Min(count - copied, buffer.IntegerSamples.Length);
					Marshal.Copy(IntPtr.Add(source, copied * 2), buffer.IntegerSamples, 0, length);
					for (int i = 0; i < length; i++) target[(offset + copied + i) % CaptureBuffer.Capacity] = buffer.IntegerSamples[i] / 32768f;
					copied += length;
				}
			}
		}

		public void StartRecording(string path)
		{
			if (IsRecording) throw new InvalidOperationException("Audio recording is already active.");
			SoundManager.Instance.Initialize();
			outputPath = path;
			sampleFrames = 0;
			PeakAmplitude = 0;
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				writer = new BinaryWriter(File.Create(path));
				WriteHeader();
				capture = new CaptureBuffer();
				CriAtomEx.Lock();
				try
				{
					if (activeCapture != null) throw new InvalidOperationException("Another CRI recording is active.");
					Volatile.Write(ref activeCapture, capture);
					criAtomExAsr_SetBusFilterCallbackByName("MasterOut", null, busCallback, IntPtr.Zero);
				}
				finally { CriAtomEx.Unlock(); }
				IsRecording = true;
			}
			catch { ClearRecording(); throw; }
		}

		public void Pump()
		{
			if (capture == null) return;
			if (Volatile.Read(ref capture.Error) != null) throw new InvalidOperationException(capture.Error);
			long written = Volatile.Read(ref capture.Written);
			while (capture.Read < written)
			{
				int count = (int)Math.Min(written - capture.Read, pcm.Length / 4);
				WriteSamples(capture.Read, count);
				Volatile.Write(ref capture.Read, capture.Read + count);
			}
		}

		private void Update()
		{
			if (!IsRecording) return;
			try { Pump(); }
			catch (Exception ex)
			{
				VideoGenerationController.Instance.HandleRecordingFailure("音频捕获失败：" + ex.Message);
			}
		}

		private void WriteSamples(long start, int count)
		{
			for (int i = 0; i < count; i++)
			{
				int index = (int)((start + i) % CaptureBuffer.Capacity);
				float l = Mathf.Clamp(capture.Left[index], -1f, 1f);
				float r = Mathf.Clamp(capture.Right[index], -1f, 1f);
				PeakAmplitude = Mathf.Max(PeakAmplitude, Mathf.Abs(l), Mathf.Abs(r));
				short a = (short)Mathf.RoundToInt(l * 32767f), b = (short)Mathf.RoundToInt(r * 32767f);
				pcm[i * 4] = (byte)a;
				pcm[i * 4 + 1] = (byte)(a >> 8);
				pcm[i * 4 + 2] = (byte)b;
				pcm[i * 4 + 3] = (byte)(b >> 8);
			}
			writer.Write(pcm, 0, count * 4);
			sampleFrames += count;
		}

		public string StopRecording()
		{
			if (!IsRecording) return null;
			try
			{
				Detach();
				Pump();
				IsRecording = false;
				writer.BaseStream.Position = 0;
				WriteHeader();
				Debug.Log($"[AudioRecorder] CRI audio captured: {RecordingDuration:F2}s, peak={PeakAmplitude:F4}");
				return sampleFrames > 0 ? outputPath : null;
			}
			finally { ClearRecording(); }
		}

		public void ClearRecording()
		{
			IsRecording = false;
			Detach();
			// A background interruption should still leave a readable partial WAV.
			if (writer != null)
			{
				try { Pump(); }
				catch (Exception ex) { Debug.LogWarning("[AudioRecorder] " + ex.Message); }
				try { writer.BaseStream.Position = 0; WriteHeader(); }
				catch (Exception ex) { Debug.LogWarning("[AudioRecorder] " + ex.Message); }
			}
			capture = null;
			writer?.Dispose();
			writer = null;
		}

		private void Detach()
		{
			if (capture == null || !ReferenceEquals(activeCapture, capture)) return;
			if (!CriAtomPlugin.IsLibraryInitialized()) { Volatile.Write(ref activeCapture, null); return; }
			CriAtomEx.Lock();
			try
			{
				criAtomExAsr_SetBusFilterCallbackByName("MasterOut", null, null, IntPtr.Zero);
				Volatile.Write(ref activeCapture, null);
			}
			finally { CriAtomEx.Unlock(); }
		}

		private void WriteHeader()
		{
			writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
			writer.Write(36 + sampleFrames * 4);
			writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
			writer.Write(16); writer.Write((short)1); writer.Write((short)Channels);
			writer.Write(SampleRate); writer.Write(SampleRate * 4);
			writer.Write((short)4); writer.Write((short)16);
			writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(sampleFrames * 4);
		}

		private void OnDestroy() => ClearRecording();
	}
}
