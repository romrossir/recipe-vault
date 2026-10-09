export interface IngredientDto {
  name: string;
  quantity: string | null;
  unit: string | null;
}

export interface RecipeSearchResponse {
  id: string;
  title: string;
  author: string | null;
  prepTime: string | null;
  cookTime: string | null;
  servings: string | null;
  ingredients: IngredientDto[];
  tags: string[];
  sourceFileId: string | null;
  sourcePages: number[];
  score: number;
}

export interface AgentQueryResponse {
  answer: string;
  results: RecipeSearchResponse[];
}

export type ChatMessage =
  | { type: 'user'; text: string }
  | { type: 'status'; text: string }
  | { type: 'search_results'; count: number }
  | { type: 'answer'; text: string }
  | { type: 'results'; results: RecipeSearchResponse[] };
