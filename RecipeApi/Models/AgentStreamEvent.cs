namespace RecipeApi.Models;

public abstract record AgentStreamEvent;

public sealed record ToolCallStreamEvent(string ToolName) : AgentStreamEvent;

public sealed record SearchResultsStreamEvent(int Count) : AgentStreamEvent;

public sealed record DeltaStreamEvent(string Text) : AgentStreamEvent;

public sealed record FinalResultsStreamEvent(IReadOnlyList<RecipeSearchResult> Results) : AgentStreamEvent;

public sealed record DoneStreamEvent : AgentStreamEvent;
