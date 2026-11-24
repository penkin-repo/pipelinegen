package main

import (
	"bytes"
	"context"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"time"

	"github.com/wailsapp/wails/v2/pkg/runtime"
)

// App struct
type App struct {
	ctx        context.Context
	projectDir string
	configFile string
}

// ModelConfig represents a verified model in registry
type ModelConfig struct {
	ID        string    `json:"id"`
	Name      string    `json:"name"`
	Provider  string    `json:"provider"`
	IsVision  bool      `json:"isVision"`
	Status    string    `json:"status"` // "verified", "error", "untested"
	StatusMsg string    `json:"statusMsg,omitempty"`
	TestedAt  time.Time `json:"testedAt"`
}

// AppConfig represents saved settings
type AppConfig struct {
	APIKey       string        `json:"apiKey"`
	DefaultModel string        `json:"defaultModel"`
	Models       []ModelConfig `json:"models"`
}

// StepDefinition represents a single step in a pipeline
type StepDefinition struct {
	ID          string `json:"id"`
	Type        string `json:"type"` // "text", "file", "ai"
	Title       string `json:"title"`
	Content     string `json:"content"`     // for text step or file info
	FileName    string `json:"fileName,omitempty"`
	FileData    string `json:"fileData,omitempty"` // Base64 or raw content
	FileType    string `json:"fileType,omitempty"` // "image/png", "text/plain", etc.
	Model       string `json:"model,omitempty"`
	Prompt      string `json:"prompt,omitempty"`
	Result      string `json:"result,omitempty"`
	Status      string `json:"status,omitempty"` // "idle", "running", "done", "error"
	RequireUser bool   `json:"requireUser,omitempty"` // Human-in-the-loop input before proceeding
	UserPrompt  string `json:"userPrompt,omitempty"`  // Instructions for user prompt in runner
	Instruction string `json:"instruction,omitempty"` // Custom instruction for step
}

// Project represents the full pipeline project
type Project struct {
	ID          string           `json:"id"`
	Name        string           `json:"name"`
	Description string           `json:"description"`
	UpdatedAt   string           `json:"updatedAt"`
	CreatedAt   string           `json:"createdAt"`
	Steps       []StepDefinition `json:"steps"`
}

// NewApp creates a new App application struct
func NewApp() *App {
	// Determine permanent config and data directory in OS User Config Dir (%APPDATA%\PipelineGen2026 on Windows)
	var baseDir string
	userCfg, err := os.UserConfigDir()
	if err == nil && userCfg != "" {
		baseDir = filepath.Join(userCfg, "PipelineGen2026")
	} else {
		if exePath, err := os.Executable(); err == nil {
			baseDir = filepath.Join(filepath.Dir(exePath), "data")
		} else {
			cwd, _ := os.Getwd()
			baseDir = filepath.Join(cwd, "data")
		}
	}
	_ = os.MkdirAll(baseDir, 0755)

	projDir := filepath.Join(baseDir, "projects")
	_ = os.MkdirAll(projDir, 0755)
	cfgFile := filepath.Join(baseDir, "config.json")

	// Candidate locations to check for existing configs (for migration)
	var candidateFiles []string
	if exePath, err := os.Executable(); err == nil {
		candidateFiles = append(candidateFiles, filepath.Join(filepath.Dir(exePath), "config.json"))
	}
	if cwd, err := os.Getwd(); err == nil {
		candidateFiles = append(candidateFiles, filepath.Join(cwd, "config.json"))
		candidateFiles = append(candidateFiles, filepath.Join(cwd, "build", "bin", "config.json"))
	}

	// If destination config file doesn't exist, try importing from candidate files
	if _, err := os.Stat(cfgFile); os.IsNotExist(err) {
		for _, cand := range candidateFiles {
			if data, err := os.ReadFile(cand); err == nil && len(data) > 0 {
				_ = os.WriteFile(cfgFile, data, 0644)
				break
			}
		}
	}

	// Also migrate any existing projects from candidate project folders
	for _, cand := range []string{"projects", filepath.Join("build", "bin", "projects")} {
		if entries, err := os.ReadDir(cand); err == nil {
			for _, entry := range entries {
				src := filepath.Join(cand, entry.Name())
				dst := filepath.Join(projDir, entry.Name())
				if _, err := os.Stat(dst); os.IsNotExist(err) {
					if entry.IsDir() {
						_ = os.MkdirAll(dst, 0755)
						if subEntries, err := os.ReadDir(src); err == nil {
							for _, sub := range subEntries {
								subData, err := os.ReadFile(filepath.Join(src, sub.Name()))
								if err == nil {
									_ = os.WriteFile(filepath.Join(dst, sub.Name()), subData, 0644)
								}
							}
						}
					}
				}
			}
		}
	}

	return &App{
		projectDir: projDir,
		configFile: cfgFile,
	}
}

