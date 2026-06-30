using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImasKoreanPatcher
{
    internal static class JsonTranslationStore
    {
        public static Dictionary<string, string> Load(string path)
        {
            Dictionary<string, string> translations = new Dictionary<string, string>(StringComparer.Ordinal);

            using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0)
                    {
                        continue;
                    }

                    string textId = TryReadStringProperty(line, "text_id");
                    string koText = TryReadStringProperty(line, "ko_text");
                    if (String.IsNullOrEmpty(textId) || String.IsNullOrEmpty(koText))
                    {
                        continue;
                    }

                    translations[textId] = koText;
                }
            }

            return translations;
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

            return ReadJsonString(json, quoteIndex);
        }

        private static string ReadJsonString(string json, int openingQuoteIndex)
        {
            StringBuilder output = new StringBuilder();
            for (int index = openingQuoteIndex + 1; index < json.Length; index++)
            {
                char ch = json[index];
                if (ch == '"')
                {
                    return output.ToString();
                }

                if (ch != '\\')
                {
                    output.Append(ch);
                    continue;
                }

                index++;
                if (index >= json.Length)
                {
                    break;
                }

                char escaped = json[index];
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
                        if (index + 4 < json.Length)
                        {
                            string hex = json.Substring(index + 1, 4);
                            int value;
                            if (Int32.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out value))
                            {
                                output.Append((char)value);
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
