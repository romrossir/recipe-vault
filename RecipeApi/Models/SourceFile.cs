using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RecipeApi.Models;

public sealed class SourceFile
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public required string OriginalFileName { get; set; }

    public required string StoredFileName { get; set; }

    public required string ContentType { get; set; }

    public DateTime UploadedAt { get; set; }
}
