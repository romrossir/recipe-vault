import type { RecipeSearchResponse } from '../api/types';

interface RecipeCardProps {
  recipe: RecipeSearchResponse;
  selected: boolean;
  onSelect: () => void;
}

export default function RecipeCard({ recipe, selected, onSelect }: RecipeCardProps) {
  return (
    <div
      className={`recipe-card ${selected ? 'selected' : ''}`}
      onClick={onSelect}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === 'Enter' && onSelect()}
    >
      <div className="recipe-card-header">
        <h3>{recipe.title}</h3>
        {recipe.score < 1 && (
          <span className="score">{Math.round(recipe.score * 100)}%</span>
        )}
      </div>

      {recipe.author && <p className="author">{recipe.author}</p>}

      <div className="recipe-meta">
        {recipe.prepTime && <span>Prépa: {recipe.prepTime}</span>}
        {recipe.cookTime && <span>Cuisson: {recipe.cookTime}</span>}
        {recipe.servings && <span>{recipe.servings}</span>}
      </div>

      {recipe.tags.length > 0 && (
        <div className="tags">
          {recipe.tags.map((tag) => (
            <span key={tag} className="tag">{tag}</span>
          ))}
        </div>
      )}

      <p className="ingredient-count">
        {recipe.ingredients.length} ingrédient{recipe.ingredients.length !== 1 ? 's' : ''}
      </p>
    </div>
  );
}
