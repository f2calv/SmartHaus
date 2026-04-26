using Microsoft.Extensions.AI;
using OllamaSharp;
using System.ClientModel;

namespace CasCap.Extensions;

/// <summary>
/// Factory methods for creating <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> instances
/// from <see cref="ProviderConfig"/>.
/// </summary>
/// <remarks>
/// This is a SmartHaus-local helper that mirrors the <c>AgentExtensions.CreateAgent</c> pattern
/// from CasCap.Common.AI. When the shared library adds a <c>CreateEmbeddingGenerator</c> method,
/// this class can be removed in favour of the shared implementation.
/// </remarks>
public static class EmbeddingGeneratorFactory
{
    /// <summary>
    /// Creates an <see cref="IEmbeddingGenerator{String, Embedding}"/> from the specified provider configuration.
    /// </summary>
    /// <param name="provider">The provider configuration containing endpoint, model, and type.</param>
    /// <param name="httpClient">Optional pre-configured HTTP client (e.g. with basic auth for dev Ollama).</param>
    /// <returns>An embedding generator for the configured provider.</returns>
    public static IEmbeddingGenerator<string, Embedding<float>> Create(ProviderConfig provider, HttpClient? httpClient = null) =>
        provider.Type switch
        {
            AgentType.Ollama => CreateOllamaEmbeddingGenerator(provider, httpClient),
            AgentType.OpenAI => CreateOpenAIEmbeddingGenerator(provider),
            AgentType.AzureOpenAI => CreateAzureOpenAIEmbeddingGenerator(provider),
            _ => throw new NotSupportedException($"Embedding generation is not supported for provider type '{provider.Type}'."),
        };

    private static IEmbeddingGenerator<string, Embedding<float>> CreateOllamaEmbeddingGenerator(ProviderConfig provider, HttpClient? httpClient)
    {
        var uri = provider.Endpoint ?? new Uri("http://localhost:11434");

        // OllamaApiClient directly implements IEmbeddingGenerator<string, Embedding<float>>.
        if (httpClient is not null)
            return new OllamaApiClient(httpClient, provider.ModelName);

        return new OllamaApiClient(uri, provider.ModelName);
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateOpenAIEmbeddingGenerator(ProviderConfig provider)
    {
        var apiKey = provider.ApiKey ?? throw new InvalidOperationException("OpenAI embedding provider requires an ApiKey.");
        var client = new OpenAI.OpenAIClient(new ApiKeyCredential(apiKey));
        return client.GetEmbeddingClient(provider.ModelName).AsIEmbeddingGenerator();
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateAzureOpenAIEmbeddingGenerator(ProviderConfig provider)
    {
        var endpoint = provider.Endpoint ?? throw new InvalidOperationException("AzureOpenAI embedding provider requires an Endpoint.");
        var apiKey = provider.ApiKey ?? throw new InvalidOperationException("AzureOpenAI embedding provider requires an ApiKey.");
        var client = new Azure.AI.OpenAI.AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));
        return client.GetEmbeddingClient(provider.ModelName).AsIEmbeddingGenerator();
    }
}
