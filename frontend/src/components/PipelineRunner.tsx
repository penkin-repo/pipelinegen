import React, { useState, useEffect } from 'react';
import { Project, AppConfig } from '../types';
import { 
  IconPlay, 
  IconUpload, 
  IconImage, 
  IconFile, 
  IconSpinner, 
  IconCheck, 
  IconCopy, 
  IconTrash, 
  IconSpark, 
  IconEdit 
} from './Icons';
import { OpenFileDialog, ExecuteStep } from '../../wailsjs/go/main/App';
import { marked } from 'marked';
import { CopyableContent } from './CopyableContent';

interface PipelineRunnerProps {
  project: Project;
  config: AppConfig;
  addLog: (type: 'info' | 'ok' | 'error', message: string) => void;
  onGoToBuilder: () => void;
  onUpdateProject?: (updater: (prev: Project) => Project) => void;
}

export const PipelineRunner: React.FC<PipelineRunnerProps> = ({
  project,
  config,
  addLog,
  onGoToBuilder,
  onUpdateProject,
}) => {
  // Step inputs state keyed by step index: text inputs & files
  const [stepTextInputs, setStepTextInputs] = useState<Record<number, string>>(() => {
    const initial: Record<number, string> = {};
    project.steps.forEach((s, idx) => {
      if (s.type === 'text') {
        initial[idx] = s.content || '';
      }
    });
    return initial;
  });

  const [stepFiles, setStepFiles] = useState<Record<number, {
    fileName: string;
    fileData: string;
    fileType: string;
    isImage: boolean;
  }>>(() => {
    const initial: Record<number, any> = {};
    project.steps.forEach((s, idx) => {
      if (s.type === 'file' && s.fileData) {
        initial[idx] = {
          fileName: s.fileName || 'file',
          fileData: s.fileData,
          fileType: s.fileType || 'image/png',
          isImage: s.fileType?.startsWith('image/') || false,
        };
      }
    });
    return initial;
  });

  // Pipeline runtime states
  const [isRunning, setIsRunning] = useState(false);
  const [activeStepIndex, setActiveStepIndex] = useState<number | null>(null);
  const [pausedAtStepIndex, setPausedAtStepIndex] = useState<number | null>(null);
  const [stepResults, setStepResults] = useState<Record<number, string>>({});
  const [stepStatuses, setStepStatuses] = useState<Record<number, 'idle' | 'running' | 'waiting_input' | 'done' | 'error'>>({});
  const [stepErrorMessages, setStepErrorMessages] = useState<Record<number, string>>({});
  const [finalResult, setFinalResult] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [expandedSteps, setExpandedSteps] = useState<Record<number, boolean>>({});

  // Follow-up interaction
  const [followUpText, setFollowUpText] = useState('');
  const [isFollowUpRunning, setIsFollowUpRunning] = useState(false);
  const [followUpHistory, setFollowUpHistory] = useState<Array<{ role: 'user' | 'assistant'; content: string }>>([]);

  // Clear all forms and step outputs for a fresh query/run
  const handleClearAll = () => {
    const texts: Record<number, string> = {};
    project.steps.forEach((s, idx) => {
      if (s.type === 'text') {
        texts[idx] = '';
      }
    });
    setStepTextInputs(texts);
    setStepFiles({});
    setStepResults({});
    setStepErrorMessages({});
    setFinalResult(null);
    setFollowUpHistory([]);
    setFollowUpText('');
    setPausedAtStepIndex(null);
    setActiveStepIndex(null);

    const statuses: Record<number, any> = {};
    project.steps.forEach((_, idx) => {
      statuses[idx] = 'idle';
    });
    setStepStatuses(statuses);

    addLog('info', '🧹 Все формы, вводы и результаты очищены. Готово к новому запросу.');
  };

  // Reset states when project changes
  useEffect(() => {
    const texts: Record<number, string> = {};
    const files: Record<number, any> = {};
    const statuses: Record<number, any> = {};

    project.steps.forEach((s, idx) => {
      statuses[idx] = 'idle';
      if (s.type === 'text') {
        texts[idx] = s.content || '';
      }
      if (s.type === 'file' && s.fileData) {
        files[idx] = {
          fileName: s.fileName || 'file',
          fileData: s.fileData,
          fileType: s.fileType || 'image/png',
          isImage: s.fileType?.startsWith('image/') || false,
        };
      }
    });

    setStepTextInputs(texts);
    setStepFiles(files);
    setStepStatuses(statuses);
    setStepErrorMessages({});
    setStepResults({});
    setFinalResult(null);
    setPausedAtStepIndex(null);
    setActiveStepIndex(null);
    setIsRunning(false);
  }, [project.id]);

  // Pick file for a specific file step
  const handleSelectFileForStep = async (stepIdx: number) => {
    try {
      const resJSON = await OpenFileDialog();
      if (!resJSON) return;
      const parsed = JSON.parse(resJSON);
      setStepFiles((prev) => ({
        ...prev,
        [stepIdx]: {
          fileName: parsed.fileName,
          fileData: parsed.fileData,
          fileType: parsed.fileType,
          isImage: parsed.isImage,
        },
      }));
      addLog('info', `Шаг ${stepIdx + 1}: файл выбран (${parsed.fileName})`);
    } catch (err: any) {
      addLog('error', `Ошибка выбора файла: ${err?.message || String(err)}`);
    }
  };

  const handleBrowserFileForStep = (stepIdx: number, e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const isImage = file.type.startsWith('image/');
    const reader = new FileReader();

    if (isImage) {
      reader.onload = (ev) => {
        const b64 = ev.target?.result as string;
        setStepFiles((prev) => ({
          ...prev,
          [stepIdx]: {
            fileName: file.name,
            fileData: b64,
            fileType: file.type,
            isImage: true,
          },
        }));
        addLog('info', `Шаг ${stepIdx + 1}: изображение загружено (${file.name})`);
      };
      reader.readAsDataURL(file);
    } else {
      reader.onload = (ev) => {
        const text = ev.target?.result as string;
        setStepFiles((prev) => ({
          ...prev,
          [stepIdx]: {
            fileName: file.name,
            fileData: text,
            fileType: file.type || 'text/plain',
            isImage: false,
          },
        }));
        addLog('info', `Шаг ${stepIdx + 1}: файл загружен (${file.name})`);
      };
      reader.readAsText(file);
    }
  };

  // Paste image helper
  const pasteImageForStep = (stepIdx: number, file: File) => {
    const reader = new FileReader();
    reader.onload = (ev) => {
      const b64 = ev.target?.result as string;
      setStepFiles((prev) => ({
        ...prev,
        [stepIdx]: {
          fileName: file.name || `скриншот_${new Date().toLocaleTimeString().replace(/:/g, '-')}.png`,
          fileData: b64,
          fileType: file.type || 'image/png',
          isImage: true,
        },
      }));
      addLog('ok', `Шаг ${stepIdx + 1}: изображение успешно вставлено из буфера обмена (Ctrl+V)`);
    };
    reader.readAsDataURL(file);
  };

  const handleClipboardButtonClick = async (stepIdx: number, e: React.MouseEvent) => {
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
            const file = new File([blob], `clipboard_${Date.now()}.png`, { type });
            pasteImageForStep(stepIdx, file);
            return;
          }
        }
      }
      addLog('info', 'В буфере обмена нет изображения. Сделайте скриншот (Win+Shift+S) и повторите.');
    } catch (err: any) {
      addLog('info', 'Для вставки изображения нажмите клавиши Ctrl+V на клавиатуре');
    }
  };

  // Global paste listener (Ctrl+V anywhere in runner)
  useEffect(() => {
    const handleGlobalPaste = (e: ClipboardEvent) => {
      // If user is focused on a textarea or text input, let them paste text normally
      const targetTag = (e.target as HTMLElement)?.tagName?.toLowerCase();
      if ((targetTag === 'textarea' || targetTag === 'input') && !(e.target as HTMLInputElement).classList.contains('hidden')) {
        // If clipboard contains an image, prioritize image paste even if in input
        const hasImage = e.clipboardData?.items && Array.from(e.clipboardData.items).some((item) => item.type.includes('image'));
        if (!hasImage) return;
      }

      if (e.clipboardData && e.clipboardData.items) {
        for (let i = 0; i < e.clipboardData.items.length; i++) {
          const item = e.clipboardData.items[i];
          if (item.type.indexOf('image') !== -1) {
            const file = item.getAsFile();
            if (file) {
              e.preventDefault();
              // Find first file step
              const fileStepIdx = project.steps.findIndex((s) => s.type === 'file');
              if (fileStepIdx >= 0) {
                pasteImageForStep(fileStepIdx, file);
              }
              return;
            }
          }
        }
      }
    };

    window.addEventListener('paste', handleGlobalPaste);
    return () => window.removeEventListener('paste', handleGlobalPaste);
  }, [project.steps]);

  const toggleStepExpand = (idx: number) => {
    setExpandedSteps((prev) => ({ ...prev, [idx]: !prev[idx] }));
  };

  // Execute pipeline from a specific start step index
  const executePipelineFrom = async (startIndex: number) => {
    if (project.steps.length === 0) {
      addLog('error', 'В пайплайне нет шагов.');
      return;
    }

    if (!config.apiKey) {
      addLog('error', 'Не указан API-ключ OpenRouter. Откройте Настройки.');
      return;
    }

    setIsRunning(true);
    setPausedAtStepIndex(null);

    const currentResults = { ...stepResults };

    // Reset statuses and outputs from startIndex onwards
    const newStatuses: Record<number, any> = { ...stepStatuses };
    for (let j = startIndex; j < project.steps.length; j++) {
      newStatuses[j] = j === startIndex ? 'running' : 'idle';
      delete currentResults[j];
    }
    setStepStatuses(newStatuses);
    setStepResults({ ...currentResults });
    setFinalResult(null);
    setFollowUpHistory([]);
    setStepErrorMessages((prev) => {
      const copy = { ...prev };
      delete copy[startIndex];
      return copy;
    });

    let currentStepIdx = startIndex;

    try {
      for (let i = startIndex; i < project.steps.length; i++) {
        currentStepIdx = i;
        const step = project.steps[i];
        const stepNum = i + 1;
        setActiveStepIndex(i);
        setStepStatuses((prev) => ({ ...prev, [i]: 'running' }));

        let stepOutput = '';

        if (step.type === 'file') {
          // File step
          const fileObj = stepFiles[i];
          if (fileObj) {
            stepOutput = fileObj.fileData;
          } else if (step.fileData) {
            stepOutput = step.fileData;
          } else {
            stepOutput = '[Нет прикрепленного файла]';
          }
          currentResults[i] = stepOutput;
          setStepResults({ ...currentResults });
          setStepStatuses((prev) => ({ ...prev, [i]: 'done' }));
          addLog('ok', `Шаг ${stepNum} [Файл: ${fileObj?.fileName || step.fileName || 'вход'}] готов`);
        } 
        else if (step.type === 'text') {
          // Text step:
          // If this is an intermediate step (i > 0) and user hasn't explicitly filled it yet, or if paused:
          const userVal = stepTextInputs[i];

          // If Step 1 is empty, don't send empty string to AI, prompt user to enter value!
          if (i === 0 && (!userVal || userVal.trim() === '')) {
            setPausedAtStepIndex(0);
            setStepStatuses((prev) => ({ ...prev, [0]: 'waiting_input' }));
            setIsRunning(false);
            addLog('error', `⚠️ Шаг 1 не заполнен! Введите исходные данные (например, название товара) перед запуском.`);
            return;
          }
          
          // If it's in the middle of the pipeline and user hasn't provided text yet:
          if (i > 0 && (!userVal || userVal.trim() === '')) {
            // PAUSE and wait for user input!
            setPausedAtStepIndex(i);
            setStepStatuses((prev) => ({ ...prev, [i]: 'waiting_input' }));
            setIsRunning(false);
            addLog('info', `⏸️ Пайплайн приостановлен на Шаге ${stepNum}: требуется ввести текст/ключевики.`);
            return; // stop execution here, user will click "Продолжить"
          }

          stepOutput = userVal || step.content || '';
          currentResults[i] = stepOutput;
          setStepResults({ ...currentResults });
          setStepStatuses((prev) => ({ ...prev, [i]: 'done' }));
          addLog('ok', `Шаг ${stepNum} [Текстовый ввод] готов`);
        } 
        else if (step.type === 'ai') {
          // OpenRouter AI Step
          const model = step.model || config.defaultModel || 'google/gemini-2.5-flash';
          let prompt = step.prompt || '';

          // Substitute macros: {{step_1}}, {{step_2}}, {{step_3}}, etc.
          for (let prevIdx = 0; prevIdx < i; prevIdx++) {
            const macro = `{{step_${prevIdx + 1}}}`;
            const prevOutput = currentResults[prevIdx] || '';
            if (prevOutput.startsWith('data:image/')) {
              prompt = prompt.split(macro).join(`[Изображение из шага ${prevIdx + 1}]`);
            } else {
              prompt = prompt.split(macro).join(prevOutput);
            }
          }

          // Check if previous step provided an image for Vision
          let attachedImageBase64: string | null = null;
          if (i > 0 && project.steps[i - 1].type === 'file') {
            const prevVal = currentResults[i - 1];
            if (prevVal && prevVal.startsWith('data:image/')) {
              attachedImageBase64 = prevVal;
            }
          } else {
            // Check if prompt specifically mentions an image step
            for (let prevIdx = 0; prevIdx < i; prevIdx++) {
              if (step.prompt?.includes(`{{step_${prevIdx + 1}}}`)) {
                const val = currentResults[prevIdx];
                if (val && val.startsWith('data:image/')) {
                  attachedImageBase64 = val;
                  break;
                }
              }
            }
          }

          if (!attachedImageBase64 && i === 1 && stepFiles[0]?.isImage) {
            attachedImageBase64 = stepFiles[0].fileData;
          }

          // Build messages
          let userContent: any = prompt;
          if (attachedImageBase64) {
            userContent = [
              {
                type: 'text',
                text: prompt || 'Распознай текст и подробно опиши содержимое этого изображения:',
              },
              {
                type: 'image_url',
                image_url: {
                  url: attachedImageBase64,
                },
              },
            ];
          }

          const reqPayload = {
            apiKey: config.apiKey,
            model,
            messages: [{ role: 'user', content: userContent }],
          };

          addLog('info', `Шаг ${stepNum} [AI: ${model}] отправка запроса...`);
          const aiResponse = await ExecuteStep(JSON.stringify(reqPayload));
          stepOutput = aiResponse;
          currentResults[i] = stepOutput;
          setStepResults({ ...currentResults });
          setStepStatuses((prev) => ({ ...prev, [i]: 'done' }));
          setExpandedSteps((prev) => ({ ...prev, [i]: true }));
          addLog('ok', `Шаг ${stepNum} [AI] успешно выполнен (${aiResponse.length} симв.)`);

          // If step is marked as requiring user review, pause before next step
          if (step.requireUser && i < project.steps.length - 1) {
            setPausedAtStepIndex(i + 1);
            setStepStatuses((prev) => ({ ...prev, [i + 1]: 'waiting_input' }));
            setIsRunning(false);
            addLog('info', `⏸️ Пайплайн приостановлен для проверки/ввода перед Шагом ${stepNum + 1}`);
            return;
          }
        }

        // Only set finalResult when the VERY LAST step of the entire pipeline finishes!
        if (i === project.steps.length - 1) {
          setFinalResult(stepOutput);
        }
      }

      addLog('ok', '🎉 Пайплайн успешно завершен!');
    } catch (err: any) {
      const errText = err?.message || String(err);
      addLog('error', `Ошибка выполнения Шага ${currentStepIdx + 1}: ${errText}`);
      setStepStatuses((prev) => ({ ...prev, [currentStepIdx]: 'error' }));
      setStepErrorMessages((prev) => ({ ...prev, [currentStepIdx]: errText }));
    } finally {
      setIsRunning(false);
      setActiveStepIndex(null);
    }
  };

  // Resume pipeline after user input on a paused step
  const handleContinueFromPaused = (stepIdx: number) => {
    executePipelineFrom(stepIdx);
  };

  // Follow-up interaction
  const handleSendFollowUp = async () => {
    if (!followUpText.trim() || !finalResult) return;

    const lastAiStep = [...project.steps].reverse().find((s) => s.type === 'ai');
    const model = lastAiStep?.model || config.defaultModel || 'google/gemini-2.5-flash';

    setIsFollowUpRunning(true);
    addLog('info', `Отправка ответа/дополнительных данных модели ${model}...`);

    try {
      const messages: any[] = [
        {
          role: 'user',
          content: 'Вот исходный результат предыдущего этапа работы:\n\n' + finalResult,
        },
        ...followUpHistory,
        {
          role: 'user',
          content: followUpText.trim(),
        },
      ];

      const reqPayload = {
        apiKey: config.apiKey,
        model,
        messages,
      };

      const response = await ExecuteStep(JSON.stringify(reqPayload));

      setFollowUpHistory((prev) => [
        ...prev,
        { role: 'user', content: followUpText.trim() },
        { role: 'assistant', content: response },
      ]);

      setFinalResult(response);
      setFollowUpText('');
      addLog('ok', 'Ответ модели успешно получен и обновлен в итоге!');
    } catch (err: any) {
      addLog('error', `Ошибка при отправке ответа: ${err?.message || String(err)}`);
    } finally {
      setIsFollowUpRunning(false);
    }
  };

  const handleSaveFollowUpAsStep = () => {
    if (!onUpdateProject || followUpHistory.length === 0) return;
    const lastUserMsg = followUpHistory[followUpHistory.length - 2]?.content || 'Дополнительная обработка';
    const lastAiStep = [...project.steps].reverse().find((s) => s.type === 'ai');

    const newStep = {
      id: `step_${Date.now()}`,
      type: 'ai' as const,
      title: `Шаг ${project.steps.length + 1}: Доработка по ключам`,
      model: lastAiStep?.model || config.defaultModel,
      prompt: `На основе предыдущего шага:\n{{step_${project.steps.length}}}\n\n${lastUserMsg}`,
      content: '',
      result: finalResult || '',
      status: 'done' as const,
    };

    onUpdateProject((prev) => ({
      ...prev,
      steps: [...prev.steps, newStep],
    }));

    addLog('ok', 'Новый шаг добавлен в цепочку пайплайна!');
  };

  const handleCopyResult = () => {
    if (finalResult) {
      navigator.clipboard.writeText(finalResult);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  return (
    <div className="flex-1 flex flex-col h-full overflow-hidden bg-[#F8F7F5]">
      
      {/* Top action header for runner */}
      <div className="px-7 py-4 bg-[#FFFFFF] border-b border-[#ACADA9] flex items-center justify-between shrink-0 shadow-xs">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-lg font-bold text-[#161C14]">{project.name}</h1>
            <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-[#D9ECD4] text-[#5CA354]">
              Готов к запуску
            </span>
          </div>
          <p className="text-[12px] text-[#60635F] mt-0.5">
            {project.description || 'Многошаговый автоматический AI-пайплайн'} · {project.steps.length} шагов в цепочке
          </p>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={handleClearAll}
            disabled={isRunning}
            className="px-3.5 py-2 border border-[#ACADA9] rounded-xl text-[12.5px] font-semibold text-[#60635F] hover:bg-[#F6E0DB] hover:text-[#C65D4E] hover:border-[#C65D4E] flex items-center gap-1.5 transition disabled:opacity-40 cursor-pointer shadow-2xs"
            title="Очистить все формы, введенные данные и результаты шагов для создания нового запроса"
          >
            <IconTrash className="w-3.5 h-3.5" />
            <span>Очистить формы</span>
          </button>

          <button
            onClick={onGoToBuilder}
            className="px-3.5 py-2 border border-[#ACADA9] rounded-xl text-[12.5px] font-semibold text-[#60635F] hover:bg-[#FAF9F8] hover:text-[#161C14] flex items-center gap-1.5 transition cursor-pointer"
          >
            <IconEdit className="w-3.5 h-3.5" />
            <span>Настройки шагов (Конструктор)</span>
          </button>

          <button
            onClick={() => executePipelineFrom(0)}
            disabled={isRunning}
            className={`px-5 py-2.5 rounded-xl font-bold text-[13.5px] flex items-center gap-2 text-white shadow-sm transition active:scale-98 ${
              isRunning
                ? 'bg-[#E0A458] cursor-wait'
                : 'bg-[#E76C5C] hover:bg-[#C0513F]'
            }`}
          >
            {isRunning ? (
              <>
                <IconSpinner className="w-4 h-4 text-white" />
                <span>Выполняется цепочка...</span>
              </>
            ) : (
              <>
                <IconPlay className="w-4 h-4" />
                <span>Запустить пайплайн сначала</span>
              </>
            )}
          </button>
        </div>
      </div>

      {/* Main Runner Body: sequential steps 1 -> 2 -> 3 -> 4 */}
      <div className="flex-1 overflow-y-auto w-full h-full">
        <div className="max-w-5xl mx-auto p-7 space-y-6 w-full">
        
        {/* Step-by-step Flow */}
        <div className="space-y-4">
          {project.steps.map((step, idx) => {
            const stepNum = idx + 1;
            const status = stepStatuses[idx] || 'idle';
            const isError = status === 'error' || (!isRunning && status === 'running');
            const isCurrent = activeStepIndex === idx;
            const isWaitingInput = pausedAtStepIndex === idx || status === 'waiting_input';
            const result = stepResults[idx];
            const isExpanded = expandedSteps[idx] !== false;
            const errorMsg = stepErrorMessages[idx];

            const isText = step.type === 'text';
            const isFile = step.type === 'file';
            const isAI = step.type === 'ai';

            // Check if step 2 receives image from step 1
            const receivesImageFromPrev = isAI && idx > 0 && project.steps[idx - 1].type === 'file' && Boolean(stepFiles[idx - 1]?.isImage || project.steps[idx - 1].fileData);

            return (
              <div
                key={step.id || idx}
                className={`p-5 rounded-2xl border transition shadow-xs ${
                  isWaitingInput
                    ? 'border-2 border-[#E76C5C] bg-[#F4DAD6]/30 shadow-md ring-2 ring-[#E76C5C]/20'
                    : isError
                    ? 'border-2 border-[#C65D4E] bg-[#F6E0DB]/40'
                    : isCurrent
                    ? 'border-[#E76C5C] bg-[#F4DAD6]/20'
                    : status === 'done'
                    ? 'border-[#ACADA9]/70 bg-white'
                    : 'border-[#ACADA9]/50 bg-white'
                }`}
              >
                {/* Step Header */}
                <div className="flex items-center justify-between pb-3 border-b border-[#ACADA9]/30">
                  <div className="flex items-center gap-3">
                    <span
                      className={`w-7 h-7 rounded-lg text-white font-bold text-xs flex items-center justify-center shrink-0 ${
                        isError
                          ? 'bg-[#C65D4E]'
                          : isRunning && isCurrent
                          ? 'bg-[#E0A458]'
                          : status === 'done'
                          ? 'bg-[#5CA354]'
                          : isWaitingInput
                          ? 'bg-[#E76C5C] animate-pulse'
                          : isAI
                          ? 'bg-[#E76C5C]'
                          : isFile
                          ? 'bg-[#E0A458]'
                          : 'bg-[#60635F]'
                      }`}
                    >
                      {isError ? (
                        '✕'
                      ) : isRunning && isCurrent ? (
                        <IconSpinner className="w-3.5 h-3.5" />
                      ) : status === 'done' ? (
                        <IconCheck className="w-3.5 h-3.5" />
                      ) : (
                        stepNum
                      )}
                    </span>

                    <div>
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-bold text-[14px] text-[#161C14]">
                          Шаг {stepNum}: {step.title}
                        </span>

                        <span className="text-[10.5px] px-2 py-0.5 rounded font-mono uppercase bg-[#FAF9F8] border border-[#ACADA9]/50 text-[#60635F]">
                          {isAI ? step.model || 'OpenRouter AI' : isFile ? 'Файл / Vision' : 'Текстовый ввод'}
                        </span>

                        {receivesImageFromPrev && (
                          <span className="text-[10.5px] px-2 py-0.5 rounded bg-[#F5EBCF] text-[#8a6a2b] font-semibold flex items-center gap-1 border border-[#e5d3a3]">
                            📷 Вход: Картинка из Шага {idx} (Vision)
                          </span>
                        )}

                        {isWaitingInput && (
                          <span className="text-[11px] px-2 py-0.5 rounded-full bg-[#E76C5C] text-white font-bold animate-pulse">
                            Ждет вашего ввода
                          </span>
                        )}

                        {isError && (
                          <span className="text-[11px] px-2 py-0.5 rounded bg-[#C65D4E] text-white font-bold">
                            Ошибка
                          </span>
                        )}
                      </div>

                      <p className="text-[11.5px] text-[#60635F] mt-0.5">
                        {isText
                          ? (idx === 0 ? 'Начальные данные для цепочки (например, название товара)' : 'Промежуточный ввод текста от пользователя (например, спарсенные ключи из Вордстат)')
                          : isFile
                          ? 'Файл или изображение для Vision анализа'
                          : `AI-обработка: обращается к ${step.model || 'OpenRouter'}`}
                      </p>
                    </div>
                  </div>

                  {/* Actions right */}
                  <div className="flex items-center gap-2">
                    {result && !result.startsWith('data:image/') && (
                      <span className="text-[11px] text-[#60635F] font-mono">
                        {result.length} симв.
                      </span>
                    )}
                    {isAI && result && (
                      <button
                        onClick={() => toggleStepExpand(idx)}
                        className="px-2 py-1 text-[11px] font-semibold text-[#60635F] hover:text-[#161C14] bg-white border border-[#ACADA9]/50 rounded-lg transition"
                      >
                        {isExpanded ? 'Свернуть ▲' : 'Показать текст ▼'}
                      </button>
                    )}
                    {!isRunning && (
                      <button
                        onClick={() => executePipelineFrom(idx)}
                        className={`px-3 py-1.5 text-[11.5px] font-bold rounded-lg transition flex items-center gap-1.5 shadow-2xs ${
                          isError
                            ? 'bg-[#C65D4E] hover:bg-[#a64537] text-white'
                            : status === 'done'
                            ? 'bg-white hover:bg-[#FAF9F8] text-[#161C14] border border-[#ACADA9]/60'
                            : 'bg-white hover:bg-[#FAF9F8] text-[#E76C5C] border border-[#E76C5C]/50'
                        }`}
                        title={isError ? 'Повторить этот шаг и продолжить цепочку' : 'Запустить цепочку начиная с этого шага'}
                      >
                        <IconPlay className="w-3 h-3" />
                        <span>{isError ? 'Повторить шаг' : status === 'done' ? 'Перезапустить' : 'Запустить отсюда'}</span>
                      </button>
                    )}
                  </div>
                </div>

                {/* Step Body */}
                <div className="pt-3.5 space-y-3">
                  {/* Error Notification Banner if step failed */}
                  {isError && (
                    <div className="p-3.5 bg-white border-2 border-[#C65D4E] rounded-xl flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 shadow-xs">
                      <div className="space-y-1">
                        <div className="text-[13px] font-bold text-[#C65D4E] flex items-center gap-1.5">
                          <span>⚠️ Ошибка выполнения Шага {stepNum}</span>
                        </div>
                        <p className="text-[11.5px] text-[#60635F] font-mono break-all leading-relaxed">
                          {errorMsg || 'Запрос был прерван или завершился с ошибкой (например, превышено время ожидания ответа модели).'}
                        </p>
                      </div>
                      <button
                        onClick={() => executePipelineFrom(idx)}
                        disabled={isRunning}
                        className="px-4 py-2 bg-[#C65D4E] hover:bg-[#a64537] text-white font-bold text-[12.5px] rounded-xl transition flex items-center gap-2 shrink-0 shadow-xs active:scale-98"
                      >
                        <IconPlay className="w-3.5 h-3.5" />
                        <span>Повторить с Шага {stepNum} ▶</span>
                      </button>
                    </div>
                  )}
                  
                  {/* 1. TEXT STEP: Input area on this step */}
                  {isText && (
                    <div className="space-y-2">
                      {step.instruction && (
                        <div className="p-3 bg-[#FAF9F8] border border-[#ACADA9]/60 rounded-xl text-[12.5px] text-[#161C14] font-medium flex items-center gap-2">
                          <IconSpark className="w-4 h-4 text-[#E0A458] shrink-0" />
                          <span>{step.instruction}</span>
                        </div>
                      )}
                      
                      <label className="block text-[12px] font-bold text-[#4a4d47]">
                        {idx === 0 ? 'Исходные данные:' : 'Данные для шага:'}
                      </label>
                      
                      <textarea
                        value={stepTextInputs[idx] ?? (step.content || '')}
                        onChange={(e) =>
                          setStepTextInputs((prev) => ({ ...prev, [idx]: e.target.value }))
                        }
                        rows={idx === 0 ? 3 : 6}
                        className="w-full p-3 border border-[#ACADA9] rounded-xl text-[12.5px] font-mono bg-white focus:outline-none focus:border-[#E76C5C] focus:ring-2 focus:ring-[#E76C5C]/20 transition leading-relaxed resize-y"
                      />

                      {/* If paused at this step waiting for input, show large prominent button */}
                      {isWaitingInput && (
                        <div className="pt-1 flex items-center justify-between bg-white p-3 rounded-xl border border-[#E76C5C]">
                          <span className="text-[12px] font-semibold text-[#161C14]">
                            Готовы передать этот текст в следующий шаг?
                          </span>
                          <button
                            onClick={() => handleContinueFromPaused(idx)}
                            className="px-5 py-2 bg-[#E76C5C] text-white font-bold text-[13px] rounded-xl hover:bg-[#C0513F] transition flex items-center gap-2 shadow-xs active:scale-98"
                          >
                            <IconPlay className="w-3.5 h-3.5" />
                            <span>Продолжить пайплайн ▶</span>
                          </button>
                        </div>
                      )}
                    </div>
                  )}

                  {/* 2. FILE STEP: Dropzone or uploaded file preview */}
                  {isFile && (
                    <div className="space-y-2">
                      {stepFiles[idx] ? (
                        <div className="flex items-center gap-3 p-3 bg-[#FAF9F8] border border-[#ACADA9] rounded-xl">
                          {stepFiles[idx].isImage ? (
                            <img
                              src={stepFiles[idx].fileData}
                              alt="Preview"
                              className="w-16 h-16 rounded-lg object-cover border border-[#ACADA9] shrink-0 shadow-xs"
                            />
                          ) : (
                            <div className="w-12 h-12 rounded-lg bg-[#E0A458] text-white flex items-center justify-center shrink-0">
                              <IconFile className="w-6 h-6" />
                            </div>
                          )}
                          <div className="min-w-0 flex-1">
                            <div className="font-semibold text-[13px] text-[#161C14] truncate">
                              {stepFiles[idx].fileName}
                            </div>
                            <div className="text-[11px] text-[#5CA354] font-medium">
                              ✓ Файл готов к передаче в Vision модель на Шаг {idx + 2}
                            </div>
                          </div>
                          <div className="flex items-center gap-1">
                            <button
                              onClick={() => handleSelectFileForStep(idx)}
                              className="p-1.5 text-[#60635F] hover:bg-white rounded-lg transition"
                              title="Заменить файл"
                            >
                              <IconUpload className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() =>
                                setStepFiles((prev) => {
                                  const copy = { ...prev };
                                  delete copy[idx];
                                  return copy;
                                })
                              }
                              className="p-1.5 text-[#C65D4E] hover:bg-[#F6E0DB] rounded-lg transition"
                              title="Удалить файл"
                            >
                              <IconTrash className="w-4 h-4" />
                            </button>
                          </div>
                        </div>
                      ) : (
                        <div
                          onClick={() => handleSelectFileForStep(idx)}
                          className="border-2 border-dashed border-[#b9b7b2] hover:border-[#E76C5C] hover:bg-[#F4DAD6]/20 rounded-xl p-5 text-center cursor-pointer transition flex flex-col items-center justify-center gap-2 group bg-[#FAF9F8]"
                        >
                          <div className="p-2.5 rounded-full bg-white group-hover:bg-[#F4DAD6] text-[#E76C5C] transition">
                            <IconUpload className="w-6 h-6" />
                          </div>
                          <div>
                            <span className="font-semibold text-[13px] text-[#161C14] group-hover:text-[#C0513F]">
                              Перетащите изображение сюда или выберите файл
                            </span>
                            <p className="text-[11px] text-[#60635F] mt-0.5">
                              PNG, JPG, WebP для распознавания текста (Vision) или текстовый документ
                            </p>
                          </div>
                          
                          <div className="flex items-center gap-2 mt-1">
                            <button
                              type="button"
                              onClick={(e) => handleClipboardButtonClick(idx, e)}
                              className="px-3.5 py-1.5 bg-white border border-[#ACADA9] hover:border-[#E76C5C] hover:bg-[#FAF9F8] rounded-xl text-[12px] font-semibold text-[#161C14] flex items-center gap-1.5 shadow-2xs transition"
                            >
                              <IconCopy className="w-3.5 h-3.5 text-[#E76C5C]" />
                              <span>Вставить из буфера обмена (Ctrl+V)</span>
                            </button>
                          </div>

                          <input
                            type="file"
                            onChange={(e) => handleBrowserFileForStep(idx, e)}
                            accept="image/*,.txt,.md,.csv,.json"
                            className="hidden"
                            id={`fileInput_${idx}`}
                          />
                        </div>
                      )}
                    </div>
                  )}

                  {/* 3. AI STEP: Output result preview & prompt info */}
                  {isAI && (
                    <div className="space-y-2.5">
                      {/* Prompt template preview (collapsible or neat) */}
                      <div className="text-[11.5px] text-[#60635F] bg-[#FAF9F8] p-2.5 rounded-xl border border-[#ACADA9]/40 font-mono">
                        <span className="font-bold text-[#4a4d47]">Инструкция: </span>
                        <span>{step.prompt?.slice(0, 160)}...</span>
                      </div>

                      {/* Result Box (Spacious 500px) */}
                      {result && isExpanded && (
                        <div className="border border-[#ACADA9]/70 rounded-xl overflow-hidden bg-white shadow-2xs">
                          <div className="px-3.5 py-2 bg-[#FAF9F8] border-b border-[#ACADA9]/50 flex items-center justify-between text-[11px] font-semibold text-[#60635F]">
                            <span>Результат Шага {stepNum}:</span>
                            <button
                              onClick={() => {
                                navigator.clipboard.writeText(result);
                                addLog('ok', `Шаг ${stepNum} скопирован в буфер`);
                              }}
                              className="hover:underline flex items-center gap-1 text-[#161C14]"
                            >
                              <IconCopy className="w-3 h-3" />
                              <span>Копировать результат этого шага</span>
                            </button>
                          </div>
                          <div className="p-4 min-h-[200px] max-h-[500px] overflow-y-auto text-[#161C14] bg-[#FAF9F8]">
                            <CopyableContent content={result} />
                          </div>
                        </div>
                      )}
                    </div>
                  )}

                </div>
              </div>
            );
          })}
        </div>

        {/* Section: Final Result Output (Enlarged) */}
        {finalResult && (
          <div className="bg-[#FFFFFF] border-2 border-[#5CA354] rounded-2xl p-6 shadow-md space-y-4 animate-pop">
            <div className="flex items-center justify-between border-b border-[#ACADA9]/40 pb-3">
              <div className="flex items-center gap-2">
                <span className="w-6 h-6 rounded-lg bg-[#5CA354] text-white flex items-center justify-center font-bold text-xs">
                  ✓
                </span>
                <h2 className="font-bold text-[15px] text-[#161C14]">Итоговый результат пайплайна</h2>
              </div>
              <div className="flex items-center gap-2">
                <button
                  onClick={handleCopyResult}
                  className="px-3.5 py-1.5 border border-[#ACADA9] rounded-xl text-[12px] font-semibold text-[#161C14] hover:bg-[#FAF9F8] flex items-center gap-1.5 transition"
                >
                  {copied ? (
                    <>
                      <IconCheck className="w-3.5 h-3.5 text-[#5CA354]" />
                      <span>Скопировано в буфер!</span>
                    </>
                  ) : (
                    <>
                      <IconCopy className="w-3.5 h-3.5" />
                      <span>Копировать результат</span>
                    </>
                  )}
                </button>
              </div>
            </div>

            {/* Rendered Markdown with generous height and interactive copy tags */}
            <div className="p-6 bg-[#FAF9F8] border border-[#ACADA9] rounded-xl text-[13.5px] leading-relaxed min-h-[300px] max-h-[650px] overflow-y-auto text-[#161C14]">
              <CopyableContent content={finalResult} />
            </div>

            {/* Follow-up reply box */}
            <div className="mt-4 p-4 bg-[#FAF9F8] border border-[#ACADA9] rounded-xl space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <IconSpark className="w-4 h-4 text-[#E76C5C]" />
                  <span className="font-bold text-[13px] text-[#161C14]">
                    Ответить модели / Докинуть данные в ответ:
                  </span>
                </div>
                <span className="text-[11px] text-[#60635F]">
                  Модель продолжит работу с учетом предыдущего результата
                </span>
              </div>

              <textarea
                value={followUpText}
                onChange={(e) => setFollowUpText(e.target.value)}
                placeholder="Введите ваш ответ, правки или уточнения..."
                rows={4}
                className="w-full p-3 border border-[#ACADA9] rounded-xl text-[12.5px] font-mono bg-white focus:outline-none focus:border-[#E76C5C] focus:ring-2 focus:ring-[#E76C5C]/20 transition resize-y leading-relaxed"
              />

              <div className="flex items-center justify-between flex-wrap gap-2">
                {followUpHistory.length > 0 && onUpdateProject && (
                  <button
                    onClick={handleSaveFollowUpAsStep}
                    className="px-3 py-1.5 border border-[#ACADA9] rounded-lg text-[11.5px] font-semibold text-[#161C14] hover:bg-white transition flex items-center gap-1.5"
                  >
                    <span>➕ Сохранить этот ответ как Шаг {project.steps.length + 1} в пайплайн</span>
                  </button>
                )}
                
                <div className="ml-auto flex items-center gap-2">
                  <button
                    onClick={() => executePipelineFrom(0)}
                    className="px-3.5 py-2 border border-[#ACADA9] bg-white text-[12px] font-semibold rounded-xl hover:bg-[#FAF9F8] transition"
                  >
                    Запустить сначала
                  </button>
                  <button
                    onClick={handleSendFollowUp}
                    disabled={isFollowUpRunning || !followUpText.trim()}
                    className="px-5 py-2 bg-[#E76C5C] text-white text-[12.5px] font-bold rounded-xl hover:bg-[#C0513F] disabled:opacity-50 transition flex items-center gap-2 shadow-xs"
                  >
                    {isFollowUpRunning ? (
                      <>
                        <IconSpinner className="w-3.5 h-3.5" />
                        <span>Обработка ответа...</span>
                      </>
                    ) : (
                      <>
                        <IconPlay className="w-3.5 h-3.5" />
                        <span>Отправить в ответ модели</span>
                      </>
                    )}
                  </button>
                </div>
              </div>
            </div>

          </div>
        )}

        </div>
      </div>

    </div>
  );
};