// startup is called when the app starts.
func (a *App) startup(ctx context.Context) {
	a.ctx = ctx
}

// GetConfig reads app configuration
func (a *App) GetConfig() (string, error) {
	// If primary config exists, read it
	data, err := os.ReadFile(a.configFile)
	if err == nil && len(data) > 0 {
		var existingCfg AppConfig
		if err := json.Unmarshal(data, &existingCfg); err == nil {
			// If existing config has API key, return it
			if existingCfg.APIKey != "" {
				return string(data), nil
			}
		}
	}

	// Otherwise, check if any candidate location has an existing config with an API key
	var candidateFiles []string
	if exePath, err := os.Executable(); err == nil {
		candidateFiles = append(candidateFiles, filepath.Join(filepath.Dir(exePath), "config.json"))
	}
	if cwd, err := os.Getwd(); err == nil {
		candidateFiles = append(candidateFiles, filepath.Join(cwd, "config.json"))
		candidateFiles = append(candidateFiles, filepath.Join(cwd, "build", "bin", "config.json"))
	}

	for _, cand := range candidateFiles {
		if cand == a.configFile {
			continue
		}
		if candData, err := os.ReadFile(cand); err == nil && len(candData) > 0 {
			var candCfg AppConfig
			if err := json.Unmarshal(candData, &candCfg); err == nil && candCfg.APIKey != "" {
				// Copy to primary
				_ = os.WriteFile(a.configFile, candData, 0644)
				return string(candData), nil
			}
		}
	}

	// If still no file at all, create default
	if len(data) == 0 {
		defaultCfg := AppConfig{
			APIKey:       "",
			DefaultModel: "google/gemini-2.5-flash",
			Models: []ModelConfig{
				{
					ID:       "google/gemini-2.5-flash",
					Name:     "Gemini 2.5 Flash",
					Provider: "google",
					IsVision: true,
					Status:   "verified",
					TestedAt: time.Now(),
				},
				{
					ID:       "anthropic/claude-3.7-sonnet",
					Name:     "Claude 3.7 Sonnet",
					Provider: "anthropic",
					IsVision: true,
					Status:   "verified",
					TestedAt: time.Now(),
				},
				{
					ID:       "openai/gpt-4.1",
					Name:     "GPT-4.1",
					Provider: "openai",
					IsVision: true,
					Status:   "verified",
					TestedAt: time.Now(),
				},
			},
		}
		defaultData, _ := json.MarshalIndent(defaultCfg, "", "  ")
		_ = os.WriteFile(a.configFile, defaultData, 0644)
		return string(defaultData), nil
	}

	return string(data), nil
}

