namespace RecipeApi.Data;

public sealed class MongoDbSettings
{
    public required string ConnectionString { get; init; }

    public required string DatabaseName { get; init; }

    public required string RecipesCollectionName { get; init; }

    public required string EmbeddingIndexName { get; init; }

    public required string SourceFilesCollectionName { get; init; }
}