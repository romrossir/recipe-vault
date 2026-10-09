import { useEffect, useRef, useState, type FormEvent } from 'react';
import Markdown from 'react-markdown';
import type { ChatMessage, RecipeSearchResponse } from '../api/types';
import RecipeCard from './RecipeCard';

interface ChatThreadProps {
  messages: ChatMessage[];
  loading: boolean;
  onSend: (query: string) => void;
  onSelectRecipe: (recipe: RecipeSearchResponse) => void;
  selectedId: string | null;
  onReset: () => void;
}

export default function ChatThread({ messages, loading, onSend, onSelectRecipe, selectedId, onReset }: ChatThreadProps) {
  const [input, setInput] = useState('');
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const trimmed = input.trim();
    if (trimmed && !loading) {
      onSend(trimmed);
      setInput('');
    }
  }

  return (
    <div className="chat-thread">
      <div className="chat-messages">
        {messages.length === 0 && (
          <div className="chat-welcome">
            <h1>Recipe Vault</h1>
            <p>Posez une question pour rechercher des recettes.</p>
          </div>
        )}

        {messages.map((msg, i) => (
          <div key={i} className={`chat-msg chat-msg-${msg.type}`}>
            {msg.type === 'user' && (
              <div className="chat-bubble user-bubble">{msg.text}</div>
            )}
            {msg.type === 'status' && (
              <div className="chat-status">{msg.text}...</div>
            )}
            {msg.type === 'search_results' && (
              <div className="chat-search-results">
                {msg.count} recette{msg.count !== 1 ? 's' : ''} trouvée{msg.count !== 1 ? 's' : ''}
              </div>
            )}
            {msg.type === 'answer' && (
              <div className="chat-bubble agent-bubble"><Markdown>{msg.text}</Markdown></div>
            )}
            {msg.type === 'results' && (
              <div className="chat-results">
                {msg.results.map((r) => (
                  <RecipeCard
                    key={r.id}
                    recipe={r}
                    selected={r.id === selectedId}
                    onSelect={() => onSelectRecipe(r)}
                  />
                ))}
              </div>
            )}
          </div>
        ))}

        {loading && messages[messages.length - 1]?.type !== 'status' && (
          <div className="chat-msg chat-msg-status">
            <div className="chat-status">Réflexion...</div>
          </div>
        )}

        <div ref={bottomRef} />
      </div>

      <div className="chat-bar">
        {messages.length > 0 && (
          <button className="chat-reset" onClick={onReset} disabled={loading} title="Nouveau chat">
            +
          </button>
        )}
      <form className="chat-input" onSubmit={handleSubmit}>
        <input
          type="text"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Rechercher des recettes..."
          disabled={loading}
        />
        <button type="submit" disabled={loading || !input.trim()}>
          Envoyer
        </button>
      </form>
      </div>
    </div>
  );
}
