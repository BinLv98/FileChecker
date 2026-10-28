using FileChecker.Data;
using FileChecker.Definitions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FileChecker.Utilities
{
    public static class HashTxtReader
    {
        private const string MD5_WORD = "MD5";

        private const string SHA_WORD = "SHA256";

        private const string SHA_WORD_DASH = "SHA-256";

        public static TxtHashCompareResult Compare(string itemPath, string md5, string sha256)
        {
            var txtHashCompareResult = new TxtHashCompareResult();
            var text = FindTxt(itemPath);
            if (text == null)
            {
                return txtHashCompareResult;
            }
            txtHashCompareResult._txtPath = text;
            txtHashCompareResult._foundTxt = true;
            string content;
            try
            {
                content = File.ReadAllText(text);
            }
            catch
            {
                txtHashCompareResult._readFailed = true;
                return txtHashCompareResult;
            }
            Parse(content, out var md6, out var sha257);
            txtHashCompareResult._md5Ref = md6;
            txtHashCompareResult._sha256Ref = sha257;
            txtHashCompareResult._md5State = CompareState(md6, md5);
            txtHashCompareResult._sha256State = CompareState(sha257, sha256);
            return txtHashCompareResult;
        }

        public static void Parse(string content, out string md5, out string sha256)
        {
            md5 = null;
            sha256 = null;
            if (string.IsNullOrEmpty(content))
            {
                return;
            }
            var array = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var t in array)
            {
                var text = t.Trim();
                if (text.Length == 0)
                {
                    continue;
                }
                if (TryPrefixedValue(text, "SHA256", out var value) || TryPrefixedValue(text, "SHA-256", out value))
                {
                    if (sha256 == null && value.Length > 0)
                    {
                        sha256 = value;
                    }
                }
                else if (TryPrefixedValue(text, "MD5", out value))
                {
                    if (md5 == null && value.Length > 0)
                    {
                        md5 = value;
                    }
                }
                else if (md5 == null)
                {
                    var text2 = PickValue(text);
                    if (text2 != null)
                    {
                        md5 = text2;
                    }
                }
                if (md5 != null && sha256 != null)
                {
                    break;
                }
            }
        }

        private static bool TryPrefixedValue(string line, string word, out string value)
        {
            value = null;
            if (line.Length <= word.Length)
            {
                return false;
            }
            if (line.IndexOf(word, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }
            var num = word.Length;
            var flag = false;
            while (num < line.Length && char.IsWhiteSpace(line[num]))
            {
                num++;
                flag = true;
            }
            if (num >= line.Length)
            {
                return false;
            }
            if (!flag && !IsSeparator(line[num]))
            {
                return false;
            }
            value = PickValue(line.Substring(num)) ?? string.Empty;
            return true;
        }

        private static string PickValue(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }
            string text = null;
            foreach (var item in Tokens(s))
            {
                if (IsHexAny(item))
                {
                    text ??= item;
                    if (item.Length is 32 or 64)
                    {
                        return item;
                    }
                }
            }
            var text2 = ComposeHex(s);
            if (IsHexAny(text2))
            {
                return text2;
            }
            return text;
        }

        private static IEnumerable<string> Tokens(string s)
        {
            var num = -1;
            for (var i = 0; i < s.Length; i++)
            {
                if (IsSeparator(s[i]))
                {
                    if (num >= 0)
                    {
                        yield return s.Substring(num, i - num);
                        num = -1;
                    }
                }
                else if (num < 0)
                {
                    num = i;
                }
            }
            if (num >= 0)
            {
                yield return s.Substring(num);
            }
        }

        private static string ComposeHex(string s)
        {
            var stringBuilder = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (!IsSeparator(c))
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString();
        }

        private static bool IsSeparator(char c)
        {
            if (c > '\u007f')
            {
                return true;
            }
            if (char.IsWhiteSpace(c))
            {
                return true;
            }
            switch (c)
            {
                case '!':
                case '"':
                case '#':
                case '$':
                case '%':
                case '&':
                case '\'':
                case '(':
                case ')':
                case '*':
                case '+':
                case ',':
                case ':':
                case ';':
                case '<':
                case '=':
                case '>':
                case '?':
                case '@':
                case '[':
                case ']':
                case '^':
                case '`':
                case '{':
                case '|':
                case '}':
                case '~':
                    return true;
                default:
                    return false;
            }
        }

        private static HashMatchState CompareState(string refValue, string computed)
        {
            if (string.IsNullOrEmpty(refValue) || string.IsNullOrEmpty(computed))
            {
                return HashMatchState.NoReference;
            }
            if (!string.Equals(refValue, computed, StringComparison.OrdinalIgnoreCase))
            {
                return HashMatchState.Mismatch;
            }
            return HashMatchState.Match;
        }

        private static string FindTxt(string itemPath)
        {
            var text = itemPath + ".txt";
            if (ExistsAndIsOtherFile(text, itemPath))
            {
                return text;
            }
            var directoryName = Path.GetDirectoryName(itemPath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(itemPath);
            var text2 = string.IsNullOrEmpty(directoryName) ? fileNameWithoutExtension + ".txt" : Path.Combine(directoryName, fileNameWithoutExtension + ".txt");
            if (ExistsAndIsOtherFile(text2, itemPath))
            {
                return text2;
            }
            return null;
        }

        private static bool ExistsAndIsOtherFile(string candidate, string itemPath)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }
            if (string.Equals(candidate, itemPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return File.Exists(candidate);
        }

        private static bool IsHexAny(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            foreach (var c in s)
            {
                if (c is (< '0' or > '9') and (< 'a' or > 'f') and (< 'A' or > 'F'))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
