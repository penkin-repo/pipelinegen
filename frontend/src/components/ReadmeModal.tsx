import React, { useState } from 'react';
import { IconClose, IconCopy, IconCheck, IconBook } from './Icons';

interface ReadmeModalProps {
  isOpen: boolean;
  onClose: () => void;
  projectName: string;
  readmeText: string;
}

export const ReadmeModal: React.FC<ReadmeModalProps> = ({
  isOpen,
  onClose,
  projectName,
  readmeText,
}) => {
  const [copied, setCopied] = useState(false);

  if (!isOpen) return null;

  const handleCopy = () => {
    navigator.clipboard.writeText(readmeText);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="fixed inset-0 bg-[#161C14]/40 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-[#FFFFFF] border border-[#ACADA9] rounded-2xl w-full max-w-3xl max-h-[90vh] flex flex-col shadow-2xl animate-pop overflow-hidden">
        
        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-3.5 border-b border-[#ACADA9] bg-[#FAF9F8]">
          <div className="p-1.5 bg-[#F4DAD6] text-[#C0513F] rounded-lg">
            <IconBook className="w-5 h-5" />
          </div>
          <div>
            <h2 className="font-bold text-[15px] text-[#161C14]">
              README.md · {projectName || 'Проект'}
            </h2>
            <p className="text-[11px] text-[#60635F]">
              Автогенерируемая документация с цепочкой шагов и контекстом для ИИ
            </p>
          </div>
          <div className="ml-auto flex items-center gap-2">
            <button
              onClick={handleCopy}
              className="px-3 py-1.5 border border-[#ACADA9] rounded-xl text-[12px] font-semibold text-[#161C14] hover:bg-white flex items-center gap-1.5 transition"
            >
              {copied ? (
                <>
                  <IconCheck className="w-3.5 h-3.5 text-[#5CA354]" />
                  <span>Скопировано!</span>
                </>
              ) : (
                <>
                  <IconCopy className="w-3.5 h-3.5" />
                  <span>Копировать Markdown</span>
                </>
              )}
            </button>
            <button
              onClick={onClose}
              className="p-1.5 rounded-lg text-[#60635F] hover:bg-[#FAF9F8] hover:text-[#161C14] transition"
            >
              <IconClose className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Body */}
        <div className="p-5 overflow-y-auto flex-1 bg-[#FAF9F8]">
          <pre className="p-4 bg-white border border-[#ACADA9] rounded-xl text-[12.5px] leading-relaxed font-mono whitespace-pre-wrap word-break-break-word text-[#161C14]">
            {readmeText || '# Документация будет сгенерирована при первом сохранении...'}
          </pre>
        </div>

        {/* Foot */}
        <div className="px-5 py-3 border-t border-[#ACADA9] bg-[#FAF9F8] text-[11px] text-[#60635F]">
          Файл автоматически обновляется на диске в папке проекта <code>projects/{projectName}/README.md</code> через Go-метод <code>SaveProject</code>.
        </div>

      </div>
    </div>
  );
};
