using FileChecker.Data;
using FileChecker.Definitions;
using FileChecker.Utilities;
using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;

namespace FileChecker
{
    /// <summary>
    /// “文件标记详情”窗口（纯代码构建，无独立 XAML）。
    /// <para>展示单个文件附加数据流的原始内容（可滚动/可复制），并提供
    /// “清除全部附加数据”操作（不可撤销，需二次确认）。</para>
    /// </summary>
    public sealed class StreamDetailWindow : Window
    {
        /// <summary>要查看详情的文件项（来自主窗口列表）。</summary>
        private readonly FileItem _item;

        /// <summary>承载原始内容的只读文本框。</summary>
        private TextBox _box;

        /// <summary>
        /// 创建详情窗口。
        /// </summary>
        /// <param name="item">要查看详情的文件项。</param>
        public StreamDetailWindow(FileItem item)
        {
            _item = item;
            // 基础窗口配置：标题、尺寸、无任务栏按钮、居中于父窗口
            Title = Localization.T("detail.title");
            Width = 680.0;
            Height = 520.0;
            MinWidth = 520.0;
            MinHeight = 360.0;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            // 复用主窗口的图标（若主窗口已存在且带图标）
            if (Application.Current != null)
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow is { Icon: not null })
                {
                    Icon = mainWindow.Icon;
                }
            }
            BuildUi();
        }

        /// <summary>以代码方式构建窗口 UI（文本框 + 三个按钮）。</summary>
        private void BuildUi()
        {
            // 根网格：第一行（*）放文本框，第二行（Auto）放按钮
            var grid = new Grid
            {
                Margin = new Thickness(18.0, 14.0, 18.0, 14.0)
            };
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(1.0, GridUnitType.Star)
            });
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });
            // 内容文本框：只读、双滚动条、等宽字体，文本来自 BuildContentText()
            var box = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(251, 252, 254)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(221, 227, 236)),
                Padding = new Thickness(10.0),
                FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                FontSize = 12.5,
                Text = BuildContentText()
            };
            Grid.SetRow(box, 0);
            grid.Children.Add(box);
            _box = box;
            // 按钮区（右对齐）：清除全部 / 复制全部文本 / 关闭
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
            };
            // 清除全部按钮：红色醒目样式，提示不可撤销
            var button = new Button
            {
                Content = Localization.T("detail.clear"),
                Width = 120.0,
                Height = 30.0,
                Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
                Cursor = Cursors.Hand,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                BorderThickness = new Thickness(0.0),
                ToolTip = Localization.T("detail.clearTip")
            };
            button.Click += ClearAll_Click;
            // 复制按钮：把文本框内容写入剪贴板
            var button2 = new Button
            {
                Content = Localization.T("detail.copy"),
                Width = 110.0,
                Height = 30.0,
                Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
                Cursor = Cursors.Hand
            };
            button2.Click += (_, _) =>
            {
                try
                {
                    Clipboard.SetText(box.Text);
                }
                catch
                {
                    // 剪贴板被其他进程占用时静默忽略
                    // ignored
                }
            };
            // 关闭按钮：默认按钮（回车可触发）
            var button3 = new Button
            {
                Content = Localization.T("detail.close"),
                Width = 80.0,
                Height = 30.0,
                IsDefault = true,
                Cursor = Cursors.Hand
            };
            button3.Click += (_, _) =>
            {
                Close();
            };
            stackPanel.Children.Add(button);
            stackPanel.Children.Add(button2);
            stackPanel.Children.Add(button3);
            Grid.SetRow(stackPanel, 1);
            grid.Children.Add(stackPanel);
            Content = grid;
        }

        /// <summary>
        /// 生成详情文本框的展示内容。
        /// <para>逻辑：扫描失败/未扫描 → 提示原因；无附加流 → “不包含任何附加数据”；
        /// 否则逐条输出非主数据流的内容原文（不可读/为空时给出对应提示）。</para>
        /// </summary>
        private string BuildContentText()
        {
            var adsResult = _item.AdsResult;
            // 尚未扫描或扫描失败：输出提示文字
            if (adsResult == null || !adsResult.Ok)
            {
                string text;
                if (adsResult == null)
                {
                    text = Localization.T("detail.rescanHint");
                }
                else
                {
                    // 有错误信息则显示，否则显示"未知原因"
                    text = string.IsNullOrEmpty(adsResult.Error) ? Localization.T("item.unknown") : adsResult.Error;
                }
                return Localization.F("detail.unreadable", text);
            }
            // 没有任何命名流：说明文件是干净的
            if (adsResult.NamedStreamCount == 0)
            {
                return Localization.T("detail.noAds");
            }
            // 逐个输出命名流内容，流之间用空行分隔
            var stringBuilder = new StringBuilder();
            var num = 0;
            foreach (var stream in adsResult.Streams)
            {
                // 只展示命名流，跳过主数据流（主数据流即文件本体内容）
                if (!stream.IsMainData)
                {
                    var text2 = AdsCleaner.ReadNamedStreamContentSafe(adsResult.Path, stream.Name);
                    // 读取失败 / 内容为空时给出本地化提示
                    if (text2 == null)
                    {
                        text2 = Localization.T("detail.streamUnreadable");
                    }
                    else if (text2.Length == 0)
                    {
                        text2 = Localization.T("detail.streamEmpty");
                    }
                    // 非首条流前加空行分隔，便于阅读
                    if (num > 0)
                    {
                        stringBuilder.AppendLine().AppendLine();
                    }
                    stringBuilder.Append(text2);
                    num++;
                }
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// “清除全部”按钮：确认后删除该文件全部附加数据流，并立即重新扫描刷新界面。
        /// </summary>
        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            // 二次确认：此操作不可撤销
            if (_item != null && MessageBox.Show(this, Localization.T("detail.clearAsk"), Localization.T("detail.clearAskTitle"), MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                AdsRemoveResult adsRemoveResult;
                int removedCount;
                string error;
                try
                {
                    // 执行清理：删除全部命名流
                    adsRemoveResult = AdsCleaner.RemoveAllStreams(_item.Path, out removedCount, out error);
                }
                catch (Exception ex)
                {
                    // 意外异常按失败处理
                    adsRemoveResult = AdsRemoveResult.Failed;
                    removedCount = 0;
                    error = ex.Message;
                }
                // 清理后立即重新扫描，刷新列表项与详情内容
                AdsScanResult ads;
                try
                {
                    ads = AdsCleaner.Scan(_item.Path);
                }
                catch (Exception ex2)
                {
                    ads = new AdsScanResult
                    {
                        Path = _item.Path,
                        Error = ex2.Message
                    };
                }
                _item.SetAds(ads);
                _box.Text = BuildContentText();
                // 按清理结果给用户反馈
                switch (adsRemoveResult)
                {
                    case AdsRemoveResult.Removed:
                        MessageBox.Show(this, Localization.F("detail.clearDone", removedCount), Localization.T("detail.clearDoneTitle"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
                        return;
                    case AdsRemoveResult.NoStream:
                        MessageBox.Show(this, Localization.T("detail.clearNone"), Localization.T("detail.clearDoneTitle"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
                        return;
                }
                // 失败（或部分成功）：展示错误信息
                var text = string.IsNullOrEmpty(error) ? Localization.T("item.unknown") : error;
                var messageBoxText = removedCount > 0 ? Localization.F("detail.clearPartial", removedCount, text) : Localization.F("detail.clearFail", text);
                MessageBox.Show(this, messageBoxText, Localization.T("detail.clearFailTitle"), MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }
    }
}
