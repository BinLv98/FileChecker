using FileChecker.Definitions;

namespace FileChecker.Data
{
    /// <summary>
    /// 单个文件去除网络标记的进度报告（由后台处理线程产生）。
    /// <para>在图形界面中，后台线程每处理完一个文件就会生成一条报告并通过
    /// 线程安全队列投递给 UI 线程，用于实时刷新列表项的状态。</para>
    /// </summary>
    internal sealed class FileReport
    {
        /// <summary>被处理的文件路径。</summary>
        public string Path { get; set; }

        /// <summary>该文件的处理结果（Removed / NoStream / Failed）。</summary>
        public AdsRemoveResult Result { get; set; }

        /// <summary>处理失败时的错误信息（成功时为 null）。</summary>
        public string Error { get; set; }
    }
}
