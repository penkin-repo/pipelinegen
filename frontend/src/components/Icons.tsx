import React from 'react';

export const IconPlus: React.FC<{ className?: string }> = ({ className = "w-3.5 h-3.5" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round">
    <path d="M8 2.5v11M2.5 8h11"/>
  </svg>
);

export const IconGear: React.FC<{ className?: string }> = ({ className = "w-4 h-4" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="none" stroke="currentColor" strokeWidth="1.4">
    <circle cx="8" cy="8" r="2.2"/>
    <path d="M8 1.5v1.8M8 12.7v1.8M14.5 8h-1.8M3.3 8H1.5M12.6 3.4l-1.3 1.3M4.7 11.3l-1.3 1.3M12.6 12.6l-1.3-1.3M4.7 4.7 3.4 3.4"/>
  </svg>
);

export const IconPlay: React.FC<{ className?: string }> = ({ className = "w-3.5 h-3.5" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="currentColor">
    <path d="M5 3.2 13 8 5 12.8z" />
  </svg>
);

export const IconBook: React.FC<{ className?: string }> = ({ className = "w-3.5 h-3.5" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="none" stroke="currentColor" strokeWidth="1.4">
    <path d="M2 2.5h5.5a1.5 1.5 0 0 1 1.5 1.5v9a1 1 0 0 0-1-1H2zM14 2.5h-5.5a1.5 1.5 0 0 0-1.5 1.5v9a1 1 0 0 1 1-1H14z"/>
  </svg>
);

export const IconCopy: React.FC<{ className?: string }> = ({ className = "w-3.5 h-3.5" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="none" stroke="currentColor" strokeWidth="1.4">
    <rect x="5.5" y="5.5" width="8" height="8" rx="1.5"/>
    <path d="M10.5 3.5H4a1.5 1.5 0 0 0-1.5 1.5v5"/>
  </svg>
);

export const IconEdit: React.FC<{ className?: string }> = ({ className = "w-3.5 h-3.5" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round">
    <path d="M11.2 2.8a1.5 1.5 0 0 1 2.1 0l1.4 1.4a1.5 1.5 0 0 1 0 2.1l-8 8-3.3.4.4-3.3z"/>
  </svg>
);

export const IconTrash: React.FC<{ className?: string }> = ({ className = "w-3.5 h-3.5" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round">
    <path d="M2.5 4h11M6.5 4V2.8a1 1 0 0 1 1-1h1a1 1 0 0 1 1 1V4M4.5 4l.7 8.5a1 1 0 0 0 1 .9h3.6a1 1 0 0 0 1-.9L11.5 4"/>
  </svg>
);

export const IconClose: React.FC<{ className?: string }> = ({ className = "w-3.5 h-3.5" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round">
    <path d="M3.5 3.5l9 9M12.5 3.5l-9 9"/>
  </svg>
);

export const IconUpload: React.FC<{ className?: string }> = ({ className = "w-6 h-6" }) => (
  <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
    <path d="M12 16V4m0 0L7.5 8.5M12 4l4.5 4.5M4 17v2.5A1.5 1.5 0 0 0 5.5 21h13a1.5 1.5 0 0 0 1.5-1.5V17"/>
  </svg>
);

export const IconFile: React.FC<{ className?: string }> = ({ className = "w-4 h-4" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinejoin="round">
    <path d="M9 1.8H4.5a1 1 0 0 0-1 1v10.4a1 1 0 0 0 1 1h7a1 1 0 0 0 1-1V5z"/>
    <path d="M9 1.8V5h4.2"/>
  </svg>
);

export const IconImage: React.FC<{ className?: string }> = ({ className = "w-4 h-4" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="none" stroke="currentColor" strokeWidth="1.4">
    <rect x="1.8" y="2.6" width="12.4" height="10.8" rx="1.6"/>
    <circle cx="5.6" cy="6.4" r="1.1"/>
    <path d="M2.5 12.5 6 8.8l2.2 2.3 2.3-2.3 3.5 3.7"/>
  </svg>
);

export const IconSpark: React.FC<{ className?: string }> = ({ className = "w-3.5 h-3.5" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="currentColor">
    <path d="M8 1 9.6 6.4 15 8l-5.4 1.6L8 15l-1.6-5.4L1 8l5.4-1.6z"/>
  </svg>
);

export const IconCheck: React.FC<{ className?: string }> = ({ className = "w-3.5 h-3.5" }) => (
  <svg viewBox="0 0 16 16" className={className} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M3 8.5 6.5 12 13 4.5"/>
  </svg>
);

export const IconSpinner: React.FC<{ className?: string }> = ({ className = "w-4 h-4" }) => (
  <svg className={`animate-spin ${className}`} viewBox="0 0 24 24" fill="none">
    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
  </svg>
);

export const IconRocket: React.FC<{ className?: string }> = ({ className = "w-4 h-4" }) => (
  <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
    <path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z"/>
    <path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z"/>
    <path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0"/>
    <path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5"/>
  </svg>
);

export const IconWrench: React.FC<{ className?: string }> = ({ className = "w-4 h-4" }) => (
  <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
    <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/>
  </svg>
);
