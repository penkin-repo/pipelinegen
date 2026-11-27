using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PipelineGenWpf.Models;
using PipelineGenWpf.Services;

namespace PipelineGenWpf.Views
{
    public partial class SettingsWindow : Window
    {
        private class PresetItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Provider { get; set; }
            public bool IsVision { get; set; }
            public override string ToString()
            {
                if (string.IsNullOrEmpty(Id)) return Name;
                return string.Format("{0} ({1}){2}", Name, Id, IsVision ? " · [Vision]" : "");
            }
        }

        private static readonly PresetItem[] PresetModels = new PresetItem[]
        {
            new PresetItem { Id = "", Name = "-- Выберите пресет --" },
            new PresetItem { Id = "deepseek/deepseek-v4.1-flash", Name = "DeepSeek V4.1 Flash", Provider = "deepseek", IsVision = false },
            new PresetItem { Id = "anthropic/claude-sonnet-5.5", Name = "Claude Sonnet 5.5", Provider = "anthropic", IsVision = true },
            new PresetItem { Id = "anthropic/claude-haiku-5.5", Name = "Claude Haiku 5.5", Provider = "anthropic", IsVision = true },
            new PresetItem { Id = "anthropic/claude-sonnet-4.6", Name = "Claude Sonnet 4.6", Provider = "anthropic", IsVision = true },
            new PresetItem { Id = "openai/gpt-4.1", Name = "GPT-4.1", Provider = "openai", IsVision = true },
            new PresetItem { Id = "openai/gpt-4o-mini", Name = "GPT-4o Mini", Provider = "openai", IsVision = true },
            new PresetItem { Id = "qwen/qwen3-vl-8b-instruct", Name = "Qwen3 VL 8B", Provider = "qwen", IsVision = true },
            new PresetItem { Id = "google/gemini-2.5-flash", Name = "Gemini 2.5 Flash", Provider = "google", IsVision = true },
            new PresetItem { Id = "google/gemini-2.5-pro", Name = "Gemini 2.5 Pro", Provider = "google", IsVision = true },
            new PresetItem { Id = "minimax/minimax-m3.1-flash", Name = "MiniMax M3.1 Flash", Provider = "minimax", IsVision = false }
        };

        private readonly StorageService _storage;
        private readonly OpenRouterService _openRouter;
        private readonly Action<string, string> _addLog;
        private readonly Action<AppConfig> _onConfigSaved;

        private string _apiKey;
        private string _defaultModel;
        private List<ModelConfig> _models;
        private bool _syncingKey;
        private List<CatalogModelItem> _catalogList = new List<CatalogModelItem>();

        public SettingsWindow(
            AppConfig currentConfig,
            StorageService storage,
            OpenRouterService openRouter,
            Action<AppConfig> onConfigSaved,
            Action<string, string> addLog)
        {
            InitializeComponent();
            _storage = storage;
            _openRouter = openRouter;
            _onConfigSaved = onConfigSaved;
            _addLog = addLog;

            _apiKey = currentConfig.ApiKey ?? "";
            _defaultModel = string.IsNullOrEmpty(currentConfig.DefaultModel) ? "deepseek/deepseek-v4.1-flash" : currentConfig.DefaultModel;
            _models = new List<ModelConfig>();
            if (currentConfig.Models != null)
            {
                foreach (ModelConfig m in currentConfig.Models)
                {
                    _models.Add(m.Clone());
                }
            }

            _syncingKey = true;
            PwdApiKey.Password = _apiKey;
            TxtApiKeyVisible.Text = _apiKey;
            _syncingKey = false;

            foreach (PresetItem p in PresetModels)
            {
                CmbPresets.Items.Add(p);
            }
            CmbPresets.SelectedIndex = 0;

            RebuildModelsList();
        }

        private void PersistNow()
        {
            AppConfig updated = new AppConfig
            {
                ApiKey = (_apiKey ?? "").Trim(),
                DefaultModel = _defaultModel,
                Models = _models.Select(m => m.Clone()).ToList()
            };
            try
            {
                _storage.SaveConfig(updated);
                if (_onConfigSaved != null)
                {
                    _onConfigSaved(updated);
                }
            }
            catch (Exception ex)
            {
                ShowError("Не удалось сохранить конфиг: " + ex.Message);
            }
        }

        private void ShowError(string msg)
        {
            ErrorBanner.Visibility = Visibility.Visible;
            TxtErrorBanner.Text = msg;
            SuccessBanner.Visibility = Visibility.Collapsed;
        }

