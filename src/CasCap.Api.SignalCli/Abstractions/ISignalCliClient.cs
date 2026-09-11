namespace CasCap.Abstractions;

/// <summary>
/// The full signal-cli REST API surface, as implemented by
/// <see cref="CasCap.Services.SignalCliRestClientService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Depend on this rather than the concrete client so consumers can substitute a fake in tests.
/// Every method returns <see langword="null"/> or <see langword="false"/> on failure and logs the
/// cause; failures are not thrown. The exception is caller-requested cancellation, which propagates
/// as <see cref="OperationCanceledException"/> so an abandoned call is never mistaken for an API error.
/// </para>
/// <para>
/// Registration, verification and device-linking operations are unavailable when the signal-cli
/// server runs in a <c>json-rpc</c> mode, per upstream documentation. See
/// <see href="https://bbernhard.github.io/signal-cli-rest-api/"/> for the full API specification.
/// </para>
/// </remarks>
public interface ISignalCliClient
{
    #region General

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.GetAbout"/>
    Task<SignalAbout?> GetAbout(CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.GetConfiguration"/>
    Task<SignalConfiguration?> GetConfiguration(CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.SetConfiguration"/>
    Task<bool> SetConfiguration(SignalConfiguration config, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.GetAccountSettings"/>
    Task<TrustModeResponse?> GetAccountSettings(string number, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.SetAccountSettings"/>
    Task<bool> SetAccountSettings(string number, TrustModeRequest request, CancellationToken cancellationToken = default);

    #endregion

    #region Messaging

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.SendMessage"/>
    Task<SignalMessageResponse?> SendMessage(SignalMessageRequest msg, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.ReceiveMessages"/>
    Task<SignalReceivedMessage[]?> ReceiveMessages(string number, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.ShowTypingIndicator"/>
    Task<bool> ShowTypingIndicator(string number, string recipient, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.HideTypingIndicator"/>
    Task<bool> HideTypingIndicator(string number, string recipient, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.SendReaction"/>
    Task<bool> SendReaction(string number, string recipient, string reaction, string targetAuthor, long timestamp, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.RemoveReaction"/>
    Task<bool> RemoveReaction(string number, string recipient, string reaction, string targetAuthor, long timestamp, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.SendReceipt"/>
    Task<bool> SendReceipt(string number, string recipient, string receiptType, long timestamp, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.RemoteDelete"/>
    Task<RemoteDeleteResponse?> RemoteDelete(string number, string recipient, long timestamp, CancellationToken cancellationToken = default);

    #endregion

    #region Registration

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.RegisterNumber"/>
    Task<bool> RegisterNumber(string number, bool useVoice = false, string? captcha = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.VerifyNumber"/>
    Task<bool> VerifyNumber(string number, string token, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.UnregisterNumber"/>
    Task<bool> UnregisterNumber(string number, bool deleteAccount = false, bool deleteLocalData = false, CancellationToken cancellationToken = default);

    #endregion

    #region Accounts

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.ListAccounts"/>
    Task<string[]?> ListAccounts(CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.SetPin"/>
    Task<bool> SetPin(string number, string pin, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.RemovePin"/>
    Task<bool> RemovePin(string number, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.SubmitRateLimitChallenge"/>
    Task<bool> SubmitRateLimitChallenge(string number, string challengeToken, string captcha, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.UpdateAccountSettings"/>
    Task<bool> UpdateAccountSettings(string number, bool? discoverableByNumber = null, bool? shareNumber = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.SetUsername"/>
    Task<SetUsernameResponse?> SetUsername(string number, string username, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.RemoveUsername"/>
    Task<bool> RemoveUsername(string number, CancellationToken cancellationToken = default);

    #endregion

    #region Contacts

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.ListContacts"/>
    Task<SignalContact[]?> ListContacts(string number, bool allRecipients = false, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.UpdateContact"/>
    Task<bool> UpdateContact(string number, string recipient, string? name = null, int? expirationInSeconds = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.SyncContacts"/>
    Task<bool> SyncContacts(string number, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.GetContact"/>
    Task<SignalContact?> GetContact(string number, string uuid, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.GetContactAvatar"/>
    Task<byte[]?> GetContactAvatar(string number, string uuid, CancellationToken cancellationToken = default);

    #endregion

    #region Devices

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.GetQrCodeLink"/>
    Task<byte[]?> GetQrCodeLink(string deviceName = "signal-cli-rest-api", CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.GetQrCodeLinkRaw"/>
    Task<DeviceLinkUriResponse?> GetQrCodeLinkRaw(string deviceName = "signal-cli-rest-api", CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.ListLinkedDevices"/>
    Task<SignalDevice[]?> ListLinkedDevices(string number, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.AddDevice"/>
    Task<bool> AddDevice(string number, string uri, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.RemoveLinkedDevice"/>
    Task<bool> RemoveLinkedDevice(string number, int deviceId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.DeleteLocalAccountData"/>
    Task<bool> DeleteLocalAccountData(string number, bool ignoreRegistered = false, CancellationToken cancellationToken = default);

    #endregion

    #region Groups

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.ListGroups"/>
    Task<SignalGroup[]?> ListGroups(string number, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.GetGroup"/>
    Task<SignalGroup?> GetGroup(string number, string groupId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.CreateGroup"/>
    Task<CreateGroupResponse?> CreateGroup(string number, CreateGroupRequest request, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.UpdateGroup"/>
    Task<bool> UpdateGroup(string number, string groupId, UpdateGroupRequest request, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.DeleteGroup"/>
    Task<bool> DeleteGroup(string number, string groupId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.AddGroupMembers"/>
    Task<bool> AddGroupMembers(string number, string groupId, string[] members, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.RemoveGroupMembers"/>
    Task<bool> RemoveGroupMembers(string number, string groupId, string[] members, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.AddGroupAdmins"/>
    Task<bool> AddGroupAdmins(string number, string groupId, string[] admins, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.RemoveGroupAdmins"/>
    Task<bool> RemoveGroupAdmins(string number, string groupId, string[] admins, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.JoinGroup"/>
    Task<bool> JoinGroup(string number, string groupId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.QuitGroup"/>
    Task<bool> QuitGroup(string number, string groupId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.BlockGroup"/>
    Task<bool> BlockGroup(string number, string groupId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.GetGroupAvatar"/>
    Task<byte[]?> GetGroupAvatar(string number, string groupId, CancellationToken cancellationToken = default);

    #endregion

    #region Identities

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.ListIdentities"/>
    Task<SignalIdentity[]?> ListIdentities(string number, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.TrustIdentity"/>
    Task<bool> TrustIdentity(string number, string numberToTrust, bool trustAllKnownKeys = false, string? verifiedSafetyNumber = null, CancellationToken cancellationToken = default);

    #endregion

    #region Attachments

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.ListAttachments"/>
    Task<string[]?> ListAttachments(CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.GetAttachment"/>
    Task<byte[]?> GetAttachment(string attachmentId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.DeleteAttachment"/>
    Task<bool> DeleteAttachment(string attachmentId, CancellationToken cancellationToken = default);

    #endregion

    #region Profile

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.UpdateProfile"/>
    Task<bool> UpdateProfile(string number, UpdateProfileRequest request, CancellationToken cancellationToken = default);

    #endregion

    #region Search

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.SearchNumbers"/>
    Task<SearchResult[]?> SearchNumbers(string number, string[] numbers, CancellationToken cancellationToken = default);

    #endregion

    #region Sticker Packs

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.ListStickerPacks"/>
    Task<SignalStickerPack[]?> ListStickerPacks(string number, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.AddStickerPack"/>
    Task<bool> AddStickerPack(string number, string packId, string packKey, CancellationToken cancellationToken = default);

    #endregion

    #region Polls

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.CreatePoll"/>
    Task<CreatePollResponse?> CreatePoll(string number, CreatePollRequest request, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.ClosePoll"/>
    Task<bool> ClosePoll(string number, ClosePollRequest request, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="CasCap.Services.SignalCliRestClientService.VotePoll"/>
    Task<bool> VotePoll(string number, VotePollRequest request, CancellationToken cancellationToken = default);

    #endregion
}