// SaveConfig saves app configuration
func (a *App) SaveConfig(configJSON string) error {
	var cfg AppConfig
	if err := json.Unmarshal([]byte(configJSON), &cfg); err != nil {
		return fmt.Errorf("invalid config JSON: %w", err)
	}

	data, err := json.MarshalIndent(cfg, "", "  ")
	if err != nil {
		return err
	}

	// 1. Save to primary AppData location
	if err := os.WriteFile(a.configFile, data, 0644); err != nil {
		return err
	}

	// 2. Also mirror to exe directory (portable backup)
	if exePath, err := os.Executable(); err == nil {
		exeCfg := filepath.Join(filepath.Dir(exePath), "config.json")
		if exeCfg != a.configFile {
			_ = os.WriteFile(exeCfg, data, 0644)
		}
	}

	// 3. Also mirror to cwd if different
	if cwd, err := os.Getwd(); err == nil {
		cwdCfg := filepath.Join(cwd, "config.json")
		if cwdCfg != a.configFile {
			_ = os.WriteFile(cwdCfg, data, 0644)
		}
	}

	return nil
}

// TestModel checks if an OpenRouter model is working with the given API key
func (a *App) TestModel(apiKey, modelID string) (string, error) {
	if apiKey == "" {
		cfgJSON, err := a.GetConfig()
		if err == nil {
			var cfg AppConfig
			_ = json.Unmarshal([]byte(cfgJSON), &cfg)
			apiKey = cfg.APIKey
		}
	}
	if apiKey == "" {
		return "", fmt.Errorf("OpenRouter API key is missing. Please set it in Settings.")
	}

	reqBody := map[string]interface{}{
		"model": modelID,
		"messages": []map[string]string{
			{"role": "user", "content": "ping"},
		},
		"max_tokens": 1,
	}

	jsonData, err := json.Marshal(reqBody)
	if err != nil {
		return "", err
	}

	req, err := http.NewRequestWithContext(context.Background(), "POST", "https://openrouter.ai/api/v1/chat/completions", bytes.NewBuffer(jsonData))
	if err != nil {
		return "", err
	}
	req.Header.Set("Authorization", "Bearer "+apiKey)
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("HTTP-Referer", "https://pipelinegen.local")
	req.Header.Set("X-Title", "AI Pipeline Studio")

	client := &http.Client{Timeout: 15 * time.Second}
	resp, err := client.Do(req)
	if err != nil {
		return "", fmt.Errorf("network error: %w", err)
	}
	defer resp.Body.Close()

	bodyBytes, _ := io.ReadAll(resp.Body)

	if resp.StatusCode != http.StatusOK {
		var errResp struct {
			Error struct {
				Message string `json:"message"`
				Code    int    `json:"code"`
			} `json:"error"`
		}
		if json.Unmarshal(bodyBytes, &errResp) == nil && errResp.Error.Message != "" {
			return "", fmt.Errorf("API error (%d): %s", resp.StatusCode, errResp.Error.Message)
		}
		return "", fmt.Errorf("API error status %d: %s", resp.StatusCode, string(bodyBytes))
	}

	return "ok", nil
}

// FetchOpenRouterModels retrieves available models list from OpenRouter API
func (a *App) FetchOpenRouterModels(apiKey string) (string, error) {
	req, err := http.NewRequestWithContext(context.Background(), "GET", "https://openrouter.ai/api/v1/models", nil)
	if err != nil {
		return "", err
	}
	if apiKey != "" {
		req.Header.Set("Authorization", "Bearer "+apiKey)
	}

	client := &http.Client{Timeout: 10 * time.Second}
	resp, err := client.Do(req)
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()

	bodyBytes, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", err
	}
	return string(bodyBytes), nil
}

