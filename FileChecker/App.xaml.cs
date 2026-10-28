using FileChecker.Data;
using FileChecker.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FileChecker
{
    /// <summary>
    /// 应用程序入口（对应 App.xaml）。
    /// <para>在窗口启动前先检查命令行参数：参数错误时弹框并退出（退出码 2），
    /// 请求帮助时显示用法（退出码 0），带路径参数时直接进入静默处理模式；
    /// 都没有则正常启动图形界面。</para>
    /// </summary>
    public partial class App
    {
        /// <summary>
        /// 应用启动入口：优先处理命令行参数，再决定是否进入 GUI 主流程。
        /// </summary>
        /// <param name="e">启动事件参数，其中 Args 为命令行参数数组。</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 解析命令行参数
            var options = CommandLine.Parse(e.Args);
            // 情形 1：参数非法 —— 弹出错误对话框后以退出码 2 结束
            if (options.Error != null)
            {
                MessageBox.Show(options.Error + "\r\n\r\n" + CommandLine.Usage, Localization.T("app.badArgsTitle"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.ExitCode = 2;
                Shutdown();
                return;
            }
            // 情形 2：请求帮助（-h / --help / /?）—— 显示用法说明，退出码 0
            else if (options.ShowHelp)
            {
                MessageBox.Show(CommandLine.Usage, Localization.T("app.helpTitle"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
                Environment.ExitCode = 0;
                Shutdown();
                return;
            }
            // 情形 3：传入了文件/文件夹路径 —— 直接静默处理，完成后退出
            else if (options.Paths.Count != 0)
            {
                Environment.ExitCode = RunSilent(options);
                Shutdown();
                return;
            }

            // 情形 4：无参数 —— 正常加载主窗口（StartupUri 指定的 MainWindow.xaml）
            base.OnStartup(e);
        }

        /// <summary>
        /// 静默模式：批量去除指定路径上文件的网络来源标记，输出结果摘要后退出。
        /// <para>无界面，纯后台执行；支持多路径、递归/非递归、日志输出，退出码
        /// 0=全部成功，1=存在失败或不可访问项。</para>
        /// </summary>
        /// <param name="opts">解析后的命令行选项。</param>
        /// <returns>进程退出码（0 或 1）。</returns>
        private static int RunSilent(CliOptions opts)
        {
            // files：最终要处理的文件完整路径列表（去重）
            var files = new List<string>();
            // seenPaths：用于路径去重（大小写不敏感）
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // problemPaths：无法处理的项目（不存在的路径、内部错误等）及原因
            var problemPaths = new List<string>();
            // inaccessible：因权限等原因无法访问的子目录数
            var inaccessible = 0;
            // 逐一展开用户给出的路径
            foreach (var path in opts.Paths)
            {
                try
                {
                    // 目录或文件存在：展开为文件列表（目录会按 recursive 设置递归）
                    if (Directory.Exists(path) || File.Exists(path))
                    {
                        foreach (var file in AdsCleaner.ExpandPath(path, opts.Recursive, ref inaccessible))
                        {
                            // 去重：同一文件被多次指定时只处理一次
                            if (seenPaths.Add(file))
                            {
                                files.Add(file);
                            }
                        }
                    }
                    else
                    {
                        // 路径不存在：记录为无法处理
                        problemPaths.Add(path);
                    }
                }
                catch (Exception ex)
                {
                    // 展开过程中的异常：记录原因（本地化格式，如"（拒绝访问）"）
                    problemPaths.Add(path + Localization.F("cli.missing", ex.Message));
                }
            }
            CleanStats stats;
            try
            {
                // 后台并行执行批量去除标记（无取消令牌，等待完成）
                var task = Task.Run(() => AdsCleaner.ProcessFiles(files, null, CancellationToken.None));
                task.Wait();
                stats = task.Result;
            }
            catch (Exception ex)
            {
                // 整体失败：全部文件按失败统计
                stats = new CleanStats
                {
                    Total = files.Count,
                    Failed = files.Count
                };
                problemPaths.Add(Localization.F("cli.internalError", ex.Message));
            }
            // 组装结果摘要文本
            var sb = new StringBuilder();
            sb.AppendLine(Localization.F("cli.summary", stats.Total, stats.Removed, stats.Clean, stats.Failed));
            // 补充不可访问的子目录数量
            if (inaccessible > 0)
            {
                sb.AppendLine(Localization.F("cli.inaccessible", inaccessible));
            }
            // 补充无法处理的项目明细
            if (problemPaths.Count > 0)
            {
                sb.AppendLine(Localization.T("cli.cannot"));
                foreach (var entry in problemPaths)
                {
                    sb.AppendLine("  - " + entry);
                }
            }
            // 输出：指定了 --log 则写入文件（UTF-8），否则打印到控制台
            if (!string.IsNullOrEmpty(opts.LogFile))
            {
                try
                {
                    File.WriteAllText(opts.LogFile, sb.ToString(), Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    // 日志写入失败：尝试退回控制台提示；静默模式下可能无控制台，全部吞掉
                    try
                    {
                        Console.WriteLine(Localization.F("cli.logFail", ex.Message));
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            else
            {
                try
                {
                    Console.WriteLine(sb.ToString());
                }
                catch
                {
                    // GUI 子系统下无控制台时忽略输出异常
                    // ignored
                }
            }
            // 退出码：只要存在失败项或不可访问项即为 1
            return stats.Failed + problemPaths.Count + inaccessible > 0 ? 1 : 0;
        }
    }
}
