import React, { useState, useMemo } from 'react';
import { marked } from 'marked';
import { IconCopy, IconCheck, IconSpark } from './Icons';

marked.setOptions({
  gfm: true,
  breaks: true,
});

interface CopyableContentProps {
  content: string;
  className?: string;
}

function escapeHtml(str: string): string {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

export const CopyableContent: React.FC<CopyableContentProps> = ({ content, className = '' }) => {
  const [copiedAll, setCopiedAll] = useState(false);
  const [copiedChipIndex, setCopiedChipIndex] = useState<number | null>(null);

  // If content starts with frontmatter or is code, default to rendered with quick toggle
  const [viewMode, setViewMode] = useState<'rendered' | 'raw'>('rendered');

  // Extract all <copy>...</copy> items
  const copyItems = useMemo(() => {
    const items: string[] = [];
    const regex = /<copy>([\s\S]*?)<\/copy>/gi;
    let match: RegExpExecArray | null;
    while ((match = regex.exec(content)) !== null) {
      const val = match[1]?.trim();
      if (val && !items.includes(val)) {
        items.push(val);
      }
    }
    return items;
  }, [content]);

  // Pre-process markdown to convert <copy>...</copy> into interactive HTML buttons
  const processedHtml = useMemo(() => {
    // Replace <copy>...</copy> with styled copyable buttons
    const replaced = content.replace(/<copy>([\s\S]*?)<\/copy>/gi, (_, innerText) => {
      const trimmed = innerText.trim();
      const escapedAttr = escapeHtml(trimmed);
      const escapedText = escapeHtml(trimmed);
      return `<button type="button" class="copy-tag-chip inline-flex items-center gap-1.5 px-2.5 py-0.5 my-0.5 rounded-lg text-[12px] font-mono font-medium bg-[#F5EBCF] border border-[#e5d3a3] text-[#8a6a2b] hover:bg-[#E0A458] hover:text-white transition cursor-pointer select-none active:scale-95 shadow-2xs group align-baseline" data-copy-text="${escapedAttr}" title="Нажмите, чтобы скопировать: ${escapedAttr}"><span class="copy-tag-label">${escapedText}</span><svg class="w-3 h-3 opacity-60 group-hover:opacity-100 shrink-0 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"></path></svg></button>`;
    });

    try {
      return marked.parse(replaced) as string;
    } catch {
      return escapeHtml(replaced);
    }
  }, [content]);

  // Copy individual tag when clicked anywhere inside the rendered html
  const handleContainerClick = (e: React.MouseEvent<HTMLDivElement>) => {
    const target = (e.target as HTMLElement).closest('.copy-tag-chip') as HTMLElement | null;
    if (target) {
      e.preventDefault();
      e.stopPropagation();
      const textToCopy = target.getAttribute('data-copy-text');
      if (textToCopy) {
        navigator.clipboard.writeText(textToCopy);

        target.classList.add('bg-[#5CA354]', 'text-white', 'border-[#5CA354]');
        target.classList.remove('bg-[#F5EBCF]', 'text-[#8a6a2b]', 'border-[#e5d3a3]');
        const label = target.querySelector('.copy-tag-label');
        const origText = label?.textContent;
        if (label) label.textContent = '✓ Скопировано!';

        setTimeout(() => {
          target.classList.remove('bg-[#5CA354]', 'text-white', 'border-[#5CA354]');
          target.classList.add('bg-[#F5EBCF]', 'text-[#8a6a2b]', 'border-[#e5d3a3]');
          if (label && origText) label.textContent = origText;
        }, 1200);
      }
    }
  };

  const copySingleItem = (text: string, idx: number) => {
    navigator.clipboard.writeText(text);
    setCopiedChipIndex(idx);
    setTimeout(() => setCopiedChipIndex(null), 1200);
  };

  const copyAllItemsList = () => {
    if (copyItems.length === 0) return;
    navigator.clipboard.writeText(copyItems.join('\n'));
    setCopiedAll(true);
    setTimeout(() => setCopiedAll(false), 1500);
  };

  return (
    <div className={`space-y-3 ${className}`}>
      {/* Header View Switcher & Stats */}
      <div className="flex items-center justify-between border-b border-[#ACADA9]/30 pb-2 mb-1 flex-wrap gap-2">
        <div className="flex items-center p-0.5 bg-[#FAF9F8] border border-[#ACADA9]/60 rounded-lg">
          <button
            type="button"
            onClick={() => setViewMode('rendered')}
            className={`px-2.5 py-1 rounded-md text-[11px] font-semibold transition cursor-pointer ${
              viewMode === 'rendered'
                ? 'bg-white text-[#161C14] shadow-2xs font-bold'
                : 'text-[#60635F] hover:text-[#161C14]'
            }`}
          >
            Форматированный вид
          </button>
          <button
            type="button"
            onClick={() => setViewMode('raw')}
            className={`px-2.5 py-1 rounded-md text-[11px] font-semibold transition cursor-pointer ${
              viewMode === 'raw'
                ? 'bg-white text-[#E76C5C] shadow-2xs font-bold'
                : 'text-[#60635F] hover:text-[#161C14]'
            }`}
          >
            Исходный текст / Код
          </button>
        </div>

        <span className="text-[11px] text-[#60635F] font-mono">
          {content.length} симв.
        </span>
      </div>

      {/* Quick Access Toolbar for detected <copy> tags */}
      {copyItems.length > 0 && (
        <div className="p-3 bg-[#F5EBCF]/60 border border-[#e5d3a3] rounded-xl space-y-2">
          <div className="flex items-center justify-between flex-wrap gap-2">
            <div className="flex items-center gap-1.5 text-[12px] font-bold text-[#8a6a2b]">
              <IconSpark className="w-3.5 h-3.5 text-[#E0A458]" />
              <span>Найдено для копирования ({copyItems.length}):</span>
            </div>
            <button
              type="button"
              onClick={copyAllItemsList}
              className="px-2.5 py-1 bg-white border border-[#e5d3a3] hover:bg-[#E0A458] hover:text-white rounded-lg text-[11px] font-semibold text-[#8a6a2b] flex items-center gap-1.5 shadow-2xs transition cursor-pointer"
            >
              {copiedAll ? (
                <>
                  <IconCheck className="w-3.5 h-3.5 text-[#5CA354]" />
                  <span>Все {copyItems.length} скопированы!</span>
                </>
              ) : (
                <>
                  <IconCopy className="w-3.5 h-3.5" />
                  <span>Скопировать все списком</span>
                </>
              )}
            </button>
          </div>

          <div className="flex flex-wrap gap-1.5 pt-1">
            {copyItems.map((item, idx) => (
              <button
                key={idx}
                type="button"
                onClick={() => copySingleItem(item, idx)}
                className={`px-2.5 py-1 rounded-lg text-[11.5px] font-mono border transition flex items-center gap-1.5 cursor-pointer shadow-2xs active:scale-95 ${
                  copiedChipIndex === idx
                    ? 'bg-[#5CA354] text-white border-[#5CA354]'
                    : 'bg-white border-[#e5d3a3] text-[#8a6a2b] hover:bg-[#E0A458] hover:text-white'
                }`}
                title="Кликните для копирования"
              >
                <span>{item}</span>
                {copiedChipIndex === idx ? (
                  <IconCheck className="w-3 h-3 text-white" />
                ) : (
                  <IconCopy className="w-3 h-3 opacity-60" />
                )}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Content Rendering: Raw or Formatted */}
      {viewMode === 'raw' ? (
        <pre className="p-4 bg-[#FAF9F8] border border-[#ACADA9]/60 rounded-xl text-[12.5px] font-mono leading-relaxed text-[#161C14] whitespace-pre-wrap overflow-x-auto select-text min-h-[220px]">
          {content}
        </pre>
      ) : (
        <div
          className="prose-custom"
          onClick={handleContainerClick}
          dangerouslySetInnerHTML={{ __html: processedHtml }}
        />
      )}
    </div>
  );
};
