using FileChecker.Data;
using FileChecker.Definitions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace FileChecker
{
    public sealed class FileItem : INotifyPropertyChanged
    {
        private ItemStatus _status;

        private string _removeError;

        private AdsScanResult _adsResult;

        private string _md5Raw;

        private string _sha256Raw;

        private string _hashError;

        private Brush _md5Foreground = _hashPendingBrush;

        private Brush _sha256Foreground = _hashPendingBrush;

        private HashStage _hashStage;

        private TxtHashCompareResult _txtCompare;

        private int _hashPercent = -1;

        private static readonly Brush _readingBrush = Make(152, 162, 179);

        private static readonly Brush _zoneBrush = Make(198, 40, 40);

        private static readonly Brush _cleanBrush = Make(24, 155, 70);

        private static readonly Brush _otherStreamBrush = Make(107, 114, 128);

        private static readonly Brush _errorBrush = Make(181, 71, 8);

        private static readonly Brush _hashPendingBrush = Make(152, 162, 179);

        private static readonly Brush _hashResultBrush = Make(17, 24, 39);

        private static readonly Brush _hashErrorBrush = Make(181, 71, 8);

        private static readonly Brush _hashMatchBrush = Make(24, 155, 70);

        private static readonly Brush _hashMismatchBrush = Make(217, 48, 37);

        public string Path { get; }

        public ItemStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    Raise("Status");
                    Raise("Message");
                    Raise("Tooltip");
                }
            }
        }

        public string Message
        {
            get
            {
                switch (_status)
                {
                    case ItemStatus.Removed:
                        return Localization.T("item.removed");
                    case ItemStatus.Clean:
                        return Localization.T("item.noMark");
                    case ItemStatus.Failed:
                        if (!string.IsNullOrWhiteSpace(_removeError))
                        {
                            return _removeError;
                        }
                        return Localization.T("item.cannotRemove");
                    default:
                        return null;
                }
            }
        }

        public AdsScanResult AdsResult => _adsResult;

        public string AdsText
        {
            get
            {
                if (_adsResult == null)
                {
                    return Localization.T("item.reading");
                }
                return _adsResult.MarkText;
            }
        }

        public Brush AdsForeground
        {
            get
            {
                if (_adsResult == null)
                {
                    return _readingBrush;
                }
                if (!_adsResult.Ok)
                {
                    return _errorBrush;
                }
                if (_adsResult.HasZone)
                {
                    return _zoneBrush;
                }
                if (_adsResult.NamedStreamCount <= 0)
                {
                    return _cleanBrush;
                }
                return _otherStreamBrush;
            }
        }

        public string AdsDetail
        {
            get
            {
                if (_adsResult == null)
                {
                    return null;
                }
                return _adsResult.ShortInfo();
            }
        }

        public string Tooltip
        {
            get
            {
                var list = new List<string> { Path };
                if (_adsResult != null)
                {
                    var adsText = AdsText;
                    if (!string.IsNullOrEmpty(adsText) && adsText != Localization.T("item.reading"))
                    {
                        list.Add(Localization.T("tip.mark") + adsText);
                    }
                    if (!string.IsNullOrEmpty(AdsDetail))
                    {
                        list.Add(AdsDetail);
                    }
                }
                var message = Message;
                if (!string.IsNullOrEmpty(message))
                {
                    list.Add(Localization.T("tip.result") + message);
                }
                if (_hashStage == HashStage.Done)
                {
                    list.Add("MD5: " + _md5Raw);
                    list.Add("SHA256: " + _sha256Raw);
                    if (_txtCompare is { FoundTxt: true })
                    {
                        list.Add(Localization.T("tip.refTxt") + _txtCompare.TxtPath);
                        if (_txtCompare.ReadFailed)
                        {
                            list.Add(Localization.T("tip.refTxtFail"));
                        }
                        else
                        {
                            list.Add(Localization.T("tip.md5Cmp") + DescribeState(_txtCompare.MD5State, _txtCompare.MD5Ref, "MD5"));
                            list.Add(Localization.T("tip.shaCmp") + DescribeState(_txtCompare.SHA256State, _txtCompare.SHA256Ref, "SHA256"));
                        }
                    }
                }
                else if (_hashStage == HashStage.Failed)
                {
                    list.Add(Localization.F("item.hashFailedTip", _hashError ?? Localization.T("item.unknown")));
                }
                return string.Join(Environment.NewLine, list);
            }
        }

        public string Md5 => HashDisplay(_md5Raw);

        public string Sha256 => HashDisplay(_sha256Raw);

        public string Md5Raw => _md5Raw;

        public string Sha256Raw => _sha256Raw;

        public HashStage HashStage
        {
            get => _hashStage;
            private set
            {
                if (_hashStage != value)
                {
                    _hashStage = value;
                    Raise("HashStage");
                    Raise("Md5");
                    Raise("Sha256");
                    Raise("Tooltip");
                }
            }
        }

        public Brush Md5Foreground
        {
            get => _md5Foreground;
            private set
            {
                if (_md5Foreground != value)
                {
                    _md5Foreground = value;
                    Raise("Md5Foreground");
                }
            }
        }

        public Brush Sha256Foreground
        {
            get => _sha256Foreground;
            private set
            {
                if (_sha256Foreground != value)
                {
                    _sha256Foreground = value;
                    Raise("Sha256Foreground");
                }
            }
        }

        public int HashPercent
        {
            get => _hashPercent;
            private set
            {
                if (_hashPercent != value)
                {
                    _hashPercent = value;
                    Raise("HashPercent");
                    Raise("HashPercentText");
                }
            }
        }

        public string HashPercentText
        {
            get
            {
                if (_hashPercent >= 0)
                {
                    return _hashPercent + "%";
                }
                return "";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public FileItem(string path)
        {
            Path = path;
        }

        public void SetAds(AdsScanResult r)
        {
            _adsResult = r;
            Raise("AdsText");
            Raise("AdsForeground");
            Raise("AdsDetail");
            Raise("Tooltip");
        }

        public void MarkAdsPending()
        {
            _adsResult = null;
            Raise("AdsText");
            Raise("AdsForeground");
            Raise("AdsDetail");
            Raise("Tooltip");
        }

        private static string DescribeState(HashMatchState state, string refValue, string what)
        {
            return state switch
            {
                HashMatchState.Match => Localization.T("tip.match"),
                HashMatchState.Mismatch => Localization.F("tip.mismatchRef", what, refValue),
                _ => Localization.F("tip.noRef", what),
            };
        }

        public void Complete(AdsRemoveResult result, string error)
        {
            _removeError = error;
            switch (result)
            {
                case AdsRemoveResult.Removed:
                    Status = ItemStatus.Removed;
                    break;
                case AdsRemoveResult.NoStream:
                    Status = ItemStatus.Clean;
                    break;
                default:
                    Status = ItemStatus.Failed;
                    break;
            }
        }

        public void MarkProcessing()
        {
            _removeError = null;
            Status = ItemStatus.Processing;
        }

        public void Reset()
        {
            _removeError = null;
            Status = ItemStatus.Pending;
        }

        private string HashDisplay(string raw)
        {
            switch (_hashStage)
            {
                case HashStage.Computing:
                    return Localization.T("item.computing");
                case HashStage.Done:
                    if (!string.IsNullOrEmpty(raw))
                    {
                        return raw;
                    }
                    return "—";
                case HashStage.Failed:
                    return Localization.T("item.readFailed");
                case HashStage.Cancelled:
                    return Localization.T("item.cancelled");
                default:
                    return "—";
            }
        }

        public void MarkHashPending()
        {
            _md5Raw = null;
            _sha256Raw = null;
            _hashError = null;
            _txtCompare = null;
            Md5Foreground = _hashPendingBrush;
            Sha256Foreground = _hashPendingBrush;
            HashStage = HashStage.Computing;
            SetHashPercent(0);
        }

        public void SetHashResult(string md5Hex, string sha256Hex)
        {
            _md5Raw = string.IsNullOrEmpty(md5Hex) ? null : md5Hex;
            _sha256Raw = string.IsNullOrEmpty(sha256Hex) ? null : sha256Hex;
            _hashError = null;
            HashStage = HashStage.Done;
            UpdateCompareForeground();
            ResetHashProgress();
        }

        public void SetTxtComparison(TxtHashCompareResult compare)
        {
            _txtCompare = compare;
            if (_hashStage == HashStage.Done)
            {
                UpdateCompareForeground();
                Raise("Tooltip");
            }
        }

        private void UpdateCompareForeground()
        {
            var txtCompare = _txtCompare;
            Md5Foreground = ForegroundFor(txtCompare?.MD5State ?? HashMatchState.NoReference);
            Sha256Foreground = ForegroundFor(txtCompare?.SHA256State ?? HashMatchState.NoReference);
        }

        private static Brush ForegroundFor(HashMatchState state)
        {
            return state switch
            {
                HashMatchState.Match => _hashMatchBrush,
                HashMatchState.Mismatch => _hashMismatchBrush,
                _ => _hashResultBrush,
            };
        }

        public void SetHashError(string error)
        {
            _md5Raw = null;
            _sha256Raw = null;
            _hashError = string.IsNullOrEmpty(error) ? Localization.T("item.unknown") : error;
            _txtCompare = null;
            Md5Foreground = _hashErrorBrush;
            Sha256Foreground = _hashErrorBrush;
            HashStage = HashStage.Failed;
            ResetHashProgress();
        }

        public void SetHashCancelled()
        {
            _md5Raw = null;
            _sha256Raw = null;
            _hashError = null;
            _txtCompare = null;
            Md5Foreground = _hashPendingBrush;
            Sha256Foreground = _hashPendingBrush;
            HashStage = HashStage.Cancelled;
            ResetHashProgress();
        }

        public void SetHashPercent(int percent)
        {
            if (percent < 0)
            {
                percent = 0;
            }
            else if (percent > 100)
            {
                percent = 100;
            }
            HashPercent = percent;
        }

        public void ResetHashProgress()
        {
            HashPercent = -1;
        }

        public void NotifyLanguageChanged()
        {
            Raise("AdsText");
            Raise("AdsForeground");
            Raise("AdsDetail");
            Raise("Message");
            Raise("Tooltip");
            Raise("Md5");
            Raise("Sha256");
        }

        private void Raise(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static Brush Make(byte r, byte g, byte b)
        {
            var solidColorBrush = new SolidColorBrush(Color.FromRgb(r, g, b));
            solidColorBrush.Freeze();
            return solidColorBrush;
        }
    }

}
