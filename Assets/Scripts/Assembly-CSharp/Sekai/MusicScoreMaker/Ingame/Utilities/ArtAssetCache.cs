using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
	public static class ArtAssetCache
	{
		private const string CacheDirectoryName = "ArtAssets";
		private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".png", ".jpg", ".jpeg", ".ttf", ".otf", ".woff"
		};

		public static string CacheRoot => Path.Combine(Application.persistentDataPath, CacheDirectoryName);

		public static string Import(string sourcePath)
		{
			if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
			{
				throw new FileNotFoundException("The art source file was not found.", sourcePath);
			}

			string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
			if (!SupportedExtensions.Contains(extension))
			{
				throw new NotSupportedException($"Unsupported art asset type: {extension}");
			}

			byte[] bytes = File.ReadAllBytes(sourcePath);
			string hash = ComputeHash(bytes);
			Directory.CreateDirectory(CacheRoot);
			string destination = Path.Combine(CacheRoot, hash + extension);
			if (!File.Exists(destination))
			{
				File.WriteAllBytes(destination, bytes);
			}
			return hash;
		}

		public static string Find(string hash)
		{
			if (!IsSha256(hash) || !Directory.Exists(CacheRoot))
			{
				return null;
			}

			foreach (string extension in SupportedExtensions)
			{
				string path = Path.Combine(CacheRoot, hash + extension);
				if (File.Exists(path))
				{
					return path;
				}
			}
			return null;
		}

		public static bool TryRead(string hash, out byte[] bytes, out string extension)
		{
			string path = Find(hash);
			if (path == null)
			{
				bytes = null;
				extension = null;
				return false;
			}

			bytes = File.ReadAllBytes(path);
			extension = Path.GetExtension(path);
			return true;
		}

		public static string ComputeHash(byte[] bytes)
		{
			if (bytes == null)
			{
				throw new ArgumentNullException(nameof(bytes));
			}

			using SHA256 sha = SHA256.Create();
			byte[] digest = sha.ComputeHash(bytes);
			StringBuilder result = new StringBuilder(digest.Length * 2);
			foreach (byte value in digest)
			{
				result.Append(value.ToString("x2"));
			}
			return result.ToString();
		}

		private static bool IsSha256(string value)
		{
			if (string.IsNullOrEmpty(value) || value.Length != 64)
			{
				return false;
			}
			foreach (char character in value)
			{
				if (!Uri.IsHexDigit(character))
				{
					return false;
				}
			}
			return true;
		}
	}
}
