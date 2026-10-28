using System.Collections.Generic;

namespace FileChecker.Data
{
    public sealed class AdsScanResult
    {
        public string Path { get; set; }

        public bool Ok { get; set; }

        public string Error { get; set; }

        public List<AdsStreamInfo> Streams { get; set; } = new();

        public bool HasZone { get; set; }

        public long ZoneLength { get; set; }

        public string ZoneContent { get; set; }

        public int ZoneId { get; set; } = -1;

        public bool ZoneHasExtraData { get; set; }

        public int NamedStreamCount
        {
            get
            {
                var num = 0;
                foreach (var stream in Streams)
                {
                    if (!stream.IsMainData)
                    {
                        num++;
                    }
                }
                return num;
            }
        }

        public int OtherNamedCount => NamedStreamCount - (HasZone ? 1 : 0);

        public bool HasExtraInfo
        {
            get
            {
                if (!HasZone || !ZoneHasExtraData)
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
                if (!HasZone || ZoneId < 0 || ZoneId > 4)
                {
                    return null;
                }
                return Localization.T("zone." + ZoneId) + "(" + ZoneId + ")";
            }
        }

        public string MarkText
        {
            get
            {
                if (!Ok)
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
                if (HasZone || NamedStreamCount > 0)
                {
                    return Localization.T("ads.other");
                }
                return Localization.T("ads.none");
            }
        }

        public string ShortInfo()
        {
            if (!Ok)
            {
                return Localization.F("item.errPrefix", string.IsNullOrEmpty(Error) ? Localization.T("item.unknown") : Error);
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
            if (HasZone)
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
