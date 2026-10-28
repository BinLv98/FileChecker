using FileChecker.Data;
using FileChecker.Definitions;
using FileChecker.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FileChecker
{
    /// <summary>
    /// 主窗口逻辑（对应 MainWindow.xaml）。
    /// <para>职责：维护文件列表（添加/移除/拖放）、自动扫描每个文件的附加数据流标记、
    /// 批量"去除网络标记"任务（后台并行 + 定时器排空结果队列）、批量 SHA256/MD5 校验码
    /// 计算（并行 + 进度上报），以及界面语言切换与各项右键菜单操作。</para>
    /// <para>线程模型：耗时操作（扫描/去除标记/哈希）都在后台线程执行，UI 状态一律通过
    /// Dispatcher 编组回 UI 线程更新，避免跨线程访问控件。</para>
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>列表控件的数据源（可观察集合，增删会自动刷新界面）。</summary>
        private readonly ObservableCollection<FileItem> _items = new();

        /// <summary>路径 → 文件项 的映射（大小写不敏感），用于去重与快速定位。</summary>
        private readonly Dictionary<string, FileItem> _map = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>去除标记任务的完成报告队列（后台线程入队，UI 线程出队）。</summary>
        private readonly ConcurrentQueue<FileReport> _queue = new();

        /// <summary>去除标记任务的取消令牌源（点击"取消"时触发）。</summary>
        private CancellationTokenSource _cts;

        /// <summary>去除标记结果排空定时器（周期性地把队列中的报告刷新到界面）。</summary>
        private DispatcherTimer _drainTimer;

        /// <summary>是否正在执行去除标记任务。</summary>
        private bool _running;

        /// <summary>去除标记后台任务是否已结束（volatile，供 UI 线程轮询）。</summary>
        private volatile bool _runDone;

        /// <summary>去除标记任务是否被用户取消（volatile）。</summary>
        private volatile bool _runCancelled;

        /// <summary>去除标记任务已完成的文件数（Interlocked 原子递增）。</summary>
        private long _finished;

        /// <summary>去除标记任务中的致命错误信息（后台线程写入，UI 展示）。</summary>
        private string _fatal;

        /// <summary>当前去除标记任务的待处理文件项快照。</summary>
        private IList<FileItem> _jobTargets;

        /// <summary>待扫描附加数据流的文件项队列（自动扫描的后台工作队列）。</summary>
        private readonly ConcurrentQueue<FileItem> _adsQueue = new();

        /// <summary>自动扫描定时器（周期性批量处理 _adsQueue 中的文件）。</summary>
        private readonly DispatcherTimer _adsTimer;

        /// <summary>是否正在执行一批自动扫描（防止重入）。</summary>
        private bool _adsBusy;

        /// <summary>是否正在执行哈希计算任务。</summary>
        private bool _hashing;

        /// <summary>哈希计算任务的取消令牌源。</summary>
        private CancellationTokenSource _hashCts;

        /// <summary>摘要栏的渲染函数（由各操作注册，语言切换后重新求值）。</summary>
        private Func<string> _summaryRender;

        /// <summary>是否正在程序内应用语言（用于抑制语言下拉框的回环触发）。</summary>
        private bool _applyingLang;

        /// <summary>拖拽区域默认背景色（浅蓝灰）。</summary>
        private static readonly Brush _dropNormalBrush = new SolidColorBrush(Color.FromRgb(240, 246, byte.MaxValue));

        /// <summary>拖拽悬停高亮背景色（稍深的蓝）。</summary>
        private static readonly Brush _dropHotBrush = new SolidColorBrush(Color.FromRgb(220, 235, byte.MaxValue));

        /// <summary>
        /// 窗口是否处于忙碌状态（正在去除标记或正在计算哈希）。
        /// <para>忙碌时禁止添加/移除/重扫/计算等操作，防止并发修改列表。</para>
        /// </summary>
        private bool Busy
        {
            get
            {
                if (!_running)
                {
                    return _hashing;
                }
                return true;
            }
        }

        /// <summary>
        /// 构造函数：初始化界面组件、事件与初始状态。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            // 绑定列表数据源并刷新空列表提示
            FileList.ItemsSource = _items;
            RefreshEmptyHint();
            // 哈希线程数下拉框：1~10，默认选中第 5 项（即 5 线程）
            HashThreadCombo.ItemsSource = Enumerable.Range(1, 10);
            HashThreadCombo.SelectedIndex = 4;
            // 语言下拉框（初始文案，ApplyLanguage 会按当前语言重设选中项）
            LangCombo.Items.Add("中文");
            LangCombo.Items.Add("English");
            // 语言切换事件：切到 UI 线程后台优先级重新应用文案
            Localization.LanguageChanged += (_, _) =>
            {
                Dispatcher.BeginInvoke(new Action(ApplyLanguage), DispatcherPriority.Background);
            };
            // 自动扫描定时器：每 120ms 排空一次扫描队列，避免阻塞 UI
            _adsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120.0)
            };
            _adsTimer.Tick += AdsTick;
            _adsTimer.Start();
            // 首次应用语言文案
            ApplyLanguage();
        }

        /// <summary>
        /// 把当前语言的文案应用到所有界面元素（按钮、列头、菜单、提示等）。
        /// </summary>
        private void ApplyLanguage()
        {
            // 置标志防止语言下拉框 SelectionChanged 回环触发
            _applyingLang = true;
            try
            {
                // 语言下拉框选中项与当前语言同步
                LangCombo.SelectedIndex = Localization.CurrentLanguage == AppLanguage.English ? 1 : 0;
                // 拖拽区文案
                DropTitle.Text = Localization.T("main.dropTitle");
                DropSub.Text = Localization.T("main.dropSub");
                // 功能按钮文案
                AddFilesBtn.Content = Localization.T("main.addFiles");
                AddFolderBtn.Content = Localization.T("main.addFolder");
                RemoveBtn.Content = Localization.T("main.removeSel");
                ClearBtn.Content = Localization.T("main.clear");
                RescanBtn.Content = Localization.T("main.rescan");
                RemoveMarkBtn.Content = Localization.T("main.removeMark");
                RemoveMarkBtn.ToolTip = Localization.T("main.removeMarkTip");
                HashBtn.Content = Localization.T("main.hash");
                RecursiveCheck.Content = Localization.T("main.recursive");
                CancelBtn.Content = Localization.T("main.cancel");
                ThreadsLabel.Text = Localization.T("main.threads");
                HashThreadCombo.ToolTip = Localization.T("main.threadsTip");
                LangLabel.Text = Localization.T("main.language");
                // 列表列标题（顺序与 XAML 中列定义一致）
                if (FileList.View is GridView gridView)
                {
                    gridView.Columns[0].Header = Localization.T("col.path");
                    gridView.Columns[1].Header = Localization.T("col.mark");
                    gridView.Columns[2].Header = Localization.T("col.progress");
                    gridView.Columns[3].Header = Localization.T("col.sha256");
                    gridView.Columns[4].Header = Localization.T("col.md5");
                }
                // 右键菜单项文案（顺序与 XAML 中 MenuItem 顺序一致）
                var array = new[] { "menu.removeMark", "menu.detail", "menu.rescan", "menu.openFolder", "menu.copyPath", "menu.copySha", "menu.copyMd5", "menu.copyBoth", "menu.remove", "menu.clear" };
                var num = 0;
                if (FileList.ContextMenu != null)
                {
                    foreach (var item in FileList.ContextMenu.Items)
                    {
                        if (item is System.Windows.Controls.MenuItem menuItem)
                        {
                            if (num < array.Length)
                            {
                                menuItem.Header = Localization.T(array[num]);
                            }
                            num++;
                        }
                    }
                }
                // 空列表提示与各文件项的本地化属性
                EmptyTitle.Text = Localization.T("empty.title");
                EmptySub.Text = Localization.T("empty.sub");
                foreach (var item2 in _items)
                {
                    item2.NotifyLanguageChanged();
                }
                // 摘要栏按当前语言重新渲染
                if (_summaryRender != null)
                {
                    SummaryText.Text = _summaryRender();
                }
            }
            finally
            {
                _applyingLang = false;
            }
        }

        /// <summary>
        /// 语言下拉框变化：若非程序内自动设置，则切换全局语言。
        /// </summary>
        private void LangCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_applyingLang && LangCombo.SelectedIndex >= 0)
            {
                Localization.Set(LangCombo.SelectedIndex == 1 ? AppLanguage.English : AppLanguage.Chinese);
            }
        }

        /// <summary>
        /// 拖拽进入：仅当空闲且有文件数据时允许"复制"效果，并高亮拖放区。
        /// </summary>
        private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            var flag = !Busy && e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop);
            e.Effects = flag ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
            DropBorder.Background = flag ? _dropHotBrush : _dropNormalBrush;
            e.Handled = true;
        }

        /// <summary>拖拽离开：恢复拖放区默认背景色。</summary>
        private void Window_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            DropBorder.Background = _dropNormalBrush;
        }

        /// <summary>
        /// 拖放完成：空闲状态下把拖入的文件/文件夹路径加入列表。
        /// </summary>
        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            DropBorder.Background = _dropNormalBrush;
            if (!Busy && e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] array && array.Length != 0)
                {
                    AddPaths(array);
                }
                e.Handled = true;
            }
        }

        /// <summary>点击拖放区域（空白处）等价于"添加文件"。</summary>
        private void DropBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            AddFiles_Click(sender, null);
        }

        /// <summary>“添加文件…”：弹出多选文件对话框并加入列表。</summary>
        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            if (!Busy)
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = Localization.T("dlg.addFilesTitle"),
                    Filter = Localization.T("dlg.filter"),
                    Multiselect = true
                };
                if (openFileDialog.ShowDialog(this) == true)
                {
                    AddPaths(openFileDialog.FileNames);
                }
            }
        }

        /// <summary>“添加文件夹…”：弹出文件夹选择对话框，递归展开其中文件后加入列表。</summary>
        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Busy)
            {
                return;
            }
            using var folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = Localization.T("dlg.folderDesc");
            folderBrowserDialog.ShowNewFolderButton = false;
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                AddPaths(new[] { folderBrowserDialog.SelectedPath });
            }
        }

        /// <summary>
        /// 把一批原始路径加入列表。
        /// <para>目录会按"包含子文件夹"设置递归展开为文件；无效路径与无权限子目录
        /// 会被计数并在摘要中提示；已存在的路径自动去重；新文件入列后立即排队
        /// 自动扫描其附加数据流标记。</para>
        /// </summary>
        /// <param name="rawPaths">用户给定的文件/文件夹路径集合。</param>
        private void AddPaths(IEnumerable<string> rawPaths)
        {
            if (Busy)
            {
                return;
            }
            var list = new List<string>();
            // 是否递归处理子文件夹（对应界面勾选框）
            var valueOrDefault = RecursiveCheck.IsChecked == true;
            var skipped = 0;
            var dirErrors = 0;
            foreach (var rawPath in rawPaths)
            {
                // 空白路径直接忽略
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    skipped++;
                    continue;
                }
                try
                {
                    if (Directory.Exists(rawPath))
                    {
                        // 目录：递归展开（无权限子目录计入 dirErrors）
                        var inaccessible = 0;
                        list.AddRange(AdsCleaner.ExpandPath(rawPath, valueOrDefault, ref inaccessible));
                        dirErrors += inaccessible;
                    }
                    else if (File.Exists(rawPath))
                    {
                        // 文件：直接加入
                        list.Add(rawPath);
                    }
                    else
                    {
                        // 路径不存在
                        skipped++;
                    }
                }
                catch
                {
                    // 展开过程异常（如非法路径）：按无效处理
                    skipped++;
                }
            }
            var added = 0;
            // 去重后加入列表，并排队自动扫描
            foreach (var item in list)
            {
                if (!_map.ContainsKey(item))
                {
                    var fileItem = new FileItem(item);
                    _items.Add(fileItem);
                    _map[item] = fileItem;
                    added++;
                    _adsQueue.Enqueue(fileItem);
                }
            }
            RefreshEmptyHint();
            UpdateProcessEnabled();
            // 摘要显示本次添加结果
            SetSummaryFunc(() =>
            {
                var text = added > 0 ? Localization.F("msg.added", added) : Localization.T("msg.noNew");
                if (skipped > 0)
                {
                    text += Localization.F("msg.ignoreInvalid", skipped);
                }
                if (dirErrors > 0)
                {
                    text += Localization.F("msg.ignoreDirs", dirErrors);
                }
                return text;
            });
            CountText.Text = _items.Count + " / " + _items.Count;
        }

        /// <summary>“移除选中”：把选中的文件项从列表移除（不删除磁盘文件）。</summary>
        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!Busy)
            {
                var list = FileList.SelectedItems.Cast<FileItem>().ToList();
                if (list.Count != 0)
                {
                    RemoveItems(list);
                }
            }
        }

        /// <summary>“清空列表”：移除全部文件项。</summary>
        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            if (!Busy)
            {
                RemoveItems(_items.ToList());
            }
        }

        /// <summary>
        /// 从列表与映射中移除指定文件项，并刷新界面状态。
        /// </summary>
        private void RemoveItems(IEnumerable<FileItem> items)
        {
            foreach (var item in items)
            {
                _items.Remove(item);
                _map.Remove(item.Path);
            }
            RefreshEmptyHint();
            UpdateProcessEnabled();
            CountText.Text = _items.Count + " / " + _items.Count;
        }

        /// <summary>“去除网络标记”（主按钮）：对列表全部文件启动去除标记任务。</summary>
        private void RemoveMarkBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!Busy)
            {
                if (_items.Count == 0)
                {
                    System.Windows.MessageBox.Show(this, Localization.T("msg.noItems"), "FileChecker", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
                else
                {
                    StartRemoveJob(_items.ToList());
                }
            }
        }

        /// <summary>右键菜单“去除选中项的网络标记”：对选中文件启动去除标记任务。</summary>
        private void RemoveMarkSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!Busy)
            {
                var list = FileList.SelectedItems.Cast<FileItem>().ToList();
                if (list.Count == 0)
                {
                    SetSummary("msg.selFirst");
                }
                else
                {
                    StartRemoveJob(list);
                }
            }
        }

        /// <summary>
        /// 启动去除网络标记任务（核心工作流）。
        /// <para>流程：清空旧队列 → 准备取消令牌/状态 → 标记所有目标为 Processing →
        /// 启动 150ms 排空定时器 → 后台线程并行调用 <see cref="AdsCleaner.ProcessFiles"/>
        /// 处理，每个文件完成后投递 <see cref="FileReport"/> 到队列并原子计数。
        /// UI 侧由 <see cref="DrainTick"/> 定时消费队列刷新列表。</para>
        /// </summary>
        /// <param name="targets">本次要去除标记的文件项。</param>
        private void StartRemoveJob(IList<FileItem> targets)
        {
            // 忙碌或目标为空时不重复启动
            if (Busy || targets == null || targets.Count == 0)
            {
                return;
            }
            // 去重（按引用去重即可）并过滤空项
            var list = targets.Where(t => t != null).Distinct().ToList();
            if (list.Count == 0)
            {
                return;
            }

            // 清空上一次遗留的队列内容（如有）
            while (_queue.TryDequeue(out _))
            {
            }
            // 初始化任务状态
            _cts = new CancellationTokenSource();
            _runDone = false;
            _runCancelled = false;
            _fatal = null;
            _finished = 0L;
            _running = true;
            _jobTargets = list;
            // 每个目标项先标记为"处理中"
            foreach (var item in list)
            {
                item.MarkProcessing();
            }
            // 进度条与计数复位
            Progress.Maximum = list.Count;
            Progress.Value = 0.0;
            CountText.Text = "0 / " + list.Count;
            SetSummary("msg.removing", list.Count);
            UpdateBusyUi(busy: true);
            var token = _cts.Token;
            // 启动排空定时器：周期把完成报告应用到界面
            _drainTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150.0)
            };
            _drainTimer.Tick += DrainTick;
            _drainTimer.Start();
            var paths = list.Select(i => i.Path).ToList();
            // 后台线程执行批量处理；每完成一个文件立即回调投递报告
            Task.Run(() =>
            {
                try
                {
                    AdsCleaner.ProcessFiles(paths, r =>
                    {
                        _queue.Enqueue(r);
                        Interlocked.Increment(ref _finished);
                    }, token);
                }
                catch (OperationCanceledException)
                {
                    // 用户取消
                    _runCancelled = true;
                }
                catch (Exception ex2)
                {
                    // 其他致命异常：记录供 UI 展示
                    _fatal = ex2.Message;
                }
                finally
                {
                    // 无论结果如何都标记任务结束（UI 据此收尾）
                    _runDone = true;
                }
            }, token);
        }

        /// <summary>
        /// “取消”按钮：正在去除标记时取消去除任务；正在计算哈希时取消哈希任务。
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_cts is { IsCancellationRequested: false })
            {
                _cts.Cancel();
                CancelBtn.IsEnabled = false;
                SetSummary("msg.cancellingRemove");
            }
            else if (_hashCts is { IsCancellationRequested: false })
            {
                _hashCts.Cancel();
                CancelBtn.IsEnabled = false;
                SetSummary("msg.cancellingHash");
            }
        }

        /// <summary>
        /// 排空定时器回调：把队列中积累的完成报告批量应用到文件项，
        /// 并同步进度条/计数；任务结束且队列清空时调用 <see cref="CompleteRun"/> 收尾。
        /// </summary>
        private void DrainTick(object sender, EventArgs e)
        {
            // 单次最多消费 5000 条，防止一次排空阻塞 UI
            var num = 0;
            while (num++ < 5000 && _queue.TryDequeue(out var result))
            {
                if (_map.TryGetValue(result.Path, out var value))
                {
                    value.Complete(result.Result, result.Error);
                }
            }
            // 进度条按原子计数刷新（不超过上限）
            var num2 = Interlocked.Read(ref _finished);
            if (num2 <= Progress.Maximum)
            {
                Progress.Value = num2;
            }
            CountText.Text = num2 + " / " + Progress.Maximum;
            // 后台任务结束且队列已清空 → 收尾
            if (_runDone && _queue.IsEmpty)
            {
                CompleteRun();
            }
        }

        /// <summary>
        /// 去除标记任务收尾：停表、清空剩余队列、统计结果、恢复界面、触发重新扫描。
        /// </summary>
        private void CompleteRun()
        {
            // 停止排空定时器
            if (_drainTimer != null)
            {
                _drainTimer.Stop();
                _drainTimer = null;
            }

            // 处理队列中可能残留的报告
            while (_queue.TryDequeue(out var result))
            {
                if (_map.TryGetValue(result.Path, out var value))
                {
                    value.Complete(result.Result, result.Error);
                }
            }
            // 仍处于 Processing 的项（未处理完，如取消）复位为 Pending
            var jobTargets = _jobTargets;
            _jobTargets = null;
            if (jobTargets != null)
            {
                foreach (var item in jobTargets)
                {
                    if (item.Status == ItemStatus.Processing)
                    {
                        item.Reset();
                    }
                }
            }
            // 统计各结果数量
            var total = jobTargets?.Count ?? 0;
            var removed = 0;
            var clean = 0;
            var failed = 0;
            if (jobTargets != null)
            {
                foreach (var item2 in jobTargets)
                {
                    if (item2.Status == ItemStatus.Removed)
                    {
                        removed++;
                    }
                    else if (item2.Status == ItemStatus.Clean)
                    {
                        clean++;
                    }
                    else if (item2.Status == ItemStatus.Failed)
                    {
                        failed++;
                    }
                }
            }
            Progress.Value = Interlocked.Read(ref _finished);
            // 摘要：致命错误 > 取消汇总 > 完成汇总
            SetSummaryFunc(() =>
            {
                if (_fatal != null)
                {
                    return Localization.F("msg.fatal", _fatal);
                }
                return _runCancelled ? Localization.F("msg.cancelSummary", removed + clean + failed, total, removed, clean, failed, total - removed - clean - failed) : Localization.F("msg.doneSummary", total, removed, clean, failed);
            });
            CountText.Text = removed + clean + failed + " / " + total;
            // 释放令牌源并复位忙碌状态
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
            _running = false;
            UpdateBusyUi(busy: false);
            // 处理过的文件重新排队扫描标记，刷新"文件标记"列
            if (jobTargets is { Count: > 0 })
            {
                RequestAdsScan(jobTargets);
            }
        }

        /// <summary>
        /// 把一批文件项排队等待自动扫描（先标记为待扫描状态）。
        /// </summary>
        private void RequestAdsScan(IEnumerable<FileItem> items)
        {
            foreach (var item in items)
            {
                item.MarkAdsPending();
                _adsQueue.Enqueue(item);
            }
        }

        /// <summary>
        /// 自动扫描定时器回调：每次从队列取最多 128 个文件，
        /// 后台线程逐个调用 <see cref="AdsCleaner.Scan"/>，结果编组回 UI 线程应用。
        /// </summary>
        private void AdsTick(object sender, EventArgs e)
        {
            // 上一批还在扫或队列为空：直接返回
            if (_adsBusy || _adsQueue.IsEmpty)
            {
                return;
            }
            // 批量取出待扫描文件（单批最多 128 个）
            var list = new List<FileItem>();
            while (list.Count < 128 && _adsQueue.TryDequeue(out var result))
            {
                list.Add(result);
            }
            if (list.Count == 0)
            {
                return;
            }
            _adsBusy = true;
            var snapshot = list.ToArray();
            Task.Run(() =>
            {
                var results = new List<KeyValuePair<FileItem, AdsScanResult>>(snapshot.Length);
                // 逐文件扫描，单个文件失败也不影响其余文件
                var array = snapshot;
                foreach (var fileItem in array)
                {
                    AdsScanResult value;
                    try
                    {
                        value = AdsCleaner.Scan(fileItem.Path);
                    }
                    catch (Exception ex)
                    {
                        // 扫描异常：包装为带错误信息的结果
                        value = new AdsScanResult
                        {
                            Error = ex.Message
                        };
                    }
                    results.Add(new KeyValuePair<FileItem, AdsScanResult>(fileItem, value));
                }
                // 结果编组回 UI 线程更新文件项
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    foreach (var item in results)
                    {
                        item.Key.SetAds(item.Value);
                    }
                }), DispatcherPriority.Background);
            }).ContinueWith(_ => _adsBusy = false, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>“重新读取文件标记”：对选中项（无选中则全部）重新排队扫描。</summary>
        private void Rescan_Click(object sender, RoutedEventArgs e)
        {
            if (!Busy)
            {
                var list = FileList.SelectedItems.Cast<FileItem>().ToList();
                if (list.Count > 0)
                {
                    RequestAdsScan(list);
                    SetSummary("msg.rescanSel", list.Count);
                }
                else
                {
                    RequestAdsScan(_items.ToList());
                    SetSummary("msg.rescanAll", _items.Count);
                }
            }
        }

        /// <summary>“计算 SHA256 / MD5”：对列表全部文件启动哈希计算任务。</summary>
        private void HashBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!Busy && _items.Count != 0)
            {
                StartHashJob(_items.ToList());
            }
        }

        /// <summary>
        /// 读取界面选择的并发线程数（1~10，非法时回退为 5）。
        /// </summary>
        private int GetHashThreads()
        {
            if (!(HashThreadCombo.SelectedItem is int num) || num < 1 || num > 10)
            {
                return 5;
            }
            return num;
        }

        /// <summary>
        /// 启动哈希计算任务（核心工作流）。
        /// <para>流程：全部文件标记为 Computing → 后台 <see cref="Parallel.ForEach"/>
        /// 按所选并发度逐文件计算 MD5/SHA256（期间经 <see cref="CreateHashReporter"/>
        /// 上报进度）→ 成功后尝试与参照 txt 比对 → 结果编组回 UI 线程写入文件项；
        /// 全部结束后经 <see cref="FinishHashJob"/> 收尾。</para>
        /// </summary>
        /// <param name="files">要计算校验码的文件项。</param>
        private void StartHashJob(IList<FileItem> files)
        {
            var total = files.Count;
            var degree = GetHashThreads();
            // 全部标记为"计算中"
            foreach (var file in files)
            {
                file.MarkHashPending();
            }
            _hashing = true;
            _hashCts = new CancellationTokenSource();
            var token = _hashCts.Token;
            var disp = Dispatcher;
            // 复位进度 UI
            UpdateBusyUi(busy: true);
            Progress.Maximum = total;
            Progress.Value = 0.0;
            CountText.Text = "0 / " + total;
            SetSummary("msg.hashing", degree);
            Task.Run(() =>
            {
                var completed = 0;
                try
                {
                    var parallelOptions = new ParallelOptions
                    {
                        CancellationToken = token,
                        MaxDegreeOfParallelism = Math.Max(1, degree)
                    };
                    // 并行遍历计算
                    Parallel.ForEach(files, parallelOptions, it =>
                    {
                        // 进度上报器（带节流：最多每 90ms 上报一次）
                        var reportProgress = CreateHashReporter(it, disp);
                        var fileHashResult = FileHasher.Compute(it.Path, token, reportProgress);
                        // 未被取消时才处理结果
                        if (!fileHashResult.Cancelled)
                        {
                            TxtHashCompareResult txtHashCompareResult = null;
                            // 计算成功时尝试读取参照 txt 并比对
                            if (fileHashResult.Ok)
                            {
                                try
                                {
                                    txtHashCompareResult = HashTxtReader.Compare(it.Path, fileHashResult.MD5, fileHashResult.SHA256);
                                }
                                catch
                                {
                                    // 参照 txt 解析失败不影响主结果
                                    txtHashCompareResult = null;
                                }
                            }
                            var r = fileHashResult;
                            var c = txtHashCompareResult;
                            // 结果编组回 UI 线程应用到文件项并推进进度
                            disp.BeginInvoke((Action)(() =>
                            {
                                it.ResetHashProgress();
                                if (r.Ok)
                                {
                                    it.SetHashResult(r.MD5, r.SHA256);
                                    if (c != null)
                                    {
                                        it.SetTxtComparison(c);
                                    }
                                }
                                else
                                {
                                    it.SetHashError(r.Error);
                                }
                                // completed 只在 UI 线程递增，避免竞态
                                var num = completed;
                                completed = num + 1;
                                Progress.Value = Math.Min(Progress.Maximum, completed);
                                CountText.Text = completed + " / " + total;
                            }), DispatcherPriority.Background);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // 取消：静默结束，由 FinishHashJob 统一收尾
                }
                finally
                {
                    // 无论如何都在 UI 线程收尾
                    disp.BeginInvoke(new Action(FinishHashJob), DispatcherPriority.Background);
                }
            }, token);
        }

        /// <summary>
        /// 创建带节流的哈希进度上报器。
        /// <para>只有百分比变化且距上次上报超过 90ms（或到达 100%）才真正
        /// 编组到 UI 线程更新进度条，避免海量小文件造成 UI 负担。</para>
        /// </summary>
        /// <param name="item">要更新进度的文件项。</param>
        /// <param name="disp">UI Dispatcher。</param>
        /// <returns>进度回调（参数为 0.0~1.0 的完成比例）。</returns>
        private static Action<double> CreateHashReporter(FileItem item, Dispatcher disp)
        {
            var lastPct = -1;
            var sw = Stopwatch.StartNew();
            return f =>
            {
                // 换算为整数百分比
                var num = f >= 1.0 ? 100 : !(f <= 0.0) ? (int)Math.Floor(f * 100.0) : 0;
                // 节流：百分比变化且（到达 100% 或距上次超过 90ms）
                if (num > lastPct && (num >= 100 || sw.ElapsedMilliseconds >= 90))
                {
                    lastPct = num;
                    var v = num;
                    disp.BeginInvoke((Action)(() =>
                    {
                        // 仅当仍处于计算阶段时更新（防止结果已写入后误刷）
                        if (item.HashStage == HashStage.Computing)
                        {
                            item.SetHashPercent(v);
                        }
                    }), DispatcherPriority.Background);
                }
            };
        }

        /// <summary>
        /// 哈希任务收尾：释放令牌源、复位忙碌、为未完成项标记取消、
        /// 统计成功/失败数量并显示摘要。
        /// </summary>
        private void FinishHashJob()
        {
            if (_hashCts != null)
            {
                _hashCts.Dispose();
                _hashCts = null;
            }
            _hashing = false;
            // 仍在"计算中"的项（任务被取消时遗留）标记为已取消
            foreach (var item in _items)
            {
                if (item.HashStage == HashStage.Computing)
                {
                    item.SetHashCancelled();
                }
            }
            // 统计成功与失败数
            var num = 0;
            var num2 = 0;
            foreach (var item2 in _items)
            {
                if (item2.HashStage == HashStage.Done)
                {
                    num++;
                }
                else if (item2.HashStage == HashStage.Failed)
                {
                    num2++;
                }
            }
            var num3 = num + num2;
            Progress.Value = num3;
            CountText.Text = num3 + " / " + (int)Progress.Maximum;
            // 摘要分三种情况：全取消 / 有失败 / 全成功
            if (num == 0 && num2 == 0)
            {
                SetSummary("msg.hashCancelled");
            }
            else if (num2 > 0)
            {
                SetSummary("msg.hashDoneFail", num, num2);
            }
            else
            {
                SetSummary("msg.hashDoneAll", num);
            }
            UpdateBusyUi(busy: false);
        }

        /// <summary>
        /// 右键“复制校验码”：根据菜单 Tag（sha256 / md5 / both）复制选中项的校验码。
        /// </summary>
        private void CopyHash_Click(object sender, RoutedEventArgs e)
        {
            var text = (sender as FrameworkElement)?.Tag as string;
            var fileItem = FileList.SelectedItems.Cast<FileItem>().FirstOrDefault();
            if (fileItem == null || text == null)
            {
                return;
            }
            var md5Raw = fileItem.Md5Raw;
            var sha256Raw = fileItem.Sha256Raw;
            string text2 = null;
            // 单值复制
            if (text != "both")
            {
                text2 = text != "md5" ? sha256Raw : md5Raw;
            }
            // 双值复制（本地化格式 "MD5: ...\nSHA256: ..."）
            else if (md5Raw != null && sha256Raw != null)
            {
                text2 = Localization.F("item.md5Copy", md5Raw, Environment.NewLine, sha256Raw);
            }
            // 还没有校验码：提示用户先计算
            if (text2 == null)
            {
                SetSummary("msg.noHashYet");
                return;
            }
            try
            {
                System.Windows.Clipboard.SetText(text2);
            }
            catch
            {
                // 剪贴板占用等异常静默忽略
                // ignored
            }
        }

        /// <summary>右键“查看文件标记详情”：以模态方式打开详情窗口。</summary>
        private void ShowDetail_Click(object sender, RoutedEventArgs e)
        {
            var fileItem = FileList.SelectedItems.Cast<FileItem>().FirstOrDefault();
            if (fileItem != null)
            {
                var streamDetailWindow = new StreamDetailWindow(fileItem)
                {
                    Owner = this
                };
                streamDetailWindow.ShowDialog();
            }
        }

        /// <summary>双击列表项：打开文件标记详情窗口。</summary>
        private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!Busy)
            {
                var fileItem = FileList.SelectedItems.Cast<FileItem>().FirstOrDefault();
                if (fileItem != null)
                {
                    var streamDetailWindow = new StreamDetailWindow(fileItem)
                    {
                        Owner = this
                    };
                    streamDetailWindow.ShowDialog();
                }
            }
        }

        /// <summary>
        /// 右键按下时预选：若右键点击的行尚未选中，先选中该行再弹菜单，
        /// 保证右键菜单操作作用于被点击的行。
        /// </summary>
        private void FileList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;
            if (ItemsControl.ContainerFromElement(FileList, element!) is System.Windows.Controls.ListViewItem { IsSelected: false } listViewItem)
            {
                FileList.SelectedItems.Clear();
                listViewItem.IsSelected = true;
            }
        }

        /// <summary>
        /// 按忙碌状态统一启停所有操作按钮。
        /// </summary>
        /// <param name="busy">是否处于忙碌状态。</param>
        private void UpdateBusyUi(bool busy)
        {
            AddFilesBtn.IsEnabled = !busy;
            AddFolderBtn.IsEnabled = !busy;
            RemoveBtn.IsEnabled = !busy;
            ClearBtn.IsEnabled = !busy;
            RescanBtn.IsEnabled = !busy;
            HashBtn.IsEnabled = !busy && _items.Count > 0;
            RemoveMarkBtn.IsEnabled = !busy && _items.Count > 0;
            RecursiveCheck.IsEnabled = !busy;
            HashThreadCombo.IsEnabled = !busy;
            CancelBtn.IsEnabled = busy;
        }

        /// <summary>
        /// 仅刷新依赖列表数量的按钮可用性（添加/移除列表后调用）。
        /// </summary>
        private void UpdateProcessEnabled()
        {
            RescanBtn.IsEnabled = !Busy;
            HashBtn.IsEnabled = !Busy && _items.Count > 0;
            RemoveMarkBtn.IsEnabled = !Busy && _items.Count > 0;
        }

        /// <summary>根据列表是否为空切换"空列表提示"的可见性。</summary>
        private void RefreshEmptyHint()
        {
            var flag = _items.Count == 0;
            EmptyHint.Visibility = !flag ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>设置摘要栏为"固定文案 + 参数"（语言切换后重新渲染）。</summary>
        private void SetSummary(string key, params object[] args)
        {
            var a = args ?? Array.Empty<object>();
            _summaryRender = () => Localization.F(key, a);
            SummaryText.Text = _summaryRender();
        }

        /// <summary>设置摘要栏为一次性纯文本（语言切换后不重渲染）。</summary>
        private void SetSummaryText(string text)
        {
            _summaryRender = null;
            SummaryText.Text = text;
        }

        /// <summary>设置摘要栏为动态渲染函数（语言切换后重新求值）。</summary>
        private void SetSummaryFunc(Func<string> render)
        {
            _summaryRender = render;
            SummaryText.Text = render();
        }

        /// <summary>右键“打开所在目录”：用资源管理器选中该文件。</summary>
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var fileItem = FileList.SelectedItems.Cast<FileItem>().FirstOrDefault();
            if (fileItem == null)
            {
                return;
            }
            try
            {
                Process.Start("explorer.exe", "/select,\"" + fileItem.Path + "\"");
            }
            catch
            {
                // 资源管理器启动失败时静默忽略
                // ignored
            }
        }

        /// <summary>右键“复制完整路径”：把选中文件的路径按行复制到剪贴板。</summary>
        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            var list = FileList.SelectedItems.Cast<FileItem>().ToList();
            if (list.Count != 0)
            {
                System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, list.Select(i => i.Path)));
            }
        }

        /// <summary>
        /// 窗口关闭确认：忙碌时弹框询问是否退出，确认后先取消后台任务。
        /// </summary>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // 不忙碌可直接关闭
            if (!Busy)
            {
                return;
            }
            var messageBoxText = _running ? Localization.T("msg.closeRemove") : Localization.T("msg.closeHash");
            // 用户选择"否"：取消关闭
            if (System.Windows.MessageBox.Show(this, messageBoxText, "FileChecker", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            // 确认退出：先请求取消正在进行的任务
            if (_cts != null)
            {
                _cts.Cancel();
            }
            if (_hashCts != null)
            {
                _hashCts.Cancel();
            }
        }

        /// <summary>快捷键：空闲状态下按 Delete 移除选中文件项。</summary>
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete && !Busy)
            {
                RemoveSelected_Click(sender, null);
                e.Handled = true;
            }
        }
    }
}
