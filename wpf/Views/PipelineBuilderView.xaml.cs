using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PipelineGenWpf.Models;
using PipelineGenWpf.Services;

namespace PipelineGenWpf.Views
{
    public partial class PipelineBuilderView : UserControl
    {
        private Project _project;
        private AppConfig _config;
        private Action _onProjectMutated;
        private Action<string, string> _addLog;
        private bool _suppressEvents;

        public PipelineBuilderView()
        {
            InitializeComponent();
        }

        public void Load(Project project, AppConfig config, Action onProjectMutated, Action<string, string> addLog)
        {
            _project = project;
            _config = config;
            _onProjectMutated = onProjectMutated;
            _addLog = addLog;

            _suppressEvents = true;
            TxtProjectDescription.Text = project != null ? (project.Description ?? "") : "";
            _suppressEvents = false;

            RebuildSteps();
        }

        public void UpdateConfig(AppConfig config)
        {
            _config = config;
            if (_project != null)
            {
                RebuildSteps();
            }
        }

        private void NotifyMutated()
        {
            if (_suppressEvents) return;
            if (_onProjectMutated != null)
            {
                _onProjectMutated();
            }
        }

        private void TxtProjectDescription_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressEvents || _project == null) return;
            _project.Description = TxtProjectDescription.Text;
            NotifyMutated();
        }

        private void RebuildSteps()
        {
            StepsPanel.Children.Clear();
            if (_project == null || _project.Steps == null)
            {
                TxtStepCount.Text = "· 0 шагов";
                return;
            }

            TxtStepCount.Text = string.Format("· {0} шагов", _project.Steps.Count);

            for (int idx = 0; idx < _project.Steps.Count; idx++)
            {
                StepsPanel.Children.Add(BuildStepCard(idx, _project.Steps[idx]));
            }
        }

        private UIElement BuildStepCard(int idx, StepDefinition step)
        {
            int stepNum = idx + 1;
            bool isAi = step.Type == "ai";
            bool isFile = step.Type == "file";
            bool isText = !isAi && !isFile;

            Border card = new Border();
            card.Background = (Brush)FindResource("SurfaceBrush");
            card.BorderBrush = (Brush)FindResource("BorderSoftBrush");
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(4);
            card.Margin = new Thickness(0, 0, 0, 8);

            StackPanel cardStack = new StackPanel();

            // Header
            Border header = new Border();
            header.Background = (Brush)FindResource("ToolbarBrush");
            header.BorderBrush = (Brush)FindResource("BorderLightBrush");
            header.BorderThickness = new Thickness(0, 0, 0, 1);
            header.CornerRadius = new CornerRadius(3, 3, 0, 0);
            header.Padding = new Thickness(10, 5, 8, 5);

            DockPanel headDock = new DockPanel();

            // Right controls: Up, Down, Delete
            StackPanel rightBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(rightBtns, Dock.Right);

            int capturedIdx = idx;
            Button btnUp = new Button
            {
                Content = "▲",
                ToolTip = "Вверх",
                Style = (Style)FindResource("GhostButtonStyle"),
                FontSize = 10,
                IsEnabled = idx > 0
            };
            btnUp.Click += delegate { MoveStep(capturedIdx, -1); };
            rightBtns.Children.Add(btnUp);

            Button btnDown = new Button
            {
                Content = "▼",
                ToolTip = "Вниз",
                Style = (Style)FindResource("GhostButtonStyle"),
                FontSize = 10,
                IsEnabled = idx < _project.Steps.Count - 1
            };
            btnDown.Click += delegate { MoveStep(capturedIdx, 1); };
            rightBtns.Children.Add(btnDown);

            Button btnDel = new Button
            {
                Content = "✕",
                ToolTip = "Удалить шаг",
                Style = (Style)FindResource("DangerGhostButtonStyle"),
                Margin = new Thickness(2, 0, 0, 0)
            };
            btnDel.Click += delegate { RemoveStep(capturedIdx); };
            rightBtns.Children.Add(btnDel);

            headDock.Children.Add(rightBtns);

            // Left: Step badge + editable title + type pill
            StackPanel leftHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            Border numBadge = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 8, 0),
                Background = (Brush)FindResource("SurfaceBrush"),
                BorderBrush = (Brush)FindResource("BorderBrushMain"),
                BorderThickness = new Thickness(1)
            };
            numBadge.Child = new TextBlock
            {
                Text = stepNum.ToString(),
                Foreground = (Brush)FindResource("TextBrush"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftHead.Children.Add(numBadge);

            TextBox titleBox = new TextBox
            {
                Text = step.Title ?? "",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(3, 1, 3, 1),
                MinWidth = 180,
                MaxWidth = 380,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleBox.GotFocus += delegate { titleBox.BorderBrush = (Brush)FindResource("AccentBrush"); };
            titleBox.LostFocus += delegate { titleBox.BorderBrush = Brushes.Transparent; };
            titleBox.TextChanged += delegate
            {
                step.Title = titleBox.Text;
                NotifyMutated();
            };
            leftHead.Children.Add(titleBox);

            Border typePill = new Border
            {
                Background = (Brush)FindResource("SurfaceBrush"),
                BorderBrush = (Brush)FindResource("BorderSoftBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            typePill.Child = new TextBlock
            {
                Text = isAi ? "AI" : (isFile ? "Файл" : "Текст"),
                FontFamily = (FontFamily)FindResource("MonoFont"),
                FontSize = 10.5,
                Foreground = (Brush)FindResource("MutedBrush")
            };
            leftHead.Children.Add(typePill);

            headDock.Children.Add(leftHead);
            header.Child = headDock;
            cardStack.Children.Add(header);

            // Body
            StackPanel body = new StackPanel { Margin = new Thickness(10, 8, 10, 10) };

            if (isText)
            {
                BuildTextStepBody(body, idx, step);
            }
            else if (isFile)
            {
                BuildFileStepBody(body, idx, step);
            }
            else
            {
                BuildAiStepBody(body, idx, step);
            }

            cardStack.Children.Add(body);
            card.Child = cardStack;
            return card;
        }

        private void BuildTextStepBody(StackPanel body, int idx, StepDefinition step)
        {
            int stepNum = idx + 1;

            DockPanel instrRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            instrRow.Children.Add(new TextBlock
            {
                Text = "Пояснение:",
                FontSize = 11.5,
                Foreground = (Brush)FindResource("MutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            TextBox instrBox = new TextBox
            {
                Text = step.Instruction ?? "",
                Style = (Style)FindResource("ModernTextBoxStyle"),
                FontSize = 12,
                Padding = new Thickness(6, 3, 6, 3)
            };
            instrBox.TextChanged += delegate
            {
                step.Instruction = instrBox.Text;
                NotifyMutated();
            };
            instrRow.Children.Add(instrBox);
            body.Children.Add(instrRow);

            DockPanel labelRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            if (idx > 0)
            {
                TextBlock pauseNote = new TextBlock
                {
                    Text = "Пауза на этом шаге",
                    FontSize = 11,
                    Foreground = (Brush)FindResource("MutedBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(pauseNote, Dock.Right);
                labelRow.Children.Add(pauseNote);
            }

            labelRow.Children.Add(new TextBlock
            {
                Text = string.Format("Текст по умолчанию ({{{{step_{0}}}}}):", stepNum),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("LabelBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });
            body.Children.Add(labelRow);

            TextBox contentBox = new TextBox
            {
                Text = step.Content ?? "",
                Style = (Style)FindResource("MonoTextBoxStyle"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 76,
                MaxHeight = 200
            };
            contentBox.TextChanged += delegate
            {
                step.Content = contentBox.Text;
                NotifyMutated();
            };
            body.Children.Add(contentBox);
        }

        private void BuildFileStepBody(StackPanel body, int idx, StepDefinition step)
        {
            int capturedIdx = idx;

            if (!string.IsNullOrEmpty(step.FileName))
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
                btnReplace.Click += delegate { PickFileForStep(capturedIdx); };
                actions.Children.Add(btnReplace);

                Button btnClear = new Button
                {
                    Content = "✕",
                    ToolTip = "Удалить файл",
                    Style = (Style)FindResource("DangerGhostButtonStyle")
                };
                btnClear.Click += delegate
                {
                    step.FileName = "";
                    step.FileData = "";
                    step.Content = "";
                    NotifyMutated();
                    RebuildSteps();
                };
                actions.Children.Add(btnClear);
                dp.Children.Add(actions);

                StackPanel infoPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                BitmapImage bmp = FileHelper.DecodeDataUrlImage(step.FileData);
                if (bmp != null)
                {
                    Border imgWrap = new Border
                    {
                        Width = 42,
                        Height = 42,
                        CornerRadius = new CornerRadius(3),
                        BorderBrush = (Brush)FindResource("BorderBrushMain"),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 10, 0),
                        ClipToBounds = true
                    };
                    imgWrap.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
                    infoPanel.Children.Add(imgWrap);
                }

                StackPanel textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textCol.Children.Add(new TextBlock
                {
                    Text = step.FileName,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("TextBrush")
                });
                textCol.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(step.FileType) ? "file" : step.FileType,
                    FontSize = 10.5,
                    FontFamily = (FontFamily)FindResource("MonoFont"),
                    Foreground = (Brush)FindResource("MutedBrush")
                });
                infoPanel.Children.Add(textCol);

                dp.Children.Add(infoPanel);
                preview.Child = dp;
                body.Children.Add(preview);
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
                dropzone.MouseLeftButtonUp += delegate { PickFileForStep(capturedIdx); };
                dropzone.Drop += delegate(object s, DragEventArgs e)
                {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                        if (files != null && files.Length > 0)
                        {
                            ApplyFileToStep(capturedIdx, FileHelper.LoadFromPath(files[0]));
                        }
                    }
                };

                DockPanel dzStack = new DockPanel();

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
                        ApplyFileToStep(capturedIdx, clip);
                    }
                    else if (_addLog != null)
                    {
                        _addLog("info", "В буфере обмена нет изображения.");
                    }
                };
                dzStack.Children.Add(btnPaste);

                dzStack.Children.Add(new TextBlock
                {
                    Text = "Выберите файл или перетащите изображение сюда",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("MutedBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                });

                dropzone.Child = dzStack;
                body.Children.Add(dropzone);
            }
        }

        private void BuildAiStepBody(StackPanel body, int idx, StepDefinition step)
        {
            // Model selection grid (2 columns)
            Grid modelGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            modelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            modelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left: Registry dropdown
            StackPanel leftCol = new StackPanel();
            leftCol.Children.Add(new TextBlock
            {
                Text = "Модель:",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("LabelBrush"),
                Margin = new Thickness(0, 0, 0, 3)
            });

            ComboBox cmbModel = new ComboBox { Style = (Style)FindResource("ModernComboBoxStyle") };
            List<ModelConfig> regModels = (_config != null && _config.Models != null) ? _config.Models : new List<ModelConfig>();
            int selectedModelIdx = -1;
            string currentModel = string.IsNullOrEmpty(step.Model)
                ? (_config != null ? _config.DefaultModel : "deepseek/deepseek-v4.1-flash")
                : step.Model;

            for (int mIdx = 0; mIdx < regModels.Count; mIdx++)
            {
                cmbModel.Items.Add(new ComboBoxItem { Content = regModels[mIdx].DisplayLabel, Tag = regModels[mIdx].Id });
                if (string.Equals(regModels[mIdx].Id, currentModel, StringComparison.OrdinalIgnoreCase))
                {
                    selectedModelIdx = mIdx;
                }
            }
            cmbModel.Items.Add(new ComboBoxItem { Content = "-- Вручную --", Tag = "custom" });
            cmbModel.SelectedIndex = selectedModelIdx >= 0 ? selectedModelIdx : (cmbModel.Items.Count - 1);
            leftCol.Children.Add(cmbModel);
            Grid.SetColumn(leftCol, 0);
            modelGrid.Children.Add(leftCol);

            // Right: Manual Model ID
            StackPanel rightCol = new StackPanel();
            rightCol.Children.Add(new TextBlock
            {
                Text = "ID модели:",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("LabelBrush"),
                Margin = new Thickness(0, 0, 0, 3)
            });

            TextBox customModelBox = new TextBox
            {
                Text = step.Model ?? "",
                Style = (Style)FindResource("MonoTextBoxStyle"),
                FontSize = 12,
                Padding = new Thickness(7, 4, 7, 4)
            };
            rightCol.Children.Add(customModelBox);
            Grid.SetColumn(rightCol, 2);
            modelGrid.Children.Add(rightCol);

            Border visionWarning = new Border
            {
                Background = (Brush)FindResource("AmberSoftBrush"),
                BorderBrush = (Brush)FindResource("AmberBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = Visibility.Collapsed
            };
            visionWarning.Child = new TextBlock
            {
                Text = "Этот шаг получает файл/изображение, но выбранная модель не поддерживает [Vision]. Выберите модель с пометкой [Vision].",
                FontSize = 11,
                Foreground = (Brush)FindResource("AmberTextBrush"),
                TextWrapping = TextWrapping.Wrap
            };

            Action refreshVisionWarning = delegate
            {
                bool needsVision = StepReceivesFile(idx, step);
                bool nonVision = IsModelNonVision(string.IsNullOrEmpty(step.Model) ? currentModel : step.Model);
                visionWarning.Visibility = (needsVision && nonVision) ? Visibility.Visible : Visibility.Collapsed;
            };

            bool syncingModel = false;
            cmbModel.SelectionChanged += delegate
            {
                if (syncingModel) return;
                ComboBoxItem sel = cmbModel.SelectedItem as ComboBoxItem;
                if (sel == null) return;
                string tag = sel.Tag as string;
                if (tag != null && tag != "custom")
                {
                    syncingModel = true;
                    step.Model = tag;
                    customModelBox.Text = tag;
                    syncingModel = false;
                    refreshVisionWarning();
                    NotifyMutated();
                }
            };
            customModelBox.TextChanged += delegate
            {
                if (syncingModel) return;
                step.Model = customModelBox.Text;
                refreshVisionWarning();
                NotifyMutated();
            };

            body.Children.Add(modelGrid);
            refreshVisionWarning();
            body.Children.Add(visionWarning);

            // Prompt TextBox
            TextBlock charCountTxt = new TextBlock
            {
                Text = string.Format("{0} симв.", (step.Prompt ?? "").Length),
                FontSize = 10.5,
                FontFamily = (FontFamily)FindResource("MonoFont"),
                Foreground = (Brush)FindResource("MutedBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBox promptBox = new TextBox
            {
                Text = step.Prompt ?? "",
                Style = (Style)FindResource("MonoTextBoxStyle"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 140,
                MaxHeight = 360,
                Padding = new Thickness(8, 6, 8, 6)
            };
            promptBox.TextChanged += delegate
            {
                step.Prompt = promptBox.Text;
                charCountTxt.Text = string.Format("{0} симв.", promptBox.Text.Length);
                refreshVisionWarning();
                NotifyMutated();
            };

            // Prompt header + macro chips
            DockPanel promptHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            DockPanel.SetDock(charCountTxt, Dock.Right);
            promptHeader.Children.Add(charCountTxt);

            WrapPanel chipsPanel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            chipsPanel.Children.Add(new TextBlock
            {
                Text = "Промпт:",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("LabelBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            for (int prev = 0; prev < idx; prev++)
            {
                string macro = string.Format("{{{{step_{0}}}}}", prev + 1);
                Button chip = new Button
                {
                    Content = macro,
                    Style = (Style)FindResource("AmberChipButtonStyle"),
                    Focusable = false,
                    Margin = new Thickness(0, 0, 4, 2)
                };
                int capturedStepNum = idx + 1;
                chip.Click += delegate
                {
                    InsertMacroAtCaret(promptBox, step, macro, capturedStepNum);
                };
                chipsPanel.Children.Add(chip);
            }

            Button copyInstrChip = new Button
            {
                Content = "+ <copy>",
                Style = (Style)FindResource("SecondaryButtonStyle"),
                FontSize = 10.5,
                MinHeight = 20,
                Padding = new Thickness(6, 1, 6, 1),
                Focusable = false,
                Margin = new Thickness(0, 0, 4, 2),
                ToolTip = "Вставить тег <copy>...</copy> в промпт"
            };
            int capNum = idx + 1;
            copyInstrChip.Click += delegate
            {
                InsertMacroAtCaret(
                    promptBox,
                    step,
                    "\n- Оберни каждую отдельную фразу в тег <copy>...</copy>\n",
                    capNum);
            };
            chipsPanel.Children.Add(copyInstrChip);

            promptHeader.Children.Add(chipsPanel);
            body.Children.Add(promptHeader);
            body.Children.Add(promptBox);

            // Human-in-the-loop option
            CheckBox chkRequireUser = new CheckBox
            {
                Content = "Пауза для проверки результата после шага",
                IsChecked = step.RequireUser,
                FontSize = 11.5,
                Foreground = (Brush)FindResource("LabelBrush"),
                Margin = new Thickness(0, 7, 0, 0)
            };
            chkRequireUser.Checked += delegate { step.RequireUser = true; NotifyMutated(); };
            chkRequireUser.Unchecked += delegate { step.RequireUser = false; NotifyMutated(); };
            body.Children.Add(chkRequireUser);
        }

        private bool StepReceivesFile(int idx, StepDefinition step)
        {
            if (_project == null || _project.Steps == null) return false;
            if (idx > 0 && _project.Steps[idx - 1].Type == "file")
            {
                return true;
            }
            string p = step != null ? (step.Prompt ?? "") : "";
            for (int prev = 0; prev < idx; prev++)
            {
                if (_project.Steps[prev].Type == "file")
                {
                    string macro = string.Format("{{{{step_{0}}}}}", prev + 1);
                    if (p.Contains(macro)) return true;
                }
            }
            return false;
        }

        private bool IsModelNonVision(string modelId)
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
            return lower.StartsWith("deepseek/") || lower.StartsWith("minimax/");
        }

        private void InsertMacroAtCaret(TextBox box, StepDefinition step, string macro, int stepNum)
        {
            string current = box.Text ?? "";
            int start = box.SelectionStart;
            int len = box.SelectionLength;
            if (start < 0 || start > current.Length) start = current.Length;

            string updated = current.Substring(0, start) + macro + current.Substring(start + len);
            box.Text = updated;
            step.Prompt = updated;
            box.Focus();
            box.CaretIndex = start + macro.Length;
            NotifyMutated();

            if (_addLog != null)
            {
                _addLog("ok", string.Format("Макрос {0} вставлен в позицию курсора промпта (Шаг {1})", macro.Trim(), stepNum));
            }
        }

        private void PickFileForStep(int stepIdx)
        {
            try
            {
                LoadedFileInfo info = FileHelper.PickFileWithDialog();
                if (info != null)
                {
                    ApplyFileToStep(stepIdx, info);
                }
            }
            catch (Exception ex)
            {
                if (_addLog != null) _addLog("error", "Ошибка загрузки файла: " + ex.Message);
            }
        }

        private void ApplyFileToStep(int stepIdx, LoadedFileInfo info)
        {
            if (info == null || _project == null || stepIdx < 0 || stepIdx >= _project.Steps.Count) return;
            StepDefinition step = _project.Steps[stepIdx];
            step.FileName = info.FileName;
            step.FileType = info.FileType;
            step.FileData = info.FileData;
            step.Content = info.Content;
            NotifyMutated();
            RebuildSteps();
            if (_addLog != null) _addLog("ok", string.Format("Шаг {0}: файл загружен ({1})", stepIdx + 1, info.FileName));
        }

        private void MoveStep(int idx, int delta)
        {
            if (_project == null || _project.Steps == null) return;
            int target = idx + delta;
            if (target < 0 || target >= _project.Steps.Count) return;

            StepDefinition tmp = _project.Steps[idx];
            _project.Steps[idx] = _project.Steps[target];
            _project.Steps[target] = tmp;
            NotifyMutated();
            RebuildSteps();
        }

        private void RemoveStep(int idx)
        {
            if (_project == null || _project.Steps == null || idx < 0 || idx >= _project.Steps.Count) return;
            _project.Steps.RemoveAt(idx);
            NotifyMutated();
            RebuildSteps();
            if (_addLog != null) _addLog("info", string.Format("Шаг {0} удален", idx + 1));
        }

        private void AddStep(string type)
        {
            if (_project == null) return;
            int stepNum = _project.Steps.Count + 1;
            long ms = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            StepDefinition step = new StepDefinition { Id = "step_" + ms, Type = type };

            if (type == "text")
            {
                step.Title = "Текстовый блок " + stepNum;
            }
            else if (type == "file")
            {
                step.Title = "Загрузка файла / Картинки " + stepNum;
            }
            else
            {
                bool afterFileStep = _project.Steps.Count > 0 && _project.Steps[_project.Steps.Count - 1].Type == "file";
                string defaultModel = afterFileStep ? "qwen/qwen3-vl-8b-instruct" : "deepseek/deepseek-v4.1-flash";

                if (afterFileStep)
                {
                    if (_config != null && _config.Models != null)
                    {
                        for (int mIdx = 0; mIdx < _config.Models.Count; mIdx++)
                        {
                            if (_config.Models[mIdx].IsVision && !string.IsNullOrEmpty(_config.Models[mIdx].Id))
                            {
                                defaultModel = _config.Models[mIdx].Id;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    if (_config != null && !string.IsNullOrEmpty(_config.DefaultModel))
                    {
                        defaultModel = _config.DefaultModel;
                    }
                    else if (_config != null && _config.Models != null && _config.Models.Count > 0)
                    {
                        defaultModel = _config.Models[0].Id;
                    }
                }

                step.Title = "AI-обработка " + stepNum;
                step.Model = defaultModel;
                step.Prompt = stepNum > 1
                    ? string.Format("Проанализируй следующие данные:\n\n{{{{step_{0}}}}}", stepNum - 1)
                    : "";
            }

            _project.Steps.Add(step);
            AddStepPopup.IsOpen = false;
            NotifyMutated();
            RebuildSteps();
            if (_addLog != null) _addLog("info", string.Format("Добавлен шаг {0} ({1})", stepNum, type));
            MainScroll.ScrollToEnd();
        }

        private void BtnAddStep_Click(object sender, RoutedEventArgs e)
        {
            AddStepPopup.IsOpen = !AddStepPopup.IsOpen;
        }

        private void BtnAddTextStep_Click(object sender, RoutedEventArgs e)
        {
            AddStep("text");
        }

        private void BtnAddFileStep_Click(object sender, RoutedEventArgs e)
        {
            AddStep("file");
        }

        private void BtnAddAiStep_Click(object sender, RoutedEventArgs e)
        {
            AddStep("ai");
        }
    }
}
