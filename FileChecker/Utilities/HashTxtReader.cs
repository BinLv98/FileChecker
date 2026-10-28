using FileChecker.Data;
using FileChecker.Definitions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FileChecker.Utilities
{
    /// <summary>
    /// 参照 txt 文件解析与校验码比对工具。
    /// <para>约定：与目标文件同目录、同名（或文件名加 ".txt"）的文本文件被视为参照文件，
    /// 其中可能以 "MD5=..."/"SHA256=..." 等形式记录了目标文件预期的校验码；
    /// 计算完实际校验码后与其比对，用于检验文件完整性。</para>
    /// </summary>
    public static class HashTxtReader
    {
        /// <summary>"MD5" 关键字的规范写法。</summary>
        private const string MD5_WORD = "MD5";

        /// <summary>"SHA256" 关键字的规范写法。</summary>
        private const string SHA_WORD = "SHA256";

        /// <summary>"SHA-256" 关键字的兼容写法（带连字符）。</summary>
        private const string SHA_WORD_DASH = "SHA-256";

        /// <summary>
        /// 比对单个文件的计算校验码与其参照 txt 记录值。
        /// </summary>
        /// <param name="itemPath">目标文件路径。</param>
        /// <param name="md5">计算得到的 MD5 值。</param>
        /// <param name="sha256">计算得到的 SHA256 值。</param>
        /// <returns>
        /// 比对结果；未找到参照 txt 时 <see cref="TxtHashCompareResult.FoundTxt"/> 为 false。
        /// </returns>
        public static TxtHashCompareResult Compare(string itemPath, string md5, string sha256)
        {
            var txtHashCompareResult = new TxtHashCompareResult();
            // 先按命名约定查找参照 txt
            var text = FindTxt(itemPath);
            if (text == null)
            {
                return txtHashCompareResult;
            }
            txtHashCompareResult.TxtPath = text;
            txtHashCompareResult.FoundTxt = true;
            string content;
            try
            {
                content = File.ReadAllText(text);
            }
            catch
            {
                // 找到了但读不了（占用、无权限等）：标记读取失败，跳过比对
                txtHashCompareResult.ReadFailed = true;
                return txtHashCompareResult;
            }
            // 解析内容中的 MD5 / SHA256 记录
            Parse(content, out var md6, out var sha257);
            txtHashCompareResult.MD5Ref = md6;
            txtHashCompareResult.SHA256Ref = sha257;
            txtHashCompareResult.MD5State = CompareState(md6, md5);
            txtHashCompareResult.SHA256State = CompareState(sha257, sha256);
            return txtHashCompareResult;
        }

        /// <summary>
        /// 从参照 txt 内容中解析 MD5 与 SHA256 记录值。
        /// <para>支持的写法：<c>MD5=xxxx</c>、<c>SHA256=xxxx</c>、<c>SHA-256=xxxx</c>；
        /// 也支持无前缀的裸哈希值（此时按 32 位→MD5、64 位→SHA256 的优先级自动识别，
        /// 否则取第一个十六进制片段作为 MD5）。</para>
        /// </summary>
        /// <param name="content">参照 txt 的文本内容。</param>
        /// <param name="md5">解析出的 MD5 值（没有则为 null）。</param>
        /// <param name="sha256">解析出的 SHA256 值（没有则为 null）。</param>
        public static void Parse(string content, out string md5, out string sha256)
        {
            md5 = null;
            sha256 = null;
            if (string.IsNullOrEmpty(content))
            {
                return;
            }
            // 按行解析
            var array = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var t in array)
            {
                var text = t.Trim();
                if (text.Length == 0)
                {
                    continue;
                }
                // 优先识别带前缀的行：SHA256 / SHA-256 / MD5
                if (TryPrefixedValue(text, "SHA256", out var value) || TryPrefixedValue(text, "SHA-256", out value))
                {
                    // 只取第一个出现的 SHA256 记录
                    if (sha256 == null && value.Length > 0)
                    {
                        sha256 = value;
                    }
                }
                else if (TryPrefixedValue(text, "MD5", out value))
                {
                    // 只取第一个出现的 MD5 记录
                    if (md5 == null && value.Length > 0)
                    {
                        md5 = value;
                    }
                }
                else if (md5 == null)
                {
                    // 无前缀的行：从中智能提取哈希值，作为 MD5 的兜底来源
                    var text2 = PickValue(text);
                    if (text2 != null)
                    {
                        md5 = text2;
                    }
                }
                // 两个值都齐了即可提前结束
                if (md5 != null && sha256 != null)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 尝试把一行按 "关键字=值" 的带前缀格式解析出值。
        /// <para>要求行首是关键字（大小写不敏感），其后允许零个或多个空白，
        /// 再跟分隔符（=、: 等）或直接跟值。</para>
        /// </summary>
        /// <param name="line">待解析的行。</param>
        /// <param name="word">关键字（如 "MD5"）。</param>
        /// <param name="value">解析出的值（格式不符时保持 null）。</param>
        /// <returns>该行是否匹配给定的前缀格式。</returns>
        private static bool TryPrefixedValue(string line, string word, out string value)
        {
            value = null;
            // 行长度必须大于关键字长度
            if (line.Length <= word.Length)
            {
                return false;
            }
            // 行首必须命中关键字
            if (line.IndexOf(word, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }
            var num = word.Length;
            var flag = false;
            // 跳过关键字后的空白字符
            while (num < line.Length && char.IsWhiteSpace(line[num]))
            {
                num++;
                flag = true;
            }
            // 关键字后没有任何内容：不算有效记录
            if (num >= line.Length)
            {
                return false;
            }
            // 若关键字与值之间没有空白，则必须紧跟分隔符，防止误匹配 "MD5XX..."
            if (!flag && !IsSeparator(line[num]))
            {
                return false;
            }
            value = PickValue(line.Substring(num)) ?? string.Empty;
            return true;
        }

        /// <summary>
        /// 从一段文本中提取哈希值。
        /// <para>策略：先按分隔符切分成片段，若存在 32 位或 64 位的十六进制片段则优先返回；
        /// 否则取第一个十六进制片段；再退而求其次，去掉全部分隔符后拼接成完整十六进制串。</para>
        /// </summary>
        private static string PickValue(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }
            string text = null;
            // 逐片段扫描：记录第一个十六进制片段，同时寻找长度恰为 32/64 的
            foreach (var item in Tokens(s))
            {
                if (IsHexAny(item))
                {
                    // 记下第一个十六进制片段作为兜底
                    text ??= item;
                    // 32 位（MD5）或 64 位（SHA256）的片段直接命中
                    if (item.Length is 32 or 64)
                    {
                        return item;
                    }
                }
            }
            // 兜底 1：去掉所有分隔符后拼接（应对 "aa bb cc ..." 的分散写法）
            var text2 = ComposeHex(s);
            if (IsHexAny(text2))
            {
                return text2;
            }
            // 兜底 2：返回找到的第一个十六进制片段（可能不是完整长度）
            return text;
        }

        /// <summary>
        /// 按分隔符把字符串切分成若干连续片段（惰性生成）。
        /// </summary>
        private static IEnumerable<string> Tokens(string s)
        {
            var num = -1;
            for (var i = 0; i < s.Length; i++)
            {
                if (IsSeparator(s[i]))
                {
                    // 遇到分隔符：结束当前片段
                    if (num >= 0)
                    {
                        yield return s.Substring(num, i - num);
                        num = -1;
                    }
                }
                else if (num < 0)
                {
                    // 新片段开始
                    num = i;
                }
            }
            // 收尾：返回最后一个片段
            if (num >= 0)
            {
                yield return s.Substring(num);
            }
        }

        /// <summary>去掉字符串中的全部分隔符，返回拼接后的紧凑字符串。</summary>
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

        /// <summary>
        /// 判断字符是否为"分隔符"。
        /// <para>分隔符包括：空白、非 ASCII 字符，以及常见的标点符号。
        /// 哈希字符串中不应包含这些字符。</para>
        /// </summary>
        private static bool IsSeparator(char c)
        {
            // 非 ASCII（如中文、全角符号）一律视为分隔符
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
                // ASCII 标点符号集合
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

        /// <summary>
        /// 比对参照值与实际计算值。
        /// </summary>
        /// <param name="refValue">参照 txt 中记录的值。</param>
        /// <param name="computed">实际计算出的值。</param>
        /// <returns>比对状态（无参照 / 一致 / 不一致）。</returns>
        private static HashMatchState CompareState(string refValue, string computed)
        {
            // 任何一侧缺失都无法比对
            if (string.IsNullOrEmpty(refValue) || string.IsNullOrEmpty(computed))
            {
                return HashMatchState.NoReference;
            }
            // 大小写不敏感比较
            if (!string.Equals(refValue, computed, StringComparison.OrdinalIgnoreCase))
            {
                return HashMatchState.Mismatch;
            }
            return HashMatchState.Match;
        }

        /// <summary>
        /// 按命名约定查找目标文件对应的参照 txt 文件。
        /// <para>查找顺序：① "完整路径.txt"（如 a.bin.txt）；② 同目录下
        /// "文件名.txt"（如 a.txt）。两者都不存在时返回 null。</para>
        /// </summary>
        private static string FindTxt(string itemPath)
        {
            // 候选 1：在文件全名后直接追加 ".txt"
            var text = itemPath + ".txt";
            if (ExistsAndIsOtherFile(text, itemPath))
            {
                return text;
            }
            // 候选 2：同名（去扩展名）加 ".txt"
            var directoryName = Path.GetDirectoryName(itemPath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(itemPath);
            var text2 = string.IsNullOrEmpty(directoryName) ? fileNameWithoutExtension + ".txt" : Path.Combine(directoryName, fileNameWithoutExtension + ".txt");
            if (ExistsAndIsOtherFile(text2, itemPath))
            {
                return text2;
            }
            return null;
        }

        /// <summary>
        /// 判断候选文件是否真实存在，且不是目标文件本身。
        /// <para>防止出现"文件叫 a.txt、参照候选也叫 a.txt"时把自己当成参照的歧义。</para>
        /// </summary>
        private static bool ExistsAndIsOtherFile(string candidate, string itemPath)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }
            // 候选与目标路径完全相同（大小写不敏感）时排除
            if (string.Equals(candidate, itemPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return File.Exists(candidate);
        }

        /// <summary>
        /// 判断字符串是否全由十六进制字符（0-9、a-f、A-F）组成。
        /// </summary>
        private static bool IsHexAny(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            foreach (var c in s)
            {
                // 不在数字 0-9 也不在小写 a-f 也不在大写 A-F 范围内 → 非法字符
                if (c is (< '0' or > '9') and (< 'a' or > 'f') and (< 'A' or > 'F'))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
