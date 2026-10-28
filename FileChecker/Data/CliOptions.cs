using System.Collections.Generic;

namespace FileChecker.Data
{
    /// <summary>
    /// 命令行参数解析结果（由 <see cref="FileChecker.Utilities.CommandLine"/> 填充）。
    /// <para>当用户通过命令行（或把文件拖到程序图标上）调用本程序时，
    /// 该对象决定程序是以图形界面启动，还是以静默模式处理指定路径后退出。</para>
    /// </summary>
    public sealed class CliOptions
    {
        /// <summary>是否请求显示命令行帮助（-h / --help / /?）。</summary>
        internal bool ShowHelp { get; set; }

        /// <summary>是否递归处理文件夹的子目录（默认开启，可用 --no-recursive 关闭）。</summary>
        internal bool Recursive { get; set; } = true;

        /// <summary>结果摘要日志文件路径（--log=文件 或 -l 文件），为空表示不写日志。</summary>
        internal string LogFile { get; set; }

        /// <summary>参数解析错误信息；为 null 表示参数合法。</summary>
        internal string Error { get; set; }

        /// <summary>要处理的文件/文件夹路径列表。</summary>
        internal List<string> Paths { get; } = new();
    }
}
