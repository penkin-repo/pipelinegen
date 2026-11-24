export interface ModelConfig {
  id: string;
  name: string;
  provider: string;
  isVision: boolean;
  status: 'verified' | 'error' | 'untested';
  statusMsg?: string;
  testedAt?: string;
}

export interface AppConfig {
  apiKey: string;
  defaultModel: string;
  models: ModelConfig[];
}

export interface StepDefinition {
  id: string;
  type: 'text' | 'file' | 'ai';
  title: string;
  content: string;
  fileName?: string;
  fileData?: string;
  fileType?: string;
  model?: string;
  prompt?: string;
  result?: string;
  status?: 'idle' | 'running' | 'done' | 'error';
  requireUser?: boolean;
  userPrompt?: string;
  instruction?: string;
}

export interface Project {
  id: string;
  name: string;
  description: string;
  updatedAt: string;
  createdAt: string;
  steps: StepDefinition[];
}

export interface LogEntry {
  id: string;
  time: string;
  type: 'info' | 'ok' | 'error';
  message: string;
}
