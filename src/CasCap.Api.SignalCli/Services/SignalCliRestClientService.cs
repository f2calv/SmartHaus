namespace CasCap.Services;

/// <summary>
/// HTTP client for the signal-cli REST API.
/// </summary>
/// <remarks>
/// See <see href="https://bbernhard.github.io/signal-cli-rest-api/"/> for the full API specification.
/// </remarks>
public sealed class SignalCliRestClientService : HttpClientBase, ISignalCliClient, ISignalCliReceiver, INotifier
{
    private readonly SignalCliConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalCliRestClientService"/> class.
    /// </summary>
    public SignalCliRestClientService(ILogger<SignalCliRestClientService> logger, IOptions<SignalCliConfig> options, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _config = options.Value;
        Client = httpClientFactory.CreateClient(nameof(SignalCliConnectionHealthCheck));
    }

    #region General

    /// <summary>
    /// Retrieves version and build information from <c>GET /v1/about</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<SignalAbout?> GetAbout(CancellationToken cancellationToken = default) =>
        GetAsync<SignalAbout>("v1/about", cancellationToken);

    /// <summary>
    /// Retrieves the current signal-cli configuration from <c>GET /v1/configuration</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<SignalConfiguration?> GetConfiguration(CancellationToken cancellationToken = default) =>
        GetAsync<SignalConfiguration>("v1/configuration", cancellationToken);

    /// <summary>
    /// Updates the signal-cli configuration via <c>POST /v1/configuration</c>.
    /// </summary>
    /// <param name="config">The configuration to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> SetConfiguration(SignalConfiguration config, CancellationToken cancellationToken = default) =>
        PostBoolAsync("v1/configuration", config, cancellationToken);

    /// <summary>
    /// Retrieves account-specific trust mode settings via <c>GET /v1/configuration/{number}/settings</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TrustModeResponse?> GetAccountSettings(string number, CancellationToken cancellationToken = default) =>
        GetAsync<TrustModeResponse>($"v1/configuration/{Esc(number)}/settings", cancellationToken);

    /// <summary>
    /// Sets account-specific trust mode settings via <c>POST /v1/configuration/{number}/settings</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="request">The trust mode to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> SetAccountSettings(string number, TrustModeRequest request, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/configuration/{Esc(number)}/settings", request, cancellationToken);

    #endregion

    #region Messaging

