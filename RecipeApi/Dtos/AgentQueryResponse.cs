namespace RecipeApi.Dtos;

public sealed record AgentQueryResponse(
    string Answer,
    IReadOnlyList<RecipeSearchResponse> Results);
