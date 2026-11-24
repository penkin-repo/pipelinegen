import React, { useState, useEffect } from 'react';
import { AppConfig, ModelConfig } from '../types';
import { IconClose, IconGear, IconCheck, IconTrash, IconSpinner, IconSpark } from './Icons';
import { TestModel, SaveConfig, FetchOpenRouterModels } from '../../wailsjs/go/main/App';

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  config: AppConfig;
  onSaveConfig: (newConfig: AppConfig) => void;
  addLog: (type: 'info' | 'ok' | 'error', message: string) => void;
}

const PRESET_MODELS = [
  { id: 'minimax/minimax-m3.1-flash', name: 'MiniMax M3.1 Flash', provider: 'minimax', isVision: false },
  { id: 'google/gemini-2.5-flash', name: 'Gemini 2.5 Flash', provider: 'google', isVision: true },
  { id: 'google/gemini-2.5-pro', name: 'Gemini 2.5 Pro', provider: 'google', isVision: true },
  { id: 'anthropic/claude-3.7-sonnet', name: 'Claude 3.7 Sonnet', provider: 'anthropic', isVision: true },
  { id: 'anthropic/claude-3.5-haiku', name: 'Claude 3.5 Haiku', provider: 'anthropic', isVision: false },
  { id: 'openai/gpt-4o-mini', name: 'GPT-4o Mini', provider: 'openai', isVision: true },
  { id: 'openai/gpt-4.1', name: 'GPT-4.1', provider: 'openai', isVision: true },
  { id: 'deepseek/deepseek-r1', name: 'DeepSeek R1', provider: 'deepseek', isVision: false },
  { id: 'meta-llama/llama-3.3-70b-instruct', name: 'Llama 3.3 70B', provider: 'meta-llama', isVision: false },
  { id: 'qwen/qwen-2.5-vl-72b-instruct', name: 'Qwen 2.5 VL (Vision)', provider: 'qwen', isVision: true },
];