    /// <summary>
    /// Sends a Signal message to one or more recipients via <c>POST /v2/send</c>.
    /// </summary>
    /// <param name="msg">The message request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SignalMessageResponse?> SendMessage(SignalMessageRequest msg, CancellationToken cancellationToken = default)
    {
        const string requestUri = "v2/send";
        try
        {
            var tpl = await PostJsonAsync<SignalMessageResponse, string>(requestUri, msg, TimeSpan.FromMilliseconds(_config.SendTimeoutMs), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (tpl.result is not null)
                _logger.LogDebug("{ClassName} message {Message} sent, timestamp {Timestamp}",
                    nameof(SignalCliRestClientService), msg.Message, tpl.result.Timestamp);
            else
                _logger.LogWarning("{ClassName} {RequestUri} failed: {ErrorBody}",
                    nameof(SignalCliRestClientService), requestUri, tpl.error);
            return tpl.result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} message send failure to {RequestUri}",
                nameof(SignalCliRestClientService), requestUri);
        }
        return null;
    }

    /// <summary>
    /// Shows a typing indicator for the specified sender number via <c>PUT /v1/typing-indicator/{number}</c>.
    /// </summary>
    /// <param name="number">The sender's phone number.</param>
    /// <param name="recipient">The recipient's phone number or group ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> ShowTypingIndicator(string number, string recipient, CancellationToken cancellationToken = default) =>
        PutAsync($"v1/typing-indicator/{Esc(number)}", new { recipient }, cancellationToken);

    /// <summary>
    /// Hides a typing indicator for the specified sender number via <c>DELETE /v1/typing-indicator/{number}</c>.
    /// </summary>
    /// <param name="number">The sender's phone number.</param>
    /// <param name="recipient">The recipient's phone number or group ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> HideTypingIndicator(string number, string recipient, CancellationToken cancellationToken = default) =>
        DeleteAsync($"v1/typing-indicator/{Esc(number)}", new { recipient }, cancellationToken);

    /// <summary>
    /// Receives pending messages for the specified account number via <c>GET /v1/receive/{number}</c>.
    /// </summary>
    /// <remarks>
    /// This performs a one-shot poll of the inbox. Each call drains queued messages from
    /// the signal-cli REST API; subsequent calls return only newly arrived messages.
    /// </remarks>
    /// <param name="number">The registered account phone number to receive messages for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SignalReceivedMessage[]?> ReceiveMessages(string number, CancellationToken cancellationToken = default)
    {
        var requestUri = $"v1/receive/{Esc(number)}";
        try
        {
            var tpl = await base.GetAsync<SignalReceivedMessage[], object>(requestUri, cancellationToken: cancellationToken).ConfigureAwait(false);
            var messages = tpl.result;
            if (messages is not null)
                _logger.LogDebug("{ClassName} received {Count} message(s) for {Number}",
                    nameof(SignalCliRestClientService), messages.Length, number);
            return messages;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to receive messages for {Number}",
                nameof(SignalCliRestClientService), number);
        }
        return null;
    }

    /// <summary>
    /// Sends a reaction to a message via <c>POST /v1/reactions/{number}</c>.
    /// </summary>
    /// <param name="number">The sender's phone number.</param>
    /// <param name="recipient">The recipient's phone number or group ID.</param>
    /// <param name="reaction">The reaction emoji.</param>
    /// <param name="targetAuthor">The author of the message being reacted to.</param>
    /// <param name="timestamp">The timestamp of the target message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> SendReaction(string number, string recipient, string reaction, string targetAuthor, long timestamp, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/reactions/{Esc(number)}", new { recipient, reaction, target_author = targetAuthor, timestamp }, cancellationToken);

    /// <summary>
    /// Removes a reaction from a message via <c>DELETE /v1/reactions/{number}</c>.
    /// </summary>
    /// <param name="number">The sender's phone number.</param>
    /// <param name="recipient">The recipient's phone number or group ID.</param>
    /// <param name="reaction">The reaction emoji to remove.</param>
    /// <param name="targetAuthor">The author of the message the reaction was on.</param>
    /// <param name="timestamp">The timestamp of the target message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> RemoveReaction(string number, string recipient, string reaction, string targetAuthor, long timestamp, CancellationToken cancellationToken = default) =>
        DeleteAsync($"v1/reactions/{Esc(number)}", new { recipient, reaction, target_author = targetAuthor, timestamp }, cancellationToken);

    /// <summary>
    /// Sends a read/viewed receipt via <c>POST /v1/receipts/{number}</c>.
    /// </summary>
    /// <param name="number">The sender's phone number.</param>
    /// <param name="recipient">The recipient's phone number.</param>
    /// <param name="receiptType">The receipt type (<c>"read"</c> or <c>"viewed"</c>).</param>
    /// <param name="timestamp">The timestamp of the message to acknowledge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> SendReceipt(string number, string recipient, string receiptType, long timestamp, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/receipts/{Esc(number)}", new { recipient, receipt_type = receiptType, timestamp }, cancellationToken);

    /// <summary>
    /// Remotely deletes a previously sent message via <c>DELETE /v1/remote-delete/{number}</c>.
    /// </summary>
    /// <param name="number">The sender's phone number.</param>
    /// <param name="recipient">The recipient's phone number or group ID.</param>
    /// <param name="timestamp">The timestamp of the message to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<RemoteDeleteResponse?> RemoteDelete(string number, string recipient, long timestamp, CancellationToken cancellationToken = default) =>
        DeleteAsync<RemoteDeleteResponse>($"v1/remote-delete/{Esc(number)}", new { recipient, timestamp }, cancellationToken);

    #endregion

    #region Registration

    /// <summary>
    /// Registers a phone number with Signal via <c>POST /v1/register/{number}</c>.
    /// </summary>
    /// <param name="number">The phone number to register in international format.</param>
    /// <param name="useVoice">Whether to use voice verification instead of SMS.</param>
    /// <param name="captcha">Optional captcha value if required by the Signal server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> RegisterNumber(string number, bool useVoice = false, string? captcha = null, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/register/{Esc(number)}", new { use_voice = useVoice, captcha }, cancellationToken);

    /// <summary>
    /// Verifies a registered phone number via <c>POST /v1/register/{number}/verify/{token}</c>.
    /// </summary>
    /// <param name="number">The phone number to verify.</param>
    /// <param name="token">The verification token received via SMS or voice call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> VerifyNumber(string number, string token, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/register/{Esc(number)}/verify/{Esc(token)}", cancellationToken: cancellationToken);

    /// <summary>
    /// Unregisters a phone number from Signal via <c>POST /v1/unregister/{number}</c>.
    /// </summary>
    /// <param name="number">The phone number to unregister.</param>
    /// <param name="deleteAccount">Whether to delete the account from the Signal server.</param>
    /// <param name="deleteLocalData">Whether to delete local data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> UnregisterNumber(string number, bool deleteAccount = false, bool deleteLocalData = false, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/unregister/{Esc(number)}", new { delete_account = deleteAccount, delete_local_data = deleteLocalData }, cancellationToken);

    #endregion

    #region Accounts

    /// <summary>
    /// Returns all registered accounts via <c>GET /v1/accounts</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<string[]?> ListAccounts(CancellationToken cancellationToken = default) =>
        GetAsync<string[]>("v1/accounts", cancellationToken);

    /// <summary>
    /// Sets a registration PIN for the specified account via <c>POST /v1/accounts/{number}/pin</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="pin">The PIN to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> SetPin(string number, string pin, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/accounts/{Esc(number)}/pin", new { pin }, cancellationToken);

    /// <summary>
    /// Removes the registration PIN from the specified account via <c>DELETE /v1/accounts/{number}/pin</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> RemovePin(string number, CancellationToken cancellationToken = default) =>
        DeleteAsync($"v1/accounts/{Esc(number)}/pin", cancellationToken: cancellationToken);

    /// <summary>
    /// Submits a rate-limit challenge via <c>POST /v1/accounts/{number}/rate-limit-challenge</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="challengeToken">The challenge token.</param>
    /// <param name="captcha">The captcha solution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> SubmitRateLimitChallenge(string number, string challengeToken, string captcha, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/accounts/{Esc(number)}/rate-limit-challenge", new { challenge_token = challengeToken, captcha }, cancellationToken);

    /// <summary>
    /// Updates account settings via <c>PUT /v1/accounts/{number}/settings</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="discoverableByNumber">Whether the account is discoverable by phone number.</param>
    /// <param name="shareNumber">Whether to share the phone number with contacts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> UpdateAccountSettings(string number, bool? discoverableByNumber = null, bool? shareNumber = null, CancellationToken cancellationToken = default) =>
        PutAsync($"v1/accounts/{Esc(number)}/settings",
            new { discoverable_by_number = discoverableByNumber, share_number = shareNumber }, cancellationToken);

    /// <summary>
    /// Sets a username for the specified account via <c>POST /v1/accounts/{number}/username</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="username">The desired username.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<SetUsernameResponse?> SetUsername(string number, string username, CancellationToken cancellationToken = default) =>
        PostAsync<SetUsernameResponse>($"v1/accounts/{Esc(number)}/username", new { username }, cancellationToken);

    /// <summary>
    /// Removes the username from the specified account via <c>DELETE /v1/accounts/{number}/username</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> RemoveUsername(string number, CancellationToken cancellationToken = default) =>
        DeleteAsync($"v1/accounts/{Esc(number)}/username", cancellationToken: cancellationToken);

    #endregion

    #region Contacts

    /// <summary>
    /// Lists contacts for the specified account via <c>GET /v1/contacts/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="allRecipients">When <see langword="true"/>, returns all known recipients (not just contacts).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<SignalContact[]?> ListContacts(string number, bool allRecipients = false, CancellationToken cancellationToken = default) =>
        GetAsync<SignalContact[]>($"v1/contacts/{Esc(number)}{(allRecipients ? "?allRecipients=true" : "")}", cancellationToken);

    /// <summary>
    /// Updates a contact for the specified account via <c>PUT /v1/contacts/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="recipient">The contact's phone number.</param>
    /// <param name="name">The display name for the contact.</param>
    /// <param name="expirationInSeconds">Optional message expiration in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> UpdateContact(string number, string recipient, string? name = null, int? expirationInSeconds = null, CancellationToken cancellationToken = default) =>
        PutAsync($"v1/contacts/{Esc(number)}",
            new { recipient, name, expiration_in_seconds = expirationInSeconds }, cancellationToken);

    /// <summary>
    /// Triggers a contact sync for the specified account via <c>POST /v1/contacts/{number}/sync</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> SyncContacts(string number, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/contacts/{Esc(number)}/sync", cancellationToken: cancellationToken);

    /// <summary>
    /// Returns a specific contact by UUID via <c>GET /v1/contacts/{number}/{uuid}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="uuid">The contact's UUID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<SignalContact?> GetContact(string number, string uuid, CancellationToken cancellationToken = default) =>
        GetAsync<SignalContact>($"v1/contacts/{Esc(number)}/{Esc(uuid)}", cancellationToken);

    /// <summary>
    /// Downloads the avatar image of a contact via <c>GET /v1/contacts/{number}/{uuid}/avatar</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="uuid">The contact's UUID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<byte[]?> GetContactAvatar(string number, string uuid, CancellationToken cancellationToken = default) =>
        GetAsync<byte[]>($"v1/contacts/{Esc(number)}/{Esc(uuid)}/avatar", cancellationToken);

    #endregion

    #region Devices

    /// <summary>
    /// Returns the QR code image bytes for linking a new device via <c>GET /v1/qrcodelink</c>.
    /// </summary>
    /// <param name="deviceName">A display name for the new linked device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<byte[]?> GetQrCodeLink(string deviceName = "signal-cli-rest-api", CancellationToken cancellationToken = default) =>
        GetAsync<byte[]>($"v1/qrcodelink?device_name={Esc(deviceName)}", cancellationToken);

    /// <summary>
    /// Returns the device-link URI for linking a new device via <c>GET /v1/qrcodelink/raw</c>.
    /// </summary>
    /// <param name="deviceName">A display name for the new linked device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<DeviceLinkUriResponse?> GetQrCodeLinkRaw(string deviceName = "signal-cli-rest-api", CancellationToken cancellationToken = default) =>
        GetAsync<DeviceLinkUriResponse>($"v1/qrcodelink/raw?device_name={Esc(deviceName)}", cancellationToken);

    /// <summary>
    /// Returns all devices linked to the specified account via <c>GET /v1/devices/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<SignalDevice[]?> ListLinkedDevices(string number, CancellationToken cancellationToken = default) =>
        GetAsync<SignalDevice[]>($"v1/devices/{Esc(number)}", cancellationToken);

    /// <summary>
    /// Links a new device to the specified account via <c>POST /v1/devices/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="uri">The device-link URI from the QR code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> AddDevice(string number, string uri, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/devices/{Esc(number)}", new { uri }, cancellationToken);

    /// <summary>
    /// Removes a linked device via <c>DELETE /v1/devices/{number}/{deviceId}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="deviceId">The device identifier to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> RemoveLinkedDevice(string number, int deviceId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"v1/devices/{Esc(number)}/{deviceId}", cancellationToken: cancellationToken);

    /// <summary>
    /// Deletes local account data via <c>DELETE /v1/devices/{number}/local-data</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="ignoreRegistered">Whether to ignore that the account may still be registered.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> DeleteLocalAccountData(string number, bool ignoreRegistered = false, CancellationToken cancellationToken = default) =>
        DeleteAsync($"v1/devices/{Esc(number)}/local-data",
            new { ignore_registered = ignoreRegistered }, cancellationToken);

    #endregion

    #region Groups

    /// <summary>
    /// Returns all groups for the specified account via <c>GET /v1/groups/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<SignalGroup[]?> ListGroups(string number, CancellationToken cancellationToken = default) =>
        GetAsync<SignalGroup[]>($"v1/groups/{Esc(number)}", cancellationToken);

    /// <summary>
    /// Returns the details of a specific group via <c>GET /v1/groups/{number}/{groupId}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<SignalGroup?> GetGroup(string number, string groupId, CancellationToken cancellationToken = default) =>
        GetAsync<SignalGroup>($"v1/groups/{Esc(number)}/{Esc(groupId)}", cancellationToken);

    /// <summary>
    /// Creates a new group via <c>POST /v1/groups/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="request">The group creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<CreateGroupResponse?> CreateGroup(string number, CreateGroupRequest request, CancellationToken cancellationToken = default)
    {
        var requestUri = $"v1/groups/{Esc(number)}";
        try
        {
            var tpl = await PostJsonAsync<CreateGroupResponse, string>(requestUri, request, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (tpl.result is not null)
                _logger.LogInformation("{ClassName} group {GroupName} created with id {GroupId}",
                    nameof(SignalCliRestClientService), request.Name, tpl.result.Id);
            else
                _logger.LogWarning("{ClassName} create group failed for {Number}: {ErrorBody}",
                    nameof(SignalCliRestClientService), number, tpl.error);
            return tpl.result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to create group for {Number}",
                nameof(SignalCliRestClientService), number);
        }
        return null;
    }

    /// <summary>
    /// Updates an existing group via <c>PUT /v1/groups/{number}/{groupId}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="request">The group update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> UpdateGroup(string number, string groupId, UpdateGroupRequest request, CancellationToken cancellationToken = default) =>
        PutAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}", request, cancellationToken);

    /// <summary>
    /// Deletes a group via <c>DELETE /v1/groups/{number}/{groupId}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> DeleteGroup(string number, string groupId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}", cancellationToken: cancellationToken);

    /// <summary>
    /// Adds members to a group via <c>POST /v1/groups/{number}/{groupId}/members</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="members">Phone numbers of members to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> AddGroupMembers(string number, string groupId, string[] members, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/members", new { members }, cancellationToken);

    /// <summary>
    /// Removes members from a group via <c>DELETE /v1/groups/{number}/{groupId}/members</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="members">Phone numbers of members to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> RemoveGroupMembers(string number, string groupId, string[] members, CancellationToken cancellationToken = default) =>
        DeleteAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/members", new { members }, cancellationToken);

    /// <summary>
    /// Adds admins to a group via <c>POST /v1/groups/{number}/{groupId}/admins</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="admins">Phone numbers of admins to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> AddGroupAdmins(string number, string groupId, string[] admins, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/admins", new { admins }, cancellationToken);

    /// <summary>
    /// Removes admins from a group via <c>DELETE /v1/groups/{number}/{groupId}/admins</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="admins">Phone numbers of admins to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> RemoveGroupAdmins(string number, string groupId, string[] admins, CancellationToken cancellationToken = default) =>
        DeleteAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/admins", new { admins }, cancellationToken);

    /// <summary>
    /// Joins a group via <c>POST /v1/groups/{number}/{groupId}/join</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> JoinGroup(string number, string groupId, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/join", cancellationToken: cancellationToken);

    /// <summary>
    /// Leaves a group via <c>POST /v1/groups/{number}/{groupId}/quit</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> QuitGroup(string number, string groupId, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/quit", cancellationToken: cancellationToken);

    /// <summary>
    /// Blocks a group via <c>POST /v1/groups/{number}/{groupId}/block</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> BlockGroup(string number, string groupId, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/block", cancellationToken: cancellationToken);

    /// <summary>
    /// Downloads the avatar image for a group via <c>GET /v1/groups/{number}/{groupId}/avatar</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<byte[]?> GetGroupAvatar(string number, string groupId, CancellationToken cancellationToken = default) =>
        GetAsync<byte[]>($"v1/groups/{Esc(number)}/{Esc(groupId)}/avatar", cancellationToken);

    #endregion

    #region Identities

    /// <summary>
    /// Lists all known identities for the specified account via <c>GET /v1/identities/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<SignalIdentity[]?> ListIdentities(string number, CancellationToken cancellationToken = default) =>
        GetAsync<SignalIdentity[]>($"v1/identities/{Esc(number)}", cancellationToken);

    /// <summary>
    /// Trusts an identity via <c>PUT /v1/identities/{number}/trust/{numberToTrust}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="numberToTrust">The phone number of the identity to trust.</param>
    /// <param name="trustAllKnownKeys">Whether to trust all known keys.</param>
    /// <param name="verifiedSafetyNumber">Optional verified safety number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> TrustIdentity(string number, string numberToTrust, bool trustAllKnownKeys = false, string? verifiedSafetyNumber = null, CancellationToken cancellationToken = default) =>
        PutAsync($"v1/identities/{Esc(number)}/trust/{Esc(numberToTrust)}",
            new { trust_all_known_keys = trustAllKnownKeys, verified_safety_number = verifiedSafetyNumber }, cancellationToken);

    #endregion

    #region Attachments

    /// <summary>
    /// Returns all stored attachment identifiers via <c>GET /v1/attachments</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<string[]?> ListAttachments(CancellationToken cancellationToken = default) =>
        GetAsync<string[]>("v1/attachments", cancellationToken);

    /// <summary>
    /// Downloads the raw bytes of an attachment via <c>GET /v1/attachments/{id}</c>.
    /// </summary>
    /// <param name="attachmentId">The attachment identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<byte[]?> GetAttachment(string attachmentId, CancellationToken cancellationToken = default) =>
        GetAsync<byte[]>($"v1/attachments/{Esc(attachmentId)}", cancellationToken);

    /// <summary>
    /// Deletes an attachment via <c>DELETE /v1/attachments/{id}</c>.
    /// </summary>
    /// <param name="attachmentId">The attachment identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> DeleteAttachment(string attachmentId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"v1/attachments/{Esc(attachmentId)}", cancellationToken: cancellationToken);

    #endregion

    #region Profile

    /// <summary>
    /// Updates the Signal profile for the specified account via <c>PUT /v1/profiles/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="request">The profile update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> UpdateProfile(string number, UpdateProfileRequest request, CancellationToken cancellationToken = default) =>
        PutAsync($"v1/profiles/{Esc(number)}", request, cancellationToken);

    #endregion

    #region Search

    /// <summary>
    /// Searches for phone numbers registered on Signal via <c>GET /v1/search/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="numbers">The phone numbers to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<SearchResult[]?> SearchNumbers(string number, string[] numbers, CancellationToken cancellationToken = default)
    {
        var query = string.Join("&", numbers.Select(n => $"numbers={Esc(n)}"));
        return GetAsync<SearchResult[]>($"v1/search/{Esc(number)}?{query}", cancellationToken);
    }

    #endregion

    #region Sticker Packs

    /// <summary>
    /// Lists all installed sticker packs via <c>GET /v1/sticker-packs/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<SignalStickerPack[]?> ListStickerPacks(string number, CancellationToken cancellationToken = default) =>
        GetAsync<SignalStickerPack[]>($"v1/sticker-packs/{Esc(number)}", cancellationToken);

    /// <summary>
    /// Installs a sticker pack via <c>POST /v1/sticker-packs/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="packId">The sticker pack identifier.</param>
    /// <param name="packKey">The sticker pack key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> AddStickerPack(string number, string packId, string packKey, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/sticker-packs/{Esc(number)}", new { pack_id = packId, pack_key = packKey }, cancellationToken);

    #endregion

    #region Polls

    /// <summary>
    /// Creates a new poll via <c>POST /v1/polls/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="request">The poll creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<CreatePollResponse?> CreatePoll(string number, CreatePollRequest request, CancellationToken cancellationToken = default)
    {
        var requestUri = $"v1/polls/{Esc(number)}";
        try
        {
            var tpl = await PostJsonAsync<CreatePollResponse, string>(requestUri, request, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (tpl.result is not null)
                _logger.LogInformation("{ClassName} poll created for {Recipient}, timestamp {Timestamp}",
                    nameof(SignalCliRestClientService), request.Recipient, tpl.result.Timestamp);
            else
                _logger.LogWarning("{ClassName} create poll failed for {Number}: {ErrorBody}",
                    nameof(SignalCliRestClientService), number, tpl.error);
            return tpl.result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to create poll for {Number}",
                nameof(SignalCliRestClientService), number);
        }
        return null;
    }

    /// <summary>
    /// Closes an existing poll via <c>DELETE /v1/polls/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="request">The poll close request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> ClosePoll(string number, ClosePollRequest request, CancellationToken cancellationToken = default) =>
        DeleteAsync($"v1/polls/{Esc(number)}", request, cancellationToken);

    /// <summary>
    /// Submits a vote on a poll via <c>POST /v1/polls/{number}/vote</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="request">The vote request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> VotePoll(string number, VotePollRequest request, CancellationToken cancellationToken = default) =>
        PostBoolAsync($"v1/polls/{Esc(number)}/vote", request, cancellationToken);

    #endregion

    #region ISignalCliReceiver

    /// <inheritdoc/>
    /// <remarks>The REST transport is connectionless, so this completes immediately.</remarks>
    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public async IAsyncEnumerable<SignalReceivedMessage> StreamMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_config.ReceivePollIntervalMs));
        while (!cancellationToken.IsCancellationRequested)
        {
            var messages = await ReceiveMessages(_config.PhoneNumber, cancellationToken).ConfigureAwait(false);
            if (messages is not null)
                foreach (var message in messages)
                    yield return message;

            if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                yield break;
        }
    }

    #endregion

    #region INotifier

    /// <inheritdoc/>
    async Task<INotificationResponse?> INotifier.SendAsync(INotificationMessage message, CancellationToken cancellationToken) =>
        message is SignalMessageRequest signalMsg
            ? await SendMessage(signalMsg, cancellationToken).ConfigureAwait(false)
            : throw new ArgumentException($"Expected {nameof(SignalMessageRequest)}", nameof(message));

    /// <inheritdoc/>
    async Task<IReceivedNotification[]?> INotifier.ReceiveAsync(string account, CancellationToken cancellationToken) =>
        await ReceiveMessages(account, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    Task<byte[]?> INotifier.GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken) =>
        GetAttachment(attachmentId, cancellationToken);

    /// <inheritdoc/>
    async Task<INotificationGroup[]?> INotifier.ListGroupsAsync(string account, CancellationToken cancellationToken) =>
        await ListGroups(account, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    Task<bool> INotifier.StartProcessingAsync(string account, string recipient, CancellationToken cancellationToken) =>
        ShowTypingIndicator(account, recipient, cancellationToken);

    /// <inheritdoc/>
    Task<bool> INotifier.StopProcessingAsync(string account, string recipient, CancellationToken cancellationToken) =>
        HideTypingIndicator(account, recipient, cancellationToken);

    /// <inheritdoc/>
    Task<bool> INotifier.SendProgressUpdateAsync(string account, string recipient, string reaction, string targetAuthor, long timestamp, CancellationToken cancellationToken) =>
        SendReaction(account, recipient, reaction, targetAuthor, timestamp, cancellationToken);

    /// <inheritdoc/>
    Task<bool> INotifier.UpdateProfileNameAsync(string account, string displayName, CancellationToken cancellationToken) =>
        UpdateProfile(account, new UpdateProfileRequest { Name = displayName }, cancellationToken);

    #endregion

    #region Private helpers

    private async Task<TResult?> GetAsync<TResult>(string requestUri, CancellationToken cancellationToken = default, [CallerMemberName] string? caller = null)
        where TResult : class
    {
        try
        {
            var tpl = await base.GetAsync<TResult, object>(requestUri, cancellationToken: cancellationToken).ConfigureAwait(false);
            return tpl.result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return default;
    }

    private async Task<TResult?> PostAsync<TResult>(string requestUri, object body, CancellationToken cancellationToken = default, [CallerMemberName] string? caller = null)
        where TResult : class
    {
        try
        {
            var tpl = await PostJsonAsync<TResult, object>(requestUri, body, cancellationToken: cancellationToken).ConfigureAwait(false);
            return tpl.result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return default;
    }

    private async Task<bool> PostBoolAsync(string requestUri, object? body = null, CancellationToken cancellationToken = default, [CallerMemberName] string? caller = null)
    {
        try
        {
            var response = body is not null
                ? await Client.PostAsJsonAsync(requestUri, body, cancellationToken).ConfigureAwait(false)
                : await Client.PostAsync(requestUri, null, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return false;
    }

    private async Task<bool> PutAsync(string requestUri, object? body = null, CancellationToken cancellationToken = default, [CallerMemberName] string? caller = null)
    {
        try
        {
            var response = body is not null
                ? await Client.PutAsJsonAsync(requestUri, body, cancellationToken).ConfigureAwait(false)
                : await Client.PutAsync(requestUri, null, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return false;
    }

    private async Task<bool> DeleteAsync(string requestUri, object? body = null, CancellationToken cancellationToken = default, [CallerMemberName] string? caller = null)
    {
        try
        {
            using var response = await SendDeleteAsync(requestUri, body, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return false;
    }

    private async Task<T?> DeleteAsync<T>(string requestUri, object? body = null, CancellationToken cancellationToken = default, [CallerMemberName] string? caller = null)
    {
        try
        {
            using var response = await SendDeleteAsync(requestUri, body, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return default;
    }

    private async Task<HttpResponseMessage> SendDeleteAsync(string requestUri, object? body, CancellationToken cancellationToken)
    {
        if (body is null)
            return await Client.DeleteAsync(requestUri, cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri)
        {
            Content = JsonContent.Create(body)
        };
        return await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static string Esc(string value) => Uri.EscapeDataString(value);

    #endregion
}
