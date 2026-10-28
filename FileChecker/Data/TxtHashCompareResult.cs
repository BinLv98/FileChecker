using FileChecker.Definitions;

namespace FileChecker.Data
{
    /// <summary>
    /// 校验码与参照 txt 文件比对的详细结果（由 <see cref="FileChecker.Utilities.HashTxtReader"/> 产生）。
    /// <para>参照 txt 是与目标文件同目录的同名 ".txt" 文件，其中可能记录有预期的
    /// MD5 / SHA256 值；比对结果会在界面悬停提示中以彩色文字与"一致 / 不一致"标注展示。</para>
    /// </summary>
    public sealed class TxtHashCompareResult
    {
        /// <summary>找到的参照 txt 文件的完整路径（未找到时为 null）。</summary>
        public string TxtPath { get; set; }

        /// <summary>是否找到了参照 txt 文件。</summary>
        public bool FoundTxt { get; set; }

        /// <summary>找到参照 txt 但读取内容失败（无法进行比对）。</summary>
        public bool ReadFailed { get; set; }

        /// <summary>参照 txt 中记录的 MD5 值（未记录时为 null）。</summary>
        public string MD5Ref { get; set; }

        /// <summary>参照 txt 中记录的 SHA256 值（未记录时为 null）。</summary>
        public string SHA256Ref { get; set; }

        /// <summary>MD5 比对结果（NoReference / Match / Mismatch）。</summary>
        public HashMatchState MD5State { get; set; }

        /// <summary>SHA256 比对结果（NoReference / Match / Mismatch）。</summary>
        public HashMatchState SHA256State { get; set; }
    }
}
