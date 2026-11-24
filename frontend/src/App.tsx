import React, { useState, useEffect, useRef } from 'react';
import { Project, AppConfig, LogEntry } from './types';
import { PipelineRunner } from './components/PipelineRunner';
import { PipelineBuilder } from './components/PipelineBuilder';
import { SettingsModal } from './components/SettingsModal';
import { ReadmeModal } from './components/ReadmeModal';
import { LogDrawer } from './components/LogDrawer';
import { 
  IconPlus, 
  IconGear, 
  IconTrash, 
  IconBook, 
  IconRocket, 
  IconWrench,
  IconCheck
} from './components/Icons';
import { 
  LoadProjects, 
  SaveProject, 
  DeleteProject, 
  GetConfig, 
  SaveConfig,
  GetReadme 
} from '../wailsjs/go/main/App';

// Default initial project for vision OCR + grammar/summary pipeline
const createDefaultProject = (): Project => ({
  id: `proj_${Date.now()}`,
  name: 'Распознавание и саммари документов',
  description: 'Многошаговая обработка: распознавание текста с картинки (Vision OCR), проверка грамматики и итоговое саммари',
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
  steps: [
    {
      id: 'step_1',
      type: 'file',
      title: 'Входное изображение или документ',
      content: '',
      fileName: '',
      fileData: '',
      fileType: 'image/png',
    },
    {
      id: 'step_2',
      type: 'ai',
      title: 'Vision OCR (Распознавание текста)',
      model: 'google/gemini-2.5-flash',
      prompt: 'Внимательно распознай весь печатный и рукописный текст с этого изображения. Сохрани оригинальную структуру абзацев и списков.',
      content: '',
      requireUser: true,
      userPrompt: 'Проверьте распознанный текст перед отправкой на следующий этап исправления и обобщения',
    },
    {
      id: 'step_3',
      type: 'ai',
      title: 'Исправление грамматики и резюме',
      model: 'anthropic/claude-3.7-sonnet',
      prompt: `На основе текста из шага 2:\n{{step_2}}\n\n1. Исправь все орфографические, пунктуационные и грамматические ошибки.\n2. Сделай структурированное резюме (Executive Summary) с ключевыми выводами и маркированным списком.`,
      content: '',
    },
  ],
});

