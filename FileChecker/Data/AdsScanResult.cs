using System.Collections.Generic;

namespace FileChecker.Data
{
    /// <summary>
    /// 单个文件的附加数据流（ADS）扫描结果。
    /// <para>扫描结果不仅包含原始流列表，还包含对 Zone.Identifier 下载来源标记的解析结论
    /// （ZoneId 分区编号、是否附带额外数据等），供界面以文字/颜色直观展示文件状态。</para>
    /// </summary>
    public sealed class AdsScanResult
    {
        /// <summary>被扫描文件的路径。</summary>
        public string Path { get; set; }

        /// <summary>扫描是否成功（失败时 Error 保存原因）。</summary>
        public bool Ok { get; set; }

        /// <summary>扫描失败时的错误信息（成功时为 null）。</summary>
        public string Error { get; set; }

        /// <summary>该文件全部附加数据流的列表（含主数据流）。</summary>
        public List<AdsStreamInfo> Streams { get; set; } = new();

        /// <summary>是否检测到 Zone.Identifier 下载来源标记流。</summary>
        public bool HasZone { get; set; }

        /// <summary>Zone.Identifier 流的字节长度。</summary>
        public long ZoneLength { get; set; }

        /// <summary>Zone.Identifier 流的原始文本内容（读取失败时为 null）。</summary>
        public string ZoneContent { get; set; }

        /// <summary>
        /// 从 Zone.Identifier 内容中解析出的分区编号（ZoneId）。
        /// <para>取值范围：0=本地、1=内网、2=受信站点、3=网络（Internet）、4=受限站点；
        /// -1 表示未解析到有效编号。</para>
        /// </summary>
        public int ZoneId { get; set; } = -1;

        /// <summary>Zone.Identifier 内容中是否还包含除分区信息外的其他额外数据。</summary>
        public bool ZoneHasExtraData { get; set; }

        /// <summary>命名（非主）数据流的数量，即附加数据流总数（含 Zone.Identifier 本身）。</summary>
        public int NamedStreamCount
        {
            get
            {
                // 遍历所有流，统计不是主数据流的个数
                var num = 0;
                foreach (var stream in Streams)
                {
                    if (!stream.IsMainData)
                    {
                        num++;
                    }
                }
                return num;
            }
        }

        /// <summary>除 Zone.Identifier 以外的其他命名流数量（额外的附加数据条数）。</summary>
        public int OtherNamedCount => NamedStreamCount - (HasZone ? 1 : 0);

        /// <summary>
        /// 该文件是否带有"额外信息"。
        /// <para>含义：除了单纯的下载来源标记之外，还携带其他附加数据（Zone 内容中的额外行，
        /// 或其他命名流），这些内容可在"文件标记详情"窗口中查看原文。</para>
        /// </summary>
        public bool HasExtraInfo
        {
            get
            {
                // 只要 Zone 内容含额外数据，或存在其他命名流，就视为有额外信息
                if (!HasZone || !ZoneHasExtraData)
                {
                    return OtherNamedCount > 0;
                }
                return true;
            }
        }

        /// <summary>
        /// 下载来源标记的本地化文字标签（如"来自网络(3)"）。
        /// <para>未检测到 Zone 或 ZoneId 不在 0~4 范围内时返回 null。</para>
        /// </summary>
        public string ZoneLabel
        {
            get
            {
                if (!HasZone || ZoneId < 0 || ZoneId > 4)
                {
                    return null;
                }
                return Localization.T("zone." + ZoneId) + "(" + ZoneId + ")";
            }
        }

        /// <summary>
        /// 列表"文件标记"列显示的简短文字。
        /// <para>优先级：读取失败 → 有 Zone 标签（必要时带"+"表示有额外信息）→
        /// 有其他附加数据 → 无任何标记。</para>
        /// </summary>
        public string MarkText
        {
            get
            {
                // 扫描失败：直接显示"读取失败"
                if (!Ok)
                {
                    return Localization.T("item.readFailed");
                }
                var zoneLabel = ZoneLabel;
                if (zoneLabel != null)
                {
                    // 有可识别的下载来源标记，带额外信息时追加"+"号
                    if (!HasExtraInfo)
                    {
                        return zoneLabel;
                    }
                    return zoneLabel + "+";
                }
                // 有 Zone 但无法识别 ZoneId，或存在其他附加流：统一显示"其他"
                if (HasZone || NamedStreamCount > 0)
                {
                    return Localization.T("ads.other");
                }
                return Localization.T("ads.none");
            }
        }

        /// <summary>
        /// 生成悬停提示中使用的简短说明文字（比 MarkText 更详细）。
        /// </summary>
        public string ShortInfo()
        {
            // 扫描失败：提示错误原因
            if (!Ok)
            {
                return Localization.F("item.errPrefix", string.IsNullOrEmpty(Error) ? Localization.T("item.unknown") : Error);
            }
            var zoneLabel = ZoneLabel;
            if (zoneLabel != null)
            {
                // 能识别 ZoneId：展示解析结果，必要时补充"还带有附加信息"
                var text = Localization.F("info.parsed", zoneLabel);
                if (HasExtraInfo)
                {
                    text += Localization.T("info.extra");
                }
                return text;
            }
            // 有 Zone 但识别不出含义 / 有其他附加流 / 完全干净，三种情况分别说明
            if (HasZone)
            {
                return Localization.T("info.unknownZone");
            }
            if (NamedStreamCount > 0)
            {
                return Localization.T("info.otherAds");
            }
            return Localization.T("info.clean");
        }

        /// <summary>
        /// 将字节长度格式化为便于阅读的字符串（MB / KB / 字节）。
        /// <para>用于详情窗口等场景展示附加数据流的大小。</para>
        /// </summary>
        /// <param name="len">字节长度。</param>
        /// <returns>格式化后的长度字符串（含单位）。</returns>
        public static string FormatLen(long len)
        {
            // 1 MB 以上按 MB 显示，保留最多两位小数
            if (len >= 1048576)
            {
                return (len / 1048576.0).ToString("0.##") + Localization.T("fmt.mb");
            }
            // 1 KB ~ 1 MB 之间按 KB 显示
            if (len >= 1024)
            {
                return (len / 1024.0).ToString("0.##") + Localization.T("fmt.kb");
            }
            // 不足 1 KB 按字节显示
            return len + Localization.T("fmt.bytes");
        }
    }
}
