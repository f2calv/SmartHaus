namespace CasCap.Services;

/// <summary>
/// HTTP client for the signal-cli REST API.
/// </summary>
/// <remarks>
/// See <see href="https://bbernhard.github.io/signal-cli-rest-api/"/> for the full API specification.
/// </remarks>
public class SignalCliRestClientService : HttpClientBase, INotifier
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
    public Task<SignalAbout?> GetAbout() =>
        GetAsync<SignalAbout>("v1/about");

    /// <summary>
    /// Retrieves the current signal-cli configuration from <c>GET /v1/configuration</c>.
    /// </summary>
    public Task<SignalConfiguration?> GetConfiguration() =>
        GetAsync<SignalConfiguration>("v1/configuration");

    /// <summary>
    /// Updates the signal-cli configuration via <c>POST /v1/configuration</c>.
    /// </summary>
    /// <param name="config">The configuration to apply.</param>
    public Task<bool> SetConfiguration(SignalConfiguration config) =>
        PostBoolAsync("v1/configuration", config);

    /// <summary>
    /// Retrieves account-specific trust mode settings via <c>GET /v1/configuration/{number}/settings</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    public Task<TrustModeResponse?> GetAccountSettings(string number) =>
        GetAsync<TrustModeResponse>($"v1/configuration/{Esc(number)}/settings");

    /// <summary>
    /// Sets account-specific trust mode settings via <c>POST /v1/configuration/{number}/settings</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="request">The trust mode to set.</param>
    public Task<bool> SetAccountSettings(string number, TrustModeRequest request) =>
        PostBoolAsync($"v1/configuration/{Esc(number)}/settings", request);

    #endregion

    #region Messaging

    /// <summary>
    /// Sends a Signal message to one or more recipients via <c>POST /v2/send</c>.
    /// </summary>
    /// <param name="msg">The message request.</param>
    public async Task<SignalMessageResponse?> SendMessage(SignalMessageRequest msg)
    {
        const string requestUri = "v2/send";
        try
        {
            var tpl = await PostJsonAsync<SignalMessageResponse, string>(requestUri, msg, TimeSpan.FromMilliseconds(_config.SendTimeoutMs));
            if (tpl.result is not null)
                _logger.LogDebug("{ClassName} message {Message} sent, timestamp {Timestamp}",
                    nameof(SignalCliRestClientService), msg.Message, tpl.result.Timestamp);
            else
                _logger.LogWarning("{ClassName} {RequestUri} failed: {ErrorBody}",
                    nameof(SignalCliRestClientService), requestUri, tpl.error);
            return tpl.result;
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
    public Task<bool> ShowTypingIndicator(string number, string recipient) =>
        PutAsync($"v1/typing-indicator/{Esc(number)}", new { recipient });

    /// <summary>
    /// Hides a typing indicator for the specified sender number via <c>DELETE /v1/typing-indicator/{number}</c>.
    /// </summary>
    /// <param name="number">The sender's phone number.</param>
    /// <param name="recipient">The recipient's phone number or group ID.</param>
    public Task<bool> HideTypingIndicator(string number, string recipient) =>
        DeleteAsync($"v1/typing-indicator/{Esc(number)}", new { recipient });

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
            //TODO: remove raw response logging once receive deserialization is confirmed working.
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Client.BaseAddress}{requestUri}");
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("{ClassName} raw receive response ({StatusCode}, {Length} chars): {RawJson}",
                nameof(SignalCliRestClientService), response.StatusCode, rawJson.Length, rawJson);
            var messages = rawJson.FromJson<SignalReceivedMessage[]>();
            if (messages is not null)
                _logger.LogDebug("{ClassName} received {Count} message(s) for {Number}",
                    nameof(SignalCliRestClientService), messages.Length, number);
            return messages;
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
    public Task<bool> SendReaction(string number, string recipient, string reaction, string targetAuthor, long timestamp) =>
        PostBoolAsync($"v1/reactions/{Esc(number)}", new { recipient, reaction, target_author = targetAuthor, timestamp });

    /// <summary>
    /// Removes a reaction from a message via <c>DELETE /v1/reactions/{number}</c>.
    /// </summary>
    /// <param name="number">The sender's phone number.</param>
    /// <param name="recipient">The recipient's phone number or group ID.</param>
    /// <param name="reaction">The reaction emoji to remove.</param>
    /// <param name="targetAuthor">The author of the message the reaction was on.</param>
    /// <param name="timestamp">The timestamp of the target message.</param>
    public Task<bool> RemoveReaction(string number, string recipient, string reaction, string targetAuthor, long timestamp) =>
        DeleteAsync($"v1/reactions/{Esc(number)}", new { recipient, reaction, target_author = targetAuthor, timestamp });

    /// <summary>
    /// Sends a read/viewed receipt via <c>POST /v1/receipts/{number}</c>.
    /// </summary>
    /// <param name="number">The sender's phone number.</param>
    /// <param name="recipient">The recipient's phone number.</param>
    /// <param name="receiptType">The receipt type (<c>"read"</c> or <c>"viewed"</c>).</param>
    /// <param name="timestamp">The timestamp of the message to acknowledge.</param>
    public Task<bool> SendReceipt(string number, string recipient, string receiptType, long timestamp) =>
        PostBoolAsync($"v1/receipts/{Esc(number)}", new { recipient, receipt_type = receiptType, timestamp });

    /// <summary>
    /// Remotely deletes a previously sent message via <c>DELETE /v1/remote-delete/{number}</c>.
    /// </summary>
    /// <param name="number">The sender's phone number.</param>
    /// <param name="recipient">The recipient's phone number or group ID.</param>
    /// <param name="timestamp">The timestamp of the message to delete.</param>
    public Task<RemoteDeleteResponse?> RemoteDelete(string number, string recipient, long timestamp) =>
        DeleteAsync<RemoteDeleteResponse>($"v1/remote-delete/{Esc(number)}", new { recipient, timestamp });

    #endregion

    #region Registration

    /// <summary>
    /// Registers a phone number with Signal via <c>POST /v1/register/{number}</c>.
    /// </summary>
    /// <param name="number">The phone number to register in international format.</param>
    /// <param name="useVoice">Whether to use voice verification instead of SMS.</param>
    /// <param name="captcha">Optional captcha value if required by the Signal server.</param>
    public Task<bool> RegisterNumber(string number, bool useVoice = false, string? captcha = null) =>
        PostBoolAsync($"v1/register/{Esc(number)}", new { use_voice = useVoice, captcha });

    /// <summary>
    /// Verifies a registered phone number via <c>POST /v1/register/{number}/verify/{token}</c>.
    /// </summary>
    /// <param name="number">The phone number to verify.</param>
    /// <param name="token">The verification token received via SMS or voice call.</param>
    public Task<bool> VerifyNumber(string number, string token) =>
        PostBoolAsync($"v1/register/{Esc(number)}/verify/{Esc(token)}");

    /// <summary>
    /// Unregisters a phone number from Signal via <c>POST /v1/unregister/{number}</c>.
    /// </summary>
    /// <param name="number">The phone number to unregister.</param>
    /// <param name="deleteAccount">Whether to delete the account from the Signal server.</param>
    /// <param name="deleteLocalData">Whether to delete local data.</param>
    public Task<bool> UnregisterNumber(string number, bool deleteAccount = false, bool deleteLocalData = false) =>
        PostBoolAsync($"v1/unregister/{Esc(number)}", new { delete_account = deleteAccount, delete_local_data = deleteLocalData });

    #endregion

    #region Accounts

    /// <summary>
    /// Returns all registered accounts via <c>GET /v1/accounts</c>.
    /// </summary>
    public Task<string[]?> ListAccounts() =>
        GetAsync<string[]>("v1/accounts");

    /// <summary>
    /// Sets a registration PIN for the specified account via <c>POST /v1/accounts/{number}/pin</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="pin">The PIN to set.</param>
    public Task<bool> SetPin(string number, string pin) =>
        PostBoolAsync($"v1/accounts/{Esc(number)}/pin", new { pin });

    /// <summary>
    /// Removes the registration PIN from the specified account via <c>DELETE /v1/accounts/{number}/pin</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    public Task<bool> RemovePin(string number) =>
        DeleteAsync($"v1/accounts/{Esc(number)}/pin");

    /// <summary>
    /// Submits a rate-limit challenge via <c>POST /v1/accounts/{number}/rate-limit-challenge</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="challengeToken">The challenge token.</param>
    /// <param name="captcha">The captcha solution.</param>
    public Task<bool> SubmitRateLimitChallenge(string number, string challengeToken, string captcha) =>
        PostBoolAsync($"v1/accounts/{Esc(number)}/rate-limit-challenge", new { challenge_token = challengeToken, captcha });

    /// <summary>
    /// Updates account settings via <c>PUT /v1/accounts/{number}/settings</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="discoverableByNumber">Whether the account is discoverable by phone number.</param>
    /// <param name="shareNumber">Whether to share the phone number with contacts.</param>
    public Task<bool> UpdateAccountSettings(string number, bool? discoverableByNumber = null, bool? shareNumber = null) =>
        PutAsync($"v1/accounts/{Esc(number)}/settings",
            new { discoverable_by_number = discoverableByNumber, share_number = shareNumber });

    /// <summary>
    /// Sets a username for the specified account via <c>POST /v1/accounts/{number}/username</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="username">The desired username.</param>
    public Task<SetUsernameResponse?> SetUsername(string number, string username) =>
        PostAsync<SetUsernameResponse>($"v1/accounts/{Esc(number)}/username", new { username });

    /// <summary>
    /// Removes the username from the specified account via <c>DELETE /v1/accounts/{number}/username</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    public Task<bool> RemoveUsername(string number) =>
        DeleteAsync($"v1/accounts/{Esc(number)}/username");

    #endregion

    #region Contacts

    /// <summary>
    /// Lists contacts for the specified account via <c>GET /v1/contacts/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="allRecipients">When <see langword="true"/>, returns all known recipients (not just contacts).</param>
    public Task<SignalContact[]?> ListContacts(string number, bool allRecipients = false) =>
        GetAsync<SignalContact[]>($"v1/contacts/{Esc(number)}{(allRecipients ? "?allRecipients=true" : "")}");

    /// <summary>
    /// Updates a contact for the specified account via <c>PUT /v1/contacts/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="recipient">The contact's phone number.</param>
    /// <param name="name">The display name for the contact.</param>
    /// <param name="expirationInSeconds">Optional message expiration in seconds.</param>
    public Task<bool> UpdateContact(string number, string recipient, string? name = null, int? expirationInSeconds = null) =>
        PutAsync($"v1/contacts/{Esc(number)}",
            new { recipient, name, expiration_in_seconds = expirationInSeconds });

    /// <summary>
    /// Triggers a contact sync for the specified account via <c>POST /v1/contacts/{number}/sync</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    public Task<bool> SyncContacts(string number) =>
        PostBoolAsync($"v1/contacts/{Esc(number)}/sync");

    /// <summary>
    /// Returns a specific contact by UUID via <c>GET /v1/contacts/{number}/{uuid}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="uuid">The contact's UUID.</param>
    public Task<SignalContact?> GetContact(string number, string uuid) =>
        GetAsync<SignalContact>($"v1/contacts/{Esc(number)}/{Esc(uuid)}");

    /// <summary>
    /// Downloads the avatar image of a contact via <c>GET /v1/contacts/{number}/{uuid}/avatar</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="uuid">The contact's UUID.</param>
    public Task<byte[]?> GetContactAvatar(string number, string uuid) =>
        GetAsync<byte[]>($"v1/contacts/{Esc(number)}/{Esc(uuid)}/avatar");

    #endregion

    #region Devices

    /// <summary>
    /// Returns the QR code image bytes for linking a new device via <c>GET /v1/qrcodelink</c>.
    /// </summary>
    /// <param name="deviceName">A display name for the new linked device.</param>
    public Task<byte[]?> GetQrCodeLink(string deviceName = "signal-cli-rest-api") =>
        GetAsync<byte[]>($"v1/qrcodelink?device_name={Esc(deviceName)}");

    /// <summary>
    /// Returns the device-link URI for linking a new device via <c>GET /v1/qrcodelink/raw</c>.
    /// </summary>
    /// <param name="deviceName">A display name for the new linked device.</param>
    public Task<DeviceLinkUriResponse?> GetQrCodeLinkRaw(string deviceName = "signal-cli-rest-api") =>
        GetAsync<DeviceLinkUriResponse>($"v1/qrcodelink/raw?device_name={Esc(deviceName)}");

    /// <summary>
    /// Returns all devices linked to the specified account via <c>GET /v1/devices/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    public Task<SignalDevice[]?> ListLinkedDevices(string number) =>
        GetAsync<SignalDevice[]>($"v1/devices/{Esc(number)}");

    /// <summary>
    /// Links a new device to the specified account via <c>POST /v1/devices/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="uri">The device-link URI from the QR code.</param>
    public Task<bool> AddDevice(string number, string uri) =>
        PostBoolAsync($"v1/devices/{Esc(number)}", new { uri });

    /// <summary>
    /// Removes a linked device via <c>DELETE /v1/devices/{number}/{deviceId}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="deviceId">The device identifier to remove.</param>
    public Task<bool> RemoveLinkedDevice(string number, int deviceId) =>
        DeleteAsync($"v1/devices/{Esc(number)}/{deviceId}");

    /// <summary>
    /// Deletes local account data via <c>DELETE /v1/devices/{number}/local-data</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="ignoreRegistered">Whether to ignore that the account may still be registered.</param>
    public Task<bool> DeleteLocalAccountData(string number, bool ignoreRegistered = false) =>
        DeleteAsync($"v1/devices/{Esc(number)}/local-data",
            new { ignore_registered = ignoreRegistered });

    #endregion

    #region Groups

    /// <summary>
    /// Returns all groups for the specified account via <c>GET /v1/groups/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    public Task<SignalGroup[]?> ListGroups(string number) =>
        GetAsync<SignalGroup[]>($"v1/groups/{Esc(number)}");

    /// <summary>
    /// Returns the details of a specific group via <c>GET /v1/groups/{number}/{groupId}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    public Task<SignalGroup?> GetGroup(string number, string groupId) =>
        GetAsync<SignalGroup>($"v1/groups/{Esc(number)}/{Esc(groupId)}");

    /// <summary>
    /// Creates a new group via <c>POST /v1/groups/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="request">The group creation request.</param>
    public async Task<CreateGroupResponse?> CreateGroup(string number, CreateGroupRequest request)
    {
        var requestUri = $"v1/groups/{Esc(number)}";
        try
        {
            var tpl = await PostJsonAsync<CreateGroupResponse, string>(requestUri, request);
            if (tpl.result is not null)
                _logger.LogInformation("{ClassName} group {GroupName} created with id {GroupId}",
                    nameof(SignalCliRestClientService), request.Name, tpl.result.Id);
            else
                _logger.LogWarning("{ClassName} create group failed for {Number}: {ErrorBody}",
                    nameof(SignalCliRestClientService), number, tpl.error);
            return tpl.result;
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
    public Task<bool> UpdateGroup(string number, string groupId, UpdateGroupRequest request) =>
        PutAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}", request);

    /// <summary>
    /// Deletes a group via <c>DELETE /v1/groups/{number}/{groupId}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    public Task<bool> DeleteGroup(string number, string groupId) =>
        DeleteAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}");

    /// <summary>
    /// Adds members to a group via <c>POST /v1/groups/{number}/{groupId}/members</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="members">Phone numbers of members to add.</param>
    public Task<bool> AddGroupMembers(string number, string groupId, string[] members) =>
        PostBoolAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/members", new { members });

    /// <summary>
    /// Removes members from a group via <c>DELETE /v1/groups/{number}/{groupId}/members</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="members">Phone numbers of members to remove.</param>
    public Task<bool> RemoveGroupMembers(string number, string groupId, string[] members) =>
        DeleteAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/members", new { members });

    /// <summary>
    /// Adds admins to a group via <c>POST /v1/groups/{number}/{groupId}/admins</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="admins">Phone numbers of admins to add.</param>
    public Task<bool> AddGroupAdmins(string number, string groupId, string[] admins) =>
        PostBoolAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/admins", new { admins });

    /// <summary>
    /// Removes admins from a group via <c>DELETE /v1/groups/{number}/{groupId}/admins</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="admins">Phone numbers of admins to remove.</param>
    public Task<bool> RemoveGroupAdmins(string number, string groupId, string[] admins) =>
        DeleteAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/admins", new { admins });

    /// <summary>
    /// Joins a group via <c>POST /v1/groups/{number}/{groupId}/join</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    public Task<bool> JoinGroup(string number, string groupId) =>
        PostBoolAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/join");

    /// <summary>
    /// Leaves a group via <c>POST /v1/groups/{number}/{groupId}/quit</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    public Task<bool> QuitGroup(string number, string groupId) =>
        PostBoolAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/quit");

    /// <summary>
    /// Blocks a group via <c>POST /v1/groups/{number}/{groupId}/block</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    public Task<bool> BlockGroup(string number, string groupId) =>
        PostBoolAsync($"v1/groups/{Esc(number)}/{Esc(groupId)}/block");

    /// <summary>
    /// Downloads the avatar image for a group via <c>GET /v1/groups/{number}/{groupId}/avatar</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="groupId">The group identifier.</param>
    public Task<byte[]?> GetGroupAvatar(string number, string groupId) =>
        GetAsync<byte[]>($"v1/groups/{Esc(number)}/{Esc(groupId)}/avatar");

    #endregion

    #region Identities

    /// <summary>
    /// Lists all known identities for the specified account via <c>GET /v1/identities/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    public Task<SignalIdentity[]?> ListIdentities(string number) =>
        GetAsync<SignalIdentity[]>($"v1/identities/{Esc(number)}");

    /// <summary>
    /// Trusts an identity via <c>PUT /v1/identities/{number}/trust/{numberToTrust}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="numberToTrust">The phone number of the identity to trust.</param>
    /// <param name="trustAllKnownKeys">Whether to trust all known keys.</param>
    /// <param name="verifiedSafetyNumber">Optional verified safety number.</param>
    public Task<bool> TrustIdentity(string number, string numberToTrust, bool trustAllKnownKeys = false, string? verifiedSafetyNumber = null) =>
        PutAsync($"v1/identities/{Esc(number)}/trust/{Esc(numberToTrust)}",
            new { trust_all_known_keys = trustAllKnownKeys, verified_safety_number = verifiedSafetyNumber });

    #endregion

    #region Attachments

    /// <summary>
    /// Returns all stored attachment identifiers via <c>GET /v1/attachments</c>.
    /// </summary>
    public Task<string[]?> ListAttachments() =>
        GetAsync<string[]>("v1/attachments");

    /// <summary>
    /// Downloads the raw bytes of an attachment via <c>GET /v1/attachments/{id}</c>.
    /// </summary>
    /// <param name="attachmentId">The attachment identifier.</param>
    public Task<byte[]?> GetAttachment(string attachmentId) =>
        GetAsync<byte[]>($"v1/attachments/{Esc(attachmentId)}");

    /// <summary>
    /// Deletes an attachment via <c>DELETE /v1/attachments/{id}</c>.
    /// </summary>
    /// <param name="attachmentId">The attachment identifier.</param>
    public Task<bool> DeleteAttachment(string attachmentId) =>
        DeleteAsync($"v1/attachments/{Esc(attachmentId)}");

    #endregion

    #region Profile

    /// <summary>
    /// Updates the Signal profile for the specified account via <c>PUT /v1/profiles/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="request">The profile update request.</param>
    public Task<bool> UpdateProfile(string number, UpdateProfileRequest request) =>
        PutAsync($"v1/profiles/{Esc(number)}", request);

    #endregion

    #region Search

    /// <summary>
    /// Searches for phone numbers registered on Signal via <c>GET /v1/search/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="numbers">The phone numbers to search for.</param>
    public Task<SearchResult[]?> SearchNumbers(string number, string[] numbers)
    {
        var query = string.Join("&", numbers.Select(n => $"numbers={Esc(n)}"));
        return GetAsync<SearchResult[]>($"v1/search/{Esc(number)}?{query}");
    }

    #endregion

    #region Sticker Packs

    /// <summary>
    /// Lists all installed sticker packs via <c>GET /v1/sticker-packs/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    public Task<SignalStickerPack[]?> ListStickerPacks(string number) =>
        GetAsync<SignalStickerPack[]>($"v1/sticker-packs/{Esc(number)}");

    /// <summary>
    /// Installs a sticker pack via <c>POST /v1/sticker-packs/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="packId">The sticker pack identifier.</param>
    /// <param name="packKey">The sticker pack key.</param>
    public Task<bool> AddStickerPack(string number, string packId, string packKey) =>
        PostBoolAsync($"v1/sticker-packs/{Esc(number)}", new { pack_id = packId, pack_key = packKey });

    #endregion

    #region Polls

    /// <summary>
    /// Creates a new poll via <c>POST /v1/polls/{number}</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="request">The poll creation request.</param>
    public async Task<CreatePollResponse?> CreatePoll(string number, CreatePollRequest request)
    {
        var requestUri = $"v1/polls/{Esc(number)}";
        try
        {
            var tpl = await PostJsonAsync<CreatePollResponse, string>(requestUri, request);
            if (tpl.result is not null)
                _logger.LogInformation("{ClassName} poll created for {Recipient}, timestamp {Timestamp}",
                    nameof(SignalCliRestClientService), request.Recipient, tpl.result.Timestamp);
            else
                _logger.LogWarning("{ClassName} create poll failed for {Number}: {ErrorBody}",
                    nameof(SignalCliRestClientService), number, tpl.error);
            return tpl.result;
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
    public Task<bool> ClosePoll(string number, ClosePollRequest request) =>
        DeleteAsync($"v1/polls/{Esc(number)}", request);

    /// <summary>
    /// Submits a vote on a poll via <c>POST /v1/polls/{number}/vote</c>.
    /// </summary>
    /// <param name="number">The account phone number.</param>
    /// <param name="request">The vote request.</param>
    public Task<bool> VotePoll(string number, VotePollRequest request) =>
        PostBoolAsync($"v1/polls/{Esc(number)}/vote", request);

    #endregion

    #region INotifier

    /// <inheritdoc/>
    async Task<INotificationResponse?> INotifier.SendAsync(INotificationMessage message, CancellationToken cancellationToken)
    {
        if (message is SignalMessageRequest signalMsg)
            return await SendMessage(signalMsg);
        throw new ArgumentException($"Expected {nameof(SignalMessageRequest)}", nameof(message));
    }

    /// <inheritdoc/>
    async Task<IReceivedNotification[]?> INotifier.ReceiveAsync(string account, CancellationToken cancellationToken) =>
        await ReceiveMessages(account, cancellationToken);

    /// <inheritdoc/>
    Task<byte[]?> INotifier.GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken) =>
        GetAttachment(attachmentId);

    /// <inheritdoc/>
    async Task<INotificationGroup[]?> INotifier.ListGroupsAsync(string account, CancellationToken cancellationToken) =>
        await ListGroups(account);

    /// <inheritdoc/>
    Task<bool> INotifier.StartProcessingAsync(string account, string recipient, CancellationToken cancellationToken) =>
        ShowTypingIndicator(account, recipient);

    /// <inheritdoc/>
    Task<bool> INotifier.StopProcessingAsync(string account, string recipient, CancellationToken cancellationToken) =>
        HideTypingIndicator(account, recipient);

    /// <inheritdoc/>
    Task<bool> INotifier.SendProgressUpdateAsync(string account, string recipient, string reaction, string targetAuthor, long timestamp, CancellationToken cancellationToken) =>
        SendReaction(account, recipient, reaction, targetAuthor, timestamp);

    /// <inheritdoc/>
    Task<bool> INotifier.UpdateProfileNameAsync(string account, string displayName, CancellationToken cancellationToken) =>
        UpdateProfile(account, new UpdateProfileRequest { Name = displayName });

    #endregion

    #region Private helpers

    private async Task<TResult?> GetAsync<TResult>(string requestUri, [CallerMemberName] string? caller = null)
        where TResult : class
    {
        try
        {
            var tpl = await base.GetAsync<TResult, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return default;
    }

    private async Task<TResult?> PostAsync<TResult>(string requestUri, object body, [CallerMemberName] string? caller = null)
        where TResult : class
    {
        try
        {
            var tpl = await PostJsonAsync<TResult, object>(requestUri, body);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return default;
    }

    private async Task<bool> PostBoolAsync(string requestUri, object? body = null, [CallerMemberName] string? caller = null)
    {
        try
        {
            var response = body is not null
                ? await Client.PostAsJsonAsync(requestUri, body)
                : await Client.PostAsync(requestUri, null);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return false;
    }

    private async Task<bool> PutAsync(string requestUri, object? body = null, [CallerMemberName] string? caller = null)
    {
        try
        {
            var response = body is not null
                ? await Client.PutAsJsonAsync(requestUri, body)
                : await Client.PutAsync(requestUri, null);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return false;
    }

    private async Task<bool> DeleteAsync(string requestUri, object? body = null, [CallerMemberName] string? caller = null)
    {
        try
        {
            HttpResponseMessage response;
            if (body is not null)
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, requestUri)
                {
                    Content = JsonContent.Create(body)
                };
                response = await Client.SendAsync(request);
            }
            else
                response = await Client.DeleteAsync(requestUri);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return false;
    }

    private async Task<T?> DeleteAsync<T>(string requestUri, object? body = null, [CallerMemberName] string? caller = null)
    {
        try
        {
            HttpResponseMessage response;
            if (body is not null)
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, requestUri)
                {
                    Content = JsonContent.Create(body)
                };
                response = await Client.SendAsync(request);
            }
            else
                response = await Client.DeleteAsync(requestUri);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {Caller} failed for {RequestUri}",
                nameof(SignalCliRestClientService), caller, requestUri);
        }
        return default;
    }

    private static string Esc(string value) => Uri.EscapeDataString(value);

    #endregion
}
