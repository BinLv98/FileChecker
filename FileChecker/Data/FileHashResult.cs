namespace FileChecker.Data
{
    public sealed class FileHashResult
    {
        public bool Ok { get; set; }

        public bool Cancelled { get; set; }

        public string MD5 { get; set; }

        public string SHA256 { get; set; }

        public string Error { get; set; }
    }
}