        private void ShowSuccess(string msg)
        {
            SuccessBanner.Visibility = Visibility.Visible;
            TxtSuccessBanner.Text = msg;
            ErrorBanner.Visibility = Visibility.Collapsed;
        }

        private void ClearBanners()
        {
            ErrorBanner.Visibility = Visibility.Collapsed;
            SuccessBanner.Visibility = Visibility.Collapsed;
        }

        private void RebuildModelsList()
        {
            ModelsListPanel.Children.Clear();
            TxtModelsCount.Text = string.Format("Всего: {0}", _models.Count);

            if (_models.Count == 0)
            {
                ModelsListPanel.Children.Add(new TextBlock
                {
                    Text = "Список моделей пуст.",
                    Foreground = (Brush)FindResource("MutedBrush"),
                    FontSize = 11.5,
                    Margin = new Thickness(4, 8, 4, 8),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return;
            }

            foreach (ModelConfig model in _models)
            {
                ModelConfig captured = model;

                Border row = new Border();
                row.Background = (Brush)FindResource("Surface2Brush");
                row.BorderBrush = (Brush)FindResource("BorderLightBrush");
                row.BorderThickness = new Thickness(1);
                row.CornerRadius = new CornerRadius(3);
                row.Padding = new Thickness(8, 5, 6, 5);
                row.Margin = new Thickness(0, 0, 0, 4);

                DockPanel dock = new DockPanel();

                // Right action buttons
                StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(actions, Dock.Right);

                Button btnTest = new Button();
                btnTest.Content = "Тест";
                btnTest.Style = (Style)FindResource("SecondaryButtonStyle");
                btnTest.FontSize = 11;
                btnTest.MinHeight = 22;
                btnTest.Padding = new Thickness(8, 2, 8, 2);
                btnTest.Margin = new Thickness(0, 0, 4, 0);
                btnTest.Click += async delegate
                {
                    await RecheckModelAsync(captured, btnTest);
                };
                actions.Children.Add(btnTest);

                Button btnDel = new Button();
                btnDel.Content = "✕";
                btnDel.ToolTip = "Удалить";
                btnDel.Style = (Style)FindResource("DangerGhostButtonStyle");
                btnDel.Click += delegate
                {
                    _models.Remove(captured);
                    PersistNow();
                    RebuildModelsList();
                    if (_addLog != null) _addLog("info", "Модель удалена: " + captured.Id);
                };
                actions.Children.Add(btnDel);

                dock.Children.Add(actions);

                // Left status dot & info
                StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                Border dot = new Border();
                dot.Width = 7;
                dot.Height = 7;
                dot.CornerRadius = new CornerRadius(4);
                dot.Margin = new Thickness(0, 0, 8, 0);
                dot.VerticalAlignment = VerticalAlignment.Center;
                if (captured.Status == "verified") dot.Background = (Brush)FindResource("GreenBrush");
                else if (captured.Status == "error") dot.Background = (Brush)FindResource("RedBrush");
                else dot.Background = (Brush)FindResource("MutedBrush");
                left.Children.Add(dot);

                StackPanel info = new StackPanel();
                WrapPanel titleLine = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
                titleLine.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(captured.Name) ? captured.Id : captured.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("TextBrush"),
                    Margin = new Thickness(0, 0, 6, 0)
                });

                if (captured.IsVision)
                {
                    titleLine.Children.Add(new TextBlock
                    {
                        Text = "[Vision]",
                        FontSize = 10.5,
                        FontFamily = (FontFamily)FindResource("MonoFont"),
                        Foreground = (Brush)FindResource("MutedBrush"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    });
                }

                info.Children.Add(titleLine);

                info.Children.Add(new TextBlock
                {
                    Text = captured.Id,
                    FontSize = 10.5,
                    FontFamily = (FontFamily)FindResource("MonoFont"),
                    Foreground = (Brush)FindResource("MutedBrush")
                });

                if (!string.IsNullOrEmpty(captured.StatusMsg))
                {
                    info.Children.Add(new TextBlock
                    {
                        Text = captured.StatusMsg,
                        FontSize = 10.5,
                        Foreground = (Brush)FindResource("RedBrush"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                }

                left.Children.Add(info);
                dock.Children.Add(left);
                row.Child = dock;
                ModelsListPanel.Children.Add(row);
            }
        }

        private async System.Threading.Tasks.Task RecheckModelAsync(ModelConfig model, Button btn)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                ShowError("Введите API-ключ OpenRouter выше.");
                return;
            }

            btn.IsEnabled = false;
            btn.Content = "...";
            ClearBanners();
            if (_addLog != null) _addLog("info", "Повторная проверка доступности " + model.Id + "...");

            try
            {
                await _openRouter.TestModelAsync(_apiKey, model.Id);
                model.Status = "verified";
                model.StatusMsg = "";
                model.TestedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                PersistNow();
                ShowSuccess(string.Format("Модель {0} подтверждена и работает!", model.Id));
                if (_addLog != null) _addLog("ok", "Модель " + model.Id + " подтверждена и обновлена.");
            }
            catch (Exception ex)
            {
                model.Status = "error";
                model.StatusMsg = ex.Message;
                ShowError("Ошибка проверки " + model.Id + ": " + ex.Message);
                if (_addLog != null) _addLog("error", "Ошибка проверки " + model.Id + ": " + ex.Message);
            }
            finally
            {
                RebuildModelsList();
            }
        }

        private void PwdApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_syncingKey) return;
            _syncingKey = true;
            _apiKey = PwdApiKey.Password;
            TxtApiKeyVisible.Text = _apiKey;
            _syncingKey = false;
        }

