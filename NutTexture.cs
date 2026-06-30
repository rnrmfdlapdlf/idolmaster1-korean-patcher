using System;
using System.Collections.Generic;
using System.IO;

namespace ImasKoreanPatcher
{
    internal sealed class NutTexture
    {
        private readonly byte[] data;
        private readonly List<NutTexturePage> pages;

        private NutTexture(byte[] data, List<NutTexturePage> pages)
        {
            this.data = data;
            this.pages = pages;
        }

        public byte[] Data
        {
            get { return data; }
        }

        public NutTexturePage GetPage(int index)
        {
            if (index < 0 || index >= pages.Count)
            {
                return null;
            }

            return pages[index];
        }

        public static NutTexture Parse(byte[] data)
        {
            if (data.Length < 0x40 || data[0] != (byte)'N' || data[1] != (byte)'T' || data[2] != (byte)'X' || data[3] != (byte)'R')
            {
                throw new InvalidDataException("NTXR magic missing.");
            }

            int width = ReadU16(data, 0x24);
            int height = ReadU16(data, 0x26);
            if (width <= 0 || height <= 0 || (width % 4) != 0 || (height % 4) != 0)
            {
                throw new InvalidDataException("Unsupported NTXR BC3 dimensions.");
            }

            int dataSize = width * height;
            List<NutTexturePage> parsed = new List<NutTexturePage>();
            int searchStart = 0;
            while (searchStart < data.Length)
            {
                int pageStart = FindPageHeader(data, searchStart);
                if (pageStart < 0)
                {
                    break;
                }

                int dataOffset = pageStart + 0x20;
                if (dataOffset + dataSize > data.Length)
                {
                    throw new InvalidDataException("NTXR page data extends past file end.");
                }

                parsed.Add(new NutTexturePage(parsed.Count, dataOffset, dataSize, width, height));
                searchStart = pageStart + 1;
            }

            if (parsed.Count == 0)
            {
                throw new InvalidDataException("NTXR eXt page headers were not found.");
            }

            return new NutTexture(data, parsed);
        }

        private static int FindPageHeader(byte[] data, int start)
        {
            for (int index = start; index + 3 < data.Length; index++)
            {
                if (data[index] == (byte)'e' && data[index + 1] == (byte)'X' && data[index + 2] == (byte)'t' && data[index + 3] == 0)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int ReadU16(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
        }
    }

    internal sealed class NutTexturePage
    {
        public NutTexturePage(int index, int dataOffset, int dataSize, int width, int height)
        {
            Index = index;
            DataOffset = dataOffset;
            DataSize = dataSize;
            Width = width;
            Height = height;
        }

        public int Index { get; private set; }
        public int DataOffset { get; private set; }
        public int DataSize { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
    }
}
