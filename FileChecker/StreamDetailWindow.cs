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
    public sealed class StreamDetailWindow : Window
    {
        private readonly FileItem _item;

        private TextBox _box;

        public StreamDetailWindow(FileItem item)
        {
            _item = item;
            Title = Localization.T("detail.title");
            Width = 680.0;
            Height = 520.0;
            MinWidth = 520.0;
            MinHeight = 360.0;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
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

        private void BuildUi()
        {
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
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
            };
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
                    // ignored
                }
            };
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

        private string BuildContentText()
        {
            var adsResult = _item.AdsResult;
            if (adsResult == null || !adsResult.Ok)
            {
                string text;
                if (adsResult == null)
                {
                    text = Localization.T("detail.rescanHint");
                }
                else
                {
                    text = string.IsNullOrEmpty(adsResult.Error) ? Localization.T("item.unknown") : adsResult.Error;
                }
                return Localization.F("detail.unreadable", text);
            }
            if (adsResult.NamedStreamCount == 0)
            {
                return Localization.T("detail.noAds");
            }
            var stringBuilder = new StringBuilder();
            var num = 0;
            foreach (var stream in adsResult.Streams)
            {
                if (!stream.IsMainData)
                {
                    var text2 = AdsCleaner.ReadNamedStreamContentSafe(adsResult.Path, stream.Name);
                    if (text2 == null)
                    {
                        text2 = Localization.T("detail.streamUnreadable");
                    }
                    else if (text2.Length == 0)
                    {
                        text2 = Localization.T("detail.streamEmpty");
                    }
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

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_item != null && MessageBox.Show(this, Localization.T("detail.clearAsk"), Localization.T("detail.clearAskTitle"), MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                AdsRemoveResult adsRemoveResult;
                int removedCount;
                string error;
                try
                {
                    adsRemoveResult = AdsCleaner.RemoveAllStreams(_item.Path, out removedCount, out error);
                }
                catch (Exception ex)
                {
                    adsRemoveResult = AdsRemoveResult.Failed;
                    removedCount = 0;
                    error = ex.Message;
                }
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
                switch (adsRemoveResult)
                {
                    case AdsRemoveResult.Removed:
                        MessageBox.Show(this, Localization.F("detail.clearDone", removedCount), Localization.T("detail.clearDoneTitle"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
                        return;
                    case AdsRemoveResult.NoStream:
                        MessageBox.Show(this, Localization.T("detail.clearNone"), Localization.T("detail.clearDoneTitle"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
                        return;
                }
                var text = string.IsNullOrEmpty(error) ? Localization.T("item.unknown") : error;
                var messageBoxText = removedCount > 0 ? Localization.F("detail.clearPartial", removedCount, text) : Localization.F("detail.clearFail", text);
                MessageBox.Show(this, messageBoxText, Localization.T("detail.clearFailTitle"), MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }
    }
}
