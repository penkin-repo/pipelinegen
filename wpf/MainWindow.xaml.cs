using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PipelineGenWpf.Models;
using PipelineGenWpf.Services;
using PipelineGenWpf.Views;

namespace PipelineGenWpf
{
    public partial class MainWindow : Window
    {
        private readonly StorageService _storage;
        private readonly OpenRouterService _openRouter;

        private AppConfig _config;
        private List<Project> _projects = new List<Project>();
        private Project _activeProject;
        private string _activeTab = "runner"; // "runner" | "builder"

        private readonly List<LogEntry> _logs = new List<LogEntry>();
        private bool _logDrawerOpen = false;
        private int _logErrorCount = 0;

        private readonly DispatcherTimer _autoSaveTimer;
        private readonly DispatcherTimer _toastTimer;
        private bool _hasUnsavedChanges = false;
        private bool _suppressTitleEvents = false;

        public MainWindow()
        {
            InitializeComponent();

            _storage = new StorageService();
            _openRouter = new OpenRouterService();

            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1200)
            };
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;

            _toastTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(2400)
            };
            _toastTimer.Tick += delegate
            {
                _toastTimer.Stop();
                ToastBorder.Visibility = Visibility.Collapsed;
            };

            InitializeData();
        }

        private void InitializeData()
        {
            _config = _storage.LoadConfig();
            _projects = _storage.LoadProjects();

            if (_projects.Count == 0)
            {
                Project defProj = StorageService.CreateDefaultProject();
                _storage.SaveProject(defProj);
                _projects.Add(defProj);
                AddLog("info", "Создан демонстрационный проект (Vision OCR + Саммари)");
            }

            TxtStatusStorage.Text = _storage.BaseDir;
            UpdateApiStatusUi();
            UpdateTabStyles();

            if (_projects.Count > 0)
            {
                SelectProject(_projects[0], true);
            }
            else
            {
                RefreshSidebarProjects();
                UpdateEmptyState();
            }

            AddLog("ok", string.Format("PIP запущен · проектов: {0}", _projects.Count));
        }

        private void UpdateApiStatusUi()
        {
            bool hasKey = _config != null && !string.IsNullOrWhiteSpace(_config.ApiKey);
            if (hasKey)
            {
                StatusApiDot.Fill = (Brush)FindResource("GreenBrush");
                TxtStatusApi.Text = "OpenRouter API подключен";
                ApiWarningBanner.Visibility = Visibility.Collapsed;
            }
            else
            {
                StatusApiDot.Fill = (Brush)FindResource("AmberBrush");
                TxtStatusApi.Text = "API ключ не задан";
                ApiWarningBanner.Visibility = Visibility.Visible;
            }

            string defModel = (_config != null && !string.IsNullOrEmpty(_config.DefaultModel))
                ? _config.DefaultModel
                : "deepseek/deepseek-v4.1-flash";
            int modelCount = (_config != null && _config.Models != null) ? _config.Models.Count : 0;
            TxtStatusModel.Text = string.Format("{0} ({1})", defModel, modelCount);
        }

        private void UpdateEmptyState()
        {
            bool hasProject = _activeProject != null;
            EmptyStatePanel.Visibility = hasProject ? Visibility.Collapsed : Visibility.Visible;
            TxtProjectName.IsEnabled = hasProject;
            BtnReadme.IsEnabled = hasProject;
            SaveStatusBadge.Visibility = hasProject ? Visibility.Visible : Visibility.Collapsed;

            if (!hasProject)
            {
                RunnerView.Visibility = Visibility.Collapsed;
                BuilderView.Visibility = Visibility.Collapsed;
            }
            else
            {
                RunnerView.Visibility = _activeTab == "runner" ? Visibility.Visible : Visibility.Collapsed;
                BuilderView.Visibility = _activeTab == "builder" ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SelectProject(Project project, bool forceResetRunner)
        {
            if (_hasUnsavedChanges && _activeProject != null && _activeProject != project)
            {
                FlushPendingSave();
            }

            _activeProject = project;
            _hasUnsavedChanges = false;
            _autoSaveTimer.Stop();

            _suppressTitleEvents = true;
            TxtProjectName.Text = project != null ? (project.Name ?? "") : "";
            _suppressTitleEvents = false;

            SetSaveBadgeState("saved");
            RefreshSidebarProjects();
            UpdateEmptyState();

            if (_activeProject != null)
            {
                BuilderView.Load(_activeProject, _config, OnProjectMutated, AddLog);
                RunnerView.Load(
                    _activeProject,
                    _config,
                    _openRouter,
                    AddLog,
                    OnProjectMutated,
                    forceResetRunner);
            }
        }

        private void RefreshSidebarProjects()
        {
            ProjectsListPanel.Children.Clear();
            TxtProjectsCount.Text = _projects.Count.ToString();

            foreach (Project proj in _projects)
            {
                Project captured = proj;
                bool isSelected = _activeProject != null && _activeProject.Id == proj.Id;

                Border item = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(8, 5, 4, 5),
                    Margin = new Thickness(0, 0, 0, 2),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(1),
                    Background = isSelected ? (Brush)FindResource("AccentSoftBrush") : Brushes.Transparent,
                    BorderBrush = isSelected ? (Brush)FindResource("AccentBrush") : Brushes.Transparent
                };

                if (!isSelected)
                {
                    item.MouseEnter += delegate
                    {
                        item.Background = new SolidColorBrush(Color.FromRgb(0xEA, 0xEA, 0xEA));
                    };
                    item.MouseLeave += delegate
                    {
                        item.Background = Brushes.Transparent;
                    };
                }

                DockPanel dock = new DockPanel();

                Button btnDelete = new Button
                {
                    Content = "✕",
                    ToolTip = "Удалить проект",
                    Style = (Style)FindResource("DangerGhostButtonStyle"),
                    Padding = new Thickness(4, 1, 4, 1),
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = isSelected ? 0.85 : 0.4
                };
                btnDelete.Click += delegate(object s, RoutedEventArgs ev)
                {
                    ev.Handled = true;
                    DeleteProjectWithConfirm(captured);
                };
                DockPanel.SetDock(btnDelete, Dock.Right);
                dock.Children.Add(btnDelete);

                StackPanel textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                TextBlock txtName = new TextBlock
                {
                    Text = string.IsNullOrEmpty(proj.Name) ? "Без названия" : proj.Name,
                    FontSize = 12,
                    FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = (Brush)FindResource("TextBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                textStack.Children.Add(txtName);

                TextBlock txtSub = new TextBlock
                {
                    Text = proj.Subtitle,
                    FontSize = 10.5,
                    Foreground = (Brush)FindResource("SubtleMutedBrush"),
                    Margin = new Thickness(0, 1, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                textStack.Children.Add(txtSub);

                dock.Children.Add(textStack);
                item.Child = dock;

                item.MouseLeftButtonUp += delegate
                {
                    if (_activeProject == null || _activeProject.Id != captured.Id)
                    {
                        SelectProject(captured, true);
                    }
                };

                ProjectsListPanel.Children.Add(item);
            }
        }

        private void DeleteProjectWithConfirm(Project proj)
        {
            MessageBoxResult res = MessageBox.Show(
                this,
                string.Format("Удалить проект «{0}»?\nФайлы проекта и README.md будут удалены с диска.", proj.Name),
                "Удаление проекта",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                _storage.DeleteProject(proj.Id);
                _projects.Remove(proj);
                AddLog("info", string.Format("Проект «{0}» удален", proj.Name));
                ShowToast("Проект удален");

                if (_activeProject != null && _activeProject.Id == proj.Id)
                {
                    _activeProject = null;
                    _hasUnsavedChanges = false;
                    _autoSaveTimer.Stop();

                    if (_projects.Count > 0)
                    {
                        SelectProject(_projects[0], true);
                    }
                    else
                    {
                        RefreshSidebarProjects();
                        UpdateEmptyState();
                    }
                }
                else
                {
                    RefreshSidebarProjects();
                }
            }
            catch (Exception ex)
            {
                AddLog("error", "Ошибка удаления проекта: " + ex.Message);
                MessageBox.Show(this, "Не удалось удалить проект:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnNewProject_Click(object sender, RoutedEventArgs e)
        {
            FlushPendingSave();

            long ms = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            string defaultModel = (_config != null && !string.IsNullOrEmpty(_config.DefaultModel))
                ? _config.DefaultModel
                : "deepseek/deepseek-v4.1-flash";

            Project proj = new Project
            {
                Id = "proj_" + ms,
                Name = string.Format("Пайплайн #{0}", _projects.Count + 1),
                Description = "",
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                UpdatedAt = DateTime.Now.ToString("HH:mm"),
                Steps = new List<StepDefinition>
                {
                    new StepDefinition
                    {
                        Id = "step_1",
                        Type = "text",
                        Title = "Входные данные",
                        Content = ""
                    },
                    new StepDefinition
                    {
                        Id = "step_2",
                        Type = "ai",
                        Title = "AI обработка",
                        Model = defaultModel,
                        Prompt = "Обработай следующие входные данные:\n{{step_1}}"
                    }
                }
            };

            try
            {
                _storage.SaveProject(proj);
                _projects.Insert(0, proj);
                _activeTab = "builder";
                UpdateTabStyles();
                SelectProject(proj, true);
                AddLog("ok", string.Format("Создан проект «{0}»", proj.Name));
                ShowToast("Создан новый проект");
            }
            catch (Exception ex)
            {
                AddLog("error", "Ошибка создания проекта: " + ex.Message);
                MessageBox.Show(this, "Не удалось создать проект:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnProjectMutated()
        {
            if (_activeProject == null) return;
            _hasUnsavedChanges = true;
            SetSaveBadgeState("pending");
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
            RunnerView.RefreshHeaderMeta();
            RefreshSidebarProjects();
        }

        private void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            _autoSaveTimer.Stop();
            FlushPendingSave();
        }

        private void FlushPendingSave()
        {
            if (!_hasUnsavedChanges || _activeProject == null) return;
            _autoSaveTimer.Stop();

            try
            {
                _activeProject.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                _activeProject.NotifyMetaChanged();
                _storage.SaveProject(_activeProject);
                _hasUnsavedChanges = false;
                SetSaveBadgeState("saved");
                RefreshSidebarProjects();
            }
            catch (Exception ex)
            {
                SetSaveBadgeState("error");
                AddLog("error", "Ошибка автосохранения: " + ex.Message);
            }
        }

        private void SetSaveBadgeState(string state)
        {
            if (state == "pending")
            {
                SaveStatusBadge.Background = Brushes.Transparent;
                TxtSaveStatus.Foreground = (Brush)FindResource("AmberTextBrush");
                TxtSaveStatus.Text = "Сохранение...";
            }
            else if (state == "error")
            {
                SaveStatusBadge.Background = (Brush)FindResource("RedSoftBrush");
                TxtSaveStatus.Foreground = (Brush)FindResource("RedBrush");
                TxtSaveStatus.Text = "Ошибка записи";
            }
            else
            {
                SaveStatusBadge.Background = Brushes.Transparent;
                TxtSaveStatus.Foreground = (Brush)FindResource("MutedBrush");
                TxtSaveStatus.Text = "Сохранено";
            }
        }

        private void SaveStatusBadge_Click(object sender, MouseButtonEventArgs e)
        {
            if (_activeProject == null) return;
            _hasUnsavedChanges = true;
            FlushPendingSave();
            ShowToast("Проект сохранен");
        }

        private void TxtProjectName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTitleEvents || _activeProject == null) return;
            _activeProject.Name = TxtProjectName.Text;
            OnProjectMutated();
        }

        private void TxtProjectName_GotFocus(object sender, RoutedEventArgs e)
        {
            TxtProjectName.Background = (Brush)FindResource("Surface2Brush");
            TxtProjectName.BorderBrush = (Brush)FindResource("AccentBrush");
        }

        private void TxtProjectName_LostFocus(object sender, RoutedEventArgs e)
        {
            TxtProjectName.Background = Brushes.Transparent;
            TxtProjectName.BorderBrush = Brushes.Transparent;
        }

        private void UpdateTabStyles()
        {
            bool isRunner = _activeTab == "runner";

            BtnTabRunner.Background = isRunner ? (Brush)FindResource("AccentBrush") : Brushes.Transparent;
            BtnTabRunner.Foreground = isRunner ? Brushes.White : (Brush)FindResource("LabelBrush");

            BtnTabBuilder.Background = !isRunner ? (Brush)FindResource("AmberAccentBrush") : Brushes.Transparent;
            BtnTabBuilder.Foreground = !isRunner ? Brushes.White : (Brush)FindResource("LabelBrush");

            TopBarBorder.Background = isRunner
                ? (Brush)FindResource("ToolbarBrush")
                : (Brush)FindResource("BuilderToolbarBrush");
            TopBarBorder.BorderBrush = isRunner
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("AmberAccentBrush");

            Title = isRunner ? "PIP — Запуск" : "PIP — Конструктор";

            if (_activeProject != null)
            {
                RunnerView.Visibility = isRunner ? Visibility.Visible : Visibility.Collapsed;
                BuilderView.Visibility = !isRunner ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SwitchToBuilderTab()
        {
            _activeTab = "builder";
            UpdateTabStyles();
            if (_activeProject != null)
            {
                BuilderView.Load(_activeProject, _config, OnProjectMutated, AddLog);
            }
        }

        private void BtnTabRunner_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == "runner") return;
            FlushPendingSave();
            _activeTab = "runner";
            UpdateTabStyles();
            if (_activeProject != null)
            {
                RunnerView.Load(
                    _activeProject,
                    _config,
                    _openRouter,
                    AddLog,
                    OnProjectMutated,
                    false);
            }
        }

        private void BtnTabBuilder_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == "builder") return;
            SwitchToBuilderTab();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsDialog();
        }

        private void ApiWarningBanner_Click(object sender, MouseButtonEventArgs e)
        {
            OpenSettingsDialog();
        }

        private void OpenSettingsDialog()
        {
            SettingsWindow win = new SettingsWindow(
                _config,
                _storage,
                _openRouter,
                delegate(AppConfig updated)
                {
                    _config = updated;
                    UpdateApiStatusUi();
                    BuilderView.UpdateConfig(_config);
                    RunnerView.UpdateConfig(_config);
                },
                AddLog);
            win.Owner = this;
            win.ShowDialog();
        }

        private void BtnReadme_Click(object sender, RoutedEventArgs e)
        {
            if (_activeProject == null) return;
            FlushPendingSave();
            string md = _storage.GetReadme(_activeProject);
            ReadmeWindow win = new ReadmeWindow(_activeProject.Name, md);
            win.Owner = this;
            win.ShowDialog();
        }

        public void AddLog(string type, string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { AddLog(type, message); }));
                return;
            }

            string level = string.IsNullOrEmpty(type) ? "info" : type.ToLowerInvariant();
            LogEntry entry = new LogEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Type = level,
                Message = message ?? ""
            };
            _logs.Add(entry);
            if (_logs.Count > 300)
            {
                _logs.RemoveAt(0);
                if (LogEntriesPanel.Children.Count > 0)
                {
                    LogEntriesPanel.Children.RemoveAt(0);
                }
            }

            if (level == "error")
            {
                _logErrorCount++;
            }

            AppendLogRow(entry);
            UpdateLogBadge();
        }

        private void AppendLogRow(LogEntry entry)
        {
            Grid row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock txtTime = new TextBlock
            {
                Text = entry.Time,
                FontFamily = (FontFamily)FindResource("MonoFont"),
                FontSize = 11,
                Foreground = (Brush)FindResource("SubtleMutedBrush")
            };
            Grid.SetColumn(txtTime, 0);
            row.Children.Add(txtTime);

            string tag = "INFO";
            Brush tagBrush = (Brush)FindResource("MutedBrush");
            if (entry.Type == "ok")
            {
                tag = "OK";
                tagBrush = (Brush)FindResource("GreenBrush");
            }
            else if (entry.Type == "error")
            {
                tag = "ERR";
                tagBrush = (Brush)FindResource("RedBrush");
            }

            TextBlock txtTag = new TextBlock
            {
                Text = tag,
                FontFamily = (FontFamily)FindResource("MonoFont"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = tagBrush
            };
            Grid.SetColumn(txtTag, 1);
            row.Children.Add(txtTag);

            TextBlock txtMsg = new TextBlock
            {
                Text = entry.Message,
                FontFamily = (FontFamily)FindResource("MonoFont"),
                FontSize = 11.5,
                Foreground = entry.Type == "error" ? (Brush)FindResource("RedBrush") : (Brush)FindResource("TextBrush"),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(txtMsg, 2);
            row.Children.Add(txtMsg);

            LogEntriesPanel.Children.Add(row);

            if (_logDrawerOpen)
            {
                LogScrollViewer.ScrollToEnd();
            }
        }

        private void UpdateLogBadge()
        {
            TxtLogBadge.Text = _logs.Count.ToString();
            TxtLogDrawerCount.Text = string.Format(" ({0})", _logs.Count);

            if (_logErrorCount > 0)
            {
                LogCountPill.Background = (Brush)FindResource("RedSoftBrush");
                TxtLogBadge.Foreground = (Brush)FindResource("RedBrush");
            }
            else
            {
                LogCountPill.Background = (Brush)FindResource("BorderLightBrush");
                TxtLogBadge.Foreground = (Brush)FindResource("LabelBrush");
            }
        }

        private void BtnToggleLogs_Click(object sender, RoutedEventArgs e)
        {
            _logDrawerOpen = !_logDrawerOpen;
            LogDrawerBorder.Visibility = _logDrawerOpen ? Visibility.Visible : Visibility.Collapsed;
            if (_logDrawerOpen)
            {
                _logErrorCount = 0;
                UpdateLogBadge();
                LogScrollViewer.ScrollToEnd();
            }
        }

        private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            _logs.Clear();
            _logErrorCount = 0;
            LogEntriesPanel.Children.Clear();
            UpdateLogBadge();
        }

        private void BtnCopyLogs_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            foreach (LogEntry l in _logs)
            {
                sb.AppendFormat("[{0}] [{1}] {2}\n", l.Time, (l.Type ?? "info").ToUpperInvariant(), l.Message);
            }
            try { Clipboard.SetText(sb.ToString()); } catch { }
            ShowToast("Журнал скопирован в буфер");
        }

        private void ShowToast(string message)
        {
            TxtToast.Text = message;
            ToastBorder.Visibility = Visibility.Visible;
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.S)
                {
                    if (_activeProject != null)
                    {
                        _hasUnsavedChanges = true;
                        FlushPendingSave();
                        ShowToast("Проект сохранен");
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.V && _activeTab == "runner" && _activeProject != null)
                {
                    try
                    {
                        if (Clipboard.ContainsImage())
                        {
                            if (RunnerView.TryHandleGlobalImagePaste())
                            {
                                ShowToast("Изображение вставлено из буфера (Ctrl+V)");
                                e.Handled = true;
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            FlushPendingSave();
        }
    }
}
