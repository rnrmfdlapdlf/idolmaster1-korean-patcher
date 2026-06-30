using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ImasKoreanPatcher
{
    internal sealed class HangulFontRemap
    {
        private readonly List<HangulFontRemapEntry> entries;

        private HangulFontRemap(List<HangulFontRemapEntry> entries)
        {
            this.entries = entries;
        }

        public IEnumerable<HangulFontRemapEntry> Entries
        {
            get { return entries; }
        }

        public static HangulFontRemap Load(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            string body = ExtractObjectBody(json, "hangul_to_donor");
            Regex entryPattern = new Regex(
                "\"(?<source>(?:\\\\.|[^\"])*)\"\\s*:\\s*\\{(?<body>.*?)\\}",
                RegexOptions.CultureInvariant | RegexOptions.Singleline);

            List<HangulFontRemapEntry> parsed = new List<HangulFontRemapEntry>();
            MatchCollection matches = entryPattern.Matches(body);
            foreach (Match match in matches)
            {
                string sourceText = DecodeJsonString(match.Groups["source"].Value);
                if (sourceText.Length != 1)
                {
                    continue;
                }

                string entryBody = match.Groups["body"].Value;
                string donorText = TryReadStringProperty(entryBody, "donor_char");
                string renderText = TryReadStringProperty(entryBody, "render_char");
                if (String.IsNullOrEmpty(renderText))
                {
                    renderText = sourceText;
                }

                int donorRecordIndex = TryReadIntProperty(entryBody, "donor_record_index", -1);
                if (donorRecordIndex < 0 || String.IsNullOrEmpty(donorText) || donorText.Length != 1 || renderText.Length != 1)
                {
                    continue;
                }

                parsed.Add(new HangulFontRemapEntry(
                    sourceText[0],
                    donorText[0],
                    renderText[0],
                    donorRecordIndex,
                    TryReadIntProperty(entryBody, "render_x_adjust", 0),
                    TryReadIntProperty(entryBody, "render_y_adjust", 0),
                    TryReadIntProperty(entryBody, "placement_x_adjust", 0),
                    TryReadIntProperty(entryBody, "placement_y_adjust", 0)));
            }

            if (parsed.Count == 0)
            {
                throw new InvalidDataException("No font remap entries were found.");
            }

            return new HangulFontRemap(parsed);
        }

        private static string ExtractObjectBody(string json, string propertyName)
        {
            string needle = "\"" + propertyName + "\"";
            int nameIndex = json.IndexOf(needle, StringComparison.Ordinal);
            if (nameIndex < 0)
            {
                throw new InvalidDataException(propertyName + " object was not found.");
            }

            int objectStart = json.IndexOf('{', nameIndex + needle.Length);
            if (objectStart < 0)
            {
                throw new InvalidDataException(propertyName + " object was not found.");
            }

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int index = objectStart; index < json.Length; index++)
            {
                char ch = json[index];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (ch == '\\')
                    {
                        escaped = true;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                }
                else if (ch == '{')
                {
                    depth++;
                }
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(objectStart + 1, index - objectStart - 1);
                    }
                }
            }

            throw new InvalidDataException(propertyName + " object is not closed.");
        }

        private static string TryReadStringProperty(string json, string propertyName)
        {
            string needle = "\"" + propertyName + "\"";
            int nameIndex = json.IndexOf(needle, StringComparison.Ordinal);
            if (nameIndex < 0)
            {
                return null;
            }

            int colonIndex = json.IndexOf(':', nameIndex + needle.Length);
            if (colonIndex < 0)
            {
                return null;
            }

            int quoteIndex = colonIndex + 1;
            while (quoteIndex < json.Length && Char.IsWhiteSpace(json[quoteIndex]))
            {
                quoteIndex++;
            }

            if (quoteIndex >= json.Length || json[quoteIndex] != '"')
            {
                return null;
            }

            int endQuote = quoteIndex + 1;
            bool escaped = false;
            while (endQuote < json.Length)
            {
                char ch = json[endQuote];
                if (escaped)
                {
                    escaped = false;
                }
                else if (ch == '\\')
                {
                    escaped = true;
                }
                else if (ch == '"')
                {
                    return DecodeJsonString(json.Substring(quoteIndex + 1, endQuote - quoteIndex - 1));
                }

                endQuote++;
            }

            return null;
        }

        private static int TryReadIntProperty(string json, string propertyName, int defaultValue)
        {
            string needle = "\"" + propertyName + "\"";
            int nameIndex = json.IndexOf(needle, StringComparison.Ordinal);
            if (nameIndex < 0)
            {
                return defaultValue;
            }

            int colonIndex = json.IndexOf(':', nameIndex + needle.Length);
            if (colonIndex < 0)
            {
                return defaultValue;
            }

            int index = colonIndex + 1;
            while (index < json.Length && Char.IsWhiteSpace(json[index]))
            {
                index++;
            }

            int start = index;
            if (index < json.Length && json[index] == '-')
            {
                index++;
            }

            while (index < json.Length && Char.IsDigit(json[index]))
            {
                index++;
            }

            int value;
            if (index > start && Int32.TryParse(json.Substring(start, index - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            return defaultValue;
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

    internal sealed class HangulFontRemapEntry
    {
        public HangulFontRemapEntry(char hangul, char donor, char render, int donorRecordIndex, int renderXAdjust, int renderYAdjust, int placementXAdjust, int placementYAdjust)
        {
            Hangul = hangul;
            Donor = donor;
            Render = render;
            DonorRecordIndex = donorRecordIndex;
            RenderXAdjust = renderXAdjust;
            RenderYAdjust = renderYAdjust;
            PlacementXAdjust = placementXAdjust;
            PlacementYAdjust = placementYAdjust;
        }

        public char Hangul { get; private set; }
        public char Donor { get; private set; }
        public char Render { get; private set; }
        public int DonorRecordIndex { get; private set; }
        public int RenderXAdjust { get; private set; }
        public int RenderYAdjust { get; private set; }
        public int PlacementXAdjust { get; private set; }
        public int PlacementYAdjust { get; private set; }
    }
}
