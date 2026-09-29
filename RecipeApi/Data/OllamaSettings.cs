namespace RecipeApi.Data;

public sealed class OllamaSettings
{
    public required string BaseUrl { get; init; }

    public required string EmbeddingModel { get; init; }

    public required string LlmModel { get; init; }
}