export const SettingsModal: React.FC<SettingsModalProps> = ({
  isOpen,
  onClose,
  config,
  onSaveConfig,
  addLog,
}) => {
  const [apiKey, setApiKey] = useState(config.apiKey || '');
  const [defaultModel, setDefaultModel] = useState(config.defaultModel || 'google/gemini-2.5-flash');
  const [models, setModels] = useState<ModelConfig[]>(config.models || []);

  // Sync state whenever modal opens or parent config changes
  useEffect(() => {
    if (isOpen) {
      setApiKey(config.apiKey || '');
      setDefaultModel(config.defaultModel || 'google/gemini-2.5-flash');
      setModels(config.models || []);
    }
  }, [isOpen, config]);

  // Helper to immediately persist config changes to disk and parent state
  const persistConfig = async (
    newApiKey: string = apiKey,
    newDefaultModel: string = defaultModel,
    newModels: ModelConfig[] = models
  ) => {
    const updatedCfg: AppConfig = {
      apiKey: newApiKey.trim(),
      defaultModel: newDefaultModel,
      models: newModels,
    };
    try {
      await SaveConfig(JSON.stringify(updatedCfg));
      onSaveConfig(updatedCfg);
      try {
        localStorage.setItem('pipelinegen_config', JSON.stringify(updatedCfg));
      } catch {}
    } catch (err: any) {
      console.error('Failed to auto-save config:', err);
    }
  };

  const [newModelId, setNewModelId] = useState('');
  const [newModelName, setNewModelName] = useState('');
  const [newIsVision, setNewIsVision] = useState(false);
  const [isTestingNew, setIsTestingNew] = useState(false);
  const [testingModelId, setTestingModelId] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Catalog browser
  const [isCatalogLoading, setIsCatalogLoading] = useState(false);
  const [catalogSearch, setCatalogSearch] = useState('');
  const [catalogList, setCatalogList] = useState<Array<{ id: string; name: string; isVision: boolean }>>([]);
  const [showCatalog, setShowCatalog] = useState(false);

  if (!isOpen) return null;

  const isBatchModel = newModelId.includes(':batch');
  const cleanBatchId = newModelId.replace(':batch', '');

  const handleSelectPreset = (presetId: string) => {
    const found = PRESET_MODELS.find((p) => p.id === presetId);
    if (found) {
      setNewModelId(found.id);
      setNewModelName(found.name);
      setNewIsVision(found.isVision);
      setErrorMsg(null);
    }
  };

  const handleFetchCatalog = async () => {
    setIsCatalogLoading(true);
    setErrorMsg(null);
    try {
      const jsonStr = await FetchOpenRouterModels(apiKey.trim());
      const data = JSON.parse(jsonStr);
      if (data && Array.isArray(data.data)) {
        const parsed = data.data
          .filter((m: any) => !m.id.includes(':batch')) // exclude batch models
          .map((m: any) => ({
            id: m.id,
            name: m.name || m.id,
            isVision: Boolean(m.architecture?.modality?.includes('image') || m.id.includes('vision') || m.id.includes('vl')),
          }));
        setCatalogList(parsed);
        setShowCatalog(true);
        addLog('ok', `Загружен каталог OpenRouter: ${parsed.length} доступных моделей`);
      }
    } catch (err: any) {
      setErrorMsg(`Не удалось загрузить каталог OpenRouter: ${err?.message || String(err)}`);
    } finally {
      setIsCatalogLoading(false);
    }
  };

  // Add model directly without API testing
  const handleForceAddModel = () => {
    if (!newModelId.trim()) {
      setErrorMsg('Укажите ID модели');
      return;
    }

    const modelId = newModelId.trim();
    const provider = modelId.split('/')[0] || 'custom';
    const name = newModelName.trim() || modelId.split('/')[1] || modelId;

    const newModel: ModelConfig = {
      id: modelId,
      name,
      provider,
      isVision: newIsVision,
      status: 'verified',
      testedAt: new Date().toISOString(),
    };

    const existingIdx = models.findIndex((m) => m.id === modelId);
    let updated: ModelConfig[];
    if (existingIdx >= 0) {
      updated = [...models];
      updated[existingIdx] = newModel;
    } else {
      updated = [...models, newModel];
    }

    setModels(updated);
    persistConfig(apiKey, defaultModel, updated);
    setSuccessMsg(`Модель ${modelId} добавлена в реестр и сохранена.`);
    addLog('ok', `Модель ${modelId} сохранена в настройках.`);
    setNewModelId('');
    setNewModelName('');
    setErrorMsg(null);
  };

  // Add model with test
  const handleTestAndAddModel = async () => {
    if (!newModelId.trim()) {
      setErrorMsg('Укажите ID модели (например, minimax/minimax-m3.1-flash)');
      return;
    }

    if (newModelId.includes(':batch')) {
      setErrorMsg('Модели с суффиксом :batch предназначены для фоновых пакетов и не работают в чате. Удалите :batch.');
      return;
    }

    if (!apiKey.trim()) {
      setErrorMsg('Сначала введите API-ключ OpenRouter выше');
      return;
    }

    setIsTestingNew(true);
    setErrorMsg(null);
    setSuccessMsg(null);

    const modelId = newModelId.trim();
    addLog('info', `Проверка доступности модели ${modelId} через OpenRouter...`);

    try {
      const res = await TestModel(apiKey.trim(), modelId);
      if (res === 'ok') {
        const provider = modelId.split('/')[0] || 'custom';
        const name = newModelName.trim() || modelId.split('/')[1] || modelId;

        const newModel: ModelConfig = {
          id: modelId,
          name,
          provider,
          isVision: newIsVision,
          status: 'verified',
          testedAt: new Date().toISOString(),
        };

        const existingIdx = models.findIndex((m) => m.id === modelId);
        let updated: ModelConfig[];
        if (existingIdx >= 0) {
          updated = [...models];
          updated[existingIdx] = newModel;
        } else {
          updated = [...models, newModel];
        }

        setModels(updated);
        persistConfig(apiKey, defaultModel, updated);
        setSuccessMsg(`Модель ${modelId} успешно проверена и сохранена!`);
        addLog('ok', `Модель ${modelId} доступна и сохранена в реестр.`);
        setNewModelId('');
        setNewModelName('');
      } else {
        throw new Error(res);
      }
    } catch (err: any) {
      const msg = err?.message || String(err);
      setErrorMsg(`Ошибка проверки: ${msg}`);
      addLog('error', `Проверка ${modelId} не удалась: ${msg}`);
    } finally {
      setIsTestingNew(false);
    }
  };

  const handleRecheckModel = async (modelId: string) => {
    if (!apiKey.trim()) {
      setErrorMsg('Введите API-ключ OpenRouter');
      return;
    }

    setTestingModelId(modelId);
    addLog('info', `Повторная проверка доступности ${modelId}...`);

    try {
      const res = await TestModel(apiKey.trim(), modelId);
      if (res === 'ok') {
        setModels((prev) => {
          const updated: ModelConfig[] = prev.map((m) =>
            m.id === modelId
              ? { ...m, status: 'verified' as const, testedAt: new Date().toISOString(), statusMsg: undefined }
              : m
          );
          persistConfig(apiKey, defaultModel, updated);
          return updated;
        });
        addLog('ok', `Модель ${modelId} подтверждена и обновлена.`);
      }
    } catch (err: any) {
      const msg = err?.message || String(err);
      setModels((prev) =>
        prev.map((m) =>
          m.id === modelId
            ? { ...m, status: 'error' as const, statusMsg: msg }
            : m
        )
      );
      addLog('error', `Ошибка проверки ${modelId}: ${msg}`);
    } finally {
      setTestingModelId(null);
    }
  };

  const handleRemoveModel = (id: string) => {
    const updated = models.filter((m) => m.id !== id);
    setModels(updated);
    persistConfig(apiKey, defaultModel, updated);
    addLog('info', `Модель удалена из реестра.`);
  };

  const handleClose = () => {
    persistConfig(apiKey, defaultModel, models);
    onClose();
  };

  const handleSaveAll = async () => {
    await persistConfig(apiKey, defaultModel, models);
    addLog('ok', 'Настройки успешно сохранены на диск.');
    onClose();
  };

  return (
    <div className="fixed inset-0 bg-[#161C14]/40 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-[#FFFFFF] border border-[#ACADA9] rounded-2xl w-full max-w-2xl max-h-[92vh] flex flex-col shadow-2xl animate-pop overflow-hidden">
        
        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-3.5 border-b border-[#ACADA9] bg-[#FAF9F8]">
          <div className="p-1.5 bg-[#F4DAD6] text-[#C0513F] rounded-lg">
            <IconGear className="w-5 h-5" />
          </div>
          <div>
            <h2 className="font-bold text-[15px] text-[#161C14]">Настройки · OpenRouter & Модели</h2>
            <p className="text-[11px] text-[#60635F]">Управление API-ключом и реестром проверенных AI-моделей</p>
          </div>
          <button 
            onClick={handleClose}
            className="ml-auto p-1.5 rounded-lg text-[#60635F] hover:bg-[#FAF9F8] hover:text-[#161C14] transition"
          >
            <IconClose className="w-4 h-4" />
          </button>
        </div>

        {/* Body */}
        <div className="p-5 overflow-y-auto space-y-5 flex-1">
          
          {/* API Key */}
          <div className="space-y-1.5">
            <label className="block text-[12px] font-bold text-[#4a4d47] uppercase tracking-wider">
              API-ключ OpenRouter
            </label>
            <input
              type="password"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              onBlur={() => persistConfig(apiKey, defaultModel, models)}
              placeholder="sk-or-v1-..."
              className="w-full px-3 py-2 border border-[#ACADA9] rounded-xl text-[13px] font-mono focus:outline-none focus:border-[#E76C5C] focus:ring-2 focus:ring-[#E76C5C]/20 transition"
            />
            <p className="text-[11px] text-[#60635F]">
              Ключ надежно хранится локально на диске в <code>config.json</code>.
            </p>
          </div>

          {/* Model Registry Section */}
          <div className="border-t border-[#ACADA9]/40 pt-4 space-y-3">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-bold text-[13.5px] text-[#161C14]">Реестр проверенных моделей</h3>
                <p className="text-[11px] text-[#60635F]">
                  Модели, доступные для быстрого выбора в шагах пайплайна
                </p>
              </div>
              <span className="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-[#D9ECD4] text-[#5CA354]">
                {models.length} моделей в списке
              </span>
            </div>

            {/* List of Models */}
            <div className="space-y-2 max-h-56 overflow-y-auto pr-1">
              {models.map((m) => (
                <div
                  key={m.id}
                  className="flex items-center justify-between p-2.5 bg-[#FAF9F8] border border-[#ACADA9]/60 rounded-xl hover:border-[#ACADA9] transition"
                >
                  <div className="flex items-center gap-2.5 min-w-0">
                    <span
                      className={`w-2.5 h-2.5 rounded-full shrink-0 ${
                        m.status === 'verified'
                          ? 'bg-[#5CA354]'
                          : m.status === 'error'
                          ? 'bg-[#C65D4E]'
                          : 'bg-[#E0A458]'
                      }`}
                    />
                    <div className="min-w-0">
                      <div className="flex items-center gap-1.5 flex-wrap">
                        <span className="font-semibold text-[13px] text-[#161C14] truncate">
                          {m.name || m.id}
                        </span>
                        {m.isVision && (
                          <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-[#F5EBCF] text-[#8a6a2b]">
                            📷 Vision
                          </span>
                        )}
                        <span className="text-[10px] text-[#60635F] uppercase font-mono">
                          {m.provider}
                        </span>
                      </div>
                      <div className="text-[11px] font-mono text-[#60635F] truncate">
                        {m.id}
                      </div>
                      {m.statusMsg && (
                        <div className="text-[10.5px] text-[#C65D4E] mt-0.5">{m.statusMsg}</div>
                      )}
                    </div>
                  </div>

                  <div className="flex items-center gap-1.5 shrink-0 ml-2">
                    <button
                      onClick={() => handleRecheckModel(m.id)}
                      disabled={testingModelId === m.id}
                      title="Проверить доступность"
                      className="px-2 py-1 text-[11px] font-medium border border-[#ACADA9] rounded-lg hover:bg-[#FFFFFF] text-[#60635F] hover:text-[#161C14] transition flex items-center gap-1"
                    >
                      {testingModelId === m.id ? (
                        <IconSpinner className="w-3 h-3 text-[#E76C5C]" />
                      ) : (
                        'Тест'
                      )}
                    </button>
                    <button
                      onClick={() => handleRemoveModel(m.id)}
                      title="Удалить из реестра"
                      className="p-1.5 text-[#C65D4E] hover:bg-[#F6E0DB] rounded-lg transition"
                    >
                      <IconTrash className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </div>
              ))}

              {models.length === 0 && (
                <div className="text-center py-6 text-[12px] text-[#60635F] italic border border-dashed border-[#ACADA9] rounded-xl">
                  Список проверенных моделей пуст. Добавьте модель ниже.
                </div>
              )}
            </div>

            {/* Add Model Box */}
            <div className="p-3.5 bg-[#FAF9F8] border border-[#ACADA9]/80 rounded-xl space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-[12px] font-bold text-[#4a4d47] flex items-center gap-1.5">
                  <IconSpark className="w-3.5 h-3.5 text-[#E76C5C]" />
                  Добавить модель в реестр
                </span>
                <button
                  type="button"
                  onClick={handleFetchCatalog}
                  disabled={isCatalogLoading || !apiKey.trim()}
                  className="text-[11px] font-semibold text-[#E76C5C] hover:underline flex items-center gap-1"
                >
                  {isCatalogLoading ? <IconSpinner className="w-3 h-3" /> : '🔍 Обзор каталога OpenRouter'}
                </button>
              </div>

              {/* Catalog Modal / Dropdown */}
              {showCatalog && (
                <div className="p-2.5 bg-white border border-[#ACADA9] rounded-xl space-y-2 max-h-48 overflow-y-auto">
                  <div className="flex items-center justify-between">
                    <span className="text-[11px] font-bold text-[#161C14]">Каталог OpenRouter (без :batch):</span>
                    <button onClick={() => setShowCatalog(false)} className="text-[11px] text-[#60635F]">Закрыть</button>
                  </div>
                  <input
                    type="text"
                    value={catalogSearch}
                    onChange={(e) => setCatalogSearch(e.target.value)}
                    placeholder="Поиск по названию или ID (minimax, gemini, claude, gpt)..."
                    className="w-full px-2 py-1 border border-[#ACADA9] rounded text-[11.5px]"
                  />
                  <div className="space-y-1">
                    {catalogList
                      .filter((m) =>
                        m.id.toLowerCase().includes(catalogSearch.toLowerCase()) ||
                        m.name.toLowerCase().includes(catalogSearch.toLowerCase())
                      )
                      .slice(0, 15)
                      .map((m) => (
                        <div
                          key={m.id}
                          onClick={() => {
                            setNewModelId(m.id);
                            setNewModelName(m.name);
                            setNewIsVision(m.isVision);
                            setShowCatalog(false);
                          }}
                          className="px-2 py-1 rounded hover:bg-[#FAF9F8] cursor-pointer text-[11.5px] flex items-center justify-between"
                        >
                          <span className="font-mono text-[#161C14]">{m.id}</span>
                          {m.isVision && <span className="text-[9.5px] text-[#8a6a2b] bg-[#F5EBCF] px-1 rounded">Vision</span>}
                        </div>
                      ))}
                  </div>
                </div>
              )}

              {/* Presets selector */}
              <div className="space-y-1">
                <label className="text-[11px] text-[#60635F] font-semibold">Быстрый выбор из проверенных пресетов:</label>
                <select
                  onChange={(e) => handleSelectPreset(e.target.value)}
                  defaultValue=""
                  className="w-full px-3 py-1.5 border border-[#ACADA9] rounded-lg text-[12px] bg-[#FFFFFF] font-mono focus:outline-none focus:border-[#E76C5C]"
                >
                  <option value="" disabled>-- Выберите пресет или введите вручную --</option>
                  {PRESET_MODELS.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} ({p.id}) {p.isVision ? '· [Vision]' : ''}
                    </option>
                  ))}
                </select>
              </div>

              {/* Manual input */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                <div>
                  <label className="block text-[11px] text-[#60635F] font-semibold mb-1">
                    ID модели OpenRouter:
                  </label>
                  <input
                    type="text"
                    value={newModelId}
                    onChange={(e) => setNewModelId(e.target.value)}
                    placeholder="minimax/minimax-m3.1-flash"
                    className="w-full px-2.5 py-1.5 border border-[#ACADA9] rounded-lg text-[12px] font-mono bg-white focus:outline-none focus:border-[#E76C5C]"
                  />
                </div>
                <div>
                  <label className="block text-[11px] text-[#60635F] font-semibold mb-1">
                    Понятное имя:
                  </label>
                  <input
                    type="text"
                    value={newModelName}
                    onChange={(e) => setNewModelName(e.target.value)}
                    placeholder="MiniMax M3.1 Flash"
                    className="w-full px-2.5 py-1.5 border border-[#ACADA9] rounded-lg text-[12px] bg-white focus:outline-none focus:border-[#E76C5C]"
                  />
                </div>
              </div>

              {/* Warning if :batch is entered */}
              {isBatchModel && (
                <div className="p-2 bg-[#F5EBCF] border border-[#e5d3a3] rounded-lg text-[11.5px] text-[#8a6a2b] flex items-center justify-between">
                  <span>
                    ⚠️ Суффикс <code>:batch</code> предназначен для пакетной обработки и не работает в обычном чате.
                  </span>
                  <button
                    type="button"
                    onClick={() => setNewModelId(cleanBatchId)}
                    className="ml-2 px-2 py-0.5 bg-[#E0A458] text-white rounded font-bold hover:bg-[#8a6a2b] transition"
                  >
                    Убрать :batch
                  </button>
                </div>
              )}

              <div className="flex items-center justify-between pt-1 flex-wrap gap-2">
                <label className="flex items-center gap-2 cursor-pointer text-[12px] text-[#4a4d47]">
                  <input
                    type="checkbox"
                    checked={newIsVision}
                    onChange={(e) => setNewIsVision(e.target.checked)}
                    className="rounded border-[#ACADA9] text-[#E76C5C] focus:ring-[#E76C5C]"
                  />
                  <span>Поддерживает изображения (Vision / OCR)</span>
                </label>

                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={handleForceAddModel}
                    disabled={!newModelId.trim()}
                    className="px-3 py-1.5 border border-[#ACADA9] bg-white hover:bg-[#FAF9F8] rounded-lg text-[11.5px] font-semibold text-[#60635F] hover:text-[#161C14] transition"
                    title="Добавить модель в список без ожидания тестового запроса"
                  >
                    Добавить без проверки
                  </button>

                  <button
                    type="button"
                    onClick={handleTestAndAddModel}
                    disabled={isTestingNew || !newModelId.trim()}
                    className="px-3.5 py-1.5 bg-[#161C14] text-white rounded-lg text-[12px] font-semibold hover:bg-[#2a3426] disabled:opacity-50 transition flex items-center gap-1.5 shadow-xs"
                  >
                    {isTestingNew ? (
                      <>
                        <IconSpinner className="w-3.5 h-3.5" />
                        <span>Проверка API...</span>
                      </>
                    ) : (
                      <>
                        <IconCheck className="w-3.5 h-3.5 text-[#5CA354]" />
                        <span>Проверить и добавить</span>
                      </>
                    )}
                  </button>
                </div>
              </div>

              {errorMsg && (
                <div className="p-2.5 bg-[#F6E0DB] border border-[#C65D4E]/30 rounded-lg text-[11.5px] text-[#C65D4E] flex items-center justify-between">
                  <span>{errorMsg}</span>
                  <button onClick={() => setErrorMsg(null)} className="font-bold ml-2">×</button>
                </div>
              )}

              {successMsg && (
                <div className="p-2.5 bg-[#D9ECD4] border border-[#5CA354]/30 rounded-lg text-[11.5px] text-[#5CA354] flex items-center justify-between">
                  <span>{successMsg}</span>
                  <button onClick={() => setSuccessMsg(null)} className="font-bold ml-2">×</button>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="px-5 py-3.5 border-t border-[#ACADA9] bg-[#FAF9F8] flex items-center justify-between">
          <span className="text-[11px] text-[#60635F]">
            Изменения применятся сразу после сохранения
          </span>
          <div className="flex items-center gap-2">
            <button
              onClick={handleClose}
              className="px-4 py-2 border border-[#ACADA9] rounded-xl text-[13px] font-semibold text-[#60635F] hover:bg-white transition"
            >
              Отмена
            </button>
            <button
              onClick={handleSaveAll}
              className="px-5 py-2 bg-[#161C14] text-white rounded-xl text-[13px] font-semibold hover:bg-[#2a3426] transition"
            >
              Сохранить настройки
            </button>
          </div>
        </div>

      </div>
    </div>
  );
};
