namespace FileChecker.Data
{
    /// <summary>
    /// 批量去除网络标记任务的汇总统计结果。
    /// <para>用于命令行静默模式与图形界面的结果汇总展示。</para>
    /// </summary>
    public sealed class CleanStats
    {
        /// <summary>本次处理的文件总数。</summary>
        public int Total;

        /// <summary>成功移除了网络标记的文件数。</summary>
        public int Removed;

        /// <summary>本身就没有网络标记（无需处理）的文件数。</summary>
        public int Clean;

        /// <summary>处理失败的文件数。</summary>
        public int Failed;
    }
}
