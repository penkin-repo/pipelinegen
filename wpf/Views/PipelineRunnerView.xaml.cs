using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PipelineGenWpf.Controls;
using PipelineGenWpf.Models;
using PipelineGenWpf.Services;

namespace PipelineGenWpf.Views
{
    public partial class PipelineRunnerView : UserControl
    {
        private Project _project;
        private AppConfig _config;
        private OpenRouterService _openRouter;
        private Action<string, string> _addLog;
        private Action _onProjectMutated;

        private readonly Dictionary<int, string> _stepTextInputs = new Dictionary<int, string>();
        private readonly Dictionary<int, LoadedFileInfo> _stepFiles = new Dictionary<int, LoadedFileInfo>();
        private readonly Dictionary<int, string> _stepResults = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _stepStatuses = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _stepErrorMessages = new Dictionary<int, string>();
        private readonly Dictionary<int, bool> _expandedSteps = new Dictionary<int, bool>();

        private bool _isRunning = false;
        private int? _activeStepIndex = null;
        private int? _pausedAtStepIndex = null;
        private string _finalResult = null;
        private readonly List<ChatMessageItem> _followUpHistory = new List<ChatMessageItem>();
        private string _loadedProjectId = null;

        public PipelineRunnerView()
        {
            InitializeComponent();
        }

        public void Load(
            Project project,
            AppConfig config,
            OpenRouterService openRouter,
            Action<string, string> addLog,
            Action onProjectMutated,
            bool forceResetState)
        {
            _project = project;
            _config = config;
            _openRouter = openRouter;
            _addLog = addLog;
            _onProjectMutated = onProjectMutated;

            bool projectChanged = project == null || project.Id != _loadedProjectId || forceResetState;
            _loadedProjectId = project != null ? project.Id : null;

            if (projectChanged)
            {
                ResetRuntimeState(false);
            }
            else
            {
                // Sync any newly added/removed steps gracefully
                SyncNewStepsDefaults();
            }

            UpdateHeader();
            RebuildStepCards();
            UpdateFinalResultCard();
        }

        public void UpdateConfig(AppConfig config)
        {
            _config = config;
        }

        public void RefreshHeaderMeta()
        {
            UpdateHeader();
        }

        public bool TryHandleGlobalImagePaste()
        {
            if (_project == null || _project.Steps == null) return false;
            if (!Clipboard.ContainsImage()) return false;

            int fileStepIdx = -1;
            for (int i = 0; i < _project.Steps.Count; i++)
            {
                if (_project.Steps[i].Type == "file")
                {
                    fileStepIdx = i;
                    break;
                }
            }
            if (fileStepIdx < 0) return false;

            LoadedFileInfo img = FileHelper.TryLoadImageFromClipboard();
            if (img == null) return false;

            _stepFiles[fileStepIdx] = img;
            RebuildStepCards();
            if (_addLog != null)
            {
                _addLog("ok", string.Format("Шаг {0}: изображение вставлено из буфера обмена (Ctrl+V)", fileStepIdx + 1));
            }
            return true;
        }

        private void ResetRuntimeState(bool clearDefaultTextAndFiles)
        {
            _stepTextInputs.Clear();
            _stepFiles.Clear();
            _stepResults.Clear();
            _stepStatuses.Clear();
            _stepErrorMessages.Clear();
            _expandedSteps.Clear();
            _followUpHistory.Clear();
            _finalResult = null;
            _pausedAtStepIndex = null;
            _activeStepIndex = null;
            _isRunning = false;
            TxtFollowUp.Text = "";

            if (_project != null && _project.Steps != null)
            {
                for (int i = 0; i < _project.Steps.Count; i++)
                {
                    StepDefinition s = _project.Steps[i];
                    _stepStatuses[i] = "idle";
                    if (s.Type == "text")
                    {
                        _stepTextInputs[i] = clearDefaultTextAndFiles ? "" : (s.Content ?? "");
                    }
                    if (!clearDefaultTextAndFiles && s.Type == "file" && !string.IsNullOrEmpty(s.FileData))
                    {
                        _stepFiles[i] = new LoadedFileInfo
                        {
                            FileName = string.IsNullOrEmpty(s.FileName) ? "file" : s.FileName,
                            FileData = s.FileData,
                            FileType = string.IsNullOrEmpty(s.FileType) ? "image/png" : s.FileType,
                            IsImage = (s.FileType ?? "").StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                                      || s.FileData.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                        };
                    }
                }
            }
        }

        private void SyncNewStepsDefaults()
        {
            if (_project == null || _project.Steps == null) return;
            for (int i = 0; i < _project.Steps.Count; i++)
            {
                StepDefinition s = _project.Steps[i];
                if (!_stepStatuses.ContainsKey(i)) _stepStatuses[i] = "idle";
                if (s.Type == "text" && !_stepTextInputs.ContainsKey(i))
                {
                    _stepTextInputs[i] = s.Content ?? "";
                }
                if (s.Type == "file" && !_stepFiles.ContainsKey(i) && !string.IsNullOrEmpty(s.FileData))
                {
                    _stepFiles[i] = new LoadedFileInfo
                    {
                        FileName = string.IsNullOrEmpty(s.FileName) ? "file" : s.FileName,
                        FileData = s.FileData,
                        FileType = string.IsNullOrEmpty(s.FileType) ? "image/png" : s.FileType,
                        IsImage = (s.FileType ?? "").StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                                  || s.FileData.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                    };
                }
            }
        }

