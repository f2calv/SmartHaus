namespace CasCap.Tests;

/// <summary>
/// Integration tests for <see cref="MieleClientService"/> against the Miele 3rd Party API.
/// </summary>
/// <remarks>
/// Populate <see cref="OAuthToken"/> with a valid Bearer token before running.
/// Tests will fail if the token is expired or invalid.
/// </remarks>
public class MieleClientServiceTests(ITestOutputHelper output)
{
    private const string MieleBaseAddress = "https://api.mcs3.miele.com/v1/";

    private const string OAuthToken = "REPLACE_WITH_VALID_TOKEN";

    private readonly ITestOutputHelper _output = output;

    private MieleClientService CreateService()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(MieleBaseAddress)
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OAuthToken);

        var factory = new SingleClientFactory(client);
        var logger = LoggerFactory.Create(b => b.AddDebug()).CreateLogger<MieleClientService>();
        var config = Options.Create(new MieleConfig
        {
            OAuthToken = OAuthToken,
            HealthCheckUri = MieleBaseAddress,
            HealthCheckExpectedHttpStatusCodes = [404],
            HealthCheck = KubernetesProbeTypes.None
        });

        return new MieleClientService(logger, config, factory);
    }

    #region Ident + State

    [Fact(Skip = "Requires a valid OAuth token")]
    public async Task GetDevices_ReturnsAllDevices()
    {
        var svc = CreateService();
        var result = await svc.GetDevices();
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        foreach (var device in result)
        {
            _output.WriteLine($"Device {device.Key}: Type={device.Value.ident?.type?.value_localized}, Status={device.Value.state?.status?.value_localized}");
        }
    }

    [Fact(Skip = "Requires a valid OAuth token")]
    public async Task GetShortDevices_ReturnsSummary()
    {
        var svc = CreateService();
        var result = await svc.GetShortDevices();
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        foreach (var device in result)
        {
            _output.WriteLine($"Device {device.fabNumber}: Type={device.type}, State={device.state}, Name={device.deviceName}");
        }
    }

    [Fact(Skip = "Requires a valid OAuth token and device ID")]
    public async Task GetDevice_ReturnsSingleDevice()
    {
        var svc = CreateService();
        var devices = await svc.GetDevices();
        Assert.NotNull(devices);
        Assert.NotEmpty(devices);

        var deviceId = devices.Keys.First();
        var result = await svc.GetDevice(deviceId);
        Assert.NotNull(result);
        Assert.NotNull(result.ident);
        Assert.NotNull(result.state);

        _output.WriteLine($"Device {deviceId}: Type={result.ident.type?.value_localized}, Status={result.state.status?.value_localized}");
    }

    [Fact(Skip = "Requires a valid OAuth token and device ID")]
    public async Task GetIdent_ReturnsDeviceIdentification()
    {
        var svc = CreateService();
        var devices = await svc.GetDevices();
        Assert.NotNull(devices);

        var deviceId = devices.Keys.First();
        var result = await svc.GetIdent(deviceId);
        Assert.NotNull(result);
        Assert.NotNull(result.type);

        _output.WriteLine($"Device {deviceId}: Type={result.type.value_localized}, TechType={result.deviceIdentLabel?.techType}");
    }

    [Fact(Skip = "Requires a valid OAuth token and device ID")]
    public async Task GetState_ReturnsDeviceState()
    {
        var svc = CreateService();
        var devices = await svc.GetDevices();
        Assert.NotNull(devices);

        var deviceId = devices.Keys.First();
        var result = await svc.GetState(deviceId);
        Assert.NotNull(result);
        Assert.NotNull(result.status);

        _output.WriteLine($"Device {deviceId}: Status={result.status.value_localized}, Program={result.ProgramID?.value_localized}");
    }

    #endregion

    #region Actions

    [Fact(Skip = "Requires a valid OAuth token and device ID")]
    public async Task GetActions_ReturnsAvailableActions()
    {
        var svc = CreateService();
        var devices = await svc.GetDevices();
        Assert.NotNull(devices);

        var deviceId = devices.Keys.First();
        var result = await svc.GetActions(deviceId);
        Assert.NotNull(result);

        _output.WriteLine($"Device {deviceId}: PowerOn={result.powerOn}, PowerOff={result.powerOff}, ProcessActions={result.processAction?.Length ?? 0}");
    }

    #endregion

    #region Programs

    [Fact(Skip = "Requires a valid OAuth token and device ID")]
    public async Task GetPrograms_ReturnsAvailablePrograms()
    {
        var svc = CreateService();
        var devices = await svc.GetDevices();
        Assert.NotNull(devices);

        var deviceId = devices.Keys.First();
        var result = await svc.GetPrograms(deviceId);
        Assert.NotNull(result);

        foreach (var program in result)
        {
            _output.WriteLine($"Program {program.programId}: {program.program}");
        }
    }

    #endregion

    #region Test helpers

    /// <summary>
    /// Minimal <see cref="IHttpClientFactory"/> that returns a pre-configured <see cref="HttpClient"/>.
    /// </summary>
    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    #endregion
}
