using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PipelineGenWpf.Models;

namespace PipelineGenWpf.Services
{
    public class StorageService
    {
        private readonly string _baseDir;
        private readonly string _projectsDir;
        private readonly string _configFile;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public StorageService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                _baseDir = Path.Combine(appData, "PipelineGen2026");
            }
            else
            {
                _baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            }

            Directory.CreateDirectory(_baseDir);
            _projectsDir = Path.Combine(_baseDir, "projects");
            Directory.CreateDirectory(_projectsDir);
            _configFile = Path.Combine(_baseDir, "config.json");

            TryMigrateLegacyData();
        }

        public string BaseDir
        {
            get { return _baseDir; }
        }

        public string ConfigFile
        {
            get { return _configFile; }
        }

        private List<string> GetCandidateConfigFiles()
        {
            List<string> list = new List<string>();
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string cwd = Environment.CurrentDirectory;

            list.Add(Path.Combine(exeDir, "config.json"));
            list.Add(Path.Combine(cwd, "config.json"));
            list.Add(Path.Combine(cwd, "build", "bin", "config.json"));

            try
            {
                DirectoryInfo parent = Directory.GetParent(exeDir.TrimEnd('\\', '/'));
                for (int depth = 0; depth < 4 && parent != null; depth++)
                {
                    list.Add(Path.Combine(parent.FullName, "config.json"));
                    list.Add(Path.Combine(parent.FullName, "build", "bin", "config.json"));
                    parent = parent.Parent;
                }
            }
            catch
            {
            }

            return list;
        }

        private void TryMigrateLegacyData()
        {
            if (!File.Exists(_configFile))
            {
                foreach (string cand in GetCandidateConfigFiles())
                {
                    try
                    {
                        if (File.Exists(cand))
                        {
                            string text = File.ReadAllText(cand, Encoding.UTF8);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                File.WriteAllText(_configFile, text, Utf8NoBom);
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            List<string> candProjectDirs = new List<string>();
            string cwd = Environment.CurrentDirectory;
            candProjectDirs.Add(Path.Combine(cwd, "projects"));
            candProjectDirs.Add(Path.Combine(cwd, "build", "bin", "projects"));
            try
            {
                DirectoryInfo parent = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/'));
                for (int depth = 0; depth < 4 && parent != null; depth++)
                {
                    candProjectDirs.Add(Path.Combine(parent.FullName, "projects"));
                    candProjectDirs.Add(Path.Combine(parent.FullName, "build", "bin", "projects"));
                    parent = parent.Parent;
                }
            }
            catch
            {
            }

            foreach (string candDir in candProjectDirs)
            {
                if (!Directory.Exists(candDir)) continue;
                try
                {
                    foreach (string subDir in Directory.GetDirectories(candDir))
                    {
                        string name = Path.GetFileName(subDir);
                        string dstDir = Path.Combine(_projectsDir, name);
                        if (!Directory.Exists(dstDir))
                        {
                            Directory.CreateDirectory(dstDir);
                            foreach (string file in Directory.GetFiles(subDir))
                            {
                                string dstFile = Path.Combine(dstDir, Path.GetFileName(file));
                                File.Copy(file, dstFile, false);
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        public AppConfig LoadConfig()
        {
            if (File.Exists(_configFile))
            {
                try
                {
                    string raw = File.ReadAllText(_configFile, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        AppConfig cfg = JsonHelper.ParseConfig(raw);
                        if (!string.IsNullOrEmpty(cfg.ApiKey))
                        {
                            return cfg;
                        }
                    }
                }
                catch
                {
                }
            }

            foreach (string cand in GetCandidateConfigFiles())
            {
                if (string.Equals(cand, _configFile, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    if (File.Exists(cand))
                    {
                        string candRaw = File.ReadAllText(cand, Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(candRaw))
                        {
                            AppConfig candCfg = JsonHelper.ParseConfig(candRaw);
                            if (!string.IsNullOrEmpty(candCfg.ApiKey))
                            {
                                File.WriteAllText(_configFile, candRaw, Utf8NoBom);
                                return candCfg;
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            if (File.Exists(_configFile))
            {
                try
                {
                    string raw = File.ReadAllText(_configFile, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        return JsonHelper.ParseConfig(raw);
                    }
                }
                catch
                {
                }
            }

            AppConfig defaultCfg = CreateDefaultConfig();
            SaveConfig(defaultCfg);
            return defaultCfg;
        }

        private static AppConfig CreateDefaultConfig()
        {
            string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            return new AppConfig
            {
                ApiKey = "",
                DefaultModel = "deepseek/deepseek-v4.1-flash",
                Models = new List<ModelConfig>
                {
                    new ModelConfig
                    {
                        Id = "deepseek/deepseek-v4.1-flash",
                        Name = "DeepSeek V4.1 Flash",
                        Provider = "deepseek",
                        IsVision = false,
                        Status = "verified",
                        TestedAt = nowIso
                    },
                    new ModelConfig
                    {
                        Id = "qwen/qwen3-vl-8b-instruct",
                        Name = "Qwen3 VL 8B",
                        Provider = "qwen",
                        IsVision = true,
                        Status = "verified",
                        TestedAt = nowIso
                    },
                    new ModelConfig
                    {
                        Id = "anthropic/claude-sonnet-5.5",
                        Name = "Claude Sonnet 5.5",
                        Provider = "anthropic",
                        IsVision = true,
                        Status = "verified",
                        TestedAt = nowIso
                    },
                    new ModelConfig
                    {
                        Id = "openai/gpt-4.1",
                        Name = "GPT-4.1",
                        Provider = "openai",
                        IsVision = true,
                        Status = "verified",
                        TestedAt = nowIso
                    }
                }
            };
        }

        public void SaveConfig(AppConfig cfg)
        {
            string json = JsonHelper.SerializeConfig(cfg);
            File.WriteAllText(_configFile, json, Utf8NoBom);

            try
            {
                string exeCfg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (!string.Equals(exeCfg, _configFile, StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(exeCfg, json, Utf8NoBom);
                }
            }
            catch
            {
            }
        }

        public List<Project> LoadProjects()
        {
            List<Project> projects = new List<Project>();
            if (!Directory.Exists(_projectsDir))
            {
                return projects;
            }

            string[] dirs = Directory.GetDirectories(_projectsDir);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);

            foreach (string dir in dirs)
            {
                string projFile = Path.Combine(dir, "project.json");
                if (!File.Exists(projFile)) continue;
                try
                {
                    string raw = File.ReadAllText(projFile, Encoding.UTF8);
                    Project proj = JsonHelper.ParseProject(raw);
                    if (proj != null)
                    {
                        projects.Add(proj);
                    }
                }
                catch
                {
                }
            }

            return projects;
        }

        public void SaveProject(Project proj)
        {
            if (string.IsNullOrEmpty(proj.Id))
            {
                long ms = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
                proj.Id = "proj_" + ms;
            }
            if (string.IsNullOrEmpty(proj.UpdatedAt))
            {
                proj.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            string projFolder = Path.Combine(_projectsDir, proj.Id);
            Directory.CreateDirectory(projFolder);

            string projJsonPath = Path.Combine(projFolder, "project.json");
            File.WriteAllText(projJsonPath, JsonHelper.SerializeProject(proj), Utf8NoBom);

            string readmePath = Path.Combine(projFolder, "README.md");
            File.WriteAllText(readmePath, GenerateReadmeMarkdown(proj), Utf8NoBom);
        }

        public void DeleteProject(string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
            {
                throw new ArgumentException("Empty project ID");
            }
            string folder = Path.Combine(_projectsDir, projectId);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        public string GetReadme(Project proj)
        {
            if (proj == null || string.IsNullOrEmpty(proj.Id))
            {
                return "";
            }
            string readmePath = Path.Combine(_projectsDir, proj.Id, "README.md");
            if (File.Exists(readmePath))
            {
                return File.ReadAllText(readmePath, Encoding.UTF8);
            }
            return GenerateReadmeMarkdown(proj);
        }

        public string GenerateReadmeMarkdown(Project p)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("# {0}\n\n", p.Name);
            if (!string.IsNullOrEmpty(p.Description))
            {
                sb.AppendFormat("> {0}\n\n", p.Description);
            }

            int stepCount = p.Steps != null ? p.Steps.Count : 0;
            sb.AppendFormat("- **ID проекта:** `{0}`\n", p.Id);
            sb.AppendFormat("- **Дата изменения:** `{0}`\n", p.UpdatedAt);
            sb.AppendFormat("- **Всего шагов:** {0}\n\n", stepCount);

            sb.Append("## Цепочка пайплайна (Шаги)\n\n");

            if (p.Steps != null)
            {
                for (int i = 0; i < p.Steps.Count; i++)
                {
                    StepDefinition step = p.Steps[i];
                    int stepNum = i + 1;
                    string typeLabel = (step.Type ?? "").ToUpperInvariant();
                    if (step.Type == "text") typeLabel = "Текстовые данные";
                    else if (step.Type == "file") typeLabel = "Файл / Изображение";
                    else if (step.Type == "ai") typeLabel = "AI-обработка (OpenRouter)";

                    sb.AppendFormat("### Шаг {0}: {1} `[{2}]`\n\n", stepNum, step.Title, typeLabel);

                    if (step.Type == "ai")
                    {
                        sb.AppendFormat("- **Модель:** `{0}`\n", step.Model);
                        if (step.RequireUser)
                        {
                            sb.Append("- **Интерактивный шаг:** Да (требует подтверждения/ввода пользователя)\n");
                        }
                        if (!string.IsNullOrEmpty(step.Prompt))
                        {
                            sb.Append("\n**Шаблон промпта:**\n```text\n");
                            sb.Append(step.Prompt);
                            sb.Append("\n```\n");
                        }
                    }
                    else if (step.Type == "file")
                    {
                        sb.AppendFormat("- **Файл:** `{0}` ({1})\n", step.FileName, step.FileType);
                        if (!string.IsNullOrEmpty(step.Content))
                        {
                            sb.Append("\n**Предпросмотр содержания:**\n```text\n");
                            string preview = step.Content;
                            if (preview.Length > 300)
                            {
                                preview = preview.Substring(0, 300) + "... [сокращено]";
                            }
                            sb.Append(preview);
                            sb.Append("\n```\n");
                        }
                    }
                    else if (step.Type == "text")
                    {
                        if (!string.IsNullOrEmpty(step.Content))
                        {
                            sb.Append("**Входной текст:**\n```text\n");
                            sb.Append(step.Content);
                            sb.Append("\n```\n");
                        }
                    }

                    if (!string.IsNullOrEmpty(step.Result))
                    {
                        sb.Append("\n**Последний результат выполнения:**\n```markdown\n");
                        sb.Append(step.Result);
                        sb.Append("\n```\n");
                    }

                    sb.Append("\n---\n\n");
                }
            }

            sb.Append("## Контекст для ИИ\n\n");
            sb.Append("Этот пайплайн спроектирован для автоматизированной многошаговой обработки данных через OpenRouter API.\n");
            sb.Append("Ниже приведено логическое описание последовательности шагов для AI-ассистентов:\n\n");

            if (p.Steps != null)
            {
                for (int i = 0; i < p.Steps.Count; i++)
                {
                    StepDefinition step = p.Steps[i];
                    int stepNum = i + 1;
                    string macro = string.Format("{{{{step_{0}}}}}", stepNum);
                    if (step.Type == "file")
                    {
                        sb.AppendFormat("- **{0} (Шаг {1}):** Источник входных данных. Принимает документ или изображение (Vision Base64), передает содержимое в последующие шаги.\n", macro, stepNum);
                    }
                    else if (step.Type == "text")
                    {
                        sb.AppendFormat("- **{0} (Шаг {1}):** Текстовый блок входных параметров или инструкций.\n", macro, stepNum);
                    }
                    else if (step.Type == "ai")
                    {
                        sb.AppendFormat("- **{0} (Шаг {1}):** AI генерация моделью `{2}`. Использует переменные контекста и выдает структурированный результат.\n", macro, stepNum, step.Model);
                    }
                }
            }

            sb.Append("\n*Сгенерировано автоматически AI Pipeline Studio (.NET Framework 4.8 WPF).*");
            return sb.ToString();
        }

        public static Project CreateDefaultProject()
        {
            long ms = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            return new Project
            {
                Id = "proj_" + ms,
                Name = "Распознавание и саммари документов",
                Description = "Многошаговая обработка: распознавание текста с картинки (Vision OCR), проверка грамматики и итоговое саммари",
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                UpdatedAt = DateTime.Now.ToString("HH:mm"),
                Steps = new List<StepDefinition>
                {
                    new StepDefinition
                    {
                        Id = "step_1",
                        Type = "file",
                        Title = "Входное изображение или документ",
                        Content = "",
                        FileName = "",
                        FileData = "",
                        FileType = "image/png"
                    },
                    new StepDefinition
                    {
                        Id = "step_2",
                        Type = "ai",
                        Title = "Vision OCR (Распознавание текста)",
                        Model = "qwen/qwen3-vl-8b-instruct",
                        Prompt = "Внимательно распознай весь печатный и рукописный текст с этого изображения. Сохрани оригинальную структуру абзацев и списков.",
                        Content = "",
                        RequireUser = true,
                        UserPrompt = "Проверьте распознанный текст перед отправкой на следующий этап исправления и обобщения"
                    },
                    new StepDefinition
                    {
                        Id = "step_3",
                        Type = "ai",
                        Title = "Исправление грамматики и резюме",
                        Model = "deepseek/deepseek-v4.1-flash",
                        Prompt = "На основе текста из шага 2:\n{{step_2}}\n\n1. Исправь все орфографические, пунктуационные и грамматические ошибки.\n2. Сделай структурированное резюме (Executive Summary) с ключевыми выводами и маркированным списком.",
                        Content = ""
                    }
                }
            };
        }
    }
}