        private void UpdateHeader()
        {
            if (_project == null) return;
            int count = _project.Steps != null ? _project.Steps.Count : 0;
            TxtProjectSubtitle.Text = string.Format("· {0} шагов", count);

            BtnClearAll.IsEnabled = !_isRunning;
            BtnRunAll.IsEnabled = !_isRunning;
            BtnRunAll.Content = _isRunning ? "Выполняется..." : "▶ Запустить";
            BtnRunAll.Background = _isRunning
                ? (Brush)FindResource("MutedBrush")
                : (Brush)FindResource("AccentBrush");
        }

        private void UpdateFinalResultCard()
        {
            if (string.IsNullOrEmpty(_finalResult))
            {
                FinalResultCard.Visibility = Visibility.Collapsed;
                return;
            }

            FinalResultCard.Visibility = Visibility.Visible;
            FinalContentControl.ContentText = _finalResult;

            if (_followUpHistory.Count > 0 && _project != null)
            {
                BtnSaveFollowUpStep.Visibility = Visibility.Visible;
                BtnSaveFollowUpStep.Content = string.Format("+ Сохранить как Шаг {0}", _project.Steps.Count + 1);
            }
            else
            {
                BtnSaveFollowUpStep.Visibility = Visibility.Collapsed;
            }
        }

        private void RebuildStepCards()
        {
            StepsPanel.Children.Clear();
            if (_project == null || _project.Steps == null) return;

            for (int idx = 0; idx < _project.Steps.Count; idx++)
            {
                StepsPanel.Children.Add(BuildRunnerStepCard(idx, _project.Steps[idx]));
            }
        }

