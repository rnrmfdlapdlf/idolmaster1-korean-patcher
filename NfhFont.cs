using System;
using System.Collections.Generic;
using System.IO;

namespace ImasKoreanPatcher
{
    internal sealed class NfhFont
    {
        private const int RecordSize = 32;

        private readonly List<NfhGlyph> glyphs;

        private NfhFont(int fontSize, List<NfhGlyph> glyphs)
        {
            FontSize = fontSize;
            this.glyphs = glyphs;
        }

        public int FontSize { get; private set; }

        public int GlyphCount
        {
            get { return glyphs.Count; }
        }

        public NfhGlyph GetGlyph(int recordIndex)
        {
            if (recordIndex < 0 || recordIndex >= glyphs.Count)
            {
                throw new IndexOutOfRangeException("NFH glyph record index is out of range.");
            }

            return glyphs[recordIndex];
        }

        public static NfhFont Parse(byte[] data)
        {
            if (data.Length < 16 || data[0] != (byte)'N' || data[1] != (byte)'F' || data[2] != (byte)'H' || data[3] != 0)
            {
                throw new InvalidDataException("NFH magic missing.");
            }

            int fontSize = (int)ReadU32(data, 4);
            int glyphCount = (int)ReadU32(data, 8);
            int recordOffset = data.Length - glyphCount * RecordSize;
            if (recordOffset < 16 || recordOffset + glyphCount * RecordSize != data.Length)
            {
                throw new InvalidDataException("NFH glyph record area does not match glyph count.");
            }

            List<NfhGlyph> parsed = new List<NfhGlyph>();
            for (int index = 0; index < glyphCount; index++)
            {
                int offset = recordOffset + index * RecordSize;
                ushort[] fields = new ushort[16];
                for (int field = 0; field < fields.Length; field++)
                {
                    fields[field] = ReadU16(data, offset + field * 2);
                }

                parsed.Add(new NfhGlyph(index, fields));
            }

            return new NfhFont(fontSize, parsed);
        }

        private static uint ReadU32(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        private static ushort ReadU16(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }
    }

    internal sealed class NfhGlyph
    {
        private readonly ushort[] fields;

        public NfhGlyph(int recordIndex, ushort[] fields)
        {
            RecordIndex = recordIndex;
            this.fields = fields;
        }

        public int RecordIndex { get; private set; }
        public int PageIndex { get { return fields[1]; } }
        public int X { get { return fields[2]; } }
        public int Y { get { return fields[3]; } }
        public int Codepoint { get { return fields[11]; } }

        public char Character
        {
            get { return (char)Codepoint; }
        }
    }
}
