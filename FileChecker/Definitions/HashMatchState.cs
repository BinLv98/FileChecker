namespace FileChecker.Definitions
{
    /// <summary>
    /// 计算出的校验码与参照 txt 文件中记录的校验码之间的比对结果状态。
    /// <para>参照 txt 是指与目标文件同名（或文件名后追加 ".txt"）的文本文件，
    /// 其中可能记录了该文件预期的 MD5 / SHA256 值，用于校验文件完整性。</para>
    /// </summary>
    public enum HashMatchState
    {
        /// <summary>参照 txt 中不存在有效的对应记录（无法比对）。</summary>
        NoReference,
        /// <summary>计算值与参照值一致。</summary>
        Match,
        /// <summary>计算值与参照值不一致（文件可能已被篡改或损坏）。</summary>
        Mismatch
    }
}