        private UIElement BuildRunnerStepCard(int idx, StepDefinition step)
        {
            int stepNum = idx + 1;
            string status = _stepStatuses.ContainsKey(idx) ? _stepStatuses[idx] : "idle";
            bool isError = status == "error" || (!_isRunning && status == "running");
            bool isCurrent = _activeStepIndex.HasValue && _activeStepIndex.Value == idx;
            bool isWaiting = (_pausedAtStepIndex.HasValue && _pausedAtStepIndex.Value == idx) || status == "waiting_input";
            string result = _stepResults.ContainsKey(idx) ? _stepResults[idx] : null;
            bool isExpanded = !_expandedSteps.ContainsKey(idx) || _expandedSteps[idx];
            string errorMsg = _stepErrorMessages.ContainsKey(idx) ? _stepErrorMessages[idx] : null;

            bool isText = step.Type == "text";
            bool isFile = step.Type == "file";
            bool isAi = step.Type == "ai";

            bool receivesImage = isAi && idx > 0 && _project.Steps[idx - 1].Type == "file" &&
                ((_stepFiles.ContainsKey(idx - 1) && _stepFiles[idx - 1].IsImage) || !string.IsNullOrEmpty(_project.Steps[idx - 1].FileData));

            Border card = new Border();
            card.CornerRadius = new CornerRadius(4);
            card.Padding = new Thickness(10, 8, 10, 8);
            card.Margin = new Thickness(0, 0, 0, 8);

            if (isWaiting)
            {
                card.BorderBrush = (Brush)FindResource("AccentBrush");
                card.BorderThickness = new Thickness(1.5);
                card.Background = (Brush)FindResource("AccentTintBrush");
            }
            else if (isError)
            {
                card.BorderBrush = (Brush)FindResource("RedBrush");
                card.BorderThickness = new Thickness(1.5);
                card.Background = (Brush)FindResource("RedSoftBrush");
            }
            else if (isCurrent)
            {
                card.BorderBrush = (Brush)FindResource("AccentBrush");
                card.BorderThickness = new Thickness(1);
                card.Background = (Brush)FindResource("AccentTintBrush");
            }
            else
            {
                card.BorderBrush = (Brush)FindResource("BorderSoftBrush");
                card.BorderThickness = new Thickness(1);
                card.Background = (Brush)FindResource("SurfaceBrush");
            }

            StackPanel stack = new StackPanel();

            // Header
            DockPanel header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

            // Right action buttons
            StackPanel rightActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(rightActions, Dock.Right);

            int capturedIdx = idx;

            if (!string.IsNullOrEmpty(result) && !result.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                rightActions.Children.Add(new TextBlock
                {
                    Text = string.Format("{0} симв.", result.Length),
                    FontFamily = (FontFamily)FindResource("MonoFont"),
                    FontSize = 10.5,
                    Foreground = (Brush)FindResource("MutedBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0)
                });
            }

            if (isAi && !string.IsNullOrEmpty(result))
            {
                Button btnToggle = new Button
                {
                    Content = isExpanded ? "Свернуть" : "Показать",
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    FontSize = 11,
                    MinHeight = 22,
                    Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(0, 0, 4, 0)
                };
                btnToggle.Click += delegate
                {
                    _expandedSteps[capturedIdx] = !isExpanded;
                    RebuildStepCards();
                };
                rightActions.Children.Add(btnToggle);
            }

            if (!_isRunning)
            {
                Button btnStartHere = new Button();
                btnStartHere.Content = isError ? "Повторить" : (status == "done" ? "Перезапустить" : "Запустить отсюда");
                btnStartHere.FontSize = 11;
                btnStartHere.MinHeight = 22;
                btnStartHere.Padding = new Thickness(8, 2, 8, 2);
                btnStartHere.Style = isError
                    ? (Style)FindResource("DangerButtonStyle")
                    : (Style)FindResource("SecondaryButtonStyle");
                btnStartHere.Click += async delegate { await ExecutePipelineFromAsync(capturedIdx); };
                rightActions.Children.Add(btnStartHere);
            }

            header.Children.Add(rightActions);

            // Left status badge + title
            StackPanel leftHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            Border badge = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            string badgeText = stepNum.ToString();
            Brush badgeFg = (Brush)FindResource("TextBrush");
            if (isError) { badge.Background = (Brush)FindResource("RedBrush"); badgeFg = Brushes.White; badgeText = "✕"; }
            else if (_isRunning && isCurrent) { badge.Background = (Brush)FindResource("AccentBrush"); badgeFg = Brushes.White; badgeText = "..."; }
            else if (status == "done") { badge.Background = (Brush)FindResource("GreenBrush"); badgeFg = Brushes.White; badgeText = "✓"; }
            else if (isWaiting) { badge.Background = (Brush)FindResource("AccentBrush"); badgeFg = Brushes.White; badgeText = "‖"; }
            else { badge.Background = (Brush)FindResource("ToolbarBrush"); badge.BorderBrush = (Brush)FindResource("BorderBrushMain"); badge.BorderThickness = new Thickness(1); }

            badge.Child = new TextBlock
            {
                Text = badgeText,
                Foreground = badgeFg,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftHead.Children.Add(badge);

            WrapPanel titleRow = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            titleRow.Children.Add(new TextBlock
            {
                Text = string.Format("{0}. {1}", stepNum, step.Title),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            Border modelBadge = new Border
            {
                Background = (Brush)FindResource("Surface2Brush"),
                BorderBrush = (Brush)FindResource("BorderSoftBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            modelBadge.Child = new TextBlock
            {
                Text = isAi ? (string.IsNullOrEmpty(step.Model) ? "AI" : step.Model) : (isFile ? "Файл" : "Текст"),
                FontFamily = (FontFamily)FindResource("MonoFont"),
                FontSize = 10.5,
                Foreground = (Brush)FindResource("MutedBrush")
            };
            titleRow.Children.Add(modelBadge);

            if (receivesImage)
            {
                Border vBadge = new Border
                {
                    Background = (Brush)FindResource("Surface2Brush"),
                    BorderBrush = (Brush)FindResource("BorderSoftBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(5, 1, 5, 1),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                vBadge.Child = new TextBlock
                {
                    Text = string.Format("Vision ← Шаг {0}", idx),
                    FontSize = 10.5,
                    Foreground = (Brush)FindResource("MutedBrush")
                };
                titleRow.Children.Add(vBadge);
            }

            if (isWaiting)
            {
                Border wBadge = new Border
                {
                    Background = (Brush)FindResource("AccentSoftBrush"),
                    BorderBrush = (Brush)FindResource("AccentBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(5, 1, 5, 1),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                wBadge.Child = new TextBlock
                {
                    Text = "Ожидает ввода",
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("AccentBrush")
                };
                titleRow.Children.Add(wBadge);
            }

            if (isError)
            {
                Border eBadge = new Border
                {
                    Background = (Brush)FindResource("RedBrush"),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(5, 1, 5, 1),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                eBadge.Child = new TextBlock
                {
                    Text = "Ошибка",
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                };
                titleRow.Children.Add(eBadge);
            }

            leftHead.Children.Add(titleRow);
            header.Children.Add(leftHead);
            stack.Children.Add(header);

            // Error banner
            if (isError)
            {
                Border errBanner = new Border
                {
                    Background = (Brush)FindResource("SurfaceBrush"),
                    BorderBrush = (Brush)FindResource("RedBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                DockPanel errDock = new DockPanel();
                Button btnRetry = new Button
                {
                    Content = "Повторить",
                    Style = (Style)FindResource("DangerButtonStyle"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                    IsEnabled = !_isRunning
                };
                DockPanel.SetDock(btnRetry, Dock.Right);
                btnRetry.Click += async delegate { await ExecutePipelineFromAsync(capturedIdx); };
                errDock.Children.Add(btnRetry);

                TextBlock errText = new TextBlock
                {
                    Text = string.IsNullOrEmpty(errorMsg)
                        ? "Запрос завершился с ошибкой."
                        : errorMsg,
                    FontFamily = (FontFamily)FindResource("MonoFont"),
                    FontSize = 11,
                    Foreground = (Brush)FindResource("RedBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
                errDock.Children.Add(errText);
                errBanner.Child = errDock;
                stack.Children.Add(errBanner);
            }

            // Body by step type
            if (isText)
            {
                BuildRunnerTextBody(stack, idx, step, isWaiting);
            }
            else if (isFile)
            {
                BuildRunnerFileBody(stack, idx);
            }
            else if (isAi)
            {
                BuildRunnerAiBody(stack, idx, step, result, isExpanded);
            }

            card.Child = stack;
            return card;
        }

        private void BuildRunnerTextBody(StackPanel stack, int idx, StepDefinition step, bool isWaiting)
        {
            if (!string.IsNullOrWhiteSpace(step.Instruction))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = step.Instruction,
                    FontSize = 11.5,
                    Foreground = (Brush)FindResource("MutedBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            string currentVal = _stepTextInputs.ContainsKey(idx) ? _stepTextInputs[idx] : (step.Content ?? "");
            int capturedIdx = idx;

            TextBox tb = new TextBox
            {
                Text = currentVal,
                Style = (Style)FindResource("MonoTextBoxStyle"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = idx == 0 ? 54 : 84,
                MaxHeight = 220
            };
            tb.TextChanged += delegate
            {
                _stepTextInputs[capturedIdx] = tb.Text;
            };
            stack.Children.Add(tb);

            if (isWaiting)
            {
                DockPanel dp = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
                Button btnCont = new Button
                {
                    Content = "▶ Продолжить",
                    Style = (Style)FindResource("PrimaryButtonStyle")
                };
                DockPanel.SetDock(btnCont, Dock.Right);
                btnCont.Click += async delegate { await ExecutePipelineFromAsync(capturedIdx); };
                dp.Children.Add(btnCont);
                stack.Children.Add(dp);
            }
        }

        private void BuildRunnerFileBody(StackPanel stack, int idx)
        {
            int capturedIdx = idx;
            LoadedFileInfo fileInfo = _stepFiles.ContainsKey(idx) ? _stepFiles[idx] : null;

            if (fileInfo != null)
            {
                Border preview = new Border
                {
                    Background = (Brush)FindResource("Surface2Brush"),
                    BorderBrush = (Brush)FindResource("BorderSoftBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(8, 6, 8, 6)
                };
                DockPanel dp = new DockPanel();

                StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(actions, Dock.Right);

                Button btnReplace = new Button
                {
                    Content = "Заменить",
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    FontSize = 11,
                    MinHeight = 22,
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(0, 0, 4, 0)
                };
                btnReplace.Click += delegate { PickFileForRunnerStep(capturedIdx); };
                actions.Children.Add(btnReplace);

                Button btnRemove = new Button
                {
                    Content = "✕",
                    ToolTip = "Удалить файл",
                    Style = (Style)FindResource("DangerGhostButtonStyle")
                };
                btnRemove.Click += delegate
                {
                    _stepFiles.Remove(capturedIdx);
                    RebuildStepCards();
                };
                actions.Children.Add(btnRemove);
                dp.Children.Add(actions);

                StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                BitmapImage bmp = fileInfo.IsImage ? FileHelper.DecodeDataUrlImage(fileInfo.FileData) : null;
                if (bmp != null)
                {
                    Border imgWrap = new Border
                    {
                        Width = 44,
                        Height = 44,
                        CornerRadius = new CornerRadius(3),
                        BorderBrush = (Brush)FindResource("BorderBrushMain"),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 10, 0),
                        ClipToBounds = true
                    };
                    imgWrap.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
                    left.Children.Add(imgWrap);
                }

                StackPanel textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textCol.Children.Add(new TextBlock
                {
                    Text = fileInfo.FileName,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("TextBrush")
                });
                textCol.Children.Add(new TextBlock
                {
                    Text = fileInfo.FileType ?? "file",
                    FontSize = 10.5,
                    FontFamily = (FontFamily)FindResource("MonoFont"),
                    Foreground = (Brush)FindResource("MutedBrush")
                });
                left.Children.Add(textCol);

                dp.Children.Add(left);
                preview.Child = dp;
                stack.Children.Add(preview);
            }
            else
            {
                Border dropzone = new Border
                {
                    Background = (Brush)FindResource("Surface2Brush"),
                    BorderBrush = (Brush)FindResource("BorderBrushMain"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(10, 8, 10, 8),
                    Cursor = Cursors.Hand,
                    AllowDrop = true
                };
                dropzone.MouseEnter += delegate
                {
                    dropzone.BorderBrush = (Brush)FindResource("AccentBrush");
                    dropzone.Background = (Brush)FindResource("AccentTintBrush");
                };
                dropzone.MouseLeave += delegate
                {
                    dropzone.BorderBrush = (Brush)FindResource("BorderBrushMain");
                    dropzone.Background = (Brush)FindResource("Surface2Brush");
                };
                dropzone.MouseLeftButtonUp += delegate { PickFileForRunnerStep(capturedIdx); };
                dropzone.Drop += delegate(object s, DragEventArgs e)
                {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                        if (files != null && files.Length > 0)
                        {
                            LoadedFileInfo loaded = FileHelper.LoadFromPath(files[0]);
                            if (loaded != null)
                            {
                                _stepFiles[capturedIdx] = loaded;
                                RebuildStepCards();
                                if (_addLog != null) _addLog("info", string.Format("Шаг {0}: файл загружен ({1})", capturedIdx + 1, loaded.FileName));
                            }
                        }
                    }
                };

                DockPanel dz = new DockPanel();
                Button btnPaste = new Button
                {
                    Content = "Вставить из буфера (Ctrl+V)",
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    FontSize = 11,
                    MinHeight = 22,
                    Padding = new Thickness(8, 2, 8, 2)
                };
                DockPanel.SetDock(btnPaste, Dock.Right);
                btnPaste.Click += delegate(object s, RoutedEventArgs e)
                {
                    e.Handled = true;
                    LoadedFileInfo clip = FileHelper.TryLoadImageFromClipboard();
                    if (clip != null)
                    {
                        _stepFiles[capturedIdx] = clip;
                        RebuildStepCards();
                        if (_addLog != null) _addLog("ok", string.Format("Шаг {0}: изображение вставлено из буфера", capturedIdx + 1));
                    }
                    else if (_addLog != null)
                    {
                        _addLog("info", "В буфере обмена нет изображения.");
                    }
                };
                dz.Children.Add(btnPaste);

                dz.Children.Add(new TextBlock
                {
                    Text = "Выберите файл или перетащите изображение сюда",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("MutedBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                });

                dropzone.Child = dz;
                stack.Children.Add(dropzone);
            }
        }

        private void BuildRunnerAiBody(StackPanel stack, int idx, StepDefinition step, string result, bool isExpanded)
        {
            string promptPreview = (step.Prompt ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (promptPreview.Length > 140)
            {
                promptPreview = promptPreview.Substring(0, 140) + "...";
            }

            if (!string.IsNullOrEmpty(promptPreview))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = promptPreview,
                    FontFamily = (FontFamily)FindResource("MonoFont"),
                    FontSize = 11,
                    Foreground = (Brush)FindResource("MutedBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            if (!string.IsNullOrEmpty(result) && isExpanded)
            {
                int stepNum = idx + 1;
                string capturedResult = result;

                Border resBorder = new Border
                {
                    Background = (Brush)FindResource("SurfaceBrush"),
                    BorderBrush = (Brush)FindResource("BorderSoftBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 6, 0, 0)
                };

                StackPanel resStack = new StackPanel();
                Border resHead = new Border
                {
                    Background = (Brush)FindResource("ToolbarBrush"),
                    BorderBrush = (Brush)FindResource("BorderLightBrush"),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(8, 3, 8, 3)
                };
                DockPanel headDp = new DockPanel();
                Button copyStepBtn = new Button
                {
                    Content = "Копировать",
                    Style = (Style)FindResource("GhostButtonStyle"),
                    Foreground = (Brush)FindResource("TextBrush"),
                    FontSize = 11
                };
                DockPanel.SetDock(copyStepBtn, Dock.Right);
                copyStepBtn.Click += delegate
                {
                    try { Clipboard.SetText(capturedResult); } catch { }
                    if (_addLog != null) _addLog("ok", string.Format("Шаг {0} скопирован в буфер", stepNum));
                };
                headDp.Children.Add(copyStepBtn);

                headDp.Children.Add(new TextBlock
                {
                    Text = string.Format("Результат шага {0}", stepNum),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("MutedBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                resHead.Child = headDp;
                resStack.Children.Add(resHead);

                Border resBody = new Border
                {
                    Background = (Brush)FindResource("SurfaceBrush"),
                    Padding = new Thickness(10, 8, 10, 8),
                    MinHeight = 100
                };
                CopyableContentControl cc = new CopyableContentControl();
                cc.ContentText = result;
                resBody.Child = cc;
                resStack.Children.Add(resBody);

                resBorder.Child = resStack;
                stack.Children.Add(resBorder);
            }
        }

        private void PickFileForRunnerStep(int stepIdx)
        {
            try
            {
                LoadedFileInfo info = FileHelper.PickFileWithDialog();
                if (info != null)
                {
                    _stepFiles[stepIdx] = info;
                    RebuildStepCards();
                    if (_addLog != null) _addLog("info", string.Format("Шаг {0}: файл выбран ({1})", stepIdx + 1, info.FileName));
                }
            }
            catch (Exception ex)
            {
                if (_addLog != null) _addLog("error", "Ошибка выбора файла: " + ex.Message);
            }
        }

        private async Task ExecutePipelineFromAsync(int startIndex)
        {
            if (_project == null || _project.Steps == null || _project.Steps.Count == 0)
            {
                if (_addLog != null) _addLog("error", "В пайплайне нет шагов.");
                return;
            }
            if (_config == null || string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                if (_addLog != null) _addLog("error", "Не указан API-ключ OpenRouter. Откройте Настройки.");
                return;
            }

            _isRunning = true;
            _pausedAtStepIndex = null;
            _finalResult = null;
            _followUpHistory.Clear();

            for (int j = startIndex; j < _project.Steps.Count; j++)
            {
                _stepStatuses[j] = (j == startIndex) ? "running" : "idle";
                _stepResults.Remove(j);
                _stepErrorMessages.Remove(j);
            }

            UpdateHeader();
            RebuildStepCards();
            UpdateFinalResultCard();

            int currentStepIdx = startIndex;

            try
            {
                for (int i = startIndex; i < _project.Steps.Count; i++)
                {
                    currentStepIdx = i;
                    StepDefinition step = _project.Steps[i];
                    int stepNum = i + 1;
                    _activeStepIndex = i;
                    _stepStatuses[i] = "running";
                    RebuildStepCards();

                    string stepOutput = "";

                    if (step.Type == "file")
                    {
                        LoadedFileInfo fileObj = _stepFiles.ContainsKey(i) ? _stepFiles[i] : null;
                        if (fileObj != null) stepOutput = fileObj.FileData;
                        else if (!string.IsNullOrEmpty(step.FileData)) stepOutput = step.FileData;
                        else stepOutput = "[Нет прикрепленного файла]";

                        _stepResults[i] = stepOutput;
                        _stepStatuses[i] = "done";
                        RebuildStepCards();
                        string fName = fileObj != null ? fileObj.FileName : (!string.IsNullOrEmpty(step.FileName) ? step.FileName : "вход");
                        if (_addLog != null) _addLog("ok", string.Format("Шаг {0} [Файл: {1}] готов", stepNum, fName));
                    }
                    else if (step.Type == "text")
                    {
                        string userVal = _stepTextInputs.ContainsKey(i) ? _stepTextInputs[i] : "";

                        if (i == 0 && string.IsNullOrWhiteSpace(userVal))
                        {
                            _pausedAtStepIndex = 0;
                            _stepStatuses[0] = "waiting_input";
                            _isRunning = false;
                            _activeStepIndex = null;
                            UpdateHeader();
                            RebuildStepCards();
                            if (_addLog != null) _addLog("error", "Шаг 1 не заполнен. Введите данные перед запуском.");
                            return;
                        }

                        if (i > 0 && string.IsNullOrWhiteSpace(userVal))
                        {
                            _pausedAtStepIndex = i;
                            _stepStatuses[i] = "waiting_input";
                            _isRunning = false;
                            _activeStepIndex = null;
                            UpdateHeader();
                            RebuildStepCards();
                            if (_addLog != null) _addLog("info", string.Format("Пауза на Шаге {0}: ожидается ввод.", stepNum));
                            return;
                        }

                        stepOutput = !string.IsNullOrEmpty(userVal) ? userVal : (step.Content ?? "");
                        _stepResults[i] = stepOutput;
                        _stepStatuses[i] = "done";
                        RebuildStepCards();
                        if (_addLog != null) _addLog("ok", string.Format("Шаг {0} [Текст] готов", stepNum));
                    }
                    else if (step.Type == "ai")
                    {
                        string model = !string.IsNullOrEmpty(step.Model)
                            ? step.Model
                            : (!string.IsNullOrEmpty(_config.DefaultModel) ? _config.DefaultModel : "deepseek/deepseek-v4.1-flash");
                        string prompt = step.Prompt ?? "";

                        // Substitute {{step_N}} macros
                        for (int prevIdx = 0; prevIdx < i; prevIdx++)
                        {
                            string macro = string.Format("{{{{step_{0}}}}}", prevIdx + 1);
                            string prevOutput = _stepResults.ContainsKey(prevIdx) ? (_stepResults[prevIdx] ?? "") : "";
                            if (prevOutput.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                            {
                                prompt = prompt.Replace(macro, string.Format("[Изображение из шага {0}]", prevIdx + 1));
                            }
                            else
                            {
                                prompt = prompt.Replace(macro, prevOutput);
                            }
                        }

                        // Detect attached Vision image
                        string attachedImage = null;
                        if (i > 0 && _project.Steps[i - 1].Type == "file")
                        {
                            string prevVal = _stepResults.ContainsKey(i - 1) ? _stepResults[i - 1] : null;
                            if (!string.IsNullOrEmpty(prevVal) && prevVal.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                            {
                                attachedImage = prevVal;
                            }
                        }
                        else
                        {
                            for (int prevIdx = 0; prevIdx < i; prevIdx++)
                            {
                                string macro = string.Format("{{{{step_{0}}}}}", prevIdx + 1);
                                if (!string.IsNullOrEmpty(step.Prompt) && step.Prompt.Contains(macro))
                                {
                                    string val = _stepResults.ContainsKey(prevIdx) ? _stepResults[prevIdx] : null;
                                    if (!string.IsNullOrEmpty(val) && val.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                                    {
                                        attachedImage = val;
                                        break;
                                    }
                                }
                            }
                        }

                        if (attachedImage == null && i == 1 && _stepFiles.ContainsKey(0) && _stepFiles[0].IsImage)
                        {
                            attachedImage = _stepFiles[0].FileData;
                        }

                        object userContent = prompt;
                        if (!string.IsNullOrEmpty(attachedImage))
                        {
                            if (IsModelKnownNonVision(model))
                            {
                                string suggested = GetSuggestedVisionModels();
                                throw new InvalidOperationException(string.Format(
                                    "Модель «{0}» не поддерживает изображения (Vision). Откройте Конструктор и выберите для Шага {1} модель с [Vision] (например: {2}).",
                                    model,
                                    stepNum,
                                    suggested));
                            }

                            List<Dictionary<string, object>> parts = new List<Dictionary<string, object>>();
                            parts.Add(new Dictionary<string, object>
                            {
                                { "type", "text" },
                                { "text", string.IsNullOrEmpty(prompt) ? "Распознай текст и подробно опиши содержимое этого изображения:" : prompt }
                            });
                            parts.Add(new Dictionary<string, object>
                            {
                                { "type", "image_url" },
                                { "image_url", new Dictionary<string, object> { { "url", attachedImage } } }
                            });
                            userContent = parts;
                        }

                        List<ChatMessageItem> msgs = new List<ChatMessageItem>
                        {
                            new ChatMessageItem { Role = "user", Content = userContent }
                        };

                        if (_addLog != null) _addLog("info", string.Format("Шаг {0} [AI: {1}] отправка запроса...", stepNum, model));
                        string aiResponse = await _openRouter.ExecuteChatAsync(_config.ApiKey, model, msgs);

                        stepOutput = aiResponse;
                        _stepResults[i] = stepOutput;
                        _stepStatuses[i] = "done";
                        _expandedSteps[i] = true;
                        RebuildStepCards();
                        if (_addLog != null) _addLog("ok", string.Format("Шаг {0} [AI] успешно выполнен ({1} симв.)", stepNum, aiResponse.Length));

                        if (step.RequireUser && i < _project.Steps.Count - 1)
                        {
                            HumanInputWindow modal = new HumanInputWindow(stepNum, step.Title, stepOutput, step.UserPrompt);
                            modal.Owner = Window.GetWindow(this);
                            bool? dlgRes = modal.ShowDialog();
                            if (dlgRes == true)
                            {
                                stepOutput = modal.EditedText;
                                _stepResults[i] = stepOutput;
                                RebuildStepCards();
                                if (_addLog != null) _addLog("info", string.Format("Шаг {0}: данные подтверждены пользователем, продолжаем...", stepNum));
                            }
                            else
                            {
                                _pausedAtStepIndex = i + 1;
                                _stepStatuses[i + 1] = "waiting_input";
                                _isRunning = false;
                                _activeStepIndex = null;
                                UpdateHeader();
                                RebuildStepCards();
                                if (_addLog != null) _addLog("info", string.Format("Пауза перед Шагом {0}", stepNum + 1));
                                return;
                            }
                        }
                    }

                    if (i == _project.Steps.Count - 1)
                    {
                        _finalResult = stepOutput;
                    }
                }

                UpdateFinalResultCard();
                if (_addLog != null) _addLog("ok", "Пайплайн завершен.");
                RunnerScroll.ScrollToEnd();
            }
            catch (Exception ex)
            {
                _stepStatuses[currentStepIdx] = "error";
                _stepErrorMessages[currentStepIdx] = ex.Message;
                if (_addLog != null) _addLog("error", string.Format("Ошибка выполнения Шага {0}: {1}", currentStepIdx + 1, ex.Message));
            }
            finally
            {
                _isRunning = false;
                _activeStepIndex = null;
                UpdateHeader();
                RebuildStepCards();
            }
        }

        private bool IsModelKnownNonVision(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId)) return false;
            string clean = modelId.Trim();

            if (_config != null && _config.Models != null)
            {
                for (int k = 0; k < _config.Models.Count; k++)
                {
                    ModelConfig m = _config.Models[k];
                    if (m != null && string.Equals(m.Id, clean, StringComparison.OrdinalIgnoreCase))
                    {
                        return !m.IsVision;
                    }
                }
            }

            string lower = clean.ToLowerInvariant();
            if (lower.StartsWith("deepseek/") || lower.StartsWith("minimax/"))
            {
                return true;
            }
            return false;
        }

        private string GetSuggestedVisionModels()
        {
            List<string> visionIds = new List<string>();
            if (_config != null && _config.Models != null)
            {
                for (int k = 0; k < _config.Models.Count; k++)
                {
                    ModelConfig m = _config.Models[k];
                    if (m != null && m.IsVision && !string.IsNullOrEmpty(m.Id))
                    {
                        visionIds.Add(m.Id);
                        if (visionIds.Count >= 3) break;
                    }
                }
            }

            if (visionIds.Count == 0)
            {
                return "qwen/qwen3-vl-8b-instruct, openai/gpt-4o-mini, anthropic/claude-sonnet-5.5";
            }
            return string.Join(", ", visionIds.ToArray());
        }

        private async void BtnRunAll_Click(object sender, RoutedEventArgs e)
        {
            await ExecutePipelineFromAsync(0);
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;
            ResetRuntimeState(true);
            UpdateHeader();
            RebuildStepCards();
            UpdateFinalResultCard();
            if (_addLog != null) _addLog("info", "Формы и результаты очищены.");
        }

        private void BtnCopyFinal_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_finalResult)) return;
            try { Clipboard.SetText(_finalResult); } catch { }
            BtnCopyFinal.Content = "Скопировано";
            BtnCopyFinal.Foreground = (Brush)FindResource("GreenBrush");

            DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
            t.Tick += delegate
            {
                t.Stop();
                BtnCopyFinal.Content = "Копировать";
                BtnCopyFinal.Foreground = (Brush)FindResource("TextBrush");
            };
            t.Start();
        }

        private async void BtnSendFollowUp_Click(object sender, RoutedEventArgs e)
        {
            string text = (TxtFollowUp.Text ?? "").Trim();
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(_finalResult)) return;
            if (_config == null || string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                if (_addLog != null) _addLog("error", "API-ключ OpenRouter не настроен.");
                return;
            }

            string model = !string.IsNullOrEmpty(_config.DefaultModel) ? _config.DefaultModel : "deepseek/deepseek-v4.1-flash";
            if (_project != null && _project.Steps != null)
            {
                for (int i = _project.Steps.Count - 1; i >= 0; i--)
                {
                    if (_project.Steps[i].Type == "ai" && !string.IsNullOrEmpty(_project.Steps[i].Model))
                    {
                        model = _project.Steps[i].Model;
                        break;
                    }
                }
            }

            BtnSendFollowUp.IsEnabled = false;
            BtnSendFollowUp.Content = "Отправка...";
            if (_addLog != null) _addLog("info", string.Format("Отправка запроса модели {0}...", model));

            try
            {
                List<ChatMessageItem> messages = new List<ChatMessageItem>();
                messages.Add(new ChatMessageItem
                {
                    Role = "user",
                    Content = "Вот исходный результат предыдущего этапа работы:\n\n" + _finalResult
                });
                foreach (ChatMessageItem h in _followUpHistory)
                {
                    messages.Add(h);
                }
                messages.Add(new ChatMessageItem { Role = "user", Content = text });

                string reply = await _openRouter.ExecuteChatAsync(_config.ApiKey, model, messages);

                _followUpHistory.Add(new ChatMessageItem { Role = "user", Content = text });
                _followUpHistory.Add(new ChatMessageItem { Role = "assistant", Content = reply });
                _finalResult = reply;
                TxtFollowUp.Text = "";
                UpdateFinalResultCard();
                if (_addLog != null) _addLog("ok", "Ответ модели получен.");
            }
            catch (Exception ex)
            {
                if (_addLog != null) _addLog("error", "Ошибка при отправке ответа: " + ex.Message);
            }
            finally
            {
                BtnSendFollowUp.IsEnabled = true;
                BtnSendFollowUp.Content = "Отправить";
            }
        }

        private void BtnSaveFollowUpStep_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null || _followUpHistory.Count < 2) return;
            string lastUserMsg = Convert.ToString(_followUpHistory[_followUpHistory.Count - 2].Content);
            if (string.IsNullOrEmpty(lastUserMsg)) lastUserMsg = "Дополнительная обработка";

            string model = _config != null ? _config.DefaultModel : "deepseek/deepseek-v4.1-flash";
            for (int i = _project.Steps.Count - 1; i >= 0; i--)
            {
                if (_project.Steps[i].Type == "ai" && !string.IsNullOrEmpty(_project.Steps[i].Model))
                {
                    model = _project.Steps[i].Model;
                    break;
                }
            }

            long ms = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            StepDefinition newStep = new StepDefinition
            {
                Id = "step_" + ms,
                Type = "ai",
                Title = string.Format("Шаг {0}: Доработка по ключам", _project.Steps.Count + 1),
                Model = model,
                Prompt = string.Format("На основе предыдущего шага:\n{{{{step_{0}}}}}\n\n{1}", _project.Steps.Count, lastUserMsg),
                Result = _finalResult ?? "",
                Status = "done"
            };

            _project.Steps.Add(newStep);
            if (_onProjectMutated != null) _onProjectMutated();
            UpdateHeader();
            RebuildStepCards();
            UpdateFinalResultCard();
            if (_addLog != null) _addLog("ok", "Новый шаг добавлен в цепочку пайплайна!");
        }
    }
}
