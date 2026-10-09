import { useRef, useState } from 'react';
import { queryAgentStream } from './api/recipeApi';
import type { ChatMessage, RecipeSearchResponse } from './api/types';
import ChatThread from './components/ChatThread';
import SourceViewer from './components/SourceViewer';
import './App.css';

export default function App() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<RecipeSearchResponse | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  function addMessage(msg: ChatMessage) {
    setMessages((prev) => [...prev, msg]);
  }

  function updateLastAnswer(text: string) {
    setMessages((prev) => {
      const last = prev[prev.length - 1];
      if (last?.type === 'answer') {
        return [...prev.slice(0, -1), { type: 'answer', text: last.text + text }];
      }
      return [...prev, { type: 'answer', text }];
    });
  }

  async function handleSend(query: string) {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    addMessage({ type: 'user', text: query });
    setLoading(true);

    await queryAgentStream(query, {
      onToolCall: (tool) => {
        const labels: Record<string, string> = {
          search_semantic: 'Recherche sémantique',
          search_by_ingredients: 'Recherche par ingrédients',
          search_by_tags: 'Recherche par tags',
          search_combined: 'Recherche combinée',
        };
        addMessage({ type: 'status', text: labels[tool] ?? tool });
      },
      onSearchResults: (count) => {
        addMessage({ type: 'search_results', count });
      },
      onDelta: (text) => {
        updateLastAnswer(text);
      },
      onResults: (results) => {
        addMessage({ type: 'results', results });
      },
      onDone: () => {
        setLoading(false);
      },
      onError: (err) => {
        addMessage({ type: 'answer', text: `Erreur : ${err.message}` });
        setLoading(false);
      },
    }, controller.signal);
  }

  return (
    <div className="app">
      <div className="main-content">
        <div className="chat-panel">
          <ChatThread
            messages={messages}
            loading={loading}
            onSend={handleSend}
            onSelectRecipe={setSelected}
            selectedId={selected?.id ?? null}
            onReset={() => { setMessages([]); setSelected(null); }}
          />
        </div>

        <div className="viewer-panel">
          {selected ? (
            <SourceViewer recipe={selected} />
          ) : (
            <div className="source-viewer empty">
              <p>Sélectionnez une recette pour voir le document source.</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