// SaveProject saves project JSON and automatically generates README.md
func (a *App) SaveProject(projectDataJSON string) error {
	var proj Project
	if err := json.Unmarshal([]byte(projectDataJSON), &proj); err != nil {
		return fmt.Errorf("invalid project JSON: %w", err)
	}

	if proj.ID == "" {
		proj.ID = fmt.Sprintf("proj_%d", time.Now().UnixMilli())
	}
	if proj.UpdatedAt == "" {
		proj.UpdatedAt = time.Now().Format("2006-01-02 15:04:05")
	}

	projFolder := filepath.Join(a.projectDir, proj.ID)
	if err := os.MkdirAll(projFolder, 0755); err != nil {
		return fmt.Errorf("failed to create project directory: %w", err)
	}

	// 1. Save project.json
	projJSONPath := filepath.Join(projFolder, "project.json")
	formattedJSON, err := json.MarshalIndent(proj, "", "  ")
	if err != nil {
		return err
	}
	if err := os.WriteFile(projJSONPath, formattedJSON, 0644); err != nil {
		return fmt.Errorf("failed to write project.json: %w", err)
	}

	// 2. Generate native README.md
	readmeContent := a.generateReadmeMarkdown(&proj)
	readmePath := filepath.Join(projFolder, "README.md")
	if err := os.WriteFile(readmePath, []byte(readmeContent), 0644); err != nil {
		return fmt.Errorf("failed to write README.md: %w", err)
	}

	return nil
}

// generateReadmeMarkdown builds structured markdown documentation with AI context
func (a *App) generateReadmeMarkdown(p *Project) string {
	var sb strings.Builder

	sb.WriteString(fmt.Sprintf("# %s\n\n", p.Name))
	if p.Description != "" {
		sb.WriteString(fmt.Sprintf("> %s\n\n", p.Description))
	}

	sb.WriteString(fmt.Sprintf("- **ID проекта:** `%s`\n", p.ID))
	sb.WriteString(fmt.Sprintf("- **Дата изменения:** `%s`\n", p.UpdatedAt))
	sb.WriteString(fmt.Sprintf("- **Всего шагов:** %d\n\n", len(p.Steps)))

	sb.WriteString("## Цепочка пайплайна (Шаги)\n\n")

	for i, step := range p.Steps {
		stepNum := i + 1
		typeLabel := strings.ToUpper(step.Type)
		switch step.Type {
		case "text":
			typeLabel = "Текстовые данные"
		case "file":
			typeLabel = "Файл / Изображение"
		case "ai":
			typeLabel = "AI-обработка (OpenRouter)"
		}

		sb.WriteString(fmt.Sprintf("### Шаг %d: %s `[%s]`\n\n", stepNum, step.Title, typeLabel))

		if step.Type == "ai" {
			sb.WriteString(fmt.Sprintf("- **Модель:** `%s`\n", step.Model))
			if step.RequireUser {
				sb.WriteString("- **Интерактивный шаг:** Да (требует подтверждения/ввода пользователя)\n")
			}
			if step.Prompt != "" {
				sb.WriteString("\n**Шаблон промпта:**\n```text\n")
				sb.WriteString(step.Prompt)
				sb.WriteString("\n```\n")
			}
		} else if step.Type == "file" {
			sb.WriteString(fmt.Sprintf("- **Файл:** `%s` (%s)\n", step.FileName, step.FileType))
			if step.Content != "" {
				sb.WriteString("\n**Предпросмотр содержания:**\n```text\n")
				preview := step.Content
				if len(preview) > 300 {
					preview = preview[:300] + "... [сокращено]"
				}
				sb.WriteString(preview)
				sb.WriteString("\n```\n")
			}
		} else if step.Type == "text" {
			if step.Content != "" {
				sb.WriteString("**Входной текст:**\n```text\n")
				sb.WriteString(step.Content)
				sb.WriteString("\n```\n")
			}
		}

		if step.Result != "" {
			sb.WriteString("\n**Последний результат выполнения:**\n```markdown\n")
			sb.WriteString(step.Result)
			sb.WriteString("\n```\n")
		}

		sb.WriteString("\n---\n\n")
	}

	// Section "AI Context"
	sb.WriteString("## Контекст для ИИ\n\n")
	sb.WriteString("Этот пайплайн спроектирован для автоматизированной многошаговой обработки данных через OpenRouter API.\n")
	sb.WriteString("Ниже приведено логическое описание последовательности шагов для AI-ассистентов:\n\n")

	for i, step := range p.Steps {
		stepNum := i + 1
		macro := fmt.Sprintf("{{step_%d}}", stepNum)
		switch step.Type {
		case "file":
			sb.WriteString(fmt.Sprintf("- **%s (Шаг %d):** Источник входных данных. Принимает документ или изображение (Vision Base64), передает содержимое в последующие шаги.\n", macro, stepNum))
		case "text":
			sb.WriteString(fmt.Sprintf("- **%s (Шаг %d):** Текстовый блок входных параметров или инструкций.\n", macro, stepNum))
		case "ai":
			sb.WriteString(fmt.Sprintf("- **%s (Шаг %d):** AI генерация моделью `%s`. Использует переменные контекста и выдает структурированный результат.\n", macro, stepNum, step.Model))
		}
	}

	sb.WriteString("\n*Сгенерировано автоматически AI Pipeline Studio (Wails v2 + Go backend).*")

	return sb.String()
}

