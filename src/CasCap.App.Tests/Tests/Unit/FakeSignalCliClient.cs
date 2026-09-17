using CasCap.Abstractions;
using CasCap.Models.Dtos;

namespace CasCap.Tests.Unit;

/// <summary>
/// Deterministic <see cref="ISignalCliClient"/> substitute exposing only the attachment surface;
/// every other member throws so an unexpected call fails the test loudly.
/// </summary>
public sealed class FakeSignalCliClient : ISignalCliClient
{
    private readonly Dictionary<string, int> _attemptsById = [];

    /// <summary>The attachment identifiers the server currently holds.</summary>
    public HashSet<string> Stored { get; } = [];

    /// <summary>Every attachment identifier passed to <see cref="DeleteAttachment"/>, in order.</summary>
    public List<string> DeleteCalls { get; } = [];

    /// <summary>Number of times the attachment store was listed.</summary>
    public int ListCallCount { get; private set; }

    /// <summary>
    /// Decides the outcome of one delete attempt, given the identifier and the one-based attempt
    /// number for that identifier. Defaults to removing the identifier and reporting success.
    /// </summary>
    public Func<string, int, bool>? DeleteResponder { get; set; }

    /// <inheritdoc/>
    public Task<string[]?> ListAttachments(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ListCallCount++;
        return Task.FromResult<string[]?>([.. Stored]);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAttachment(string attachmentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteCalls.Add(attachmentId);
        var attempt = _attemptsById.TryGetValue(attachmentId, out var previous) ? previous + 1 : 1;
        _attemptsById[attachmentId] = attempt;

        if (DeleteResponder is null)
        {
            Stored.Remove(attachmentId);
            return Task.FromResult(true);
        }

        var deleted = DeleteResponder(attachmentId, attempt);
        if (deleted)
            Stored.Remove(attachmentId);
        return Task.FromResult(deleted);
    }

    /// <summary>Counts the delete attempts recorded for one attachment identifier.</summary>
    public int AttemptsFor(string attachmentId) => DeleteCalls.Count(id => id == attachmentId);

    #region Unused surface

    /// <inheritdoc/>
    public Task<byte[]?> GetAttachment(string attachmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SignalAbout?> GetAbout(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SignalConfiguration?> GetConfiguration(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> SetConfiguration(SignalConfiguration config, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<TrustModeResponse?> GetAccountSettings(string number, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> SetAccountSettings(string number, TrustModeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SignalMessageResponse?> SendMessage(SignalMessageRequest msg, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SignalReceivedMessage[]?> ReceiveMessages(string number, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> ShowTypingIndicator(string number, string recipient, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> HideTypingIndicator(string number, string recipient, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> SendReaction(string number, string recipient, string reaction, string targetAuthor, long timestamp, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> RemoveReaction(string number, string recipient, string reaction, string targetAuthor, long timestamp, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> SendReceipt(string number, string recipient, string receiptType, long timestamp, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<RemoteDeleteResponse?> RemoteDelete(string number, string recipient, long timestamp, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> RegisterNumber(string number, bool useVoice = false, string? captcha = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> VerifyNumber(string number, string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> UnregisterNumber(string number, bool deleteAccount = false, bool deleteLocalData = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<string[]?> ListAccounts(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> SetPin(string number, string pin, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> RemovePin(string number, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> SubmitRateLimitChallenge(string number, string challengeToken, string captcha, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> UpdateAccountSettings(string number, bool? discoverableByNumber = null, bool? shareNumber = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SetUsernameResponse?> SetUsername(string number, string username, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> RemoveUsername(string number, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SignalContact[]?> ListContacts(string number, bool allRecipients = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> UpdateContact(string number, string recipient, string? name = null, int? expirationInSeconds = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> SyncContacts(string number, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SignalContact?> GetContact(string number, string uuid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<byte[]?> GetContactAvatar(string number, string uuid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<byte[]?> GetQrCodeLink(string deviceName = "signal-cli-rest-api", CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<DeviceLinkUriResponse?> GetQrCodeLinkRaw(string deviceName = "signal-cli-rest-api", CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SignalDevice[]?> ListLinkedDevices(string number, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> AddDevice(string number, string uri, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> RemoveLinkedDevice(string number, int deviceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> DeleteLocalAccountData(string number, bool ignoreRegistered = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SignalGroup[]?> ListGroups(string number, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SignalGroup?> GetGroup(string number, string groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<CreateGroupResponse?> CreateGroup(string number, CreateGroupRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> UpdateGroup(string number, string groupId, UpdateGroupRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> DeleteGroup(string number, string groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> AddGroupMembers(string number, string groupId, string[] members, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> RemoveGroupMembers(string number, string groupId, string[] members, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> AddGroupAdmins(string number, string groupId, string[] admins, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> RemoveGroupAdmins(string number, string groupId, string[] admins, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> JoinGroup(string number, string groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> QuitGroup(string number, string groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> BlockGroup(string number, string groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<byte[]?> GetGroupAvatar(string number, string groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SignalIdentity[]?> ListIdentities(string number, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> TrustIdentity(string number, string numberToTrust, bool trustAllKnownKeys = false, string? verifiedSafetyNumber = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> UpdateProfile(string number, UpdateProfileRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SearchResult[]?> SearchNumbers(string number, string[] numbers, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SignalStickerPack[]?> ListStickerPacks(string number, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> AddStickerPack(string number, string packId, string packKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<CreatePollResponse?> CreatePoll(string number, CreatePollRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> ClosePoll(string number, ClosePollRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> VotePoll(string number, VotePollRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    #endregion
}
