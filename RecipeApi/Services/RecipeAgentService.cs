using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RecipeApi.Data;
using RecipeApi.Models;

namespace RecipeApi.Services;

public sealed class RecipeAgentService : IRecipeAgentService
{
    private const int MaxIterations = 5;

    private const string SystemPrompt = """
        Tu es un assistant spécialisé dans la recherche de recettes de cuisine.
        Tu disposes de plusieurs outils pour trouver des recettes dans la base de données.

        Choisis l'outil le plus adapté à la demande de l'utilisateur :
        - search_semantic : recherche par similarité sémantique (texte libre, noms de plats, descriptions)
        - search_by_ingredients : recherche par ingrédients spécifiques
        - search_by_tags : recherche par catégories/tags (ex: dessert, viennoiserie, chocolat)
        - search_combined : combine une recherche sémantique avec un filtre par ingrédients et/ou tags

        Règles strictes :
        - Utilise toujours au moins un outil avant de répondre.
        - Ne mentionne QUE les recettes retournées par les outils. N'invente jamais de recette.
        - Si aucun résultat n'est trouvé, dis-le clairement.
        - Réponds en français de manière concise (2-3 phrases max).
        - Ne liste PAS les ingrédients ni les détails des recettes, l'interface s'en charge.
        - Indique simplement quelles recettes correspondent et pourquoi.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly object[] ToolDefinitions =
    [
        new
        {
            type = "function",
            function = new
            {
                name = "search_semantic",
                description = "Recherche sémantique par texte libre. Utilise les embeddings pour trouver les recettes les plus similaires à la requête.",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["query"] = new { type = "string", description = "La requête de recherche en texte libre" },
                        ["limit"] = new { type = "integer", description = "Nombre maximum de résultats (défaut: 5)" }
                    },
                    required = new[] { "query" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "search_by_ingredients",
                description = "Recherche des recettes contenant des ingrédients spécifiques.",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["ingredients"] = new { type = "array", items = new { type = "string" }, description = "Liste des ingrédients à chercher" },
                        ["match_all"] = new { type = "boolean", description = "true = tous les ingrédients requis, false = au moins un (défaut: false)" },
                        ["limit"] = new { type = "integer", description = "Nombre maximum de résultats (défaut: 10)" }
                    },
                    required = new[] { "ingredients" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "search_by_tags",
                description = "Recherche des recettes par tags/catégories (ex: dessert, viennoiserie, chocolat, tarte).",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["tags"] = new { type = "array", items = new { type = "string" }, description = "Liste des tags à chercher" },
                        ["limit"] = new { type = "integer", description = "Nombre maximum de résultats (défaut: 10)" }
                    },
                    required = new[] { "tags" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "search_combined",
                description = "Combine une recherche sémantique avec un filtre par ingrédients et/ou tags. Utile quand l'utilisateur veut un type de plat précis avec des contraintes.",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["query"] = new { type = "string", description = "La requête sémantique" },
                        ["ingredients"] = new { type = "array", items = new { type = "string" }, description = "Ingrédients à filtrer (optionnel)" },
                        ["tags"] = new { type = "array", items = new { type = "string" }, description = "Tags à filtrer (optionnel)" },
                        ["limit"] = new { type = "integer", description = "Nombre maximum de résultats (défaut: 5)" }
                    },
                    required = new[] { "query" }
                }
            }
        }
    ];

    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly IRecipeService _recipeService;
    private readonly ILogger<RecipeAgentService> _logger;

    public RecipeAgentService(
        HttpClient httpClient,
        OllamaSettings settings,
        IRecipeService recipeService,
        ILogger<RecipeAgentService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _recipeService = recipeService;
        _logger = logger;
    }

    public async Task<RecipeAgentResult> QueryAsync(string query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Agent query: '{Query}'", query);

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = SystemPrompt },
            new() { Role = "user", Content = query }
        };

        var allResults = new Dictionary<string, RecipeSearchResult>();

        for (var i = 0; i < MaxIterations; i++)
        {
            var response = await CallOllamaAsync(messages, cancellationToken);

            if (response.Message.ToolCalls is not { Count: > 0 })
            {
                _logger.LogInformation("Agent finished after {Iterations} iteration(s).", i + 1);
                return new RecipeAgentResult(
                    response.Message.Content ?? "",
                    allResults.Values.ToList());
            }

            messages.Add(response.Message);

            foreach (var toolCall in response.Message.ToolCalls)
            {
                var toolName = toolCall.Function.Name;
                var args = toolCall.Function.Arguments;

                _logger.LogInformation("Agent calling tool '{Tool}' with args: {Args}", toolName, args);

                var results = await DispatchToolCallAsync(toolName, args, cancellationToken);

                foreach (var r in results)
                {
                    allResults.TryAdd(r.Recipe.Id!, r);
                }

                var toolResponse = results.Select(r => new
                {
                    id = r.Recipe.Id,
                    title = r.Recipe.Title,
                    author = r.Recipe.Author,
                    tags = r.Recipe.Tags,
                    ingredients = r.Recipe.Ingredients.Select(ing => ing.Name),
                    score = r.Score
                });

                messages.Add(new ChatMessage
                {
                    Role = "tool",
                    Content = JsonSerializer.Serialize(toolResponse, JsonOptions)
                });
            }
        }

        _logger.LogWarning("Agent reached max iterations ({Max}).", MaxIterations);

        var finalResponse = await CallOllamaAsync(messages, cancellationToken, includeTools: false);
        return new RecipeAgentResult(
            finalResponse.Message.Content ?? "",
            allResults.Values.ToList());
    }

    public async IAsyncEnumerable<AgentStreamEvent> QueryStreamAsync(
        string query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Agent stream query: '{Query}'", query);

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = SystemPrompt },
            new() { Role = "user", Content = query }
        };

        var allResults = new Dictionary<string, RecipeSearchResult>();
        var toolPhaseComplete = false;

        for (var i = 0; i < MaxIterations; i++)
        {
            var response = await CallOllamaAsync(messages, cancellationToken);

            if (response.Message.ToolCalls is not { Count: > 0 })
            {
                toolPhaseComplete = true;
                break;
            }

            messages.Add(response.Message);

            foreach (var toolCall in response.Message.ToolCalls)
            {
                var toolName = toolCall.Function.Name;
                var args = toolCall.Function.Arguments;

                yield return new ToolCallStreamEvent(toolName);

                var results = await DispatchToolCallAsync(toolName, args, cancellationToken);

                foreach (var r in results)
                    allResults.TryAdd(r.Recipe.Id!, r);

                var toolResponse = results.Select(r => new
                {
                    id = r.Recipe.Id,
                    title = r.Recipe.Title,
                    author = r.Recipe.Author,
                    tags = r.Recipe.Tags,
                    ingredients = r.Recipe.Ingredients.Select(ing => ing.Name),
                    score = r.Score
                });

                messages.Add(new ChatMessage
                {
                    Role = "tool",
                    Content = JsonSerializer.Serialize(toolResponse, JsonOptions)
                });
            }

            if (allResults.Count > 0)
                yield return new SearchResultsStreamEvent(allResults.Count);
        }

        if (!toolPhaseComplete)
            _logger.LogWarning("Agent reached max iterations ({Max}), streaming final answer.", MaxIterations);

        await foreach (var token in StreamOllamaAsync(messages, cancellationToken))
        {
            yield return new DeltaStreamEvent(token);
        }

        if (allResults.Count > 0)
            yield return new FinalResultsStreamEvent(allResults.Values.ToList());

        yield return new DoneStreamEvent();
    }

    private async IAsyncEnumerable<string> StreamOllamaAsync(
        List<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest
        {
            Model = _settings.LlmModel,
            Messages = messages,
            Stream = true,
            Tools = null
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        using var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions);
            if (chunk?.Message.Content is { Length: > 0 } content)
                yield return content;
        }
    }

    private async Task<IReadOnlyList<RecipeSearchResult>> DispatchToolCallAsync(
        string toolName, JsonElement args, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "search_semantic" => await SearchSemanticAsync(args, cancellationToken),
            "search_by_ingredients" => await SearchByIngredientsAsync(args, cancellationToken),
            "search_by_tags" => await SearchByTagsAsync(args, cancellationToken),
            "search_combined" => await SearchCombinedAsync(args, cancellationToken),
            _ => []
        };
    }

    private static int GetIntArg(JsonElement args, string name, int defaultValue)
    {
        if (!args.TryGetProperty(name, out var el)) return defaultValue;
        return el.ValueKind == JsonValueKind.Number
            ? el.GetInt32()
            : int.TryParse(el.GetString(), out var v) ? v : defaultValue;
    }

    private static bool GetBoolArg(JsonElement args, string name, bool defaultValue)
    {
        if (!args.TryGetProperty(name, out var el)) return defaultValue;
        if (el.ValueKind is JsonValueKind.True or JsonValueKind.False) return el.GetBoolean();
        return bool.TryParse(el.GetString(), out var v) ? v : defaultValue;
    }

    private async Task<IReadOnlyList<RecipeSearchResult>> SearchSemanticAsync(
        JsonElement args, CancellationToken cancellationToken)
    {
        var query = args.GetProperty("query").GetString()!;
        var limit = GetIntArg(args, "limit", 5);
        return await _recipeService.SearchAsync(query, limit, cancellationToken);
    }

    private async Task<IReadOnlyList<RecipeSearchResult>> SearchByIngredientsAsync(
        JsonElement args, CancellationToken cancellationToken)
    {
        var ingredients = args.GetProperty("ingredients").EnumerateArray()
            .Select(e => e.GetString()!).ToList();
        var matchAll = GetBoolArg(args, "match_all", false);
        var limit = GetIntArg(args, "limit", 10);
        return await _recipeService.SearchByIngredientsAsync(ingredients, matchAll, limit, cancellationToken);
    }

    private async Task<IReadOnlyList<RecipeSearchResult>> SearchByTagsAsync(
        JsonElement args, CancellationToken cancellationToken)
    {
        var tags = args.GetProperty("tags").EnumerateArray()
            .Select(e => e.GetString()!).ToList();
        var limit = GetIntArg(args, "limit", 10);
        return await _recipeService.SearchByTagsAsync(tags, limit, cancellationToken);
    }

    private async Task<IReadOnlyList<RecipeSearchResult>> SearchCombinedAsync(
        JsonElement args, CancellationToken cancellationToken)
    {
        var query = args.GetProperty("query").GetString()!;
        var limit = GetIntArg(args, "limit", 5);

        var semanticResults = await _recipeService.SearchAsync(query, limit * 4, cancellationToken);

        var matchingIds = new HashSet<string>(semanticResults.Select(r => r.Recipe.Id!));

        if (args.TryGetProperty("ingredients", out var ingsEl))
        {
            var ingredients = ingsEl.EnumerateArray().Select(e => e.GetString()!).ToList();
            if (ingredients.Count > 0)
            {
                var ingResults = await _recipeService.SearchByIngredientsAsync(ingredients, false, 100, cancellationToken);
                matchingIds.IntersectWith(ingResults.Select(r => r.Recipe.Id!));
            }
        }

        if (args.TryGetProperty("tags", out var tagsEl))
        {
            var tags = tagsEl.EnumerateArray().Select(e => e.GetString()!).ToList();
            if (tags.Count > 0)
            {
                var tagResults = await _recipeService.SearchByTagsAsync(tags, 100, cancellationToken);
                matchingIds.IntersectWith(tagResults.Select(r => r.Recipe.Id!));
            }
        }

        return semanticResults
            .Where(r => matchingIds.Contains(r.Recipe.Id!))
            .Take(limit)
            .ToList();
    }

    private async Task<OllamaChatResponse> CallOllamaAsync(
        List<ChatMessage> messages, CancellationToken cancellationToken, bool includeTools = true)
    {
        var request = new OllamaChatRequest
        {
            Model = _settings.LlmModel,
            Messages = messages,
            Stream = false,
            Tools = includeTools ? ToolDefinitions : null
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Ollama returned no response.");
    }

    internal sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public string? Content { get; init; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ToolCall>? ToolCalls { get; init; }
    }

    internal sealed class ToolCall
    {
        [JsonPropertyName("function")]
        public required ToolCallFunction Function { get; init; }
    }

    internal sealed class ToolCallFunction
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("arguments")]
        public required JsonElement Arguments { get; init; }
    }

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required List<ChatMessage> Messages { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object[]? Tools { get; init; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public required ChatMessage Message { get; init; }
    }
}