// LoadProjects reads all saved projects from disk
func (a *App) LoadProjects() ([]string, error) {
	entries, err := os.ReadDir(a.projectDir)
	if err != nil {
		return []string{}, err
	}

	var projects []string
	for _, entry := range entries {
		if entry.IsDir() {
			projFile := filepath.Join(a.projectDir, entry.Name(), "project.json")
			if data, err := os.ReadFile(projFile); err == nil {
				projects = append(projects, string(data))
			}
		}
	}
	return projects, nil
}

// DeleteProject removes a project folder from disk
func (a *App) DeleteProject(projectID string) error {
	if projectID == "" {
		return fmt.Errorf("empty project ID")
	}
	folder := filepath.Join(a.projectDir, projectID)
	return os.RemoveAll(folder)
}

// GetReadme retrieves generated README.md for a project
func (a *App) GetReadme(projectID string) (string, error) {
	readmePath := filepath.Join(a.projectDir, projectID, "README.md")
	data, err := os.ReadFile(readmePath)
	if err != nil {
		return "", err
	}
	return string(data), nil
}

// OpenFileDialog prompts user to choose a local file
func (a *App) OpenFileDialog() (string, error) {
	selection, err := runtime.OpenFileDialog(a.ctx, runtime.OpenDialogOptions{
		Title: "Выберите файл для пайплайна",
		Filters: []runtime.FileFilter{
			{DisplayName: "Все поддерживаемые файлы", Pattern: "*.png;*.jpg;*.jpeg;*.webp;*.txt;*.md;*.csv;*.json"},
			{DisplayName: "Изображения (Vision)", Pattern: "*.png;*.jpg;*.jpeg;*.webp"},
			{DisplayName: "Текстовые файлы", Pattern: "*.txt;*.md;*.csv;*.json"},
		},
	})
	if err != nil {
		return "", err
	}
	if selection == "" {
		return "", nil
	}

	fileInfo, err := os.Stat(selection)
	if err != nil {
		return "", err
	}

	bytesData, err := os.ReadFile(selection)
	if err != nil {
		return "", err
	}

	ext := strings.ToLower(filepath.Ext(selection))
	isImage := ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp"

	var mimeType string
	switch ext {
	case ".png":
		mimeType = "image/png"
	case ".jpg", ".jpeg":
		mimeType = "image/jpeg"
	case ".webp":
		mimeType = "image/webp"
	case ".json":
		mimeType = "application/json"
	case ".csv":
		mimeType = "text/csv"
	default:
		mimeType = "text/plain"
	}

	resultMap := map[string]interface{}{
		"fileName": fileInfo.Name(),
		"fileSize": fileInfo.Size(),
		"fileType": mimeType,
		"isImage":  isImage,
	}

	if isImage {
		b64 := base64.StdEncoding.EncodeToString(bytesData)
		resultMap["fileData"] = fmt.Sprintf("data:%s;base64,%s", mimeType, b64)
		resultMap["content"] = fmt.Sprintf("[Изображение: %s (%d KB)]", fileInfo.Name(), fileInfo.Size()/1024)
	} else {
		textContent := string(bytesData)
		resultMap["content"] = textContent
		resultMap["fileData"] = textContent
	}

	resJSON, _ := json.Marshal(resultMap)
	return string(resJSON), nil
}

