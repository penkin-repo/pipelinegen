using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PipelineGenWpf.Views
{
    public partial class ReadmeWindow : Window
    {
        private readonly string _readmeText;

        public ReadmeWindow(string projectName, string readmeText)
        {
            InitializeComponent();
            _readmeText = readmeText ?? "";
            string safeName = string.IsNullOrEmpty(projectName) ? "Проект" : projectName;
            Title = "README.md · " + safeName;
            TxtTitle.Text = "README.md · " + safeName;
            TxtReadme.Text = string.IsNullOrEmpty(_readmeText)
                ? "# Документация будет сгенерирована при первом сохранении..."
                : _readmeText;
            TxtFooter.Text = "%APPDATA%\\PipelineGen2026\\projects\\<id>\\README.md";
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(_readmeText); } catch { }
            BtnCopy.Content = "Скопировано";
            BtnCopy.Foreground = (Brush)FindResource("GreenBrush");

            DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
            t.Tick += delegate
            {
                t.Stop();
                BtnCopy.Content = "Копировать";
                BtnCopy.Foreground = (Brush)FindResource("TextBrush");
            };
            t.Start();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
