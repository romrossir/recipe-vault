using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RecipeApi.Models;

[BsonIgnoreExtraElements]
public sealed class Recipe
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public required string Title { get; set; }

    public string? Author { get; set; }

    public string? PrepTime { get; set; }

    public string? CookTime { get; set; }

    public string? Servings { get; set; }

    public List<Ingredient> Ingredients { get; set; } = [];

    public List<string> Tags { get; set; } = [];

    public string? SourceFileId { get; set; }

    public List<int> SourcePages { get; set; } = [];

    public string? SearchText { get; set; }

    public float[]? Embedding { get; set; }
}