export default function App() {
  // App state
  const [config, setConfig] = useState<AppConfig>({
    apiKey: '',
    defaultModel: 'google/gemini-2.5-flash',
    models: [],
  });

  const [projects, setProjects] = useState<Project[]>([]);
  const [activeProjectId, setActiveProjectId] = useState<string>('');
  const [activeTab, setActiveTab] = useState<'runner' | 'builder'>('runner');
  
  // Autosave status
  const [saveStatus, setSaveStatus] = useState<'saved' | 'saving'>('saved');
  const [lastSavedTime, setLastSavedTime] = useState<string>('');

  // Modals and Drawers
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isReadmeOpen, setIsReadmeOpen] = useState(false);
  const [readmeContent, setReadmeContent] = useState('');
  const [isLogOpen, setIsLogOpen] = useState(false);
  const [logs, setLogs] = useState<LogEntry[]>([]);

  // Toast notifications
  const [toast, setToToast] = useState<{ message: string; type: 'ok' | 'error' } | null>(null);

  const activeProject = projects.find((p) => p.id === activeProjectId) || projects[0];

  // Helper for adding logs
  const addLog = (type: 'info' | 'ok' | 'error', message: string) => {
    const entry: LogEntry = {
      id: `log_${Date.now()}_${Math.random().toString(36).substr(2, 4)}`,
      time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
      type,
      message,
    };
    setLogs((prev) => [entry, ...prev.slice(0, 99)]);
  };

  const showToast = (message: string, type: 'ok' | 'error' = 'ok') => {
    setToToast({ message, type });
    setTimeout(() => setToToast(null), 3000);
  };

  // 1. Initial Load: Config & Projects
  useEffect(() => {
    async function init() {
      try {
        // Load config with local redundancy
        let localCfg: AppConfig | null = null;
        try {
          const localStr = localStorage.getItem('pipelinegen_config');
          if (localStr) localCfg = JSON.parse(localStr);
        } catch {}

        const cfgStr = await GetConfig();
        if (cfgStr) {
          const parsedCfg: AppConfig = JSON.parse(cfgStr);
          // If backend didn't have apiKey, but localStorage did, merge and save
          if (!parsedCfg.apiKey && localCfg?.apiKey) {
            parsedCfg.apiKey = localCfg.apiKey;
            if (localCfg.models && localCfg.models.length > 0 && (!parsedCfg.models || parsedCfg.models.length === 0)) {
              parsedCfg.models = localCfg.models;
            }
            await SaveConfig(JSON.stringify(parsedCfg));
          }
          setConfig(parsedCfg);
          try {
            localStorage.setItem('pipelinegen_config', JSON.stringify(parsedCfg));
          } catch {}
        } else if (localCfg) {
          setConfig(localCfg);
          await SaveConfig(JSON.stringify(localCfg));
        }

        // Load projects
        const projsRaw = await LoadProjects();
        if (projsRaw && projsRaw.length > 0) {
          const loaded: Project[] = projsRaw.map((str) => JSON.parse(str));
          setProjects(loaded);
          setActiveProjectId(loaded[0].id);
          addLog('ok', `Загружено проектов с диска: ${loaded.length}`);
        } else {
          // Create default demo project
          const defaultProj = createDefaultProject();
          setProjects([defaultProj]);
          setActiveProjectId(defaultProj.id);
          await SaveProject(JSON.stringify(defaultProj));
          addLog('info', 'Создан начальный шаблон проекта');
        }
      } catch (err: any) {
        addLog('error', `Ошибка инициализации: ${err?.message || String(err)}`);
      }
    }
    init();
  }, []);

  // 2. Autosave with debounce
  const isFirstRender = useRef(true);
  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }
    if (!activeProject) return;

    setSaveStatus('saving');
    const timer = setTimeout(async () => {
      try {
        const toSave = {
          ...activeProject,
          updatedAt: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        };
        await SaveProject(JSON.stringify(toSave));
        setSaveStatus('saved');
        setLastSavedTime(toSave.updatedAt);
      } catch (err: any) {
        setSaveStatus('saved');
        addLog('error', `Автосохранение не удалось: ${err?.message || String(err)}`);
      }
    }, 1200);

    return () => clearTimeout(timer);
  }, [activeProject]);

  // Project management
  const handleCreateNewProject = async () => {
    const newProj: Project = {
      id: `proj_${Date.now()}`,
      name: `Новый пайплайн ${projects.length + 1}`,
      description: 'Описание пайплайна',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      steps: [
        {
          id: `step_${Date.now()}`,
          type: 'text',
          title: 'Входные данные 1',
          content: '',
        },
        {
          id: `step_${Date.now() + 1}`,
          type: 'ai',
          title: 'AI Обработка 2',
          model: config.models?.[0]?.id || config.defaultModel,
          prompt: 'Обработай текст из предыдущего шага: {{step_1}}',
          content: '',
        },
      ],
    };

    try {
      await SaveProject(JSON.stringify(newProj));
      setProjects((prev) => [newProj, ...prev]);
      setActiveProjectId(newProj.id);
      setActiveTab('builder');
      addLog('ok', `Создан проект «${newProj.name}»`);
      showToast('Новый проект создан');
    } catch (err: any) {
      addLog('error', `Не удалось создать проект: ${err?.message || String(err)}`);
    }
  };

  const handleDeleteProject = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (projects.length <= 1) {
      showToast('Нельзя удалить единственный проект', 'error');
      return;
    }

    try {
      await DeleteProject(id);
      const remaining = projects.filter((p) => p.id !== id);
      setProjects(remaining);
      if (activeProjectId === id) {
        setActiveProjectId(remaining[0].id);
      }
      addLog('ok', 'Проект удален');
      showToast('Проект удален');
    } catch (err: any) {
      addLog('error', `Ошибка удаления проекта: ${err?.message || String(err)}`);
    }
  };

  const handleUpdateActiveProject = (updater: (prev: Project) => Project) => {
    setProjects((prevList) =>
      prevList.map((p) => (p.id === activeProjectId ? updater(p) : p))
    );
  };

  const handleOpenReadme = async () => {
    if (!activeProject) return;
    try {
      const md = await GetReadme(activeProject.id);
      setReadmeContent(md);
      setIsReadmeOpen(true);
    } catch (err: any) {
      addLog('error', `Не удалось загрузить README: ${err?.message || String(err)}`);
    }
  };

  return (
    <div className="flex h-screen w-screen overflow-hidden bg-[#F8F7F5] text-[#161C14]">
      
      {/* ================= SIDEBAR ================= */}
      <aside className="w-[280px] bg-white border-r border-[#ACADA9] flex flex-col shrink-0">
        
        {/* Brand */}
        <div className="flex items-center gap-3 p-4 border-b border-[#ACADA9]/40">
          <div className="relative w-12 h-7 shrink-0">
            <span className="absolute w-3.5 h-3.5 rounded-md left-0 top-0 bg-[#E76C5C]" />
            <span className="absolute w-3.5 h-3.5 rounded-md left-4 top-1.5 bg-[#E0A458]" />
            <span className="absolute w-3.5 h-3.5 rounded-md left-8 top-3 bg-[#5CA354]" />
          </div>
          <div>
            <div className="font-bold text-[14.5px] text-[#161C14] leading-tight">
              AI Pipeline Studio
            </div>
            <div className="text-[10.5px] text-[#60635F]">
              Wails v2 · OpenRouter
            </div>
          </div>
        </div>

        {/* Action: New Project */}
        <div className="p-3">
          <button
            onClick={handleCreateNewProject}
            className="w-full py-2 px-3 bg-white border border-[#ACADA9] hover:bg-[#FAF9F8] rounded-xl font-bold text-[12.5px] text-[#161C14] flex items-center justify-center gap-2 transition shadow-2xs hover:border-[#8e908c]"
          >
            <IconPlus className="w-3.5 h-3.5 text-[#E76C5C]" />
            <span>Новый проект</span>
          </button>
        </div>

        {/* Projects List */}
        <div className="flex-1 overflow-y-auto px-3 py-1 space-y-1">
          <div className="text-[10.5px] font-bold uppercase tracking-wider text-[#60635F] px-2 py-1">
            Сохраненные пайплайны
          </div>

          {projects.map((proj) => {
            const isActive = proj.id === activeProjectId;
            return (
              <div
                key={proj.id}
                onClick={() => setActiveProjectId(proj.id)}
                className={`group flex items-center justify-between p-2.5 rounded-xl cursor-pointer border transition text-left ${
                  isActive
                    ? 'bg-[#F4DAD6]/40 border-[#E76C5C] shadow-2xs font-semibold'
                    : 'bg-transparent border-transparent hover:bg-[#FAF9F8] text-[#161C14]'
                }`}
              >
                <div className="min-w-0 flex-1 pr-2">
                  <div className="text-[13px] truncate">
                    {proj.name}
                  </div>
                  <div className="text-[10.5px] text-[#60635F] flex items-center gap-1.5 mt-0.5">
                    <span>{proj.steps.length} шагов</span>
                    <span>·</span>
                    <span>{proj.updatedAt || 'сейчас'}</span>
                  </div>
                </div>

                <button
                  onClick={(e) => handleDeleteProject(proj.id, e)}
                  title="Удалить проект"
                  className="opacity-0 group-hover:opacity-100 p-1 text-[#C65D4E] hover:bg-[#F6E0DB] rounded-lg transition shrink-0"
                >
                  <IconTrash className="w-3.5 h-3.5" />
                </button>
              </div>
            );
          })}
        </div>

        {/* Sidebar Footer */}
        <div className="p-3 border-t border-[#ACADA9]/60 bg-[#FAF9F8] space-y-2">
          <button
            onClick={() => setIsSettingsOpen(true)}
            className="w-full py-2 px-3 rounded-xl border border-[#ACADA9]/70 bg-white hover:bg-[#FAF9F8] text-[12px] font-semibold text-[#161C14] flex items-center gap-2 transition"
          >
            <IconGear className="w-3.5 h-3.5 text-[#60635F]" />
            <span>Настройки & Модели</span>
            {!config.apiKey && (
              <span className="ml-auto w-2 h-2 rounded-full bg-[#E0A458]" title="Ключ не указан" />
            )}
          </button>
          
          <div className="text-center text-[10px] text-[#98968f]">
            Wails v2 · React + Tailwind · pnpm
          </div>
        </div>

      </aside>

      {/* ================= MAIN COLUMN ================= */}
      <main className="flex-1 flex flex-col min-w-0 overflow-hidden">
        
        {/* Topbar */}
        <header className="px-6 py-3 bg-white border-b border-[#ACADA9] flex items-center gap-4 shrink-0 shadow-2xs">
          
          {/* Editable Project Title */}
          <input
            type="text"
            value={activeProject?.name || ''}
            onChange={(e) =>
              handleUpdateActiveProject((prev) => ({ ...prev, name: e.target.value }))
            }
            className="font-bold text-[16px] text-[#161C14] bg-transparent border-b border-transparent hover:border-[#ACADA9] focus:border-[#E76C5C] focus:outline-none px-1.5 py-0.5 transition max-w-sm truncate"
          />

          {/* Mode Switcher Tabs */}
          <div className="flex items-center p-1 bg-[#FAF9F8] border border-[#ACADA9]/70 rounded-xl">
            <button
              onClick={() => setActiveTab('runner')}
              className={`px-3.5 py-1.5 rounded-lg text-[12.5px] font-bold flex items-center gap-1.5 transition ${
                activeTab === 'runner'
                  ? 'bg-white text-[#E76C5C] shadow-xs'
                  : 'text-[#60635F] hover:text-[#161C14]'
              }`}
            >
              <IconRocket className="w-3.5 h-3.5" />
              <span>Запуск (Раннер)</span>
            </button>

            <button
              onClick={() => setActiveTab('builder')}
              className={`px-3.5 py-1.5 rounded-lg text-[12.5px] font-bold flex items-center gap-1.5 transition ${
                activeTab === 'builder'
                  ? 'bg-white text-[#161C14] shadow-xs'
                  : 'text-[#60635F] hover:text-[#161C14]'
              }`}
            >
              <IconWrench className="w-3.5 h-3.5" />
              <span>Конструктор шагов</span>
            </button>
          </div>

          <div className="flex-1" />

          {/* Autosave Status Indicator */}
          <div className="flex items-center gap-1.5 text-[11.5px] text-[#60635F]">
            <span
              className={`w-2 h-2 rounded-full ${
                saveStatus === 'saving'
                  ? 'bg-[#E0A458] animate-pulse-slow'
                  : 'bg-[#5CA354]'
              }`}
            />
            <span>
              {saveStatus === 'saving'
                ? 'Сохранение...'
                : lastSavedTime
                ? `Сохранено ${lastSavedTime}`
                : 'Автосохранение активно'}
            </span>
          </div>

          {/* Readme Button */}
          <button
            onClick={handleOpenReadme}
            className="px-3 py-1.5 border border-[#ACADA9] rounded-xl text-[12px] font-semibold text-[#161C14] hover:bg-[#FAF9F8] flex items-center gap-1.5 transition"
          >
            <IconBook className="w-3.5 h-3.5" />
            <span>README.md</span>
          </button>

        </header>

        {/* Content View: Runner or Builder */}
        <div className="flex-1 flex flex-col min-h-0 overflow-hidden">
          {activeProject && (
            activeTab === 'runner' ? (
              <PipelineRunner
                project={activeProject}
                config={config}
                addLog={addLog}
                onGoToBuilder={() => setActiveTab('builder')}
                onUpdateProject={handleUpdateActiveProject}
              />
            ) : (
              <PipelineBuilder
                project={activeProject}
                onUpdateProject={handleUpdateActiveProject}
                config={config}
                addLog={addLog}
              />
            )
          )}
        </div>

        {/* Event Logs Drawer */}
        <LogDrawer
          isOpen={isLogOpen}
          onClose={() => setIsLogOpen(false)}
          logs={logs}
          onClear={() => setLogs([])}
        />

        {/* Statusbar */}
        <footer className="h-8 px-6 bg-white border-t border-[#ACADA9] flex items-center gap-3 text-[11.5px] text-[#60635F] shrink-0">
          <div className="flex items-center gap-1.5">
            <span
              className={`w-2 h-2 rounded-full ${
                config.apiKey ? 'bg-[#5CA354]' : 'bg-[#E0A458]'
              }`}
            />
            <button
              onClick={() => setIsSettingsOpen(true)}
              className="hover:underline font-medium text-[#161C14]"
            >
              {config.apiKey ? 'OpenRouter: Ключ настроен' : 'OpenRouter: Ключ не настроен'}
            </button>
          </div>

          <span>·</span>
          <span>Моделей в реестре: {config.models?.length || 0}</span>
          <span>·</span>
          <span>Backend: Go / Wails v2</span>

          <div className="flex-1" />

          <button
            onClick={() => setIsLogOpen(!isLogOpen)}
            className="px-2 py-0.5 border border-[#ACADA9]/60 rounded-md hover:bg-[#FAF9F8] text-[#161C14] font-medium transition flex items-center gap-1"
          >
            <span>Журнал ({logs.length})</span>
          </button>
        </footer>

      </main>

      {/* ================= MODALS & TOASTS ================= */}
      <SettingsModal
        isOpen={isSettingsOpen}
        onClose={() => setIsSettingsOpen(false)}
        config={config}
        onSaveConfig={(newCfg) => {
          setConfig(newCfg);
          try {
            localStorage.setItem('pipelinegen_config', JSON.stringify(newCfg));
          } catch {}
        }}
        addLog={addLog}
      />

      <ReadmeModal
        isOpen={isReadmeOpen}
        onClose={() => setIsReadmeOpen(false)}
        projectName={activeProject?.name || ''}
        readmeText={readmeContent}
      />

      {toast && (
        <div className="fixed bottom-12 right-6 z-50 bg-[#161C14] text-white px-4 py-2.5 rounded-xl shadow-xl text-[12.5px] font-semibold flex items-center gap-2 animate-slide">
          <IconCheck className="w-3.5 h-3.5 text-[#5CA354]" />
          <span>{toast.message}</span>
        </div>
      )}

    </div>
  );
}
