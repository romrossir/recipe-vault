import type { AgentQueryResponse, RecipeSearchResponse } from './types';

export async function queryAgent(query: string): Promise<AgentQueryResponse> {
  const response = await fetch('/api/recipes/agent', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ query }),
  });

  if (!response.ok) {
    throw new Error(`Agent query failed: ${response.statusText}`);
  }

  return response.json();
}

export interface AgentStreamCallbacks {
  onToolCall: (tool: string) => void;
  onSearchResults: (count: number) => void;
  onDelta: (text: string) => void;
  onResults: (results: RecipeSearchResponse[]) => void;
  onDone: () => void;
  onError: (error: Error) => void;
}

export async function queryAgentStream(
  query: string,
  callbacks: AgentStreamCallbacks,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch('/api/recipes/agent/stream', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ query }),
    signal,
  });

  if (!response.ok) {
    callbacks.onError(new Error(`Agent query failed: ${response.statusText}`));
    return;
  }

  const reader = response.body?.getReader();
  if (!reader) {
    callbacks.onError(new Error('No response body'));
    return;
  }

  const decoder = new TextDecoder();
  let buffer = '';

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop() ?? '';

      let eventType: string | null = null;

      for (const line of lines) {
        if (line.startsWith('event: ')) {
          eventType = line.slice(7);
        } else if (line.startsWith('data: ') && eventType) {
          const data = JSON.parse(line.slice(6));
          switch (eventType) {
            case 'tool_call':
              callbacks.onToolCall(data.tool);
              break;
            case 'search_results':
              callbacks.onSearchResults(data.count);
              break;
            case 'delta':
              callbacks.onDelta(data.text);
              break;
            case 'results':
              callbacks.onResults(data as RecipeSearchResponse[]);
              break;
            case 'done':
              callbacks.onDone();
              break;
          }
          eventType = null;
        }
      }
    }
  } catch (err) {
    if ((err as Error).name !== 'AbortError') {
      callbacks.onError(err instanceof Error ? err : new Error(String(err)));
    }
  }
}

export function getSourceContentUrl(sourceFileId: string): string {
  return `/api/sources/${sourceFileId}/content`;
}
