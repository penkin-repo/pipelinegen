import React from 'react';
import { LogEntry } from '../types';
import { IconClose } from './Icons';

interface LogDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  logs: LogEntry[];
  onClear: () => void;
}

export const LogDrawer: React.FC<LogDrawerProps> = ({
  isOpen,
  onClose,
  logs,
  onClear,
}) => {
  if (!isOpen) return null;

  return (
    <div className="bg-[#FAF9F8] border-t border-[#ACADA9] flex flex-col max-h-56 shrink-0 transition-all">
      <div className="flex items-center justify-between px-5 py-2 border-b border-[#ACADA9]/60 bg-white">
        <div className="flex items-center gap-2">
          <span className="font-bold text-[12px] text-[#161C14]">Журнал событий & API лог</span>
          <span className="text-[11px] text-[#60635F]">({logs.length} записей)</span>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={onClear}
            className="text-[11.5px] font-semibold text-[#60635F] hover:text-[#161C14] px-2 py-0.5 rounded hover:bg-[#FAF9F8] transition"
          >
            Очистить
          </button>
          <button
            onClick={onClose}
            className="p-1 text-[#60635F] hover:text-[#161C14] rounded transition"
          >
            <IconClose className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      <div className="p-3 overflow-y-auto space-y-1 font-mono text-[11.5px] flex-1">
        {logs.map((log) => (
          <div
            key={log.id}
            className={`flex items-start gap-2.5 px-2 py-1 rounded transition ${
              log.type === 'error'
                ? 'bg-[#F6E0DB]/60 text-[#C65D4E]'
                : log.type === 'ok'
                ? 'bg-[#D9ECD4]/40 text-[#4a7a3d]'
                : 'text-[#4a4d47]'
            }`}
          >
            <span
              className={`w-2 h-2 rounded-full shrink-0 mt-1 ${
                log.type === 'error'
                  ? 'bg-[#C65D4E]'
                  : log.type === 'ok'
                  ? 'bg-[#5CA354]'
                  : 'bg-[#E76C5C]'
              }`}
            />
            <span className="text-[#98968f] shrink-0">{log.time}</span>
            <span className="break-all">{log.message}</span>
          </div>
        ))}

        {logs.length === 0 && (
          <div className="text-center py-4 text-[#98968f] italic text-[11px]">
            Журнал пуст. Здесь будут отображаться события выполнения и ошибки API.
          </div>
        )}
      </div>
    </div>
  );
};
