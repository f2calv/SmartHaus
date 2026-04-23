using CasCap.Common.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CasCap.Services;

/// <summary>
/// Concerned with raw HTTP interaction with the KM200 unit.
/// </summary>
public class BuderusKm200ClientService : HttpClientBase
{
    private readonly BuderusConfig _config;
    private readonly BuderusKm200Reader _buderusKm200Reader;

    /// <summary>
    /// The 32-byte AES key derived from the gateway and private passwords.
    /// </summary>
    /// <remarks>
    /// The algorithm mirrors the private <c>CalculateKey</c> method inside <see cref="BuderusKm200Reader"/>:
    /// <c>MD5(gatewayPasswordBytes + salt) || MD5(salt + privatePasswordBytes)</c>
    /// where salt is the 32-byte fixed constant used by the library.
    /// </remarks>
    private readonly byte[] _encryptionKey;

    /// <summary>
    /// Fixed 32-byte salt used by the PeterPuff BuderusKm200Reader key-derivation algorithm.
    /// </summary>
    private static readonly byte[] _salt =
    [
        134, 120, 69, 233, 124, 78, 41, 220, 229, 34,
        185, 167, 211, 163, 224, 123, 21, 43, 255, 173,
        221, 190, 215, 245, 255, 216, 66, 233, 137, 90,
        209, 228
    ];

