using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ImasKoreanPatcher
{
    internal sealed class XboxTextPatcher
    {
        private const int ScbSectionTable = 0x70;
        private const int ScbSectionCount = 7;
        private const int MsgCountOffset = 0x20;
        private const int MsgTableOffset = 0x30;

        private readonly Dictionary<string, string> translations;

        public XboxTextPatcher(Dictionary<string, string> translations)
        {
            this.translations = translations;
        }

        public TranslationPatchResult PatchExtractedRoot(string extractedRoot, Action<int, string> progress)
        {
            TranslationPatchResult result = new TranslationPatchResult();
            result.TranslationRows = translations.Count;

            string[] bnaFiles = Directory.GetFiles(extractedRoot, "*.bna", SearchOption.AllDirectories);
            for (int index = 0; index < bnaFiles.Length; index++)
            {
                if (progress != null && (index == 0 || index % 25 == 0))
                {
                    int percent = 35 + (int)(30.0 * index / Math.Max(1, bnaFiles.Length));
                    progress(percent, String.Format("BNA \ud328\uce58 \uc911... {0:N0}/{1:N0}", index, bnaFiles.Length));
                }

                result.BnaFilesScanned++;
                try
                {
                    PatchBnaFile(bnaFiles[index], result);
                }
                catch
                {
                    result.Errors++;
                }
            }

            if (progress != null)
            {
                progress(65, String.Format("BNA \ud328\uce58 \uc644\ub8cc: {0:N0}\uac1c \ud30c\uc77c, {1:N0}\uac1c \ubb38\uc790\uc5f4", result.BnaFilesPatched, result.MsgEntriesPatched));
            }

            return result;
        }

        private void PatchBnaFile(string path, TranslationPatchResult result)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 8 || data[0] != (byte)'B' || data[1] != (byte)'N' || data[2] != (byte)'A' || data[3] != (byte)'0')
            {
                return;
            }

            List<BnaEntry> entries = ParseBna(data);
            bool changed = false;

            for (int i = 0; i < entries.Count; i++)
            {
                BnaEntry entry = entries[i];
                if (!entry.Path.EndsWith(".scb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.ScbFilesScanned++;
                ScbPatchResult scbPatch = PatchScb(entry.Data);
                result.MsgEntriesScanned += scbPatch.MsgEntriesScanned;
                result.MsgEntriesPatched += scbPatch.MsgEntriesPatched;
                result.MissingTranslations += scbPatch.MissingTranslations;

                if (scbPatch.Changed)
                {
                    entry.Data = scbPatch.Data;
                    entries[i] = entry;
                    result.ScbFilesPatched++;
                    changed = true;
                }
            }

            if (changed)
            {
                File.WriteAllBytes(path, RebuildBna(entries));
                result.BnaFilesPatched++;
            }
        }

        private ScbPatchResult PatchScb(byte[] scbData)
        {
            ScbPatchResult result = new ScbPatchResult();
            result.Data = scbData;

            if (scbData.Length < ScbSectionTable + ScbSectionCount * 16 || !StartsWithAscii(scbData, "SCB"))
            {
                return result;
            }

            List<ScbSection> sections = ParseScb(scbData);
            for (int i = 0; i < sections.Count; i++)
            {
                ScbSection section = sections[i];
                if (section.Label != "MSG")
                {
                    continue;
                }

                MsgPatchResult msgPatch = PatchMsg(section.Data);
                result.MsgEntriesScanned += msgPatch.MsgEntriesScanned;
                result.MsgEntriesPatched += msgPatch.MsgEntriesPatched;
                result.MissingTranslations += msgPatch.MissingTranslations;

                if (msgPatch.Changed)
                {
                    section.Data = msgPatch.Data;
                    sections[i] = section;
                    result.Data = RebuildScb(scbData, sections);
                    result.Changed = true;
                }

                return result;
            }

            return result;
        }

        private MsgPatchResult PatchMsg(byte[] msgData)
        {
            MsgPatchResult result = new MsgPatchResult();
            result.Data = msgData;

            if (msgData.Length < MsgTableOffset || !StartsWithAscii(msgData, "MSG"))
            {
                return result;
            }

            int count = ReadU16(msgData, MsgCountOffset);
            int tableEnd = MsgTableOffset + count * 8;
            int zeroPoint = Align(tableEnd, 0x10);
            if (zeroPoint > msgData.Length)
            {
                return result;
            }

            List<string> texts = new List<string>();
            bool changed = false;

            for (int i = 0; i < count; i++)
            {
                int tableOffset = MsgTableOffset + i * 8;
                int size = (int)ReadU32(msgData, tableOffset);
                int textOffset = (int)ReadU32(msgData, tableOffset + 4);
                int start = zeroPoint + textOffset;
                int end = start + size;
                if (start < 0 || end > msgData.Length || size < 2)
                {
                    return result;
                }

                string text = DecodeUtf16Be(msgData, start, size - 2);
                result.MsgEntriesScanned++;

                string textId = "jp_" + Sha1Prefix(text);
                string koText;
                if (translations.TryGetValue(textId, out koText))
                {
                    texts.Add(koText);
                    result.MsgEntriesPatched++;
                    changed = true;
                }
                else
                {
                    texts.Add(text);
                    result.MissingTranslations++;
                }
            }

            if (changed)
            {
                result.Data = BuildMsg(msgData, texts);
                result.Changed = true;
            }

            return result;
        }

        private static List<BnaEntry> ParseBna(byte[] data)
        {
            int count = (int)ReadU32(data, 4);
            int tableEnd = 8 + count * 16;
            if (tableEnd > data.Length)
            {
                throw new InvalidDataException("BNA table extends past file end.");
            }

            List<BnaEntry> entries = new List<BnaEntry>();
            for (int index = 0; index < count; index++)
            {
                int offset = 8 + index * 16;
                int dirOffset = (int)ReadU32(data, offset);
                int fileOffset = (int)ReadU32(data, offset + 4);
                int dataOffset = (int)ReadU32(data, offset + 8);
                int size = (int)ReadU32(data, offset + 12);
                if (dataOffset < 0 || dataOffset + size > data.Length)
                {
                    throw new InvalidDataException("BNA entry extends past file end.");
                }

                string dirName = ReadCString(data, dirOffset);
                string fileName = ReadCString(data, fileOffset);
                string entryPath = NormalizePath((dirName.Length > 0 && fileName.Length > 0) ? dirName + "/" + fileName : dirName + fileName);

                byte[] entryData = new byte[size];
                Buffer.BlockCopy(data, dataOffset, entryData, 0, size);

                entries.Add(new BnaEntry
                {
                    DirectoryName = dirName,
                    FileName = fileName,
                    Path = entryPath,
                    Data = entryData
                });
            }

            return entries;
        }

        private static byte[] RebuildBna(List<BnaEntry> entries)
        {
            MemoryStream stream = new MemoryStream();
            WriteAscii(stream, "BNA0");
            WriteU32(stream, (uint)entries.Count);

            long tablePosition = stream.Position;
            for (int i = 0; i < entries.Count * 16; i++)
            {
                stream.WriteByte(0);
            }

            Dictionary<string, uint> nameOffsets = new Dictionary<string, uint>(StringComparer.Ordinal);
            List<BnaTableRow> tableRows = new List<BnaTableRow>();
            for (int i = 0; i < entries.Count; i++)
            {
                tableRows.Add(new BnaTableRow
                {
                    DirectoryOffset = GetNameOffset(stream, nameOffsets, entries[i].DirectoryName),
                    FileOffset = GetNameOffset(stream, nameOffsets, entries[i].FileName),
                    Size = (uint)entries[i].Data.Length
                });
            }

            for (int i = 0; i < entries.Count; i++)
            {
                PadStream(stream, 0x80, 0);
                BnaTableRow row = tableRows[i];
                row.DataOffset = (uint)stream.Position;
                tableRows[i] = row;
                byte[] entryData = entries[i].Data;
                stream.Write(entryData, 0, entryData.Length);
            }

            byte[] output = stream.ToArray();
            for (int i = 0; i < tableRows.Count; i++)
            {
                int rowOffset = (int)tablePosition + i * 16;
                WriteU32(output, rowOffset, tableRows[i].DirectoryOffset);
                WriteU32(output, rowOffset + 4, tableRows[i].FileOffset);
                WriteU32(output, rowOffset + 8, tableRows[i].DataOffset);
                WriteU32(output, rowOffset + 12, tableRows[i].Size);
            }

            return output;
        }

        private static List<ScbSection> ParseScb(byte[] data)
        {
            List<ScbSection> sections = new List<ScbSection>();
            for (int index = 0; index < ScbSectionCount; index++)
            {
                int tableOffset = ScbSectionTable + index * 16;
                byte[] labelRaw = new byte[4];
                Buffer.BlockCopy(data, tableOffset, labelRaw, 0, 4);
                uint size = ReadU32(data, tableOffset + 4);
                uint offset = ReadU32(data, tableOffset + 8);
                byte[] pad = new byte[4];
                Buffer.BlockCopy(data, tableOffset + 12, pad, 0, 4);
                if (offset + size > data.Length)
                {
                    throw new InvalidDataException("SCB section extends past file end.");
                }

                byte[] sectionData = new byte[size];
                Buffer.BlockCopy(data, (int)offset, sectionData, 0, (int)size);

                sections.Add(new ScbSection
                {
                    Index = index,
                    LabelRaw = labelRaw,
                    Label = DecodeSectionLabel(labelRaw),
                    Offset = (int)offset,
                    Pad = pad,
                    Data = sectionData
                });
            }

            return sections;
        }

        private static byte[] RebuildScb(byte[] original, List<ScbSection> sections)
        {
            List<ScbSection> ordered = new List<ScbSection>(sections);
            ordered.Sort(delegate(ScbSection left, ScbSection right)
            {
                return left.Offset.CompareTo(right.Offset);
            });

            int firstOffset = ordered[0].Offset;
            MemoryStream stream = new MemoryStream();
            stream.Write(original, 0, firstOffset);

            bool postMsg = false;
            Dictionary<int, int> newOffsets = new Dictionary<int, int>();
            Dictionary<int, int> newSizes = new Dictionary<int, int>();
            for (int i = 0; i < ordered.Count; i++)
            {
                ScbSection section = ordered[i];
                newOffsets[section.Index] = (int)stream.Position;
                newSizes[section.Index] = section.Data.Length;
                stream.Write(section.Data, 0, section.Data.Length);
                PadStream(stream, 0x10, postMsg ? (byte)0xCC : (byte)0xCD);
                if (section.Label == "MSG")
                {
                    postMsg = true;
                }
            }

            PadStream(stream, 0x10, 0xCC);
            byte[] output = stream.ToArray();
            WriteU32(output, 0x10, (uint)(output.Length - 0x20));

            for (int i = 0; i < sections.Count; i++)
            {
                ScbSection section = sections[i];
                int tableOffset = ScbSectionTable + section.Index * 16;
                Buffer.BlockCopy(section.LabelRaw, 0, output, tableOffset, 4);
                WriteU32(output, tableOffset + 4, (uint)newSizes[section.Index]);
                WriteU32(output, tableOffset + 8, (uint)newOffsets[section.Index]);
                Buffer.BlockCopy(section.Pad, 0, output, tableOffset + 12, 4);
            }

            return output;
        }

        private static byte[] BuildMsg(byte[] original, List<string> texts)
        {
            MemoryStream stream = new MemoryStream();
            int prefixLength = Math.Min(MsgTableOffset, original.Length);
            stream.Write(original, 0, prefixLength);
            while (stream.Length < MsgTableOffset)
            {
                stream.WriteByte(0);
            }

            WriteU16ToStreamBuffer(stream, MsgCountOffset, (ushort)texts.Count);

            List<byte[]> payloads = new List<byte[]>();
            int stringDataSize = 0;
            for (int i = 0; i < texts.Count; i++)
            {
                byte[] encoded = EncodeUtf16Be(texts[i]);
                byte[] payload = new byte[encoded.Length + 2];
                Buffer.BlockCopy(encoded, 0, payload, 0, encoded.Length);
                payloads.Add(payload);
                stringDataSize += payload.Length;
            }

            int headerSize = 16 + texts.Count * 8 + ((texts.Count % 2) == 1 ? 8 : 0);
            WriteU16ToStreamBuffer(stream, 0x26, (ushort)(stringDataSize & 0xFFFF));
            WriteU16ToStreamBuffer(stream, 0x2A, (ushort)headerSize);

            int textOffset = 0;
            for (int i = 0; i < payloads.Count; i++)
            {
                WriteU32(stream, (uint)payloads[i].Length);
                WriteU32(stream, (uint)textOffset);
                textOffset += payloads[i].Length;
            }

            PadStream(stream, 0x10, 0xCD);
            for (int i = 0; i < payloads.Count; i++)
            {
                stream.Write(payloads[i], 0, payloads[i].Length);
            }

            byte[] output = stream.ToArray();
            WriteU32(output, 0x10, (uint)(output.Length - 0x20));
            return output;
        }

        private static uint GetNameOffset(MemoryStream stream, Dictionary<string, uint> offsets, string name)
        {
            if (name == null)
            {
                name = String.Empty;
            }

            uint offset;
            if (offsets.TryGetValue(name, out offset))
            {
                return offset;
            }

            offset = (uint)stream.Position;
            offsets[name] = offset;
            byte[] bytes = Encoding.UTF8.GetBytes(name);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte(0);
            return offset;
        }

        private static bool StartsWithAscii(byte[] data, string value)
        {
            if (data.Length < value.Length)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (data[i] != (byte)value[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static string DecodeUtf16Be(byte[] data, int offset, int byteCount)
        {
            Encoding encoding = Encoding.BigEndianUnicode;
            return encoding.GetString(data, offset, byteCount);
        }

        private static byte[] EncodeUtf16Be(string text)
        {
            return Encoding.BigEndianUnicode.GetBytes(text);
        }

        private static string Sha1Prefix(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(16);
                for (int i = 0; i < 8; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private static uint ReadU32(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        private static int ReadU16(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
        }

        private static void WriteU32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >> 8) & 0xFF);
            data[offset + 3] = (byte)(value & 0xFF);
        }

        private static void WriteU32(Stream stream, uint value)
        {
            stream.WriteByte((byte)((value >> 24) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private static void WriteU16ToStreamBuffer(MemoryStream stream, int offset, ushort value)
        {
            long oldPosition = stream.Position;
            stream.Position = offset;
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
            stream.Position = oldPosition;
        }

        private static void WriteAscii(Stream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void PadStream(Stream stream, int boundary, byte value)
        {
            while ((stream.Position % boundary) != 0)
            {
                stream.WriteByte(value);
            }
        }

        private static int Align(int value, int boundary)
        {
            int over = value % boundary;
            return over == 0 ? value : value + boundary - over;
        }

        private static string ReadCString(byte[] data, int offset)
        {
            if (offset < 0 || offset >= data.Length)
            {
                return String.Empty;
            }

            int end = offset;
            while (end < data.Length && data[end] != 0)
            {
                end++;
            }

            return Encoding.UTF8.GetString(data, offset, end - offset);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        private static string DecodeSectionLabel(byte[] bytes)
        {
            int length = 0;
            while (length < bytes.Length && bytes[length] != 0)
            {
                length++;
            }

            return Encoding.ASCII.GetString(bytes, 0, length);
        }

        private struct BnaEntry
        {
            public string DirectoryName;
            public string FileName;
            public string Path;
            public byte[] Data;
        }

        private struct BnaTableRow
        {
            public uint DirectoryOffset;
            public uint FileOffset;
            public uint DataOffset;
            public uint Size;
        }

        private struct ScbSection
        {
            public int Index;
            public byte[] LabelRaw;
            public string Label;
            public int Offset;
            public byte[] Pad;
            public byte[] Data;
        }

        private sealed class ScbPatchResult
        {
            public bool Changed;
            public byte[] Data;
            public int MsgEntriesScanned;
            public int MsgEntriesPatched;
            public int MissingTranslations;
        }

        private sealed class MsgPatchResult
        {
            public bool Changed;
            public byte[] Data;
            public int MsgEntriesScanned;
            public int MsgEntriesPatched;
            public int MissingTranslations;
        }
    }
}
