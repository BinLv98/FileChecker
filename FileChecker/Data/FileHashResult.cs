using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileChecker.Data
{
    public sealed class FileHashResult
    {
        public bool _ok;

        public bool _cancelled;

        public string _md5;

        public string _sha256;

        public string _error;
    }
}
