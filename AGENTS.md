# AGENTS.md — Контекст проекта PIP (PipelineGen 2026)

Этот файл предназначен для быстрого восстановления контекста в новых сессиях разработки.

## 1. Текущий статус и архитектура
- **Приложение:** `wpf/` — самостоятельное Windows-приложение на чистом **.NET Framework 4.8 (WPF)** без сторонних библиотек и NuGet-пакетов.
- **Брендинг:** короткое имя **PIP**, иконка `wpf/Resources/app.ico` (красные буквы `PIP`).
- **Архитектура:** чистая модульная структура (`Models/`, `Services/`, `Views/`, `Controls/`, `Resources/`), 0 внешних зависимостей, собирается в один exe-файл (~187 КБ). Доработки следует делать точечно внутри `wpf/`.

## 2. Критические технические ограничения (ОБЯЗАТЕЛЬНО)
1. **Компилятор — C# 5 (`MSBuild 4.0` из `.NET Framework 4.8`)!**
   - Сборка выполняется через `/c/Windows/Microsoft.NET/Framework64/v4.0.30319/MSBuild.exe`.
   - **НЕ использовать синтаксис C# 6+:**
     - Запрещены `?.`, `?[]`, `$"..."`, `nameof(...)`, expression-bodied members (`=>`), `using var`, `out var`, pattern matching, кортежи `(a, b)`, `target-typed new()`.
     - **Запрещен `await` внутри блоков `catch` / `finally`** (`CS1985`). Если нужен `Task.Delay` при ретрае — сохраняйте флаг/задержку и делайте `await` после `try/catch`.
   - Использовать классический C# 5: `string.Format(...)`, явные проверки `!= null`, `delegate { ... }`.
2. **Ноль сторонних зависимостей.**
   - Только стандартные сборки .NET 4.8 (`PresentationCore`, `PresentationFramework`, `WindowsBase`, `System.Net.Http`, `System.Web.Extensions` для `JavaScriptSerializer`, `System.Drawing` и т.п.).
3. **XAML-экранирование фигурных скобок.**
   - Строки вроде `{{step_1}}` в XAML-атрибутах `Text="..."` обязательно экранировать ведущим `{}` (например, `Text="{}{{step_1}}"`).
4. **Не трогать пользовательские данные.**
   - Живой конфиг и проекты пользователя лежат в `%APPDATA%\PipelineGen2026\config.json` и `%APPDATA%\PipelineGen2026\projects\`.

## 3. Команды сборки и проверки
### Сборка Release (должно быть 0 ошибок и 0 предупреждений)
```bash
/c/Windows/Microsoft.NET/Framework64/v4.0.30319/MSBuild.exe wpf/PipelineGenWpf.csproj -p:Configuration=Release -t:Rebuild -v:minimal
```
Результат: `wpf/bin/Release/PipelineGenWpf.exe`.

### Быстрый smoke-тест запуска (Git Bash)
В Git Bash флаги Windows-утилит нужно экранировать двойным слэшем (`//FI`, `//IM`, `//F`):
```bash
"wpf/bin/Release/PipelineGenWpf.exe" &
sleep 2
tasklist.exe //FI "IMAGENAME eq PipelineGenWpf.exe"
taskkill.exe //IM PipelineGenWpf.exe //F
```

## 4. Карта файлов `wpf/`
- `App.xaml.cs` — `ConfigureNetworkDefaults()` (`TLS 1.2/1.3`, `Expect100Continue = false`, `DefaultConnectionLimit = 24`).
- `Resources/Styles.xaml` — палитра и стили:
  - Режим **Запуск**: нейтральный фон `BgBrush` (`#F3F3F3`), акцент `AccentBrush` (`#0067C0`).
  - Режим **Конструктор**: теплый фон `BuilderBgBrush` (`#F7F3EA`), панель `BuilderToolbarBrush` (`#F4ECDC`), акцент `AmberAccentBrush` (`#B4690E`).
