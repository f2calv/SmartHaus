using CasCap.Extensions;
using Microsoft.Extensions.AI;

namespace CasCap.Tests.Misc;

/// <summary>
/// Integration tests for llama.cpp / LM Studio inference via <see cref="IChatClient"/>
/// created through <see cref="AgentExtensions.CreateAgent"/>.
/// </summary>
/// <remarks>
/// These tests require a running OpenAI-compatible inference server (e.g. llama.cpp, LM Studio)
/// configured via <c>appsettings.Development.json</c> under <c>CasCap:AIConfig:Providers</c>.
/// They will fail if the server is not available.
/// </remarks>
// TODO: Review these llama.cpp integration tests and their runtime dependencies. The configured server
// may be reachable only after deployment to the cluster; provide a deterministic local endpoint or an
// explicit availability-based skip, and remove this TODO when the suite can distinguish an unavailable
// dependency from a client regression on every supported test environment.
[Trait("Category", "Integration")]
public class LlamaCppApiClientTests(ITestOutputHelper output) : TestBase(output)
{
    private const string DefaultProviderKey = "EdgeGpu";

    private readonly string _filePath = @"C:\temp\wine.png";
    private readonly string _fileMimeType = "image/png";

    private (ProviderConfig provider, AgentConfig agentConfig) CreateTestConfig()
    {
        var provider = _aiConfig.Providers[DefaultProviderKey];

        var agentConfig = new AgentConfig
        {
            Provider = DefaultProviderKey,
            Instructions = "You are a helpful assistant.",
            Description = "llama.cpp integration test agent",
            Name = "llama-cpp-test",
            Prompt = "Respond to the user's request.",
        };

        return (provider, agentConfig);
    }

    [Fact]
    public async Task GetResponseAsync_ReturnsChatResponse_IfServerAvailable()
    {
        var (provider, agentConfig) = CreateTestConfig();
        var (chatClient, _, _) = AgentExtensions.CreateAgent(provider, agentConfig);

        var messages = new[] { new ChatMessage(ChatRole.User, "Hello from integration test") };

        try
        {
            var response = await chatClient.GetResponseAsync(messages, cancellationToken: CancellationToken.None);
            Assert.NotNull(response);
            _output.WriteLine($"Received response: ResponseId={response.ResponseId}, ModelId={response.ModelId}");
            _output.WriteLine($"Full response Text={response.Text}");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"llama.cpp server not available at {provider.Endpoint} - failing integration test: " + ex.Message);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _output.WriteLine("Request timed out or was cancelled - failing integration test: " + ex.Message);
            throw;
        }
    }

    [Fact]
    public async Task GetStreamingResponseAsync_YieldsAtLeastOneUpdate_IfServerAvailable()
    {
        var (provider, agentConfig) = CreateTestConfig();
        var (chatClient, _, _) = AgentExtensions.CreateAgent(provider, agentConfig);

        var messages = new[] { new ChatMessage(ChatRole.User, "Hello streaming test") };

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var seenAny = false;

            await foreach (var update in chatClient.GetStreamingResponseAsync(messages, cancellationToken: cts.Token))
            {
                Assert.NotNull(update);
                _output.WriteLine($"Streaming update: ResponseId={update.ResponseId}, ModelId={update.ModelId}, FinishReason={update.FinishReason}");
                seenAny = true;
                break;
            }

            if (!seenAny)
            {
                _output.WriteLine("No streaming updates received within timeout.");
                Assert.Fail("No streaming updates received from llama.cpp within timeout.");
            }
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"llama.cpp server not available at {provider.Endpoint} - failing streaming integration test: " + ex.Message);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _output.WriteLine("Streaming request timed out or was cancelled - failing integration test: " + ex.Message);
            throw;
        }
    }

    // TODO: re-enable once the test image is committed. The fixture depends on an uncommitted
    // C:\temp\wine.png, so it fails on every machine that does not happen to have that file.
    // Commit a small image under the test project and copy it to the output directory instead,
    // then drop the Skip and the absolute path. Its sibling in AIAgentExtensionsTests exercises
    // the same multimodal path and self-skips, so nothing is currently unguarded by this.
    [Fact(Skip = @"Depends on an uncommitted image at C:\temp\wine.png; see the TODO above.")]
    public async Task GetResponseAsync_WithFileContent_ReturnsChatResponse()
    {
        if (!File.Exists(_filePath))
            Assert.Fail($"Test file not found at '{_filePath}'");

        var (provider, agentConfig) = CreateTestConfig();
        var (chatClient, _, _) = AgentExtensions.CreateAgent(provider, agentConfig);

        var contents = new List<AIContent>
        {
            new TextContent("please describe this file in a maximum of two sentences"),
            new DataContent(await File.ReadAllBytesAsync(_filePath, TestContext.Current.CancellationToken), _fileMimeType),
            //new DataContent(await File.ReadAllBytesAsync(_filePath2), "text/plain")//not working?
            //new DataContent("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mP8z8BQz0AEYBxVSF+FABJADveWkH6oAAAAAElFTkSuQmCC", "image/png")
        };

        var message = new ChatMessage(ChatRole.User, contents);

        try
        {
            var response = await chatClient.GetResponseAsync([message], cancellationToken: CancellationToken.None);
            Assert.NotNull(response);
            _output.WriteLine($"Received response for file message: ResponseId={response.ResponseId}, ModelId={response.ModelId}");
            _output.WriteLine($"Full response Text={response.Text}");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"llama.cpp server not available at {provider.Endpoint} - failing file upload integration test: " + ex.Message);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _output.WriteLine("Request timed out or was cancelled - failing file upload integration test: " + ex.Message);
            throw;
        }
    }
}
