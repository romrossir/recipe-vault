import type { RecipeSearchResponse } from '../api/types';
import RecipeCard from './RecipeCard';

interface ResultsListProps {
  answer: string;
  results: RecipeSearchResponse[];
  selectedId: string | null;
  onSelect: (recipe: RecipeSearchResponse) => void;
}

export default function ResultsList({ answer, results, selectedId, onSelect }: ResultsListProps) {
  if (!answer && results.length === 0) {
    return null;
  }

  return (
    <div className="results-list">
      {answer && (
        <div className="agent-answer">
          <p>{answer}</p>
        </div>
      )}

      {results.length === 0 && answer && (
        <p className="no-results">Aucune recette trouvée.</p>
      )}

      {results.map((recipe) => (
        <RecipeCard
          key={recipe.id}
          recipe={recipe}
          selected={recipe.id === selectedId}
          onSelect={() => onSelect(recipe)}
        />
      ))}
    </div>
  );
}
