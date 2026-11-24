import React, { useState, useEffect } from 'react';
import { IconSpark, IconPlay } from './Icons';

interface HumanInputModalProps {
  isOpen: boolean;
  stepNumber: number;
  stepTitle: string;
  initialValue: string;
  instruction?: string;
  onSubmit: (userText: string) => void;
}

export const HumanInputModal: React.FC<HumanInputModalProps> = ({
  isOpen,
  stepNumber,
  stepTitle,
  initialValue,
  instruction,
  onSubmit,
}) => {
  const [val, setVal] = useState(initialValue);

  useEffect(() => {
    setVal(initialValue);
  }, [initialValue]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-[#161C14]/50 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-[#FFFFFF] border-2 border-[#E76C5C] rounded-2xl w-full max-w-2xl max-h-[90vh] flex flex-col shadow-2xl animate-pop overflow-hidden">
        
        {/* Head */}
        <div className="flex items-center gap-3 px-5 py-4 bg-[#FAF9F8] border-b border-[#ACADA9]">
          <div className="w-8 h-8 rounded-xl bg-[#E76C5C] text-white flex items-center justify-center font-bold text-sm shadow-sm">
            {stepNumber}
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="font-bold text-[15px] text-[#161C14]">{stepTitle}</h2>
              <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-[#F4DAD6] text-[#C0513F] uppercase tracking-wide">
                Требуется ваш ввод / проверка
              </span>
            </div>
            <p className="text-[11.5px] text-[#60635F]">
              Пайплайн приостановлен для ручной корректировки данных перед отправкой следующей модели
            </p>
          </div>
        </div>

        {/* Body */}
        <div className="p-5 space-y-3 flex-1 overflow-y-auto">
          {instruction && (
            <div className="p-3 bg-[#F5EBCF] border border-[#e5d3a3] rounded-xl text-[12px] text-[#8a6a2b] font-medium flex items-center gap-2">
              <IconSpark className="w-4 h-4 shrink-0 text-[#E0A458]" />
              <span>{instruction}</span>
            </div>
          )}

          <div className="space-y-1.5">
            <label className="block text-[12px] font-bold text-[#4a4d47]">
              Данные для следующего шага (проверьте или отредактируйте):
            </label>
            <textarea
              value={val}
              onChange={(e) => setVal(e.target.value)}
              rows={9}
              placeholder="Введите или скорректируйте текст..."
              className="w-full p-3 border border-[#ACADA9] rounded-xl font-mono text-[12.5px] focus:outline-none focus:border-[#E76C5C] focus:ring-2 focus:ring-[#E76C5C]/20 transition leading-relaxed resize-y"
            />
          </div>
        </div>

        {/* Foot */}
        <div className="px-5 py-3.5 bg-[#FAF9F8] border-t border-[#ACADA9] flex items-center justify-between">
          <span className="text-[11.5px] text-[#60635F]">
            Нажмите «Продолжить», чтобы передать эти данные в следующий шаг
          </span>
          <button
            onClick={() => onSubmit(val)}
            className="px-5 py-2.5 bg-[#E76C5C] text-white font-bold text-[13px] rounded-xl hover:bg-[#C0513F] shadow-sm flex items-center gap-2 transition active:scale-98"
          >
            <IconPlay className="w-3.5 h-3.5" />
            <span>Продолжить пайплайн</span>
          </button>
        </div>

      </div>
    </div>
  );
};
