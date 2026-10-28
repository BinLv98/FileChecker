using FileChecker.Data;
using System;

namespace FileChecker.Utilities
{
    /// <summary>
    /// 命令行参数解析工具。
    /// <para>支持 /?、-h、--help、-r、--recursive、--no-recursive、-s、--silent、
    /// --log=文件、-l 文件等选项；也支持用 "--" 分隔符结束选项解析（后续参数一律视为路径）。
    /// 除路径外的未知选项会产生错误信息。</para>
    /// </summary>
    internal static class CommandLine
    {
        /// <summary>命令行帮助文本（来自本地化资源，含用法、选项说明、示例与退出码约定）。</summary>
        internal static string Usage { get; } = Localization.T("cmd.usage");

        /// <summary>
        /// 解析命令行参数。
        /// </summary>
        /// <param name="args">原始参数数组（不含程序名）。</param>
        /// <returns>解析结果对象；发生错误时 <see cref="CliOptions.Error"/> 非空。</returns>
        internal static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            // pathMode：是否已遇到 "--" 分隔符；之后所有参数一律按路径处理
            var pathMode = false;
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                // "--" 单独出现：切换为"后续均为路径"模式
                if (!pathMode && arg == "--")
                {
                    pathMode = true;
                }
                else
                {
                    // 以 "-" 或 "/" 开头且未处于路径模式 → 按选项解析
                    if (!pathMode && arg.Length > 0 && (arg[0] == '-' || arg[0] == '/'))
                    {
                        switch (arg)
                        {
                            // 帮助选项
                            case "/?":
                            case "-h":
                            case "--help":
                                options.ShowHelp = true;
                                continue;
                            // 递归处理子文件夹（默认已开启，此处显式开启）
                            case "-r":
                            case "--recursive":
                                options.Recursive = true;
                                continue;
                            // 关闭递归：仅处理文件夹内的直接文件
                            case "--no-recursive":
                            case "-t":
                                options.Recursive = false;
                                continue;
                            // 静默模式：传路径时本就默认静默，仅做接受处理
                            case "-s":
                            case "--silent":
                                continue;
                        }
                        // --log=文件：从等号后提取日志路径（去除可能包裹的引号）
                        if (arg.StartsWith("--log=", StringComparison.OrdinalIgnoreCase))
                        {
                            var logPath = arg.Substring("--log=".Length).Trim('"');
                            if (logPath.Length > 0)
                            {
                                options.LogFile = logPath;
                            }
                            continue;
                        }
                        // -l 文件：日志路径作为下一个参数传入
                        if (arg == "-l")
                        {
                            if (i + 1 < args.Length)
                            {
                                i++;
                                options.LogFile = args[i].Trim('"');
                            }
                            continue;
                        }
                        // 无法识别的选项：记录错误并立即返回
                        options.Error = Localization.F("cli.badArg", arg);
                        return options;
                    }
                    // 其余参数视为待处理的文件/文件夹路径
                    options.Paths.Add(arg);
                }
            }
            return options;
        }
    }
}