    /// <summary>Initializes a new instance of the <see cref="BuderusKm200ClientService"/> class.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="config">Buderus configuration.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    public BuderusKm200ClientService(ILogger<BuderusKm200ClientService> logger,
        IOptions<BuderusConfig> config,
        IHttpClientFactory httpClientFactory
        )
    {
        _logger = logger;
        _config = config.Value;
        //_httpClientFactory = httpClientFactory;
        Client = httpClientFactory.CreateClient(nameof(BuderusKm200ConnectionHealthCheck));

        _buderusKm200Reader = new BuderusKm200Reader(_config.BaseAddress, _config.Port, _config.GatewayPassword, _config.PrivatePassword);
        _encryptionKey = DeriveKey(_config.GatewayPassword, _config.PrivatePassword);
    }

    //https://www.smarthomeng.de/user/plugins/buderus/URLs.html?highlight=km200
    private static readonly string[] dataPointRoots = "/dhwCircuits,/heatingCircuits,/recordings,/solarCircuits,/system,/gateway,/heatSources,/notifications,/application".Split(',');

    /// <summary>Gets all available datapoints from the KM200 device.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all discovered datapoints.</returns>
    public async Task<List<Km200DatapointObject>> GetAllDataPoints(CancellationToken cancellationToken)
    {
        var datapoints = new List<Km200DatapointObject>();

        foreach (var id in dataPointRoots)
        {
            var dp = await GetDP(id);
            if (dp is not null) datapoints.Add(dp);
        }

        return datapoints;

        async Task<Km200DatapointObject?> GetDP(string requestUriOrId)
        {
            if (cancellationToken.IsCancellationRequested) return null;
            var dp = await GetDataPoint(requestUriOrId);
            if (dp is not null)
            {
                datapoints.Add(dp);
                if (!dp.References.IsNullOrEmpty())
                    foreach (var dpref in dp.References!)
                        _ = await GetDP(dpref.Uri);//recursive
            }
            return dp;
        }
    }

    /// <summary>Gets a specific datapoint from the KM200 device.</summary>
    /// <param name="requestUriOrId">Datapoint URI or ID.</param>
    /// <returns>Datapoint object, or null if not found.</returns>
    public async Task<Km200DatapointObject?> GetDataPoint(string requestUriOrId)
    {
        requestUriOrId = $"{_config.BaseAddress}:{_config.Port}{requestUriOrId}";
        var tpl = await GetAsync<string, string>(requestUriOrId);
        if (tpl.result is null || tpl.result.IndexOf("Sorry, the requested file does not exist on this server.") > -1)
            _logger.LogWarning("url '{RequestUriOrId}' not found", requestUriOrId);
        else
        {
            var json = _buderusKm200Reader.Decrypt(tpl.result);
            if (string.IsNullOrWhiteSpace(json))
                _logger.LogWarning("no data returned for url '{RequestUriOrId}'", requestUriOrId);
            else
                return json.FromJson<Km200DatapointObject>()!;
        }
        return null;
    }

    /// <summary>
    /// Writes a new value to the specified KM200 datapoint via HTTP POST.
    /// </summary>
    /// <param name="datapointId">The KM200 datapoint path, e.g. <c>/dhwCircuits/dhw1/setTemperature</c>.</param>
    /// <param name="value">
    /// The new value to write. For <see cref="MyDatapointType.floatValue"/> datapoints, pass a numeric value.
    /// For <see cref="MyDatapointType.stringValue"/> datapoints, pass the option string.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if the device accepted the write; <see langword="false"/> otherwise.
    /// </returns>
    public async Task<bool> SetDataPoint(string datapointId, object value, CancellationToken cancellationToken = default)
    {
        var dp = await GetDataPoint(datapointId);
        if (dp is null)
        {
            _logger.LogWarning("{ClassName} datapoint '{DatapointId}' not found, cannot write", nameof(BuderusKm200ClientService), datapointId);
            return false;
        }
        if (dp.Writeable != 1)
        {
            _logger.LogWarning("{ClassName} datapoint '{DatapointId}' is not writeable", nameof(BuderusKm200ClientService), datapointId);
            return false;
        }

        var payload = BuildPayload(dp.Type, value);
        var encrypted = Encrypt(payload);
        var requestUri = $"{_config.BaseAddress}:{_config.Port}{datapointId}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Content = new ByteArrayContent(encrypted);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        _logger.LogInformation("{ClassName} writing to '{DatapointId}' value={Value}", nameof(BuderusKm200ClientService), datapointId, value);

        var response = await Client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("{ClassName} failed to write to '{DatapointId}', StatusCode={StatusCode}",
                nameof(BuderusKm200ClientService), datapointId, response.StatusCode);
            return false;
        }

        _logger.LogInformation("{ClassName} successfully wrote to '{DatapointId}'", nameof(BuderusKm200ClientService), datapointId);
        return true;
    }

    #region private helpers

    /// <summary>
    /// Builds the JSON write payload for the given datapoint type and value.
    /// </summary>
    private static string BuildPayload(MyDatapointType type, object value)
    {
        var node = new JsonObject();
        if (type == MyDatapointType.floatValue && double.TryParse(value.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
            node["value"] = d;
        else
            node["value"] = value.ToString();
        return node.ToJsonString();
    }

    /// <summary>
    /// Derives the 32-byte AES key from the gateway and private passwords,
    /// using the same algorithm as the PeterPuff <see cref="BuderusKm200Reader"/>.
    /// </summary>
    /// <remarks>
    /// MD5 is used here solely for KM200 device protocol compatibility — it is not used for security purposes.
    /// </remarks>
    private static byte[] DeriveKey(string gatewayPassword, string privatePassword)
    {
        var gwBytes = Encoding.ASCII.GetBytes(gatewayPassword.Replace("-", string.Empty));
        var pvBytes = Encoding.ASCII.GetBytes(privatePassword);
        using var md5 = MD5.Create();
        var part1 = md5.ComputeHash([.. gwBytes, .. _salt]);
        var part2 = md5.ComputeHash([.. _salt, .. pvBytes]);
        return [.. part1, .. part2];
    }

    /// <summary>
    /// Encrypts the plaintext JSON using AES-256 ECB with zero-padding, matching the KM200 wire format.
    /// </summary>
    /// <remarks>
    /// ECB mode is mandated by the KM200 device protocol. It is not used by choice — all client
    /// implementations targeting this device must use the same cipher settings.
    /// </remarks>
    private byte[] Encrypt(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        // Pad to a multiple of 16 bytes with zeros
        var blockSize = 16;
        var padded = plainBytes.Length % blockSize == 0
            ? plainBytes
            : [.. plainBytes, .. new byte[blockSize - (plainBytes.Length % blockSize)]];

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Key = _encryptionKey;

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        cs.Write(padded, 0, padded.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    #endregion
}
