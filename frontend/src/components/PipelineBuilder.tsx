import React, { useState } from 'react';
import { Project, StepDefinition, AppConfig } from '../types';
import { 
  IconPlus, 
  IconTrash, 
  IconPlay, 
  IconCopy, 
  IconCheck, 
  IconFile, 
  IconImage, 
  IconUpload, 
  IconSpinner, 
  IconEdit, 
  IconSpark 
} from './Icons';
import { OpenFileDialog, ExecuteStep } from '../../wailsjs/go/main/App';
import { marked } from 'marked';
import { CopyableContent } from './CopyableContent';

interface PipelineBuilderProps {
  project: Project;
  onUpdateProject: (updater: (prev: Project) => Project) => void;
  config: AppConfig;
  addLog: (type: 'info' | 'ok' | 'error', message: string) => void;
}

export const PipelineBuilder: React.FC<PipelineBuilderProps> = ({
  project,
  onUpdateProject,
  config,
  addLog,
}) => {
  const [showAddMenu, setShowAddMenu] = useState(false);

  // Add a new step
  const addStep = (type: 'text' | 'file' | 'ai') => {
    const stepNum = project.steps.length + 1;
    let newStep: StepDefinition;

    if (type === 'text') {
      newStep = {
        id: `step_${Date.now()}`,
        type: 'text',
        title: `Текстовый блок ${stepNum}`,
        content: '',
      };
    } else if (type === 'file') {
      newStep = {
        id: `step_${Date.now()}`,
        type: 'file',
        title: `Загрузка файла / Картинки ${stepNum}`,
        content: '',
      };
    } else {
      const defaultModel = config.models?.[0]?.id || config.defaultModel || 'google/gemini-2.5-flash';
      newStep = {
        id: `step_${Date.now()}`,
        type: 'ai',
        title: `AI-обработка ${stepNum}`,
        model: defaultModel,
        prompt: stepNum > 1 ? `Проанализируй следующие данные:\n\n{{step_${stepNum - 1}}}` : '',
        content: '',
      };
    }

    onUpdateProject((prev) => ({
      ...prev,
      steps: [...prev.steps, newStep],
    }));
    setShowAddMenu(false);
    addLog('info', `Добавлен шаг ${stepNum} (${type})`);
  };

  const removeStep = (index: number) => {
    onUpdateProject((prev) => {
      const updated = prev.steps.filter((_, i) => i !== index);
      return { ...prev, steps: updated };
    });
    addLog('info', `Шаг ${index + 1} удален`);
  };

  const moveStep = (index: number, direction: 'up' | 'down') => {
    onUpdateProject((prev) => {
      const targetIndex = direction === 'up' ? index - 1 : index + 1;
      if (targetIndex < 0 || targetIndex >= prev.steps.length) return prev;
      const stepsCopy = [...prev.steps];
      const temp = stepsCopy[index];
      stepsCopy[index] = stepsCopy[targetIndex];
      stepsCopy[targetIndex] = temp;
      return { ...prev, steps: stepsCopy };
    });
  };

  const updateStep = (index: number, partial: Partial<StepDefinition>) => {
    onUpdateProject((prev) => {
      const updatedSteps = [...prev.steps];
      updatedSteps[index] = { ...updatedSteps[index], ...partial };
      return { ...prev, steps: updatedSteps };
    });
  };

  // Insert macro into prompt textarea at exact cursor position
  const insertMacro = (stepIndex: number, macroStr: string) => {
    const step = project.steps[stepIndex];
    const currentPrompt = step.prompt || '';
    const textarea = document.getElementById(`prompt_textarea_${step.id}`) as HTMLTextAreaElement | null;

    let nextPos = currentPrompt.length + macroStr.length;
    let updatedPrompt = currentPrompt;

    if (textarea) {
      const start = textarea.selectionStart ?? currentPrompt.length;
      const end = textarea.selectionEnd ?? currentPrompt.length;
      updatedPrompt = currentPrompt.substring(0, start) + macroStr + currentPrompt.substring(end);
      nextPos = start + macroStr.length;

      // Immediate DOM sync so cursor range can be set right away
      textarea.value = updatedPrompt;
      textarea.focus();
      textarea.setSelectionRange(nextPos, nextPos);
    } else {
      updatedPrompt = currentPrompt ? `${currentPrompt} ${macroStr}` : macroStr;
    }

    updateStep(stepIndex, { prompt: updatedPrompt });

    // Ensure cursor and focus remain intact after React state update render
    setTimeout(() => {
      const el = document.getElementById(`prompt_textarea_${step.id}`) as HTMLTextAreaElement | null;
      if (el) {
        el.focus();
        el.setSelectionRange(nextPos, nextPos);
      }
    }, 25);

    addLog('ok', `Макрос ${macroStr} вставлен в позицию курсора промпта (Шаг ${stepIndex + 1})`);
  };

  // Select file for a file step
  const handleSelectFileForStep = async (stepIndex: number) => {
    try {
      const resJSON = await OpenFileDialog();
      if (!resJSON) return;
      const parsed = JSON.parse(resJSON);

      updateStep(stepIndex, {
        fileName: parsed.fileName,
        fileType: parsed.fileType,
        fileData: parsed.fileData,
        content: parsed.content,
      });

      addLog('ok', `Шаг ${stepIndex + 1}: файл загружен (${parsed.fileName})`);
    } catch (err: any) {
      addLog('error', `Ошибка загрузки файла: ${err?.message || String(err)}`);
    }
  };

  const handleClipboardPasteForStep = async (stepIndex: number, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      if (!navigator.clipboard?.read) {
        addLog('info', 'Нажмите Ctrl+V для вставки скриншота из буфера.');
        return;
      }
      const items = await navigator.clipboard.read();
      for (const item of items) {
        for (const type of item.types) {
          if (type.startsWith('image/')) {
            const blob = await item.getType(type);
            const reader = new FileReader();
            reader.onload = (ev) => {
              const b64 = ev.target?.result as string;
              updateStep(stepIndex, {
                fileName: `скриншот_${new Date().toLocaleTimeString().replace(/:/g, '-')}.png`,
                fileType: type,
                fileData: b64,
                content: '[Изображение из буфера обмена]',
              });
              addLog('ok', `Шаг ${stepIndex + 1}: изображение вставлено из буфера обмена (Ctrl+V)`);
            };
            reader.readAsDataURL(blob);
            return;
          }
        }
      }
      addLog('info', 'В буфере обмена нет изображения. Сделайте скриншот (Win+Shift+S) и повторите.');
    } catch (err: any) {
      addLog('info', 'Для вставки изображения нажмите клавиши Ctrl+V');
    }
  };

  return (
    <div className="flex-1 overflow-y-auto w-full h-full">
      <div className="max-w-5xl mx-auto p-7 space-y-6 w-full">
      
      {/* Hint Banner */}
      <div className="flex items-center justify-between text-[12px] text-[#60635F] bg-white border border-[#ACADA9]/60 px-4 py-2.5 rounded-xl shadow-2xs">
        <div>
          <span>Конструктор цепочки: шаги выполняются по порядку. Подставляйте данные через макросы </span>
          <code className="bg-[#FAF9F8] border border-[#ACADA9]/40 px-1.5 py-0.5 rounded font-mono text-[#E76C5C]">
            {'{{step_N}}'}
          </code>
        </div>
        <span className="font-semibold text-[#161C14]">{project.steps.length} шагов</span>
      </div>

      {/* Steps List */}
      <div className="space-y-4">
        {project.steps.map((step, idx) => {
          const stepNum = idx + 1;
          const isAI = step.type === 'ai';
          const isFile = step.type === 'file';
          const isText = step.type === 'text';

          return (
            <div
              key={step.id || idx}
              className="bg-white border border-[#ACADA9] rounded-2xl shadow-xs overflow-hidden transition hover:border-[#8e908c]"
            >
              {/* Step Head */}
              <div className="flex items-center gap-3 px-4 py-3 bg-[#FAF9F8] border-b border-[#ACADA9]/60">
                <span
                  className={`w-7 h-7 rounded-lg text-white font-bold text-xs flex items-center justify-center shrink-0 ${
                    isAI
                      ? 'bg-[#E76C5C]'
                      : isFile
                      ? 'bg-[#E0A458]'
                      : 'bg-[#60635F]'
                  }`}
                >
                  {stepNum}
                </span>

                <input
                  type="text"
                  value={step.title}
                  onChange={(e) => updateStep(idx, { title: e.target.value })}
                  className="font-bold text-[13.5px] text-[#161C14] bg-transparent border-b border-transparent hover:border-[#ACADA9] focus:border-[#E76C5C] focus:outline-none px-1 py-0.5 transition"
                />

                <span className="text-[11px] font-mono uppercase text-[#60635F] bg-white px-2 py-0.5 rounded border border-[#ACADA9]/40">
                  {isAI ? 'OpenRouter AI' : isFile ? 'Файл / Vision' : 'Текст'}
                </span>

                {/* Step controls */}
                <div className="ml-auto flex items-center gap-1">
                  <button
                    onClick={() => moveStep(idx, 'up')}
                    disabled={idx === 0}
                    className="p-1 text-[#60635F] hover:text-[#161C14] disabled:opacity-30 rounded transition text-xs font-bold"
                    title="Переместить вверх"
                  >
                    ▲
                  </button>
                  <button
                    onClick={() => moveStep(idx, 'down')}
                    disabled={idx === project.steps.length - 1}
                    className="p-1 text-[#60635F] hover:text-[#161C14] disabled:opacity-30 rounded transition text-xs font-bold"
                    title="Переместить вниз"
                  >
                    ▼
                  </button>
                  <button
                    onClick={() => removeStep(idx)}
                    className="p-1 text-[#C65D4E] hover:bg-[#F6E0DB] rounded-lg transition"
                    title="Удалить шаг"
                  >
                    <IconTrash className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>

              {/* Step Body */}
              <div className="p-4 space-y-4">
                
                {/* 1. TEXT STEP */}
                {isText && (
                  <div className="space-y-3">
                    <div className="space-y-1">
                      <label className="block text-[11.5px] font-bold text-[#4a4d47]">
                        Инструкция / Пояснение к шагу (для себя при запуске):
                      </label>
                      <input
                        type="text"
                        value={step.instruction || ''}
                        onChange={(e) => updateStep(idx, { instruction: e.target.value })}
                        placeholder="Например: Вставьте сюда список ключей из Вордстат..."
                        className="w-full px-3 py-1.5 border border-[#ACADA9] rounded-lg text-[12px] bg-white focus:outline-none focus:border-[#E76C5C]"
                      />
                    </div>

                    <div className="space-y-1">
                      <div className="flex items-center justify-between">
                        <label className="block text-[11.5px] font-bold text-[#4a4d47]">
                          Значение по умолчанию (доступно через <code>{`{{step_${stepNum}}}`}</code>):
                        </label>
                        {idx > 0 && (
                          <span className="text-[10.5px] font-semibold text-[#8a6a2b] bg-[#F5EBCF] px-2 py-0.5 rounded border border-[#e5d3a3]">
                            ⏸️ Пайплайн остановится на этом шаге
                          </span>
                        )}
                      </div>
                      <textarea
                        value={step.content || ''}
                        onChange={(e) => updateStep(idx, { content: e.target.value })}
                        rows={6}
                        className="w-full p-2.5 border border-[#ACADA9] rounded-xl text-[12.5px] font-mono focus:outline-none focus:border-[#E76C5C] transition resize-y"
                      />
                    </div>
                  </div>
                )}

                {/* 2. FILE STEP */}
                {isFile && (
                  <div className="space-y-2">
                    <label className="block text-[11.5px] font-bold text-[#4a4d47]">
                      Прикрепленный файл / Изображение (передается в Vision модели):
                    </label>
                    
                    {step.fileName ? (
                      <div className="flex items-center gap-3 p-3 bg-[#FAF9F8] border border-[#ACADA9] rounded-xl">
                        {step.fileData?.startsWith('data:image/') ? (
                          <img
                            src={step.fileData}
                            alt="preview"
                            className="w-14 h-14 object-cover rounded-lg border border-[#ACADA9]"
                          />
                        ) : (
                          <div className="w-10 h-10 rounded-lg bg-[#E0A458] text-white flex items-center justify-center">
                            <IconFile className="w-5 h-5" />
                          </div>
                        )}
                        <div className="min-w-0 flex-1">
                          <div className="font-semibold text-[13px] text-[#161C14] truncate">
                            {step.fileName}
                          </div>
                          <div className="text-[11px] text-[#60635F]">
                            {step.fileType || 'file'} · Готов к Vision OCR
                          </div>
                        </div>
                        <button
                          onClick={() => handleSelectFileForStep(idx)}
                          className="px-3 py-1 text-[11.5px] border border-[#ACADA9] rounded-lg hover:bg-white text-[#60635F] transition"
                        >
                          Заменить
                        </button>
                        <button
                          onClick={() => updateStep(idx, { fileName: '', fileData: '', content: '' })}
                          className="p-1.5 text-[#C65D4E] hover:bg-[#F6E0DB] rounded-lg transition"
                        >
                          <IconTrash className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    ) : (
                      <div
                        onClick={() => handleSelectFileForStep(idx)}
                        className="border-2 border-dashed border-[#b9b7b2] hover:border-[#E76C5C] rounded-xl p-4 text-center cursor-pointer transition bg-[#FAF9F8] hover:bg-[#F4DAD6]/20 flex flex-col items-center justify-center gap-1.5"
                      >
                        <IconUpload className="w-6 h-6 text-[#E76C5C]" />
                        <span className="text-[12.5px] font-semibold text-[#161C14]">
                          Нажмите для выбора файла или изображения
                        </span>
                        <span className="text-[10.5px] text-[#60635F]">
                          Изображения автоматически кодируются в Base64 для мультимодальных моделей
                        </span>
                        <div className="flex items-center gap-2 mt-1">
                          <button
                            type="button"
                            onClick={(e) => handleClipboardPasteForStep(idx, e)}
                            className="px-3 py-1 bg-white border border-[#ACADA9] hover:border-[#E76C5C] rounded-lg text-[11.5px] font-semibold text-[#161C14] flex items-center gap-1.5 shadow-2xs transition"
                          >
                            <IconCopy className="w-3.5 h-3.5 text-[#E76C5C]" />
                            <span>Вставить из буфера обмена (Ctrl+V)</span>
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {/* 3. AI STEP */}
                {isAI && (
                  <div className="space-y-3">
                    
                    {/* Model selector from verified registry */}
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                      <div>
                        <label className="block text-[11.5px] font-bold text-[#4a4d47] mb-1">
                          Модель OpenRouter (из реестра):
                        </label>
                        <select
                          value={step.model || config.defaultModel}
                          onChange={(e) => updateStep(idx, { model: e.target.value })}
                          className="w-full px-3 py-2 border border-[#ACADA9] rounded-xl text-[12.5px] font-mono bg-white focus:outline-none focus:border-[#E76C5C]"
                        >
                          {config.models?.map((m) => (
                            <option key={m.id} value={m.id}>
                              {m.name || m.id} {m.isVision ? '· [📷 Vision]' : ''} ({m.provider})
                            </option>
                          ))}
                          <option value="custom">-- Ввести свой ID вручную --</option>
                        </select>
                      </div>

                      <div>
                        <label className="block text-[11.5px] font-bold text-[#4a4d47] mb-1">
                          Кастомный ID модели (если нет в списке):
                        </label>
                        <input
                          type="text"
                          value={step.model || ''}
                          onChange={(e) => updateStep(idx, { model: e.target.value })}
                          placeholder="например, anthropic/claude-3.7-sonnet"
                          className="w-full px-3 py-2 border border-[#ACADA9] rounded-xl text-[12.5px] font-mono bg-white focus:outline-none focus:border-[#E76C5C]"
                        />
                      </div>
                    </div>

                    {/* Macro chips & helpers */}
                    <div className="space-y-1">
                      <div className="flex items-center justify-between">
                        <span className="text-[11px] text-[#60635F] font-semibold">
                          {idx > 0 ? 'Вставить вывод шагов или теги копирования (кликните):' : 'Быстрые теги в промпт:'}
                        </span>
                      </div>
                      <div className="flex flex-wrap gap-1.5 items-center">
                        {idx > 0 &&
                          project.steps.slice(0, idx).map((_, prevI) => {
                            const macro = `{{step_${prevI + 1}}}`;
                            return (
                              <button
                                key={macro}
                                type="button"
                                onMouseDown={(e) => e.preventDefault()}
                                onClick={() => insertMacro(idx, macro)}
                                className="px-2.5 py-1 rounded-md text-[12px] font-mono font-medium bg-[#F5EBCF] border border-[#e5d3a3] text-[#8a6a2b] hover:bg-[#E0A458] hover:text-white transition cursor-pointer select-none active:scale-95"
                                title={`Кликните, чтобы вставить ${macro} в место курсора`}
                              >
                                {macro}
                              </button>
                            );
                          })}

                        <button
                          type="button"
                          onMouseDown={(e) => e.preventDefault()}
                          onClick={() =>
                            insertMacro(
                              idx,
                              '\n- Оберни каждую отдельную фразу/ключевик в тег <copy>...</copy>, например: - <copy>купить печать документов</copy>\n'
                            )
                          }
                          className="px-2.5 py-1 rounded-md text-[11.5px] font-semibold bg-[#FAF9F8] border border-[#ACADA9] text-[#161C14] hover:bg-[#F4DAD6] hover:border-[#E76C5C] hover:text-[#C0513F] transition cursor-pointer select-none active:scale-95 flex items-center gap-1"
                          title="Вставить в промпт инструкцию для ИИ оборачивать ключевики в <copy>...</copy>"
                        >
                          <IconCopy className="w-3 h-3 text-[#E76C5C]" />
                          <span>+ Инструкция для &lt;copy&gt;</span>
                        </button>
                      </div>
                    </div>

                    {/* Vision linkage banner if preceded by file */}
                    {idx > 0 && project.steps[idx - 1].type === 'file' && (
                      <div className="p-2.5 bg-[#F5EBCF] border border-[#e5d3a3] rounded-xl text-[12px] text-[#8a6a2b] flex items-center gap-2">
                        <IconSpark className="w-4 h-4 text-[#E0A458] shrink-0" />
                        <span>
                          <strong>Связь цепочки:</strong> Картинка/файл из Шага {idx} автоматически передается в эту модель как Vision Image. Модель проанализирует изображение по вашей инструкции ниже.
                        </span>
                      </div>
                    )}

                    {/* Prompt Template (2x taller for convenient prompt editing) */}
                    <div className="space-y-1.5">
                      <div className="flex items-center justify-between">
                        <label className="block text-[12px] font-bold text-[#4a4d47]">
                          Шаблон промпта:
                        </label>
                        <span className="text-[11px] text-[#60635F]">
                          Длина текста: {step.prompt?.length || 0} симв.
                        </span>
                      </div>
                      <textarea
                        id={`prompt_textarea_${step.id}`}
                        value={step.prompt || ''}
                        onChange={(e) => updateStep(idx, { prompt: e.target.value })}
                        rows={14}
                        placeholder="Напишите инструкцию для модели. Например: Исправь грамматику и сделай краткое резюме текста: {{step_1}}"
                        className="w-full p-3.5 border border-[#ACADA9] rounded-xl text-[13px] font-mono focus:outline-none focus:border-[#E76C5C] focus:ring-2 focus:ring-[#E76C5C]/20 transition resize-y leading-relaxed min-h-[300px]"
                      />
                    </div>

                  </div>
                )}

              </div>
            </div>
          );
        })}
      </div>

      {/* Add Step Button */}
      <div className="relative pt-2">
        <button
          onClick={() => setShowAddMenu(!showAddMenu)}
          className="w-full py-3.5 border-2 border-dashed border-[#b9b7b2] hover:border-[#E76C5C] hover:bg-[#F4DAD6]/30 text-[#60635F] hover:text-[#C0513F] rounded-2xl font-bold text-[13px] flex items-center justify-center gap-2 transition"
        >
          <IconPlus className="w-4 h-4" />
          <span>Добавить шаг в цепочку</span>
        </button>

        {showAddMenu && (
          <div className="absolute left-1/2 -translate-x-1/2 bottom-full mb-2 bg-white border border-[#ACADA9] rounded-xl shadow-xl p-1.5 min-w-[280px] z-30 animate-pop">
            <button
              onClick={() => addStep('text')}
              className="w-full text-left px-3 py-2 rounded-lg text-[13px] font-semibold text-[#161C14] hover:bg-[#FAF9F8] flex items-center gap-2.5 transition"
            >
              <div className="w-5 h-5 rounded bg-[#60635F] text-white flex items-center justify-center text-[10px] font-bold">
                T
              </div>
              <div>
                <div>Текстовое поле</div>
                <div className="text-[10.5px] text-[#60635F] font-normal">Ручной ввод текста, ключевиков или промпт-переменных</div>
              </div>
            </button>

            <button
              onClick={() => addStep('file')}
              className="w-full text-left px-3 py-2 rounded-lg text-[13px] font-semibold text-[#161C14] hover:bg-[#FAF9F8] flex items-center gap-2.5 transition"
            >
              <div className="w-5 h-5 rounded bg-[#E0A458] text-white flex items-center justify-center text-[10px] font-bold">
                F
              </div>
              <div>
                <div>Файл / Картинка (Vision)</div>
                <div className="text-[10.5px] text-[#60635F] font-normal">Загрузка документов или изображений для распознавания</div>
              </div>
            </button>

            <button
              onClick={() => addStep('ai')}
              className="w-full text-left px-3 py-2 rounded-lg text-[13px] font-semibold text-[#161C14] hover:bg-[#FAF9F8] flex items-center gap-2.5 transition"
            >
              <div className="w-5 h-5 rounded bg-[#E76C5C] text-white flex items-center justify-center text-[10px] font-bold">
                AI
              </div>
              <div>
                <div>AI-обработка (OpenRouter)</div>
                <div className="text-[10.5px] text-[#60635F] font-normal">Запрос к проверенной модели с подстановкой макросов</div>
              </div>
            </button>
          </div>
        )}
      </div>

      </div>
    </div>
  );
};
