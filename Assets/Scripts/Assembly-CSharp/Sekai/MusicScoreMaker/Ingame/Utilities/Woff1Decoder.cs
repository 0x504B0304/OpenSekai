using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
	public static class Woff1Decoder
	{
		private const uint WoffSignature = 0x774F4646;

		private sealed class Table
		{
			public uint Tag;
			public uint Checksum;
			public byte[] Data;
			public uint Offset;
		}

		public static byte[] Decode(byte[] woff)
		{
			if (woff == null || woff.Length < 44)
			{
				throw new InvalidDataException("WOFF1 header is missing or truncated.");
			}

			using MemoryStream input = new MemoryStream(woff, false);
			using BinaryReader reader = new BinaryReader(input);
			if (ReadUInt32BE(reader) != WoffSignature)
			{
				throw new InvalidDataException("Only WOFF1 files are supported.");
			}

			uint flavor = ReadUInt32BE(reader);
			uint declaredLength = ReadUInt32BE(reader);
			ushort tableCount = ReadUInt16BE(reader);
			reader.ReadUInt16();
			uint totalSfntSize = ReadUInt32BE(reader);
			reader.BaseStream.Position = 44;

			if (declaredLength > woff.Length || tableCount == 0 || tableCount > 4096 || totalSfntSize < 12 + tableCount * 16)
			{
				throw new InvalidDataException("WOFF1 header contains invalid sizes.");
			}

			List<Table> tables = new List<Table>(tableCount);
			for (int i = 0; i < tableCount; i++)
			{
				uint tag = ReadUInt32BE(reader);
				uint offset = ReadUInt32BE(reader);
				uint compressedLength = ReadUInt32BE(reader);
				uint originalLength = ReadUInt32BE(reader);
				uint checksum = ReadUInt32BE(reader);
				if (offset > woff.Length || compressedLength > woff.Length - offset || originalLength > int.MaxValue)
				{
					throw new InvalidDataException("WOFF1 table lies outside the file.");
				}

				byte[] stored = new byte[compressedLength];
				Buffer.BlockCopy(woff, (int)offset, stored, 0, stored.Length);
				byte[] data = compressedLength < originalLength ? Inflate(stored, (int)originalLength) : stored;
				if (data.Length != originalLength)
				{
					throw new InvalidDataException("WOFF1 table decompressed to an unexpected size.");
				}
				tables.Add(new Table { Tag = tag, Checksum = checksum, Data = data });
			}

			uint directorySize = (uint)(12 + tableCount * 16);
			uint cursor = Align4(directorySize);
			foreach (Table table in tables)
			{
				table.Offset = cursor;
				cursor = Align4(cursor + (uint)table.Data.Length);
			}
			if (cursor != totalSfntSize)
			{
				throw new InvalidDataException("WOFF1 sfnt size does not match its table directory.");
			}

			using MemoryStream output = new MemoryStream((int)cursor);
			using BinaryWriter writer = new BinaryWriter(output);
			WriteUInt32BE(writer, flavor);
			WriteUInt16BE(writer, tableCount);
			ushort maxPower = HighestPowerOfTwo(tableCount);
			WriteUInt16BE(writer, (ushort)(maxPower * 16));
			WriteUInt16BE(writer, Log2(maxPower));
			WriteUInt16BE(writer, (ushort)(tableCount * 16 - maxPower * 16));
			foreach (Table table in tables)
			{
				WriteUInt32BE(writer, table.Tag);
				WriteUInt32BE(writer, table.Checksum);
				WriteUInt32BE(writer, table.Offset);
				WriteUInt32BE(writer, (uint)table.Data.Length);
			}
			foreach (Table table in tables)
			{
				while (output.Position < table.Offset) writer.Write((byte)0);
				writer.Write(table.Data);
				while ((output.Position & 3) != 0) writer.Write((byte)0);
			}
			return output.ToArray();
		}

		private static byte[] Inflate(byte[] compressed, int expectedLength)
		{
			if (compressed == null || compressed.Length < 6)
			{
				throw new InvalidDataException("WOFF1 table has a truncated zlib stream.");
			}
			int cmf = compressed[0];
			int flg = compressed[1];
			if ((cmf & 0x0f) != 8 || ((cmf << 8) + flg) % 31 != 0 || (flg & 0x20) != 0)
			{
				throw new InvalidDataException("WOFF1 table has an invalid zlib header.");
			}
			using MemoryStream result = new MemoryStream(expectedLength);
			using (MemoryStream source = new MemoryStream(compressed, 2, compressed.Length - 6, false))
			using (DeflateStream stream = new DeflateStream(source, CompressionMode.Decompress))
			{
				stream.CopyTo(result);
			}
			byte[] data = result.ToArray();
			uint expectedAdler = ((uint)compressed[compressed.Length - 4] << 24)
				| ((uint)compressed[compressed.Length - 3] << 16)
				| ((uint)compressed[compressed.Length - 2] << 8)
				| compressed[compressed.Length - 1];
			if (Adler32(data) != expectedAdler)
			{
				throw new InvalidDataException("WOFF1 table failed its zlib checksum.");
			}
			return data;
		}

		private static uint Adler32(byte[] data)
		{
			const uint modulus = 65521;
			uint a = 1;
			uint b = 0;
			foreach (byte value in data)
			{
				a = (a + value) % modulus;
				b = (b + a) % modulus;
			}
			return (b << 16) | a;
		}

		private static uint Align4(uint value) => (value + 3u) & ~3u;
		private static ushort HighestPowerOfTwo(ushort value)
		{
			ushort result = 1;
			while (result <= value / 2) result *= 2;
			return result;
		}
		private static ushort Log2(ushort value)
		{
			ushort result = 0;
			while (value > 1) { value /= 2; result++; }
			return result;
		}
		private static ushort ReadUInt16BE(BinaryReader reader)
		{
			byte a = reader.ReadByte();
			byte b = reader.ReadByte();
			return (ushort)((a << 8) | b);
		}
		private static uint ReadUInt32BE(BinaryReader reader)
		{
			return ((uint)reader.ReadByte() << 24) | ((uint)reader.ReadByte() << 16) | ((uint)reader.ReadByte() << 8) | reader.ReadByte();
		}
		private static void WriteUInt16BE(BinaryWriter writer, ushort value)
		{
			writer.Write((byte)(value >> 8)); writer.Write((byte)value);
		}
		private static void WriteUInt32BE(BinaryWriter writer, uint value)
		{
			writer.Write((byte)(value >> 24)); writer.Write((byte)(value >> 16)); writer.Write((byte)(value >> 8)); writer.Write((byte)value);
		}
	}
}
