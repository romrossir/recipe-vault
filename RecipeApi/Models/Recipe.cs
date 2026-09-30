using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RecipeApi.Models;

public sealed class Recipe
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public required string Title { get; set; }

    public List<Ingredient> Ingredients { get; set; } = [];

    public List<string> Steps { get; set; } = [];

    public string? SourceFileId { get; set; }

    public List<int> SourcePages { get; set; } = [];

    public string? SearchText { get; set; }

    public float[]? Embedding { get; set; }
}
