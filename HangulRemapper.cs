using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ImasKoreanPatcher
{
    internal sealed class HangulRemapper
    {
        private readonly Dictionary<char, char> map;

        private HangulRemapper(Dictionary<char, char> map)
        {
            this.map = map;
        }

        public int Count
        {
            get { return map.Count; }
        }

        public static HangulRemapper Load(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            Dictionary<char, char> loaded = new Dictionary<char, char>();
            Regex entryPattern = new Regex(
                "\"(?<source>(?:\\\\.|[^\"])*)\"\\s*:\\s*\\{\\s*\"donor_char\"\\s*:\\s*\"(?<donor>(?:\\\\.|[^\"])*)\"",
                RegexOptions.CultureInvariant);

            MatchCollection matches = entryPattern.Matches(json);
            foreach (Match match in matches)
            {
                string sourceText = DecodeJsonString(match.Groups["source"].Value);
                string donorText = DecodeJsonString(match.Groups["donor"].Value);
                if (sourceText.Length != 1 || donorText.Length != 1)
                {
                    throw new InvalidDataException("Remap entries must use single UTF-16 characters.");
                }

                loaded[sourceText[0]] = donorText[0];
            }

            if (loaded.Count == 0)
            {
                throw new InvalidDataException("No hangul_to_donor entries were found in the remap file.");
            }

            return new HangulRemapper(loaded);
        }

        public void ValidateAll(IEnumerable<string> texts)
        {
            Dictionary<char, bool> missing = new Dictionary<char, bool>();
            foreach (string text in texts)
            {
                if (String.IsNullOrEmpty(text))
                {
                    continue;
                }

                for (int index = 0; index < text.Length; index++)
                {
                    char ch = text[index];
                    if (NeedsRemap(ch) && !map.ContainsKey(ch))
                    {
                        missing[ch] = true;
                    }
                }
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException("Remap data is missing Korean glyph(s): " + FormatMissing(missing));
            }
        }

        public string Apply(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return text;
            }

            StringBuilder output = null;
            Dictionary<char, bool> missing = null;
            for (int index = 0; index < text.Length; index++)
            {
                char ch = text[index];
                char donor;
                if (map.TryGetValue(ch, out donor))
                {
                    if (output == null)
                    {
                        output = new StringBuilder(text.Length);
                        output.Append(text, 0, index);
                    }

                    output.Append(donor);
                    continue;
                }

                if (NeedsRemap(ch))
                {
                    if (missing == null)
                    {
                        missing = new Dictionary<char, bool>();
                    }

                    missing[ch] = true;
                }

                if (output != null)
                {
                    output.Append(ch);
                }
            }

            if (missing != null && missing.Count > 0)
            {
                throw new InvalidOperationException("Remap data is missing Korean glyph(s): " + FormatMissing(missing));
            }

            return output == null ? text : output.ToString();
        }

        private static bool NeedsRemap(char ch)
        {
            return (ch >= '\u1100' && ch <= '\u11FF')
                || (ch >= '\u3130' && ch <= '\u318F')
                || (ch >= '\uA960' && ch <= '\uA97F')
                || (ch >= '\uAC00' && ch <= '\uD7A3')
                || (ch >= '\uD7B0' && ch <= '\uD7FF');
        }

        private static string FormatMissing(Dictionary<char, bool> missing)
        {
            List<char> chars = new List<char>(missing.Keys);
            chars.Sort();

            StringBuilder builder = new StringBuilder();
            int limit = Math.Min(chars.Count, 16);
            for (int index = 0; index < limit; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(chars[index]);
                builder.Append(" U+");
                builder.Append(((int)chars[index]).ToString("X4", CultureInfo.InvariantCulture));
            }

            if (chars.Count > limit)
            {
                builder.Append(", ... +");
                builder.Append((chars.Count - limit).ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string DecodeJsonString(string value)
        {
            StringBuilder output = new StringBuilder();
            for (int index = 0; index < value.Length; index++)
            {
                char ch = value[index];
                if (ch != '\\')
                {
                    output.Append(ch);
                    continue;
                }

                index++;
                if (index >= value.Length)
                {
                    break;
                }

                char escaped = value[index];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        output.Append(escaped);
                        break;
                    case 'b':
                        output.Append('\b');
                        break;
                    case 'f':
                        output.Append('\f');
                        break;
                    case 'n':
                        output.Append('\n');
                        break;
                    case 'r':
                        output.Append('\r');
                        break;
                    case 't':
                        output.Append('\t');
                        break;
                    case 'u':
                        if (index + 4 < value.Length)
                        {
                            string hex = value.Substring(index + 1, 4);
                            int decoded;
                            if (Int32.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out decoded))
                            {
                                output.Append((char)decoded);
                                index += 4;
                            }
                        }
                        break;
                    default:
                        output.Append(escaped);
                        break;
                }
            }

            return output.ToString();
        }
    }
}
