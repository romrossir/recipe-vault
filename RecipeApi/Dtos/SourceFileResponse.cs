namespace RecipeApi.Dtos;

public sealed record SourceFileResponse(
    string Id,
    string OriginalFileName,
    string ContentType,
    DateTime UploadedAt);
