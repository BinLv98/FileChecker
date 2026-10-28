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
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var cliOptions = CommandLine.Parse(e.Args);
            if (cliOptions.Error != null)
            {
                MessageBox.Show(cliOptions.Error + "\r\n\r\n" + CommandLine.Usage, Localization.T("app.badArgsTitle"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.ExitCode = 2;
                Shutdown();
                return;
            }
            else if (cliOptions.ShowHelp)
            {
                MessageBox.Show(CommandLine.Usage, Localization.T("app.helpTitle"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
                Environment.ExitCode = 0;
                Shutdown();
                return;
            }
            else if (cliOptions.Paths.Count != 0)
            {
                Environment.ExitCode = RunSilent(cliOptions);
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        private static int RunSilent(CliOptions opts)
        {
            var files = new List<string>();
            var hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            var inaccessible = 0;
            foreach (var path in opts.Paths)
            {
                try
                {
                    if (Directory.Exists(path) || File.Exists(path))
                    {
                        foreach (var item in AdsCleaner.ExpandPath(path, opts.Recursive, ref inaccessible))
                        {
                            if (hashSet.Add(item))
                            {
                                files.Add(item);
                            }
                        }
                    }
                    else
                    {
                        list.Add(path);
                    }
                }
                catch (Exception ex)
                {
                    list.Add(path + Localization.F("cli.missing", ex.Message));
                }
            }
            CleanStats cleanStats;
            try
            {
                var task = Task.Run(() => AdsCleaner.ProcessFiles(files, null, CancellationToken.None));
                task.Wait();
                cleanStats = task.Result;
            }
            catch (Exception ex2)
            {
                cleanStats = new CleanStats
                {
                    Total = files.Count,
                    Failed = files.Count
                };
                list.Add(Localization.F("cli.internalError", ex2.Message));
            }
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(Localization.F("cli.summary", cleanStats.Total, cleanStats.Removed, cleanStats.Clean, cleanStats.Failed));
            if (inaccessible > 0)
            {
                stringBuilder.AppendLine(Localization.F("cli.inaccessible", inaccessible));
            }
            if (list.Count > 0)
            {
                stringBuilder.AppendLine(Localization.T("cli.cannot"));
                foreach (var item2 in list)
                {
                    stringBuilder.AppendLine("  - " + item2);
                }
            }
            if (!string.IsNullOrEmpty(opts.LogFile))
            {
                try
                {
                    File.WriteAllText(opts.LogFile, stringBuilder.ToString(), Encoding.UTF8);
                }
                catch (Exception ex3)
                {
                    try
                    {
                        Console.WriteLine(Localization.F("cli.logFail", ex3.Message));
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
                    Console.WriteLine(stringBuilder.ToString());
                }
                catch
                {
                    // ignored
                }
            }
            return cleanStats.Failed + list.Count + inaccessible > 0 ? 1 : 0;
        }
    }
}
