using CasCap.Extensions;

namespace CasCap.Tests.Misc;

/// <summary>
/// Integration tests for <see cref="AgentExtensions.RunAnalysisAsync"/> that execute against a live AI agent.
/// </summary>
/// <remarks>
/// These tests require a running inference server (e.g. llama.cpp, Ollama, LM Studio)
/// configured via <c>appsettings.Development.json</c> under <c>CasCap:AIConfig:Providers</c>.
/// They will fail if the server is not available.
/// </remarks>
public class AIAgentExtensionsTests(ITestOutputHelper output) : TestBase(output)
{
    private const string DefaultProviderKey = "EdgeGpu";

    private (ProviderConfig provider, AgentConfig agentConfig) CreateTestConfig(string? modelOverride = null)
    {
        var provider = _aiConfig.Providers[DefaultProviderKey];
        if (modelOverride is not null)
            provider = provider with { ModelName = modelOverride };

        var agentConfig = new AgentConfig
        {
            Provider = DefaultProviderKey,
            Instructions = "You are a helpful assistant for a smart home system.",
            Description = "Test agent for integration tests",
            Name = "test-agent",
            Prompt = "Describe what you see or respond to the user's request.",
        };

        return (provider, agentConfig);
    }

    [Theory]
    [InlineData(null, "Describe the purpose of a KNX bus system in a smart home in two sentences.")]
    public async Task RunAnalysisAsync_TextOnly_ReturnsResult(string? modelOverride, string prompt)
    {
        var (provider, agentConfig) = CreateTestConfig(modelOverride);
        var (_, agent, resolvedInstructions) = AgentExtensions.CreateAgent(provider, agentConfig);

        try
        {
            var message = AgentExtensions.BuildChatMessage(prompt);
            var chatOptions = AgentExtensions.BuildChatOptions(agentConfig, resolvedInstructions);
            var result = await agent.RunAnalysisAsync(
                provider,
                agentConfig,
                message,
                chatOptions,
                cancellationToken: CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.OutputText));
            Assert.False(string.IsNullOrWhiteSpace(result.FormattedResult));
            Assert.True(result.Elapsed > TimeSpan.Zero);
            Assert.NotNull(result.Session);

            _output.WriteLine($"Elapsed: {result.Elapsed}");
            _output.WriteLine($"Output: {result.OutputText}");
            _output.WriteLine($"Formatted: {result.FormattedResult}");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"AI server not available at {provider.Endpoint} - failing integration test: {ex.Message}");
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _output.WriteLine($"Request timed out or was cancelled - failing integration test: {ex.Message}");
            throw;
        }
    }

    [Theory]
    [InlineData(@"C:\temp\wine.png", "image/png", "Please describe this image in a maximum of two sentences.", "wine bottle")]
    public async Task RunAnalysisAsync_WithImage_ReturnsResult(string filePath, string mimeType, string prompt, string expectedContent)
    {
        if (!File.Exists(filePath))
        {
            _output.WriteLine($"Test file not found at '{filePath}', skipping image test.");
            return;
        }

        var (provider, agentConfig) = CreateTestConfig();
        var (_, agent, resolvedInstructions) = AgentExtensions.CreateAgent(provider, agentConfig);
        var imageBytes = await File.ReadAllBytesAsync(filePath);

        try
        {
            var message = AgentExtensions.BuildChatMessage(prompt,
                binaryContent: imageBytes, mimeType: mimeType);
            var chatOptions = AgentExtensions.BuildChatOptions(agentConfig, resolvedInstructions);
            var result = await agent.RunAnalysisAsync(
                provider,
                agentConfig,
                message,
                chatOptions,
                cancellationToken: CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.OutputText));
            Assert.True(result.OutputText.Contains(expectedContent, StringComparison.OrdinalIgnoreCase));

            _output.WriteLine($"Elapsed: {result.Elapsed}");
            _output.WriteLine($"Output: {result.OutputText}");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"AI server not available at {provider.Endpoint} - failing integration test: {ex.Message}");
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _output.WriteLine($"Request timed out or was cancelled - failing integration test: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task RunAnalysisAsync_SessionResumption_MaintainsContext()
    {
        var (provider, agentConfig) = CreateTestConfig();
        var (_, agent, resolvedInstructions) = AgentExtensions.CreateAgent(provider, agentConfig);

        try
        {
            var chatOptions = AgentExtensions.BuildChatOptions(agentConfig, resolvedInstructions);

            var message1 = AgentExtensions.BuildChatMessage("Remember the number 42. Just confirm you have noted it.");
            var result1 = await agent.RunAnalysisAsync(
                provider,
                agentConfig,
                message1,
                chatOptions,
                cancellationToken: CancellationToken.None);

            Assert.NotNull(result1);
            _output.WriteLine($"First call output: {result1.OutputText}");

            var message2 = AgentExtensions.BuildChatMessage("What number did I ask you to remember?");
            var result2 = await agent.RunAnalysisAsync(
                provider,
                agentConfig,
                message2,
                chatOptions,
                session: result1.Session,
                cancellationToken: CancellationToken.None);

            Assert.NotNull(result2);
            Assert.Contains("42", result2.OutputText);
            _output.WriteLine($"Second call output: {result2.OutputText}");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"AI server not available at {provider.Endpoint} - failing integration test: {ex.Message}");
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _output.WriteLine($"Request timed out or was cancelled - failing integration test: {ex.Message}");
            throw;
        }
    }
}
