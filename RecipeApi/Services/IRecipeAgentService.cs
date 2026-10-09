using RecipeApi.Models;

namespace RecipeApi.Services;

public interface IRecipeAgentService
{
    Task<RecipeAgentResult> QueryAsync(string query, CancellationToken cancellationToken = default);

    IAsyncEnumerable<AgentStreamEvent> QueryStreamAsync(string query, CancellationToken cancellationToken = default);
}
