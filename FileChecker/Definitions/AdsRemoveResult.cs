namespace FileChecker.Definitions
{
    /// <summary>
    /// 移除文件附加数据流（ADS，Alternate Data Streams）操作的结果状态。
    /// <para>
    /// 文件附加数据流是 NTFS 文件系统中的一种机制，允许在常规文件内容之外再挂载多个
    /// 独立的数据流。下载自网络的文件的 Zone.Identifier 流（即"下载来源标记"）就是
    /// 其中最常见的一种，它会导致资源管理器打开文件时弹出"安全警告"。
    /// </para>
    /// </summary>
    public enum AdsRemoveResult
    {
        /// <summary>成功移除了一个或多个附加数据流。</summary>
        Removed,
        /// <summary>文件本来就不存在任何附加数据流（无需移除）。</summary>
        NoStream,
        /// <summary>移除失败，具体原因由调用方通过 out 参数获取。</summary>
        Failed
    }
}
