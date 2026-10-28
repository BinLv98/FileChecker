namespace FileChecker.Data
{
    /// <summary>
    /// 单个文件校验码（MD5 / SHA256）计算的结果（由 <see cref="FileChecker.Utilities.FileHasher"/> 产生）。
    /// <para>校验码以十六进制小写字符串形式保存，用于在界面展示、复制，以及与参照 txt 比对。</para>
    /// </summary>
    public sealed class FileHashResult
    {
        /// <summary>是否成功计算出了两个校验码。</summary>
        public bool Ok { get; set; }

        /// <summary>是否因用户取消而中止计算。</summary>
        public bool Cancelled { get; set; }

        /// <summary>计算得到的 MD5 值（32 位十六进制字符串）。</summary>
        public string MD5 { get; set; }

        /// <summary>计算得到的 SHA256 值（64 位十六进制字符串）。</summary>
        public string SHA256 { get; set; }

        /// <summary>计算失败时的错误信息。</summary>
        public string Error { get; set; }
    }
}
