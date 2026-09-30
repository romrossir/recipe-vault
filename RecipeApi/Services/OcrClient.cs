using System.Net.Http.Headers;

namespace RecipeApi.Services;

public sealed class OcrClient : IOcrClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OcrClient> _logger;

    public OcrClient(HttpClient httpClient, ILogger<OcrClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> ExtractTextAsync(string fileName, string contentType, Stream stream, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending '{FileName}' to OCR service.", fileName);

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "image", fileName);

        var response = await _httpClient.PostAsync("/ocr", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OCR service returned {StatusCode}: {Error}", (int)response.StatusCode, errorBody);
            throw new HttpRequestException($"OCR service returned {(int)response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<OcrResponse>(cancellationToken)
            ?? throw new InvalidOperationException("OCR service returned an empty response.");

        if (string.IsNullOrWhiteSpace(result.Text))
            throw new InvalidOperationException("OCR service returned no text.");

        _logger.LogInformation("OCR extracted {Length} characters from '{FileName}'.", result.Text.Length, fileName);
        return result.Text;
    }

    private sealed class OcrResponse
    {
        public string? Text { get; set; }
    }
}
