using System.Collections.Generic;

namespace FileChecker.Data
{
    public sealed class CliOptions
    {
        internal bool ShowHelp { get; set; }

        internal bool Recursive { get; set; } = true;

        internal string LogFile { get; set; }

        internal string Error { get; set; }

        internal List<string> Paths { get; } = new();
    }
}
