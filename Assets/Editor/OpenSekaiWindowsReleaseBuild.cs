using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Sekai.CustomMusicScoreManager;
using UnityEditor.Build;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace Sekai.EditorTools
{
	public static class OpenSekaiWindowsReleaseBuild
	{
		public static void Build()
		{
			string ffmpegRoot = Environment.GetEnvironmentVariable("OPENSEKAI_FFMPEG_ROOT");
			ValidateFFmpegDistribution(ffmpegRoot);
			string binaryDirectory = GetBinaryDirectory(ffmpegRoot);
			if (FFmpegLocator.FindAvailable(binaryDirectory, null) == null)
				throw new BuildFailedException("The supplied FFmpeg cannot run. Check its executable and runtime DLLs.");

			OpenSekaiAssetBundleBuildPipeline.BuildWindowsPlayer();
			string projectRoot = Path.GetDirectoryName(Application.dataPath);
			string playerDirectory = Path.GetFullPath(Path.Combine(projectRoot,
				Environment.GetEnvironmentVariable("OPENSEKAI_WINDOWS_OUTPUT") ?? "Builds/Windows"));
			Package(playerDirectory, ffmpegRoot, Path.Combine(projectRoot, "Builds/Release"));
			Debug.Log("Windows release archives built: Builds/Release/win_amd64.zip and Builds/Release/win_amd64_ffmpeg.zip");
		}

		public static void Package(string playerDirectory, string ffmpegRoot, string outputDirectory)
		{
			ValidateFFmpegDistribution(ffmpegRoot);
			if (!File.Exists(Path.Combine(playerDirectory, "OpenSekai.exe")))
				throw new BuildFailedException("The Windows player executable is missing.");
			Directory.CreateDirectory(outputDirectory);
			string plainZip = Path.Combine(outputDirectory, "win_amd64.zip");
			string bundledZip = Path.Combine(outputDirectory, "win_amd64_ffmpeg.zip");
			string plainTemp = plainZip + ".tmp";
			string bundledTemp = bundledZip + ".tmp";
			try
			{
				using (var stream = new FileStream(plainTemp, FileMode.Create))
				using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
				{
					foreach (string file in Directory.EnumerateFiles(playerDirectory, "*", SearchOption.AllDirectories))
					{
						string relative = file.Substring(playerDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1).Replace('\\', '/');
						if (IsPlayerFile(relative)) archive.CreateEntryFromFile(file, relative, CompressionLevel.Optimal);
					}
				}
				File.Copy(plainTemp, bundledTemp, true);
				using (var archive = ZipFile.Open(bundledTemp, ZipArchiveMode.Update))
				{
					string binaries = GetBinaryDirectory(ffmpegRoot);
					archive.CreateEntryFromFile(Path.Combine(binaries, "ffmpeg.exe"), "ffmpeg/ffmpeg.exe", CompressionLevel.Optimal);
					foreach (string dll in Directory.EnumerateFiles(binaries, "*.dll"))
						archive.CreateEntryFromFile(dll, "ffmpeg/" + Path.GetFileName(dll), CompressionLevel.Optimal);
					archive.CreateEntryFromFile(Path.Combine(ffmpegRoot, "LICENSE"), "ffmpeg/LICENSE", CompressionLevel.Optimal);
					string readme = Path.Combine(ffmpegRoot, "README.txt");
					if (File.Exists(readme)) archive.CreateEntryFromFile(readme, "ffmpeg/README.txt", CompressionLevel.Optimal);
				}
				File.Delete(plainZip);
				File.Move(plainTemp, plainZip);
				File.Delete(bundledZip);
				File.Move(bundledTemp, bundledZip);
			}
			finally
			{
				File.Delete(plainTemp);
				File.Delete(bundledTemp);
			}
		}

		private static bool IsPlayerFile(string relative)
		{
			string[] parts = relative.Split('/');
			if (parts.Any(part => part.IndexOf("DoNotShip", StringComparison.OrdinalIgnoreCase) >= 0 ||
				part.IndexOf("ButDontShipItWithYourGame", StringComparison.OrdinalIgnoreCase) >= 0 ||
				part.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase))) return false;
			string name = Path.GetFileName(relative);
			if (name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) ||
				name.Equals("ffplay.exe", StringComparison.OrdinalIgnoreCase) ||
				name.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase)) return false;
			string extension = Path.GetExtension(relative);
			return !new[] { ".pdb", ".mdb", ".log", ".tmp" }.Contains(extension, StringComparer.OrdinalIgnoreCase);
		}

		private static string GetBinaryDirectory(string root) =>
			File.Exists(Path.Combine(root, "bin", "ffmpeg.exe")) ? Path.Combine(root, "bin") : root;

		private static void ValidateFFmpegDistribution(string root)
		{
			if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
				throw new BuildFailedException("Set OPENSEKAI_FFMPEG_ROOT to the extracted FFmpeg distribution directory.");
			if (!File.Exists(Path.Combine(GetBinaryDirectory(root), "ffmpeg.exe")) || !File.Exists(Path.Combine(root, "LICENSE")))
				throw new BuildFailedException("The FFmpeg distribution must contain ffmpeg.exe (at root or in bin) and LICENSE.");
		}
	}
}
