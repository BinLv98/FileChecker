using FileChecker.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileChecker.Data
{
    public sealed class TxtHashCompareResult
    {
        public string _txtPath;

        public bool _foundTxt;

        public bool _readFailed;

        public string _md5Ref;

        public string _sha256Ref;

        public HashMatchState _md5State;

        public HashMatchState _sha256State;
    }
}
