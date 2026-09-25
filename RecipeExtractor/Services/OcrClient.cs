using System.Net.Http.Headers;
using System.Text.Json;

namespace RecipeExtractor.Services;

public class OcrClient : IOcrClient
{
    private readonly HttpClient _httpClient;

    public OcrClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> ExtractTextAsync(
        IFormFile image,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();

        await using var stream = image.OpenReadStream();

        using var imageContent = new StreamContent(stream);

        imageContent.Headers.ContentType =
            new MediaTypeHeaderValue(
                image.ContentType ?? "application/octet-stream");

        content.Add(
            imageContent,
            "image",
            image.FileName);

        var response = await _httpClient.PostAsync(
            "/ocr",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<OcrResponse>(
                cancellationToken);

        return result?.Text
            ?? throw new InvalidOperationException(
                "OCR service returned no text.");
    }

    private class OcrResponse
    {
        public string? Text { get; set; }
    }
}