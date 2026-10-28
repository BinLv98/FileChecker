using System.Collections.Generic;

namespace FileChecker.Data
{
    public sealed class AdsScanResult
    {
        public string _path;

        public bool _ok;

        public string _error;

        public List<AdsStreamInfo> _streams = new();

        public bool _hasZone;

        public long _zoneLength;

        public string _zoneContent;

        public int _zoneId = -1;

        public bool _zoneHasExtraData;

        public int NamedStreamCount
        {
            get
            {
                var num = 0;
                foreach (var stream in _streams)
                {
                    if (!stream._isMainData)
                    {
                        num++;
                    }
                }
                return num;
            }
        }

        public int OtherNamedCount => NamedStreamCount - (_hasZone ? 1 : 0);

        public bool HasExtraInfo
        {
            get
            {
                if (!_hasZone || !_zoneHasExtraData)
                {
                    return OtherNamedCount > 0;
                }
                return true;
            }
        }

        public string ZoneLabel
        {
            get
            {
                if (!_hasZone || _zoneId < 0 || _zoneId > 4)
                {
                    return null;
                }
                return Localization.T("zone." + _zoneId) + "(" + _zoneId + ")";
            }
        }

        public string MarkText
        {
            get
            {
                if (!_ok)
                {
                    return Localization.T("item.readFailed");
                }
                var zoneLabel = ZoneLabel;
                if (zoneLabel != null)
                {
                    if (!HasExtraInfo)
                    {
                        return zoneLabel;
                    }
                    return zoneLabel + "+";
                }
                if (_hasZone || NamedStreamCount > 0)
                {
                    return Localization.T("ads.other");
                }
                return Localization.T("ads.none");
            }
        }

        public string ShortInfo()
        {
            if (!_ok)
            {
                return Localization.F("item.errPrefix", string.IsNullOrEmpty(_error) ? Localization.T("item.unknown") : _error);
            }
            var zoneLabel = ZoneLabel;
            if (zoneLabel != null)
            {
                var text = Localization.F("info.parsed", zoneLabel);
                if (HasExtraInfo)
                {
                    text += Localization.T("info.extra");
                }
                return text;
            }
            if (_hasZone)
            {
                return Localization.T("info.unknownZone");
            }
            if (NamedStreamCount > 0)
            {
                return Localization.T("info.otherAds");
            }
            return Localization.T("info.clean");
        }

        public static string FormatLen(long len)
        {
            if (len >= 1048576)
            {
                return (len / 1048576.0).ToString("0.##") + Localization.T("fmt.mb");
            }
            if (len >= 1024)
            {
                return (len / 1024.0).ToString("0.##") + Localization.T("fmt.kb");
            }
            return len + Localization.T("fmt.bytes");
        }
    }
}
