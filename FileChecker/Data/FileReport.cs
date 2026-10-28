using FileChecker.Definitions;

namespace FileChecker.Data
{
    internal sealed class FileReport
    {
        public string Path { get; set; }

        public AdsRemoveResult Result { get; set; }

        public string Error { get; set; }
    }
}