- `MainWindow.xaml(.cs)` — сайдбар проектов, единая верхняя панель с названием проекта, переключателем `Запуск` / `Конструктор`, кнопкой `README`, статус-баром и выдвижным журналом (`Логи`).
- `Views/PipelineRunnerView.xaml(.cs)` — панель `РЕЖИМ ЗАПУСКА` (`Сбросить`, `▶ Запустить`), карточки шагов, выполнение пайплайна, предварительная проверка Vision-модели перед отправкой картинки, доработка результата.
- `Views/PipelineBuilderView.xaml(.cs)` — панель `РЕЖИМ КОНСТРУКТОРА`, редактирование описания и шагов, живое предупреждение о не-Vision модели на шагах с файлами/изображениями, автоподбор Vision-модели при добавлении AI-шага после файлового.
- `Views/SettingsWindow.xaml(.cs)` — API-ключ, пресеты моделей, подгрузка каталога OpenRouter (`/api/v1/models`), тестирование моделей.
- `Services/OpenRouterService.cs` — общий `HttpClient` с `GZip/Deflate`, ретраи `429/502/503/504`, `max_tokens = 16` в `TestModelAsync`, человекочитаемые сообщения об ошибках (включая `404: No endpoints found that support image input`).
- `Services/StorageService.cs` — загрузка/сохранение `%APPDATA%\PipelineGen2026\`, миграция из старых папок, генерация `README.md` проекта, шаблон дефолтного проекта.
- `Services/JsonHelper.cs` — парсинг и сериализация JSON через `JavaScriptSerializer`.
- `Services/FileHelper.cs` — диалог выбора файла, Drag-and-Drop, вставка картинки из буфера (`Ctrl+V`), декодирование Data URL.
- `Services/MarkdownFlowBuilder.cs` — рендер Markdown в `FlowDocument`.
- `Controls/CopyableContentControl.xaml(.cs)` — рендер Markdown и блоков `<copy>...</copy>` с кнопкой копирования.

## 5. Важные особенности UI и API
- **UI-стиль (предпочтения пользователя):**
  - Компактный, сдержанный нативный Windows-интерфейс (`Segoe UI`, радиусы `2–4px`, минимум отступов и цветов).
  - Без лишних «ИИ-подсказок», длинных баннеров-объяснений и эмодзи.
  - Без дублирования кнопок управления: переключение `Запуск` / `Конструктор` — только в верхней панели `MainWindow`.
  - Четкое визуальное отличие режима `Конструктор` от `Запуск` (цвет вкладки, верхняя акцентная полоса 2px, плашка режима, теплый фон рабочей области).
- **OpenRouter и Vision-модели:**
  - Текстовые модели (например, `deepseek/deepseek-v4.1-flash`, `minimax/minimax-m3.1-flash`) не принимают `image_url` и возвращают `404: No endpoints found that support image input`.
  - Для шагов с изображениями использовать модели с `IsVision = true` (например, `qwen/qwen3-vl-8b-instruct`, `openai/gpt-4o-mini`, `openai/gpt-4.1`, `anthropic/claude-sonnet-5.5`).
  - Модели Google (`google/gemini-2.5-*`) могут возвращать `HTTP 403` в зависимости от региона ключа, поэтому по умолчанию для Vision OCR используется `qwen/qwen3-vl-8b-instruct`.

## 6. Формат данных (`%APPDATA%\PipelineGen2026\`)
- `config.json`: `{ apiKey, defaultModel, models: [{ id, name, provider, isVision, status, statusMsg, testedAt }] }`
- `projects/<id>/project.json`: `{ id, name, description, createdAt, updatedAt, steps: [{ id, type, title, instruction, content, fileName, fileData, fileType, model, prompt, result, requireUser, userPrompt }] }`
  - `type`: `"text"` | `"file"` | `"ai"`
  - Макросы подстановки в `prompt`: `{{step_1}}`, `{{step_2}}`, ... (1-based индекс шага).