        private void TxtApiKeyVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingKey) return;
            _syncingKey = true;
            _apiKey = TxtApiKeyVisible.Text;
            PwdApiKey.Password = _apiKey;
            _syncingKey = false;
        }

        private void ApiKey_LostFocus(object sender, RoutedEventArgs e)
        {
            PersistNow();
        }

        private void BtnToggleKey_Click(object sender, RoutedEventArgs e)
        {
            if (TxtApiKeyVisible.Visibility == Visibility.Collapsed)
            {
                TxtApiKeyVisible.Visibility = Visibility.Visible;
                PwdApiKey.Visibility = Visibility.Collapsed;
                BtnToggleKey.Content = "Скрыть";
            }
            else
            {
                TxtApiKeyVisible.Visibility = Visibility.Collapsed;
                PwdApiKey.Visibility = Visibility.Visible;
                BtnToggleKey.Content = "Показать";
            }
        }

        private void CmbPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PresetItem preset = CmbPresets.SelectedItem as PresetItem;
            if (preset == null || string.IsNullOrEmpty(preset.Id)) return;

            TxtNewModelId.Text = preset.Id;
            TxtNewModelName.Text = preset.Name;
            ChkNewVision.IsChecked = preset.IsVision;
            ClearBanners();
        }

        private void TxtNewModelId_TextChanged(object sender, TextChangedEventArgs e)
        {
            string val = TxtNewModelId.Text ?? "";
            bool hasBatch = val.IndexOf(":batch", StringComparison.OrdinalIgnoreCase) >= 0;
            BatchWarningBanner.Visibility = hasBatch ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnCleanBatch_Click(object sender, RoutedEventArgs e)
        {
            string val = TxtNewModelId.Text ?? "";
            TxtNewModelId.Text = val.Replace(":batch", "").Replace(":BATCH", "");
        }

        private async void BtnFetchCatalog_Click(object sender, RoutedEventArgs e)
        {
            BtnFetchCatalog.IsEnabled = false;
            BtnFetchCatalog.Content = "Загрузка...";
            ClearBanners();

            try
            {
                _catalogList = await _openRouter.FetchCatalogAsync(_apiKey);
                CatalogPanel.Visibility = Visibility.Visible;
                RenderCatalogItems();
                if (_addLog != null) _addLog("ok", string.Format("Загружен каталог OpenRouter: {0} доступных моделей", _catalogList.Count));
            }
            catch (Exception ex)
            {
                ShowError("Не удалось загрузить каталог OpenRouter: " + ex.Message);
            }
            finally
            {
                BtnFetchCatalog.IsEnabled = true;
                BtnFetchCatalog.Content = "Каталог OpenRouter";
            }
        }

        private void BtnCloseCatalog_Click(object sender, RoutedEventArgs e)
        {
            CatalogPanel.Visibility = Visibility.Collapsed;
        }

        private void TxtCatalogSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            RenderCatalogItems();
        }

        private void RenderCatalogItems()
        {
            CatalogItemsPanel.Children.Clear();
            string q = (TxtCatalogSearch.Text ?? "").Trim().ToLowerInvariant();
            int shown = 0;

            foreach (CatalogModelItem item in _catalogList)
            {
                if (!string.IsNullOrEmpty(q))
                {
                    bool matchId = item.Id.ToLowerInvariant().Contains(q);
                    bool matchName = (item.Name ?? "").ToLowerInvariant().Contains(q);
                    if (!matchId && !matchName) continue;
                }

                CatalogModelItem captured = item;
                Border row = new Border();
                row.Padding = new Thickness(7, 4, 7, 4);
                row.CornerRadius = new CornerRadius(6);
                row.Cursor = Cursors.Hand;
                row.MouseEnter += delegate { row.Background = (Brush)FindResource("Surface2Brush"); };
                row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
                row.MouseLeftButtonUp += delegate
                {
                    TxtNewModelId.Text = captured.Id;
                    TxtNewModelName.Text = captured.Name;
                    ChkNewVision.IsChecked = captured.IsVision;
                    CatalogPanel.Visibility = Visibility.Collapsed;
                };

                DockPanel dp = new DockPanel();
                if (captured.IsVision)
                {
                    Border vb = new Border
                    {
                        Background = (Brush)FindResource("AmberSoftBrush"),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(5, 1, 5, 1)
                    };
                    DockPanel.SetDock(vb, Dock.Right);
                    vb.Child = new TextBlock
                    {
                        Text = "Vision",
                        FontSize = 9.5,
                        Foreground = (Brush)FindResource("AmberTextBrush")
                    };
                    dp.Children.Add(vb);
                }

                dp.Children.Add(new TextBlock
                {
                    Text = captured.Id,
                    FontFamily = (FontFamily)FindResource("MonoFont"),
                    FontSize = 11.5,
                    Foreground = (Brush)FindResource("TextBrush")
                });
                row.Child = dp;
                CatalogItemsPanel.Children.Add(row);

                shown++;
                if (shown >= 25) break;
            }
        }

        private void UpsertModel(string modelId, string friendlyName, bool isVision)
        {
            string provider = modelId.Contains("/") ? modelId.Split('/')[0] : "custom";
            string fallbackName = modelId.Contains("/") ? modelId.Split('/')[1] : modelId;
            string name = string.IsNullOrWhiteSpace(friendlyName) ? fallbackName : friendlyName.Trim();

            ModelConfig entry = new ModelConfig
            {
                Id = modelId,
                Name = name,
                Provider = provider,
                IsVision = isVision,
                Status = "verified",
                TestedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            int idx = _models.FindIndex(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) _models[idx] = entry;
            else _models.Add(entry);

            PersistNow();
            RebuildModelsList();
            TxtNewModelId.Text = "";
            TxtNewModelName.Text = "";
        }

        private void BtnForceAdd_Click(object sender, RoutedEventArgs e)
        {
            string modelId = (TxtNewModelId.Text ?? "").Trim();
            if (string.IsNullOrEmpty(modelId))
            {
                ShowError("Укажите ID модели.");
                return;
            }

            UpsertModel(modelId, TxtNewModelName.Text, ChkNewVision.IsChecked == true);
            ShowSuccess(string.Format("Модель {0} добавлена в реестр и сохранена.", modelId));
            if (_addLog != null) _addLog("ok", "Модель " + modelId + " сохранена в настройках.");
        }

        private async void BtnTestAndAdd_Click(object sender, RoutedEventArgs e)
        {
            string modelId = (TxtNewModelId.Text ?? "").Trim();
            if (string.IsNullOrEmpty(modelId))
            {
                ShowError("Укажите ID модели (например, deepseek/deepseek-v4.1-flash).");
                return;
            }
            if (modelId.IndexOf(":batch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ShowError("Модели с суффиксом :batch не работают в чате. Удалите :batch.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                ShowError("Сначала введите API-ключ OpenRouter выше.");
                return;
            }

            BtnTestAndAdd.IsEnabled = false;
            BtnTestAndAdd.Content = "Проверка API...";
            ClearBanners();
            if (_addLog != null) _addLog("info", "Проверка доступности модели " + modelId + " через OpenRouter...");

            try
            {
                await _openRouter.TestModelAsync(_apiKey, modelId);
                UpsertModel(modelId, TxtNewModelName.Text, ChkNewVision.IsChecked == true);
                ShowSuccess(string.Format("Модель {0} успешно проверена и сохранена!", modelId));
                if (_addLog != null) _addLog("ok", "Модель " + modelId + " доступна и сохранена в реестр.");
            }
            catch (Exception ex)
            {
                ShowError("Ошибка проверки: " + ex.Message);
                if (_addLog != null) _addLog("error", "Проверка " + modelId + " не удалась: " + ex.Message);
            }
            finally
            {
                BtnTestAndAdd.IsEnabled = true;
                BtnTestAndAdd.Content = "Проверить и добавить";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            PersistNow();
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            PersistNow();
            if (_addLog != null) _addLog("ok", "Настройки успешно сохранены на диск.");
            DialogResult = true;
            Close();
        }
    }
}
