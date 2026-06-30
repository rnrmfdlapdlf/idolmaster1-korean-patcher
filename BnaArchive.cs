using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImasKoreanPatcher
{
    internal sealed class BnaArchive
    {
        private readonly byte[] data;
        private readonly List<BnaEntry> entries;

        private BnaArchive(byte[] data, List<BnaEntry> entries)
        {
            this.data = data;
            this.entries = entries;
        }

        public static BnaArchive Load(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            return new BnaArchive(bytes, Parse(bytes));
        }

        public byte[] ReadEntry(string internalPath)
        {
            BnaEntry entry = FindEntry(internalPath);
            byte[] output = new byte[entry.Size];
            Buffer.BlockCopy(data, entry.Offset, output, 0, entry.Size);
            return output;
        }

        public void ReplaceEntry(string internalPath, byte[] replacement)
        {
            BnaEntry entry = FindEntry(internalPath);
            if (replacement.Length != entry.Size)
            {
                throw new InvalidDataException(
                    "Replacement size mismatch for " + internalPath + ": "
                    + replacement.Length.ToString() + " != " + entry.Size.ToString());
            }

            Buffer.BlockCopy(replacement, 0, data, entry.Offset, replacement.Length);
        }

        public void Save(string path)
        {
            File.WriteAllBytes(path, data);
        }

        private BnaEntry FindEntry(string internalPath)
        {
            string normalized = NormalizePath(internalPath);
            for (int index = 0; index < entries.Count; index++)
            {
                if (String.Equals(entries[index].Path, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return entries[index];
                }
            }

            throw new FileNotFoundException("BNA entry was not found.", internalPath);
        }

        private static List<BnaEntry> Parse(byte[] bytes)
        {
            if (bytes.Length < 8 || bytes[0] != (byte)'B' || bytes[1] != (byte)'N' || bytes[2] != (byte)'A' || bytes[3] != (byte)'0')
            {
                throw new InvalidDataException("BNA0 magic missing.");
            }

            int count = (int)ReadU32(bytes, 4);
            int tableEnd = 8 + count * 16;
            if (tableEnd > bytes.Length)
            {
                throw new InvalidDataException("BNA table extends past file end.");
            }

            List<BnaEntry> rows = new List<BnaEntry>();
            for (int index = 0; index < count; index++)
            {
                int rowOffset = 8 + index * 16;
                string dirName = ReadCString(bytes, (int)ReadU32(bytes, rowOffset));
                string fileName = ReadCString(bytes, (int)ReadU32(bytes, rowOffset + 4));
                int dataOffset = (int)ReadU32(bytes, rowOffset + 8);
                int size = (int)ReadU32(bytes, rowOffset + 12);
                if (dataOffset < 0 || size < 0 || dataOffset + size > bytes.Length)
                {
                    throw new InvalidDataException("BNA entry extends past file end.");
                }

                string path;
                if (dirName.Length > 0 && fileName.Length > 0)
                {
                    path = dirName + "/" + fileName;
                }
                else
                {
                    path = fileName.Length > 0 ? fileName : dirName;
                }

                rows.Add(new BnaEntry(NormalizePath(path), dataOffset, size));
            }

            return rows;
        }

        private static uint ReadU32(byte[] bytes, int offset)
        {
            return (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
        }

        private static string ReadCString(byte[] bytes, int offset)
        {
            if (offset < 0 || offset >= bytes.Length)
            {
                return String.Empty;
            }

            int end = offset;
            while (end < bytes.Length && bytes[end] != 0)
            {
                end++;
            }

            return Encoding.UTF8.GetString(bytes, offset, end - offset);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        private struct BnaEntry
        {
            public readonly string Path;
            public readonly int Offset;
            public readonly int Size;

            public BnaEntry(string path, int offset, int size)
            {
                Path = path;
                Offset = offset;
                Size = size;
            }
        }
    }
}
