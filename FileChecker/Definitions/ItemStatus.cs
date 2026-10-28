namespace FileChecker.Definitions
{
    /// <summary>
    /// 列表中单个文件项在处理"去除网络标记"任务过程中的状态。
    /// </summary>
    public enum ItemStatus
    {
        /// <summary>待处理（已加入列表但尚未开始去除标记）。</summary>
        Pending,
        /// <summary>正在处理中（后台线程正在尝试移除该文件的标记）。</summary>
        Processing,
        /// <summary>处理完成：已成功移除网络标记。</summary>
        Removed,
        /// <summary>处理完成：文件本来就没有网络标记。</summary>
        Clean,
        /// <summary>处理失败：无法移除标记，原因记录在错误字段中。</summary>
        Failed
    }
}
