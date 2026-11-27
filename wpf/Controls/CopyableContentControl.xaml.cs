using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PipelineGenWpf.Services;

namespace PipelineGenWpf.Controls
{
    public partial class CopyableContentControl : UserControl
    {
        private string _contentText = "";
        private List<string> _copyItems = new List<string>();
        private bool _isRawMode = false;

        public CopyableContentControl()
        {
            InitializeComponent();
        }

        public string ContentText
        {
            get { return _contentText; }
            set
            {
                _contentText = value ?? "";
                RenderContent();
            }
        }

        private void RenderContent()
        {
            TxtCharCount.Text = string.Format("{0} симв.", _contentText.Length);
            RawTextBox.Text = _contentText;

            _copyItems = MarkdownFlowBuilder.ExtractCopyItems(_contentText);
            CopyChipsPanel.Children.Clear();

            if (_copyItems.Count > 0)
            {
                CopyToolbarBorder.Visibility = Visibility.Visible;
                TxtCopyCount.Text = string.Format("Фрагменты для копирования ({0}):", _copyItems.Count);

                foreach (string item in _copyItems)
                {
                    string captured = item;
                    Button chipBtn = new Button();
                    chipBtn.Content = captured;
                    chipBtn.Style = (Style)FindResource("AmberChipButtonStyle");
                    chipBtn.Background = (Brush)FindResource("SurfaceBrush");
                    chipBtn.Margin = new Thickness(0, 0, 4, 4);
                    chipBtn.ToolTip = "Копировать";
                    chipBtn.Click += delegate
                    {
                        try { Clipboard.SetText(captured); } catch { }
                        chipBtn.Background = (Brush)FindResource("GreenBrush");
                        chipBtn.Foreground = Brushes.White;
                        chipBtn.BorderBrush = (Brush)FindResource("GreenBrush");
                        chipBtn.Content = "✓ " + captured;

                        DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                        t.Tick += delegate
                        {
                            t.Stop();
                            chipBtn.Background = (Brush)FindResource("SurfaceBrush");
                            chipBtn.Foreground = (Brush)FindResource("LabelBrush");
                            chipBtn.BorderBrush = (Brush)FindResource("BorderBrushMain");
                            chipBtn.Content = captured;
                        };
                        t.Start();
                    };
                    CopyChipsPanel.Children.Add(chipBtn);
                }
            }
            else
            {
                CopyToolbarBorder.Visibility = Visibility.Collapsed;
            }

            DocViewer.Document = MarkdownFlowBuilder.BuildDocument(_contentText);
            UpdateViewModeUi();
        }

        private void UpdateViewModeUi()
        {
            if (_isRawMode)
            {
                DocViewer.Visibility = Visibility.Collapsed;
                RawTextBox.Visibility = Visibility.Visible;

                BtnRaw.Background = (Brush)FindResource("SurfaceBrush");
                BtnRaw.Foreground = (Brush)FindResource("AccentBrush");
                BtnRaw.FontWeight = FontWeights.Bold;

                BtnRendered.Background = Brushes.Transparent;
                BtnRendered.Foreground = (Brush)FindResource("MutedBrush");
                BtnRendered.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                DocViewer.Visibility = Visibility.Visible;
                RawTextBox.Visibility = Visibility.Collapsed;

                BtnRendered.Background = (Brush)FindResource("SurfaceBrush");
                BtnRendered.Foreground = (Brush)FindResource("TextBrush");
                BtnRendered.FontWeight = FontWeights.Bold;

                BtnRaw.Background = Brushes.Transparent;
                BtnRaw.Foreground = (Brush)FindResource("MutedBrush");
                BtnRaw.FontWeight = FontWeights.SemiBold;
            }
        }

        private void BtnRendered_Click(object sender, RoutedEventArgs e)
        {
            _isRawMode = false;
            UpdateViewModeUi();
        }

        private void BtnRaw_Click(object sender, RoutedEventArgs e)
        {
            _isRawMode = true;
            UpdateViewModeUi();
        }

        private void BtnCopyAllTags_Click(object sender, RoutedEventArgs e)
        {
            if (_copyItems.Count == 0) return;
            try { Clipboard.SetText(string.Join(Environment.NewLine, _copyItems.ToArray())); } catch { }

            BtnCopyAllTags.Content = string.Format("Скопировано ({0})", _copyItems.Count);
            BtnCopyAllTags.Foreground = (Brush)FindResource("GreenBrush");

            DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            t.Tick += delegate
            {
                t.Stop();
                BtnCopyAllTags.Content = "Копировать всё";
                BtnCopyAllTags.Foreground = (Brush)FindResource("TextBrush");
            };
            t.Start();
        }

        private void DocViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Bubble scroll wheel event up to parent ScrollViewer so nested FlowDocument never traps scrolling
            if (!e.Handled)
            {
                e.Handled = true;
                MouseWheelEventArgs eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                UIElement parent = VisualTreeHelper.GetParent(this) as UIElement;
                if (parent != null)
                {
                    parent.RaiseEvent(eventArg);
                }
            }
        }
    }
}
