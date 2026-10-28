using FileChecker.Data;
using System;

namespace FileChecker
{
    internal static class CommandLine
    {
        internal static string Usage { get; } = Localization.T("cmd.usage");

        internal static CliOptions Parse(string[] args)
        {
            var cliOptions = new CliOptions();
            var flag = false;
            for (var i = 0; i < args.Length; i++)
            {
                var text = args[i];
                if (!flag && text == "--")
                {
                    flag = true;
                }
                else
                {
                    if (!flag && text.Length > 0 && (text[0] == '-' || text[0] == '/'))
                    {
                        switch (text)
                        {
                            case "/?":
                            case "-h":
                            case "--help":
                                cliOptions.ShowHelp = true;
                                continue;
                            case "-r":
                            case "--recursive":
                                cliOptions.Recursive = true;
                                continue;
                            case "--no-recursive":
                            case "-t":
                                cliOptions.Recursive = false;
                                continue;
                            case "-s":
                            case "--silent":
                                continue;
                        }
                        if (text.StartsWith("--log=", StringComparison.OrdinalIgnoreCase))
                        {
                            var text2 = text.Substring("--log=".Length).Trim('"');
                            if (text2.Length > 0)
                            {
                                cliOptions.LogFile = text2;
                            }
                            continue;
                        }
                        if (text == "-l")
                        {
                            if (i + 1 < args.Length)
                            {
                                i++;
                                cliOptions.LogFile = args[i].Trim('"');
                            }
                            continue;
                        }
                        cliOptions.Error = Localization.F("cli.badArg", text);
                        return cliOptions;
                    }
                    cliOptions.Paths.Add(text);
                }
            }
            return cliOptions;
        }
    }
}
