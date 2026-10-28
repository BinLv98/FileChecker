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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly ObservableCollection<FileItem> _items = new();

        private readonly Dictionary<string, FileItem> _map = new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentQueue<FileReport> _queue = new();

        private CancellationTokenSource _cts;

        private DispatcherTimer _drainTimer;

        private bool _running;

        private volatile bool _runDone;

        private volatile bool _runCancelled;

        private long _finished;

        private string _fatal;

        private IList<FileItem> _jobTargets;

        private readonly ConcurrentQueue<FileItem> _adsQueue = new();

        private readonly DispatcherTimer _adsTimer;

        private bool _adsBusy;

        private bool _hashing;

        private CancellationTokenSource _hashCts;

        private Func<string> _summaryRender;

        private bool _applyingLang;

        private static readonly Brush _dropNormalBrush = new SolidColorBrush(Color.FromRgb(240, 246, byte.MaxValue));

        private static readonly Brush _dropHotBrush = new SolidColorBrush(Color.FromRgb(220, 235, byte.MaxValue));

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

        public MainWindow()
        {
            InitializeComponent();
            FileList.ItemsSource = _items;
            RefreshEmptyHint();
            HashThreadCombo.ItemsSource = Enumerable.Range(1, 10);
            HashThreadCombo.SelectedIndex = 4;
            LangCombo.Items.Add("中文");
            LangCombo.Items.Add("English");
            Localization.LanguageChanged += (_, _) =>
            {
                Dispatcher.BeginInvoke(new Action(ApplyLanguage), DispatcherPriority.Background);
            };
            _adsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120.0)
            };
            _adsTimer.Tick += AdsTick;
            _adsTimer.Start();
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            _applyingLang = true;
            try
            {
                LangCombo.SelectedIndex = ((Localization.CurrentLanguage == AppLanguage.English) ? 1 : 0);
                DropTitle.Text = Localization.T("main.dropTitle");
                DropSub.Text = Localization.T("main.dropSub");
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
                if (FileList.View is GridView gridView)
                {
                    gridView.Columns[0].Header = Localization.T("col.path");
                    gridView.Columns[1].Header = Localization.T("col.mark");
                    gridView.Columns[2].Header = Localization.T("col.progress");
                    gridView.Columns[3].Header = Localization.T("col.sha256");
                    gridView.Columns[4].Header = Localization.T("col.md5");
                }
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
                EmptyTitle.Text = Localization.T("empty.title");
                EmptySub.Text = Localization.T("empty.sub");
                foreach (var item2 in _items)
                {
                    item2.NotifyLanguageChanged();
                }
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

        private void LangCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_applyingLang && LangCombo.SelectedIndex >= 0)
            {
                Localization.Set((LangCombo.SelectedIndex == 1) ? AppLanguage.English : AppLanguage.Chinese);
            }
        }

        private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            var flag = !Busy && e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop);
            e.Effects = (flag ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None);
            DropBorder.Background = (flag ? _dropHotBrush : _dropNormalBrush);
            e.Handled = true;
        }

        private void Window_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            DropBorder.Background = _dropNormalBrush;
        }

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

        private void DropBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            AddFiles_Click(sender, null);
        }

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

        private void AddPaths(IEnumerable<string> rawPaths)
        {
            if (Busy)
            {
                return;
            }
            var list = new List<string>();
            var valueOrDefault = RecursiveCheck.IsChecked == true;
            var skipped = 0;
            var dirErrors = 0;
            foreach (var rawPath in rawPaths)
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    skipped++;
                    continue;
                }
                try
                {
                    if (Directory.Exists(rawPath))
                    {
                        var inaccessible = 0;
                        list.AddRange(AdsCleaner.ExpandPath(rawPath, valueOrDefault, ref inaccessible));
                        dirErrors += inaccessible;
                    }
                    else if (File.Exists(rawPath))
                    {
                        list.Add(rawPath);
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch
                {
                    skipped++;
                }
            }
            var added = 0;
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
            SetSummaryFunc(() =>
            {
                var text = ((added > 0) ? Localization.F("msg.added", added) : Localization.T("msg.noNew"));
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

        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            if (!Busy)
            {
                RemoveItems(_items.ToList());
            }
        }

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

        private void StartRemoveJob(IList<FileItem> targets)
        {
            if (Busy || targets == null || targets.Count == 0)
            {
                return;
            }
            var list = targets.Where(t => t != null).Distinct().ToList();
            if (list.Count == 0)
            {
                return;
            }

            while (_queue.TryDequeue(out _))
            {
            }
            _cts = new CancellationTokenSource();
            _runDone = false;
            _runCancelled = false;
            _fatal = null;
            _finished = 0L;
            _running = true;
            _jobTargets = list;
            foreach (var item in list)
            {
                item.MarkProcessing();
            }
            Progress.Maximum = list.Count;
            Progress.Value = 0.0;
            CountText.Text = "0 / " + list.Count;
            SetSummary("msg.removing", list.Count);
            UpdateBusyUi(busy: true);
            var token = _cts.Token;
            _drainTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150.0)
            };
            _drainTimer.Tick += DrainTick;
            _drainTimer.Start();
            var paths = list.Select(i => i.Path).ToList();
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
                    _runCancelled = true;
                }
                catch (Exception ex2)
                {
                    _fatal = ex2.Message;
                }
                finally
                {
                    _runDone = true;
                }
            });
        }

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

        private void DrainTick(object sender, EventArgs e)
        {
            var num = 0;
            FileReport result;
            while (num++ < 5000 && _queue.TryDequeue(out result))
            {
                if (_map.TryGetValue(result._path, out var value))
                {
                    value.Complete(result._result, result._error);
                }
            }
            var num2 = Interlocked.Read(ref _finished);
            if (num2 <= Progress.Maximum)
            {
                Progress.Value = num2;
            }
            CountText.Text = num2 + " / " + Progress.Maximum;
            if (_runDone && _queue.IsEmpty)
            {
                CompleteRun();
            }
        }

        private void CompleteRun()
        {
            if (_drainTimer != null)
            {
                _drainTimer.Stop();
                _drainTimer = null;
            }
            FileReport result;
            while (_queue.TryDequeue(out result))
            {
                if (_map.TryGetValue(result._path, out var value))
                {
                    value.Complete(result._result, result._error);
                }
            }
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
            SetSummaryFunc(() =>
            {
                if (_fatal != null)
                {
                    return Localization.F("msg.fatal", _fatal);
                }
                return _runCancelled ? Localization.F("msg.cancelSummary", removed + clean + failed, total, removed, clean, failed, total - removed - clean - failed) : Localization.F("msg.doneSummary", total, removed, clean, failed);
            });
            CountText.Text = removed + clean + failed + " / " + total;
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
            _running = false;
            UpdateBusyUi(busy: false);
            if (jobTargets is { Count: > 0 })
            {
                RequestAdsScan(jobTargets);
            }
        }

        private void RequestAdsScan(IEnumerable<FileItem> items)
        {
            foreach (var item in items)
            {
                item.MarkAdsPending();
                _adsQueue.Enqueue(item);
            }
        }

        private void AdsTick(object sender, EventArgs e)
        {
            if (_adsBusy || _adsQueue.IsEmpty)
            {
                return;
            }
            var list = new List<FileItem>();
            FileItem result;
            while (list.Count < 128 && _adsQueue.TryDequeue(out result))
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
                        value = new AdsScanResult
                        {
                            _error = ex.Message
                        };
                    }
                    results.Add(new KeyValuePair<FileItem, AdsScanResult>(fileItem, value));
                }
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    foreach (var item in results)
                    {
                        item.Key.SetAds(item.Value);
                    }
                }), DispatcherPriority.Background);
            }).ContinueWith(_ => _adsBusy = false, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }

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

        private void HashBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!Busy && _items.Count != 0)
            {
                StartHashJob(_items.ToList());
            }
        }

        private int GetHashThreads()
        {
            if (!(HashThreadCombo.SelectedItem is int num) || num < 1 || num > 10)
            {
                return 5;
            }
            return num;
        }

        private void StartHashJob(IList<FileItem> files)
        {
            var total = files.Count;
            var degree = GetHashThreads();
            foreach (var file in files)
            {
                file.MarkHashPending();
            }
            _hashing = true;
            _hashCts = new CancellationTokenSource();
            var token = _hashCts.Token;
            var disp = Dispatcher;
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
                    Parallel.ForEach(files, parallelOptions, it =>
                    {
                        var reportProgress = CreateHashReporter(it, disp);
                        var fileHashResult = FileHasher.Compute(it.Path, token, reportProgress);
                        if (!fileHashResult._cancelled)
                        {
                            TxtHashCompareResult txtHashCompareResult = null;
                            if (fileHashResult._ok)
                            {
                                try
                                {
                                    txtHashCompareResult = HashTxtReader.Compare(it.Path, fileHashResult._md5, fileHashResult._sha256);
                                }
                                catch
                                {
                                    txtHashCompareResult = null;
                                }
                            }
                            var r = fileHashResult;
                            var c = txtHashCompareResult;
                            disp.BeginInvoke((Action)(() =>
                            {
                                it.ResetHashProgress();
                                if (r._ok)
                                {
                                    it.SetHashResult(r._md5, r._sha256);
                                    if (c != null)
                                    {
                                        it.SetTxtComparison(c);
                                    }
                                }
                                else
                                {
                                    it.SetHashError(r._error);
                                }
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
                }
                finally
                {
                    disp.BeginInvoke(new Action(FinishHashJob), DispatcherPriority.Background);
                }
            });
        }

        private static Action<double> CreateHashReporter(FileItem item, Dispatcher disp)
        {
            var lastPct = -1;
            var sw = Stopwatch.StartNew();
            return f =>
            {
                var num = ((f >= 1.0) ? 100 : ((!(f <= 0.0)) ? ((int)Math.Floor(f * 100.0)) : 0));
                if (num > lastPct && (num >= 100 || sw.ElapsedMilliseconds >= 90))
                {
                    lastPct = num;
                    var v = num;
                    disp.BeginInvoke((Action)(() =>
                    {
                        if (item.HashStage == HashStage.Computing)
                        {
                            item.SetHashPercent(v);
                        }
                    }), DispatcherPriority.Background);
                }
            };
        }

        private void FinishHashJob()
        {
            if (_hashCts != null)
            {
                _hashCts.Dispose();
                _hashCts = null;
            }
            _hashing = false;
            foreach (var item in _items)
            {
                if (item.HashStage == HashStage.Computing)
                {
                    item.SetHashCancelled();
                }
            }
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
            if (!(text == "both"))
            {
                text2 = ((!(text == "md5")) ? sha256Raw : md5Raw);
            }
            else if (md5Raw != null && sha256Raw != null)
            {
                text2 = Localization.F("item.md5Copy", md5Raw, Environment.NewLine, sha256Raw);
            }
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
                // ignored
            }
        }

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

        private void FileList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;
            if (ItemsControl.ContainerFromElement(FileList, element!) is System.Windows.Controls.ListViewItem { IsSelected: false } listViewItem)
            {
                FileList.SelectedItems.Clear();
                listViewItem.IsSelected = true;
            }
        }

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

        private void UpdateProcessEnabled()
        {
            RescanBtn.IsEnabled = !Busy;
            HashBtn.IsEnabled = !Busy && _items.Count > 0;
            RemoveMarkBtn.IsEnabled = !Busy && _items.Count > 0;
        }

        private void RefreshEmptyHint()
        {
            var flag = _items.Count == 0;
            EmptyHint.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
        }

        private void SetSummary(string key, params object[] args)
        {
            var a = args ?? new object[0];
            _summaryRender = () => Localization.F(key, a);
            SummaryText.Text = _summaryRender();
        }

        private void SetSummaryText(string text)
        {
            _summaryRender = null;
            SummaryText.Text = text;
        }

        private void SetSummaryFunc(Func<string> render)
        {
            _summaryRender = render;
            SummaryText.Text = render();
        }

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
                // ignored
            }
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            var list = FileList.SelectedItems.Cast<FileItem>().ToList();
            if (list.Count != 0)
            {
                System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, list.Select(i => i.Path)));
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!Busy)
            {
                return;
            }
            var messageBoxText = (_running ? Localization.T("msg.closeRemove") : Localization.T("msg.closeHash"));
            if (System.Windows.MessageBox.Show(this, messageBoxText, "FileChecker", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            if (_cts != null)
            {
                _cts.Cancel();
            }
            if (_hashCts != null)
            {
                _hashCts.Cancel();
            }
        }

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