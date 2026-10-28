namespace FileChecker.Data
{
    /// <summary>
    /// 单个文件附加数据流（ADS）的描述信息。
    /// <para>NTFS 文件可以包含多个附加数据流，其中主数据流（$DATA）承载常规文件内容，
    /// 其余命名数据流承载附加信息（如下载来源标记 Zone.Identifier）。</para>
    /// </summary>
    public sealed class AdsStreamInfo
    {
        /// <summary>数据流的名称（不含前导冒号），主数据流为空字符串。</summary>
        public string Name { get; set; }

        /// <summary>数据流的字节长度。</summary>
        public long Length { get; set; }

        /// <summary>是否为文件的主数据流（即承载常规文件内容的数据流）。</summary>
        public bool IsMainData { get; set; }
    }
}
