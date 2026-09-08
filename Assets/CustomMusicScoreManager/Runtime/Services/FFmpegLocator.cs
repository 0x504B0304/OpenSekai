using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Sekai.CustomMusicScoreManager
{
	public static class FFmpegLocator
	{
		// Resolve absolute paths so encoding uses exactly the executable checked by the UI.
		public static string FindAvailable(string applicationDirectory, string environmentPath)
		{
			foreach (string candidate in GetCandidates(applicationDirectory, environmentPath))
				if (CanRun(candidate)) return candidate;
			return null;
		}

		public static IEnumerable<string> GetCandidates(string applicationDirectory, string environmentPath)
		{
			var directories = new List<string>();
			if (!string.IsNullOrEmpty(applicationDirectory))
			{
				directories.Add(applicationDirectory);
				directories.Add(Path.Combine(applicationDirectory, "ffmpeg"));
			}
			if (!string.IsNullOrEmpty(environmentPath)) directories.AddRange(environmentPath.Split(';'));
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string entry in directories)
			{
				string candidate;
				try
				{
					string directory = Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"'));
					if (string.IsNullOrWhiteSpace(directory)) continue;
					candidate = Path.GetFullPath(Path.Combine(directory, "ffmpeg.exe"));
				}
				catch (ArgumentException) { continue; }
				catch (NotSupportedException) { continue; }
				catch (PathTooLongException) { continue; }
				if (seen.Add(candidate) && File.Exists(candidate)) yield return candidate;
			}
		}

		private static bool CanRun(string executable)
		{
			try
			{
				using (var process = new Process())
				{
					process.StartInfo = new ProcessStartInfo
					{
						FileName = executable,
						Arguments = "-version",
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						ErrorDialog = false
					};
					if (!process.Start()) return false;
					// Drain both pipes concurrently; a full pipe must not block the probe.
					var output = process.StandardOutput.ReadToEndAsync();
					var error = process.StandardError.ReadToEndAsync();
					if (!process.WaitForExit(3000))
					{
						process.Kill();
						return false;
					}
					if (!System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[] { output, error }, 1000))
						return false;
					return process.ExitCode == 0 && output.Result.StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase);
				}
			}
			catch (Exception)
			{
				// Missing runtime DLLs, invalid executables and launch failures are unavailable.
				return false;
			}
		}
	}
}