// OpenRouterChatMessage represents a message in completion request
type OpenRouterChatMessage struct {
	Role    string      `json:"role"`
	Content interface{} `json:"content"` // string or []map[string]interface{} for vision
}

// ExecuteStepRequest payload
type ExecuteStepRequest struct {
	APIKey   string                  `json:"apiKey"`
	Model    string                  `json:"model"`
	Messages []OpenRouterChatMessage `json:"messages"`
}

// ExecuteStep sends a chat completion request to OpenRouter (supports Vision and Text)
func (a *App) ExecuteStep(reqJSON string) (string, error) {
	var execReq ExecuteStepRequest
	if err := json.Unmarshal([]byte(reqJSON), &execReq); err != nil {
		return "", fmt.Errorf("invalid request JSON: %w", err)
	}

	apiKey := execReq.APIKey
	if apiKey == "" {
		cfgJSON, _ := a.GetConfig()
		var cfg AppConfig
		_ = json.Unmarshal([]byte(cfgJSON), &cfg)
		apiKey = cfg.APIKey
	}
	if apiKey == "" {
		return "", fmt.Errorf("API-ключ OpenRouter не указан. Перейдите в Настройки и укажите ключ.")
	}

	openRouterPayload := map[string]interface{}{
		"model":    execReq.Model,
		"messages": execReq.Messages,
	}

	jsonData, err := json.Marshal(openRouterPayload)
	if err != nil {
		return "", err
	}

	req, err := http.NewRequestWithContext(context.Background(), "POST", "https://openrouter.ai/api/v1/chat/completions", bytes.NewBuffer(jsonData))
	if err != nil {
		return "", err
	}

	req.Header.Set("Authorization", "Bearer "+apiKey)
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("HTTP-Referer", "https://pipelinegen.local")
	req.Header.Set("X-Title", "AI Pipeline Studio")

	client := &http.Client{Timeout: 300 * time.Second}
	resp, err := client.Do(req)
	if err != nil {
		return "", fmt.Errorf("network error connecting to OpenRouter: %w", err)
	}
	defer resp.Body.Close()

	bodyBytes, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", fmt.Errorf("failed to read response: %w", err)
	}

	if resp.StatusCode != http.StatusOK {
		var errResp struct {
			Error struct {
				Message string `json:"message"`
				Code    int    `json:"code"`
			} `json:"error"`
		}
		if json.Unmarshal(bodyBytes, &errResp) == nil && errResp.Error.Message != "" {
			return "", fmt.Errorf("OpenRouter Error (%d): %s", resp.StatusCode, errResp.Error.Message)
		}
		return "", fmt.Errorf("OpenRouter Error %d: %s", resp.StatusCode, string(bodyBytes))
	}

	var openRouterResp struct {
		Choices []struct {
			Message struct {
				Role    string `json:"role"`
				Content string `json:"content"`
			} `json:"message"`
			FinishReason string `json:"finish_reason"`
		} `json:"choices"`
		Usage struct {
			PromptTokens     int `json:"prompt_tokens"`
			CompletionTokens int `json:"completion_tokens"`
			TotalTokens      int `json:"total_tokens"`
		} `json:"usage"`
	}

	if err := json.Unmarshal(bodyBytes, &openRouterResp); err != nil {
		return "", fmt.Errorf("failed to parse response JSON: %w", err)
	}

	if len(openRouterResp.Choices) == 0 {
		return "", fmt.Errorf("no response choices returned by model")
	}

	reply := openRouterResp.Choices[0].Message.Content
	return reply, nil
}
