namespace FileChecker.Definitions
{
    /// <summary>
    /// 单个文件的校验码（MD5 / SHA256）计算所处的阶段，驱动界面中进度条与文本的展示。
    /// </summary>
    public enum HashStage
    {
        /// <summary>尚未开始计算（初始状态，界面显示占位符"—"）。</summary>
        None,
        /// <summary>正在计算中（界面显示实时进度条与百分比）。</summary>
        Computing,
        /// <summary>计算成功完成（显示 MD5 / SHA256 结果）。</summary>
        Done,
        /// <summary>计算失败（显示读取失败提示，悬停可查看原因）。</summary>
        Failed,
        /// <summary>计算被用户取消。</summary>
        Cancelled
    }
}
