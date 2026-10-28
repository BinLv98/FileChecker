using FileChecker.Definitions;

namespace FileChecker.Data
{
    public sealed class TxtHashCompareResult
    {
        public string TxtPath { get; set; }

        public bool FoundTxt { get; set; }

        public bool ReadFailed { get; set; }

        public string MD5Ref { get; set; }

        public string SHA256Ref { get; set; }

        public HashMatchState MD5State { get; set; }

        public HashMatchState SHA256State { get; set; }
    }
}
