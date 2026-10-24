using CasCap.Exceptions;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CasCap.Services;

/// <summary>
/// HTTP client for the Miele 3rd Party API v1.
/// </summary>
/// <remarks>
/// See <see href="https://developer.miele.com"/> for the full API specification.
/// Uses OAuth 2.0 Bearer token authentication.
/// </remarks>
public sealed partial class MieleClientService : HttpClientBase
{
    private static readonly JsonSerializerOptions _requestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="MieleClientService"/> class.
    /// </summary>
    public MieleClientService(ILogger<MieleClientService> logger,
        IOptions<MieleConfig> config,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        Client = httpClientFactory.CreateClient(nameof(MieleConnectionHealthCheck));
    }

    #region Ident + State

    /// <summary>
    /// Returns all information about all appliances linked to the user account.
    /// </summary>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Dictionary<string, MieleDevice>?> GetDevices(string language = "en", CancellationToken cancellationToken = default)
        => ReadAsync<Dictionary<string, MieleDevice>>($"devices?language={Uri.EscapeDataString(language)}", cancellationToken);

    /// <summary>
    /// Returns a reduced information set about all appliances for lightweight clients.
    /// </summary>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleShortDevice[]?> GetShortDevices(string language = "en", CancellationToken cancellationToken = default)
        => ReadAsync<MieleShortDevice[]>($"short/devices?language={Uri.EscapeDataString(language)}", cancellationToken);

    /// <summary>
    /// Returns all information about a single device.
    /// </summary>
    /// <param name="deviceId">The device ID to query.</param>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleDevice?> GetDevice(string deviceId, string language = "en", CancellationToken cancellationToken = default)
        => ReadAsync<MieleDevice>($"{DevicePath(deviceId)}?language={Uri.EscapeDataString(language)}", cancellationToken);

    /// <summary>
    /// Returns the identification information of a single device.
    /// </summary>
    /// <param name="deviceId">The device ID to query.</param>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleIdent?> GetIdent(string deviceId, string language = "en", CancellationToken cancellationToken = default)
        => ReadAsync<MieleIdent>($"{DevicePath(deviceId)}/ident?language={Uri.EscapeDataString(language)}", cancellationToken);

    /// <summary>
    /// Returns the state information of a single device.
    /// </summary>
    /// <param name="deviceId">The device ID to query.</param>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleState?> GetState(string deviceId, string language = "en", CancellationToken cancellationToken = default)
        => ReadAsync<MieleState>($"{DevicePath(deviceId)}/state?language={Uri.EscapeDataString(language)}", cancellationToken);

    #endregion

    #region Actions

    /// <summary>
    /// Returns the current available actions for a device.
    /// </summary>
    /// <param name="deviceId">The device ID to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleActions?> GetActions(string deviceId, CancellationToken cancellationToken = default)
        => ReadAsync<MieleActions>($"{DevicePath(deviceId)}/actions", cancellationToken);

    /// <summary>
    /// Invokes an action on a single device.
    /// </summary>
    /// <param name="deviceId">The device ID to act on.</param>
    /// <param name="action">The action payload (e.g. processAction, powerOn, light).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Acceptance, appliance absence or explicit command rejection.</returns>
    public Task<MieleCommandResult> PutAction(string deviceId, MieleActionRequest action, CancellationToken cancellationToken = default)
        => WriteAsync($"{DevicePath(deviceId)}/actions", action, cancellationToken);

    #endregion

    #region Programs

    /// <summary>
    /// Returns the current available programs for a device.
    /// </summary>
    /// <param name="deviceId">The device ID to query.</param>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleProgram[]?> GetPrograms(string deviceId, string language = "en", CancellationToken cancellationToken = default)
        => ReadAsync<MieleProgram[]>($"{DevicePath(deviceId)}/programs?language={Uri.EscapeDataString(language)}", cancellationToken);

    /// <summary>
    /// Selects a program and starts the appliance. The device must be set to MobileStart or MobileControl.
    /// </summary>
    /// <param name="deviceId">The device ID to act on.</param>
    /// <param name="programRequest">The program selection payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Acceptance, appliance absence or explicit command rejection.</returns>
    public Task<MieleCommandResult> PutProgram(string deviceId, MieleProgramRequest programRequest, CancellationToken cancellationToken = default)
        => WriteAsync($"{DevicePath(deviceId)}/programs", programRequest, cancellationToken);

    #endregion

    private async Task<T?> ReadAsync<T>(string uri, CancellationToken cancellationToken) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            throw Failure("The Miele API returned an unsuccessful response.", response.StatusCode);
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false)
                ?? throw Failure("The Miele API returned an empty result.", response.StatusCode);
        }
        catch (JsonException)
        {
            throw Failure("The Miele API returned invalid JSON.", response.StatusCode);
        }
    }

    private async Task<MieleCommandResult> WriteAsync<T>(string uri, T payload, CancellationToken cancellationToken) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validator.ValidateObject(payload, new ValidationContext(payload), validateAllProperties: true);
        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(payload, options: _requestJsonOptions),
        };
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
            return MieleCommandResult.Accepted;
        if (response.StatusCode == HttpStatusCode.NotFound)
            return MieleCommandResult.NotFound;
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.MethodNotAllowed or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
        {
            LogRejection(_logger, nameof(MieleClientService), response.StatusCode);
            return MieleCommandResult.Rejected;
        }
        throw Failure("The Miele API returned an unsuccessful response.", response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw Failure("The Miele API could not be reached.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Failure("The Miele API request timed out.");
        }
        catch (TimeoutRejectedException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw Failure("The Miele API request timed out.");
        }
        catch (BrokenCircuitException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw Failure("The Miele API is temporarily unavailable.");
        }
    }

    private MieleApiException Failure(string message, HttpStatusCode? statusCode = null)
    {
        LogFailure(_logger, nameof(MieleClientService), statusCode);
        return new MieleApiException(message, statusCode);
    }

    private static string DevicePath(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return $"devices/{Uri.EscapeDataString(deviceId)}";
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} Miele API request failed with upstream status {StatusCode}")]
    private static partial void LogFailure(ILogger logger, string className, HttpStatusCode? statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} Miele API rejected a command with upstream status {StatusCode}")]
    private static partial void LogRejection(ILogger logger, string className, HttpStatusCode statusCode);
}
