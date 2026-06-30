using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ImasKoreanPatcher
{
    internal sealed class XexTextPatcher
    {
        private const int MaxCandidateChars = 256;

        private readonly Dictionary<string, string> translations;
        private readonly HangulRemapper remapper;

        public XexTextPatcher(Dictionary<string, string> translations, HangulRemapper remapper)
        {
            this.translations = translations;
            this.remapper = remapper;
        }

        public XexPatchResult PatchExtractedRoot(string extractedRoot, string assetRoot, string workRoot, Action<int, string> progress)
        {
            XexPatchResult result = new XexPatchResult();
            result.TranslationRows = translations.Count;
            if (translations.Count == 0)
            {
                return result;
            }

            string defaultXexPath = Path.Combine(extractedRoot, "default.xex");
            string xexToolPath = Path.Combine(assetRoot, Path.Combine("Tools", "xextool.exe"));
            if (!File.Exists(defaultXexPath))
            {
                throw new FileNotFoundException("default.xex를 찾을 수 없습니다.", defaultXexPath);
            }

            if (!File.Exists(xexToolPath))
            {
                throw new FileNotFoundException("xextool.exe를 찾을 수 없습니다.", xexToolPath);
            }

            string patchWorkRoot = Path.Combine(workRoot, "xex_patch");
            Directory.CreateDirectory(patchWorkRoot);

            string decryptedPath = Path.Combine(patchWorkRoot, "default_decrypted_uncompressed.xex");
            Report(progress, 66, "default.xex 변환 중...");
            ExternalToolRunner.Run(
                xexToolPath,
                "-e d -c u -o " + ExternalToolRunner.QuoteArgument(decryptedPath) + " " + ExternalToolRunner.QuoteArgument(defaultXexPath),
                Path.GetDirectoryName(xexToolPath),
                "xextool.exe");

            if (!File.Exists(decryptedPath))
            {
                throw new FileNotFoundException("변환된 default.xex를 찾을 수 없습니다.", decryptedPath);
            }

            byte[] data = File.ReadAllBytes(decryptedPath);
            int originalSize = checked((int)new FileInfo(defaultXexPath).Length);
            Report(progress, 69, "default.xex 문자열 패치 중...");
            PatchUtf16BeStrings(data, result);

            if (result.StringsPatched == 0)
            {
                throw new InvalidOperationException("default.xex에 반영된 문자열이 0개입니다.");
            }

            if (data.Length > originalSize)
            {
                throw new InvalidOperationException("패치된 default.xex가 원본 ISO 파일보다 큽니다.");
            }

            if (data.Length < originalSize)
            {
                byte[] padded = new byte[originalSize];
                Buffer.BlockCopy(data, 0, padded, 0, data.Length);
                data = padded;
            }

            File.WriteAllBytes(defaultXexPath, data);
            Report(progress, 72, String.Format("default.xex 패치 완료: {0:N0}개 문자열", result.StringsPatched));
            return result;
        }

        private void PatchUtf16BeStrings(byte[] data, XexPatchResult result)
        {
            List<PatchedRange> patchedRanges = new List<PatchedRange>();

            for (int offset = 0; offset + 4 <= data.Length; offset += 2)
            {
                CandidateString candidate;
                if (!TryReadCandidate(data, offset, out candidate))
                {
                    continue;
                }

                result.CandidateStringsScanned++;
                string textId = "jp_" + Sha1Prefix(candidate.Text);
                string koText;
                if (!translations.TryGetValue(textId, out koText))
                {
                    continue;
                }

                result.StringsMatched++;
                if (OverlapsAny(offset, candidate.EndOffset, patchedRanges))
                {
                    continue;
                }

                string donorText;
                try
                {
                    donorText = remapper.Apply(koText);
                }
                catch
                {
                    result.MissingRemapErrors++;
                    continue;
                }

                byte[] encoded = Encoding.BigEndianUnicode.GetBytes(donorText);
                int payloadLength = encoded.Length + 2;
                if (payloadLength > candidate.SlotBytes)
                {
                    result.ReplacementsTooLong++;
                    continue;
                }

                Array.Clear(data, offset, candidate.SlotBytes);
                Buffer.BlockCopy(encoded, 0, data, offset, encoded.Length);
                patchedRanges.Add(new PatchedRange(offset, offset + candidate.SlotBytes));
                result.StringsPatched++;
            }
        }

        private static bool TryReadCandidate(byte[] data, int offset, out CandidateString candidate)
        {
            candidate = null;
            StringBuilder builder = new StringBuilder();
            int position = offset;

            while (position + 1 < data.Length && builder.Length <= MaxCandidateChars)
            {
                int value = (data[position] << 8) | data[position + 1];
                position += 2;
                if (value == 0)
                {
                    break;
                }

                char ch = (char)value;
                if (!IsLikelyTextChar(ch))
                {
                    return false;
                }

                builder.Append(ch);
            }

            if (builder.Length == 0 || builder.Length > MaxCandidateChars || position + 1 >= data.Length)
            {
                return false;
            }

            int end = position;
            while (end + 1 < data.Length && data[end] == 0 && data[end + 1] == 0)
            {
                end += 2;
                if (end - offset > (MaxCandidateChars + 1) * 2)
                {
                    break;
                }
            }

            candidate = new CandidateString(builder.ToString(), end - offset, end);
            return true;
        }

        private static bool IsLikelyTextChar(char ch)
        {
            if (ch == '\n' || ch == '\r' || ch == '\t')
            {
                return true;
            }

            if (Char.IsSurrogate(ch) || Char.IsControl(ch))
            {
                return false;
            }

            return ch >= 0x20;
        }

        private static bool OverlapsAny(int start, int end, List<PatchedRange> ranges)
        {
            for (int index = 0; index < ranges.Count; index++)
            {
                PatchedRange range = ranges[index];
                if (!(end <= range.Start || start >= range.End))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Sha1Prefix(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(16);
                for (int index = 0; index < 8; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static void Report(Action<int, string> progress, int percent, string message)
        {
            if (progress != null)
            {
                progress(percent, message);
            }
        }

        private sealed class CandidateString
        {
            public readonly string Text;
            public readonly int SlotBytes;
            public readonly int EndOffset;

            public CandidateString(string text, int slotBytes, int endOffset)
            {
                Text = text;
                SlotBytes = slotBytes;
                EndOffset = endOffset;
            }
        }

        private struct PatchedRange
        {
            public readonly int Start;
            public readonly int End;

            public PatchedRange(int start, int end)
            {
                Start = start;
                End = end;
            }
        }
    }
}
