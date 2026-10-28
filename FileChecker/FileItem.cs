using FileChecker.Data;
using FileChecker.Definitions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace FileChecker
{
    /// <summary>
    /// 文件列表中的单个文件项（视图模型）。
    /// <para>实现 <see cref="INotifyPropertyChanged"/> 供 WPF 数据绑定：集中维护该文件
    /// 的附加数据流扫描结果（<see cref="AdsResult"/>）、去除标记的处理状态
    /// （<see cref="Status"/>）、校验码计算阶段（<see cref="HashStage"/>）及其展示
    /// 文字、颜色、悬停提示等信息；所有对外属性变化都通过 <see cref="Raise"/> 通知界面。</para>
    /// </summary>
    public sealed class FileItem : INotifyPropertyChanged
    {
        /// <summary>去除网络标记的处理状态（见 <see cref="Definitions.ItemStatus"/>）。</summary>
        private ItemStatus _status;

        /// <summary>去除标记失败时的错误信息。</summary>
        private string _removeError;

        /// <summary>该文件的附加数据流扫描结果（null 表示尚未扫描或正在重新读取）。</summary>
        private AdsScanResult _adsResult;

        /// <summary>计算得到的 MD5 原始值（十六进制小写字符串）。</summary>
        private string _md5Raw;

        /// <summary>计算得到的 SHA256 原始值（十六进制小写字符串）。</summary>
        private string _sha256Raw;

        /// <summary>校验码计算失败时的错误信息。</summary>
        private string _hashError;

        /// <summary>MD5 列的显示颜色（随计算阶段/比对结果变化）。</summary>
        private Brush _md5Foreground = _hashPendingBrush;

        /// <summary>SHA256 列的显示颜色（随计算阶段/比对结果变化）。</summary>
        private Brush _sha256Foreground = _hashPendingBrush;

        /// <summary>校验码计算阶段（见 <see cref="Definitions.HashStage"/>）。</summary>
        private HashStage _hashStage;

        /// <summary>与参照 txt 比对的详细结果（null 表示未比对或未找到参照）。</summary>
        private TxtHashCompareResult _txtCompare;

        /// <summary>哈希计算进度百分比（-1 表示不显示进度条）。</summary>
        private int _hashPercent = -1;

        // —— 各类状态对应的显示颜色（全部为冻结画笔，可跨线程安全使用）——

        /// <summary>“读取中…”状态的颜色（灰色）。</summary>
        private static readonly Brush _readingBrush = Make(152, 162, 179);

        /// <summary>检测到下载来源标记时的颜色（红色）。</summary>
        private static readonly Brush _zoneBrush = Make(198, 40, 40);

        /// <summary>无标记/校验一致时的颜色（绿色）。</summary>
        private static readonly Brush _cleanBrush = Make(24, 155, 70);

        /// <summary>存在其他附加数据流时的颜色（灰色）。</summary>
        private static readonly Brush _otherStreamBrush = Make(107, 114, 128);

        /// <summary>读取/计算失败时的颜色（橙色）。</summary>
        private static readonly Brush _errorBrush = Make(181, 71, 8);

        /// <summary>哈希待计算/已取消时的颜色（灰色）。</summary>
        private static readonly Brush _hashPendingBrush = Make(152, 162, 179);

        /// <summary>哈希正常完成时的颜色（深色）。</summary>
        private static readonly Brush _hashResultBrush = Make(17, 24, 39);

        /// <summary>哈希读取失败时的颜色（橙色）。</summary>
        private static readonly Brush _hashErrorBrush = Make(181, 71, 8);

        /// <summary>与参照 txt 比对一致时的颜色（绿色）。</summary>
        private static readonly Brush _hashMatchBrush = Make(24, 155, 70);

        /// <summary>与参照 txt 比对不一致时的颜色（红色）。</summary>
        private static readonly Brush _hashMismatchBrush = Make(217, 48, 37);

        /// <summary>文件完整路径（只读，创建时固定）。</summary>
        public string Path { get; }

        /// <summary>
        /// 去除网络标记的处理状态；变化时联动刷新 <see cref="Message"/> 与悬停提示。
        /// </summary>
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

        /// <summary>
        /// 处理状态对应的本地化说明文字。
        /// <para>已移除 → “已移除网络标记”；无标记 → “未检测到网络标记”；
        /// 失败 → 优先显示具体错误，否则显示“无法移除该标记”；其余状态为 null。</para>
        /// </summary>
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

        /// <summary>该文件的附加数据流扫描结果（null = 尚未扫描）。</summary>
        public AdsScanResult AdsResult => _adsResult;

        /// <summary>“文件标记”列显示的简短文字（未扫描时显示“读取中…”）。</summary>
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

        /// <summary>
        /// “文件标记”列文字的颜色。
        /// <para>优先级：未扫描=灰；扫描失败=橙；有 Zone 标记=红；
        /// 无任何附加流=绿；有其他附加流=灰。</para>
        /// </summary>
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

        /// <summary>标记的详细说明（用于悬停提示；未扫描时为 null）。</summary>
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

        /// <summary>
        /// 悬停提示全文：按行汇总路径、标记、处理结果、校验码与参照比对信息。
        /// </summary>
        public string Tooltip
        {
            get
            {
                // 第一行始终是文件完整路径
                var list = new List<string> { Path };
                // 附加数据流信息
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
                // 去除标记的处理结果
                var message = Message;
                if (!string.IsNullOrEmpty(message))
                {
                    list.Add(Localization.T("tip.result") + message);
                }
                // 哈希计算完成：展示校验码与参照比对结论
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
                // 哈希计算失败：展示失败原因
                else if (_hashStage == HashStage.Failed)
                {
                    list.Add(Localization.F("item.hashFailedTip", _hashError ?? Localization.T("item.unknown")));
                }
                return string.Join(Environment.NewLine, list);
            }
        }

        /// <summary>MD5 列的展示文字（按计算阶段显示占位/进度/结果/错误）。</summary>
        public string Md5 => HashDisplay(_md5Raw);

        /// <summary>SHA256 列的展示文字（按计算阶段显示占位/进度/结果/错误）。</summary>
        public string Sha256 => HashDisplay(_sha256Raw);

        /// <summary>MD5 原始十六进制值（供复制等场景使用，可能为 null）。</summary>
        public string Md5Raw => _md5Raw;

        /// <summary>SHA256 原始十六进制值（供复制等场景使用，可能为 null）。</summary>
        public string Sha256Raw => _sha256Raw;

        /// <summary>
        /// 校验码计算阶段；变化时联动刷新阶段文字、校验码列与悬停提示。
        /// </summary>
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

        /// <summary>MD5 列显示颜色（绑定用，仅能由内部逻辑更新）。</summary>
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

        /// <summary>SHA256 列显示颜色（绑定用，仅能由内部逻辑更新）。</summary>
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

        /// <summary>哈希进度百分比（0~100；-1 表示不展示进度条）。</summary>
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

        /// <summary>进度百分比文字（如 "45%"；未开始时为空字符串）。</summary>
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

        /// <summary>属性变化通知事件（WPF 数据绑定依赖）。</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 创建文件项。
        /// </summary>
        /// <param name="path">文件完整路径。</param>
        public FileItem(string path)
        {
            Path = path;
        }

        /// <summary>
        /// 更新附加数据流扫描结果并刷新相关绑定属性。
        /// </summary>
        /// <param name="r">扫描结果。</param>
        public void SetAds(AdsScanResult r)
        {
            _adsResult = r;
            Raise("AdsText");
            Raise("AdsForeground");
            Raise("AdsDetail");
            Raise("Tooltip");
        }

        /// <summary>
        /// 标记为“待重新扫描”：清空旧扫描结果并通知界面（显示“读取中…”）。
        /// </summary>
        public void MarkAdsPending()
        {
            _adsResult = null;
            Raise("AdsText");
            Raise("AdsForeground");
            Raise("AdsDetail");
            Raise("Tooltip");
        }

        /// <summary>
        /// 生成参照比对状态的本地化描述。
        /// </summary>
        /// <param name="state">比对状态。</param>
        /// <param name="refValue">参照值（不一致时用于展示）。</param>
        /// <param name="what">算法名称（"MD5" 或 "SHA256"）。</param>
        /// <returns>如 "一致"、"不一致（txt 中记录的 MD5 为 …）"、"txt 中未找到有效的 MD5 记录"。</returns>
        private static string DescribeState(HashMatchState state, string refValue, string what)
        {
            return state switch
            {
                HashMatchState.Match => Localization.T("tip.match"),
                HashMatchState.Mismatch => Localization.F("tip.mismatchRef", what, refValue),
                _ => Localization.F("tip.noRef", what),
            };
        }

        /// <summary>
        /// 单个文件去除标记处理完成：根据结果更新处理状态。
        /// </summary>
        /// <param name="result">处理结果（Removed / NoStream / Failed）。</param>
        /// <param name="error">失败时的错误信息。</param>
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

        /// <summary>标记为开始处理（清空旧错误，状态置为 Processing）。</summary>
        public void MarkProcessing()
        {
            _removeError = null;
            Status = ItemStatus.Processing;
        }

        /// <summary>重置处理状态（用于取消后回退到待处理）。</summary>
        public void Reset()
        {
            _removeError = null;
            Status = ItemStatus.Pending;
        }

        /// <summary>
        /// 生成校验码列的展示文字（按当前阶段分派）。
        /// </summary>
        /// <param name="raw">原始校验码值。</param>
        /// <returns>计算中→"计算中…"；完成→原值或"—"；失败→"读取失败"；取消→"已取消"；未开始→"—"。</returns>
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

        /// <summary>
        /// 标记哈希开始计算：清空旧结果，进入 Computing 阶段并复位进度。
        /// </summary>
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

        /// <summary>
        /// 写入计算完成的校验码并进入 Done 阶段，同时按参照比对结果刷新颜色。
        /// </summary>
        /// <param name="md5Hex">MD5 十六进制值。</param>
        /// <param name="sha256Hex">SHA256 十六进制值。</param>
        public void SetHashResult(string md5Hex, string sha256Hex)
        {
            _md5Raw = string.IsNullOrEmpty(md5Hex) ? null : md5Hex;
            _sha256Raw = string.IsNullOrEmpty(sha256Hex) ? null : sha256Hex;
            _hashError = null;
            HashStage = HashStage.Done;
            UpdateCompareForeground();
            ResetHashProgress();
        }

        /// <summary>
        /// 写入参照 txt 比对结果；若哈希已计算完成，立即按比对状态刷新列颜色。
        /// </summary>
        /// <param name="compare">比对结果。</param>
        public void SetTxtComparison(TxtHashCompareResult compare)
        {
            _txtCompare = compare;
            if (_hashStage == HashStage.Done)
            {
                UpdateCompareForeground();
                Raise("Tooltip");
            }
        }

        /// <summary>
        /// 依据参照比对状态刷新 MD5 / SHA256 列颜色：
        /// 一致→绿，不一致→红，无参照→深色。
        /// </summary>
        private void UpdateCompareForeground()
        {
            var txtCompare = _txtCompare;
            Md5Foreground = ForegroundFor(txtCompare?.MD5State ?? HashMatchState.NoReference);
            Sha256Foreground = ForegroundFor(txtCompare?.SHA256State ?? HashMatchState.NoReference);
        }

        /// <summary>把参照比对状态映射为列颜色。</summary>
        private static Brush ForegroundFor(HashMatchState state)
        {
            return state switch
            {
                HashMatchState.Match => _hashMatchBrush,
                HashMatchState.Mismatch => _hashMismatchBrush,
                _ => _hashResultBrush,
            };
        }

        /// <summary>
        /// 记录哈希计算失败：清空结果、进入 Failed 阶段、列显示错误色。
        /// </summary>
        /// <param name="error">失败原因（为空时使用本地化"未知原因"）。</param>
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

        /// <summary>
        /// 记录哈希计算被取消：清空结果、进入 Cancelled 阶段、列恢复待计算色。
        /// </summary>
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

        /// <summary>
        /// 设置哈希进度百分比（自动夹取到 0~100）。
        /// </summary>
        /// <param name="percent">目标百分比。</param>
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

        /// <summary>复位哈希进度（-1，隐藏进度条）。</summary>
        public void ResetHashProgress()
        {
            HashPercent = -1;
        }

        /// <summary>
        /// 语言切换后刷新所有依赖本地化文案的展示属性。
        /// </summary>
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

        /// <summary>触发指定属性的变化通知。</summary>
        /// <param name="name">属性名。</param>
        private void Raise(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 创建 RGB 颜色画笔并冻结（冻结后可跨线程共享，性能更优）。
        /// </summary>
        private static Brush Make(byte r, byte g, byte b)
        {
            var solidColorBrush = new SolidColorBrush(Color.FromRgb(r, g, b));
            solidColorBrush.Freeze();
            return solidColorBrush;
        }
    }

}
