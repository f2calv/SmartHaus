namespace CasCap.Tests;

/// <summary>
/// Integration tests for <see cref="SignalCliRestClientService"/> against a real signal-cli REST API instance.
/// </summary>
/// <remarks>
/// These tests require a running signal-cli REST API configured via <c>appsettings.Development.json</c>
/// with a valid <c>SignalCliConfig.BaseAddress</c> and <c>SignalCliConfig.PhoneNumber</c>.
/// </remarks>
public class SignalCliRestClientServiceTests(ITestOutputHelper output) : TestBase(output)
{
    #region General

    [Fact]
    public async Task GetAbout_ReturnsVersionInfo()
    {
        var result = await svc.GetAbout();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Version));
        _output.WriteLine($"Version={result.Version}, Build={result.Build}, Mode={result.Mode}");
        if (result.Versions.Length > 0)
            _output.WriteLine($"SupportedVersions={string.Join(", ", result.Versions)}");
        if (result.Capabilities is not null)
            _output.WriteLine($"Capabilities={string.Join(", ", result.Capabilities.Keys)}");
    }

    [Fact]
    public async Task GetConfiguration_ReturnsConfiguration()
    {
        var result = await svc.GetConfiguration();
        Assert.NotNull(result);
        _output.WriteLine($"Logging.Level={result.Logging?.Level ?? "(null)"}");
    }

    [Fact]
    public async Task SetConfiguration_ReturnsTrue()
    {
        var config = new SignalConfiguration
        {
            Logging = new LoggingConfiguration { Level = "INFO" }
        };
        var result = await svc.SetConfiguration(config);
        _output.WriteLine($"SetConfiguration={result}");
    }

    #endregion

    #region Messaging

    [Fact]
    public async Task SendMessage_ToSelf_ReturnsTimestamp()
    {
        var msg = new SignalMessageRequest
        {
            Id = Guid.NewGuid(),
            Message = $"Integration test message sent at {DateTime.UtcNow:O}",
            Number = _config.PhoneNumber,
            Recipients = [_config.PhoneNumber]
        };

        var result = await svc.SendMessage(msg);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Timestamp));
        _output.WriteLine($"Timestamp={result.Timestamp}");
    }

    [Fact]
    public async Task SendMessage_ToGroup_ReturnsTimestamp()
    {
        // Flush pending envelopes so signal-cli processes group membership
        // changes (e.g. a member accepting an invite) before we read state.
        var pending = await svc.ReceiveMessages(_config.PhoneNumber);
        _output.WriteLine($"FlushedMessages={pending?.Length ?? 0}");

        var groups = await svc.ListGroups(_config.PhoneNumber);
        Assert.NotNull(groups);
        var smartHaus = groups.FirstOrDefault(g => g.Name == _groupName);
        Assert.NotNull(smartHaus);

        _output.WriteLine($"GroupId={smartHaus.Id}");
        _output.WriteLine($"Members=[{string.Join(", ", smartHaus.Members)}]");
        _output.WriteLine($"PendingInvites=[{string.Join(", ", smartHaus.PendingInvites)}]");

        // Trust all member identities
        // current Sender Keys for members who joined after the last session.
        foreach (var member in smartHaus.Members.Where(m => m != _config.PhoneNumber))
        {
            var trusted = await svc.TrustIdentity(_config.PhoneNumber, member, trustAllKnownKeys: true);
            _output.WriteLine($"TrustIdentity {member}={trusted}");
        }
        var synced = await svc.SyncContacts(_config.PhoneNumber);
        _output.WriteLine($"SyncContacts={synced}");

        var msg = new SignalMessageRequest
        {
            Id = Guid.NewGuid(),
            Message = $"Group test message sent at {DateTime.UtcNow:O}",
            Number = _config.PhoneNumber,
            Recipients = [smartHaus.Id]
        };

        var result = await svc.SendMessage(msg);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Timestamp));
        _output.WriteLine($"Timestamp={result.Timestamp}");
    }

    [Fact]
    public async Task SendMessage_DirectToMember_ReturnsTimestamp()
    {
        // Diagnostic: test direct 1:1 delivery to the second group member
        // to isolate whether the issue is group-specific or general.
        var groups = await svc.ListGroups(_config.PhoneNumber);
        Assert.NotNull(groups);
        var smartHaus = groups.FirstOrDefault(g => g.Name == _groupName);
        Assert.NotNull(smartHaus);

        _output.WriteLine($"Members=[{string.Join(", ", smartHaus.Members)}]");
        _output.WriteLine($"PendingInvites=[{string.Join(", ", smartHaus.PendingInvites)}]");

        var otherMember = smartHaus.Members.FirstOrDefault(m => m != _config.PhoneNumber);
        Assert.NotNull(otherMember);
        _output.WriteLine($"TargetMember={otherMember}");

        var trusted = await svc.TrustIdentity(_config.PhoneNumber, otherMember, trustAllKnownKeys: true);
        _output.WriteLine($"TrustIdentity={trusted}");

        var msg = new SignalMessageRequest
        {
            Id = Guid.NewGuid(),
            Message = $"Direct test message sent at {DateTime.UtcNow:O}",
            Number = _config.PhoneNumber,
            Recipients = [otherMember]
        };

        var result = await svc.SendMessage(msg);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Timestamp));
        _output.WriteLine($"Timestamp={result.Timestamp}");
    }

    [Fact]
    public async Task SendMessage_WithStyledTextMode_ReturnsTimestamp()
    {
        var msg = new SignalMessageRequest
        {
            Id = Guid.NewGuid(),
            Message = $"*Bold* _italic_ test at {DateTime.UtcNow:O}",
            Number = _config.PhoneNumber,
            Recipients = [_config.PhoneNumber],
            TextMode = "styled"
        };

        var result = await svc.SendMessage(msg);
        Assert.NotNull(result);
        _output.WriteLine($"Timestamp={result.Timestamp}");
    }

    [Fact]
    public async Task SendMessage_WithMentions_ReturnsTimestamp()
    {
        var mention = new MessageMention
        {
            Author = _config.PhoneNumber,
            Start = 0,
            Length = 5
        };

        var msg = new SignalMessageRequest
        {
            Id = Guid.NewGuid(),
            Message = $"@test mention test at {DateTime.UtcNow:O}",
            Number = _config.PhoneNumber,
            Recipients = [_config.PhoneNumber],
            Mentions = [mention]
        };

        var result = await svc.SendMessage(msg);
        Assert.NotNull(result);
        _output.WriteLine($"Timestamp={result.Timestamp}");
    }

    [Fact]
    public async Task SendMessage_WithLinkPreview_ReturnsTimestamp()
    {
        var preview = new SignalLinkPreview
        {
            Url = "https://example.com",
            Title = "Test Link"
        };

        var msg = new SignalMessageRequest
        {
            Id = Guid.NewGuid(),
            Message = $"Link preview test at {DateTime.UtcNow:O}",
            Number = _config.PhoneNumber,
            Recipients = [_config.PhoneNumber],
            LinkPreview = preview
        };

        var result = await svc.SendMessage(msg);
        Assert.NotNull(result);
        _output.WriteLine($"Timestamp={result.Timestamp}");
    }

    [Fact]
    public async Task ShowTypingIndicator_ReturnsTrue()
    {
        var result = await svc.ShowTypingIndicator(_config.PhoneNumber, _config.PhoneNumber);
        Assert.True(result);
        _output.WriteLine("Typing indicator shown");
    }

    [Fact]
    public async Task HideTypingIndicator_ReturnsTrue()
    {
        await svc.ShowTypingIndicator(_config.PhoneNumber, _config.PhoneNumber);
        var result = await svc.HideTypingIndicator(_config.PhoneNumber, _config.PhoneNumber);
        Assert.True(result);
        _output.WriteLine("Typing indicator hidden");
    }

    [Fact]
    public async Task ReceiveMessages_ReturnsMessages()
    {
        var result = await svc.ReceiveMessages(_config.PhoneNumber);
        Assert.NotNull(result);
        _output.WriteLine($"ReceivedMessages={result.Length}");
        foreach (var msg in result)
        {
            var data = msg.Envelope.DataMessage;
            _output.WriteLine($"  From={msg.Envelope.Source}, Account={msg.Account}, "
                + $"Message={data?.Message ?? "(no text)"}, "
                + $"Attachments={data?.Attachments?.Length ?? 0}");
            if (msg.Envelope.SyncMessage?.SentMessage is not null)
                _output.WriteLine($"  SyncSentMessage={msg.Envelope.SyncMessage.SentMessage.Message}");
            if (msg.Envelope.TypingMessage is not null)
                _output.WriteLine($"  TypingAction={msg.Envelope.TypingMessage.Action}");
            if (msg.Envelope.ReceiptMessage is not null)
                _output.WriteLine($"  ReceiptType={msg.Envelope.ReceiptMessage.Type}, "
                    + $"When={msg.Envelope.ReceiptMessage.When}");
            if (data?.GroupInfo is not null)
                _output.WriteLine($"  GroupId={data.GroupInfo.GroupId}, Type={data.GroupInfo.Type}");
            if (data?.Attachments is not null)
                foreach (var att in data.Attachments)
                    _output.WriteLine($"  Attachment id={att.Id}, type={att.ContentType}, "
                        + $"file={att.Filename}, size={att.Size}");
        }
    }

    [Fact]
    public async Task SendReaction_ReturnsTrue()
    {
        // Send a message first to get a timestamp to react to
        var msg = new SignalMessageRequest
        {
            Id = Guid.NewGuid(),
            Message = $"React target at {DateTime.UtcNow:O}",
            Number = _config.PhoneNumber,
            Recipients = [_config.PhoneNumber]
        };
        var sent = await svc.SendMessage(msg);
        Assert.NotNull(sent);

        var timestamp = long.Parse(sent.Timestamp);
        var result = await svc.SendReaction(_config.PhoneNumber, _config.PhoneNumber, "👍",
            _config.PhoneNumber, timestamp);
        _output.WriteLine($"SendReaction={result}");
    }

    [Fact]
    public async Task RemoveReaction_ReturnsTrue()
    {
        var msg = new SignalMessageRequest
        {
            Id = Guid.NewGuid(),
            Message = $"Remove reaction target at {DateTime.UtcNow:O}",
            Number = _config.PhoneNumber,
            Recipients = [_config.PhoneNumber]
        };
        var sent = await svc.SendMessage(msg);
        Assert.NotNull(sent);

        var timestamp = long.Parse(sent.Timestamp);
        await svc.SendReaction(_config.PhoneNumber, _config.PhoneNumber, "👍",
            _config.PhoneNumber, timestamp);
        var result = await svc.RemoveReaction(_config.PhoneNumber, _config.PhoneNumber, "👍",
            _config.PhoneNumber, timestamp);
        _output.WriteLine($"RemoveReaction={result}");
    }

    [Fact]
    public async Task SendReceipt_ReturnsTrue()
    {
        var msg = new SignalMessageRequest
        {
            Id = Guid.NewGuid(),
            Message = $"Receipt target at {DateTime.UtcNow:O}",
            Number = _config.PhoneNumber,
            Recipients = [_config.PhoneNumber]
        };
        var sent = await svc.SendMessage(msg);
        Assert.NotNull(sent);

        var timestamp = long.Parse(sent.Timestamp);
        var result = await svc.SendReceipt(_config.PhoneNumber, _config.PhoneNumber, "read", timestamp);
        _output.WriteLine($"SendReceipt={result}");
    }

    [Fact]
    public async Task RemoteDelete_ReturnsResponse()
    {
        var msg = new SignalMessageRequest
        {
            Id = Guid.NewGuid(),
            Message = $"Delete target at {DateTime.UtcNow:O}",
            Number = _config.PhoneNumber,
            Recipients = [_config.PhoneNumber]
        };
        var sent = await svc.SendMessage(msg);
        Assert.NotNull(sent);

        var timestamp = long.Parse(sent.Timestamp);
        var result = await svc.RemoteDelete(_config.PhoneNumber, _config.PhoneNumber, timestamp);
        _output.WriteLine($"RemoteDelete timestamp={result?.Timestamp ?? "(null)"}");
    }

    #endregion

    #region Registration

    [Fact(Skip = "RegisterNumber requires a dedicated test phone number")]
    public async Task RegisterNumber_RequiresDedicatedTestNumber()
    {
        var result = await svc.RegisterNumber("+10000000000");
        _output.WriteLine($"RegisterNumber={result}");
    }

    [Fact(Skip = "VerifyNumber requires a dedicated test phone number and token")]
    public async Task VerifyNumber_RequiresDedicatedTestNumber()
    {
        var result = await svc.VerifyNumber("+10000000000", "000000");
        _output.WriteLine($"VerifyNumber={result}");
    }

    [Fact(Skip = "UnregisterNumber is destructive and requires a dedicated test number")]
    public async Task UnregisterNumber_RequiresDedicatedTestNumber()
    {
        var result = await svc.UnregisterNumber("+10000000000");
        _output.WriteLine($"UnregisterNumber={result}");
    }

    #endregion

    #region Accounts

    [Fact]
    public async Task ListAccounts_ReturnsAccounts()
    {
        var result = await svc.ListAccounts();
        Assert.NotNull(result);
        _output.WriteLine($"Accounts={string.Join(", ", result)}");
    }

    [Fact]
    public async Task SetPin_ReturnsTrue()
    {
        var result = await svc.SetPin(_config.PhoneNumber, "123456");
        _output.WriteLine($"SetPin={result}");

        // Clean up
        await svc.RemovePin(_config.PhoneNumber);
    }

    [Fact]
    public async Task RemovePin_ReturnsTrue()
    {
        // Set then remove
        await svc.SetPin(_config.PhoneNumber, "654321");
        var result = await svc.RemovePin(_config.PhoneNumber);
        _output.WriteLine($"RemovePin={result}");
    }

    [Fact(Skip = "SubmitRateLimitChallenge requires a real challenge token and captcha")]
    public async Task SubmitRateLimitChallenge_RequiresRealToken()
    {
        var result = await svc.SubmitRateLimitChallenge(_config.PhoneNumber, "token", "captcha");
        _output.WriteLine($"SubmitRateLimitChallenge={result}");
    }

    [Fact]
    public async Task UpdateAccountSettings_ReturnsTrue()
    {
        var result = await svc.UpdateAccountSettings(_config.PhoneNumber, discoverableByNumber: true);
        _output.WriteLine($"UpdateAccountSettings={result}");
    }

    [Fact]
    public async Task SetAndRemoveUsername_RoundTrip()
    {
        var setResult = await svc.SetUsername(_config.PhoneNumber, $"testuser_{DateTime.UtcNow:yyyyMMddHHmmss}");
        _output.WriteLine($"SetUsername: username={setResult?.Username}, link={setResult?.UsernameLink}");

        var removeResult = await svc.RemoveUsername(_config.PhoneNumber);
        _output.WriteLine($"RemoveUsername={removeResult}");
    }

    #endregion

    #region Contacts

    [Fact]
    public async Task ListContacts_ReturnsContacts()
    {
        var result = await svc.ListContacts(_config.PhoneNumber);
        Assert.NotNull(result);
        _output.WriteLine($"Contacts={result.Length}");
        foreach (var contact in result)
        {
            _output.WriteLine($"  Contact number={contact.Number}, name={contact.Name}, "
                + $"uuid={contact.Uuid}, username={contact.Username}, "
                + $"profileName={contact.ProfileName}, givenName={contact.GivenName}, "
                + $"blocked={contact.Blocked}, messageExpiration={contact.MessageExpiration}, "
                + $"color={contact.Color}, note={contact.Note}");
            if (contact.Nickname is not null)
                _output.WriteLine($"    Nickname: given={contact.Nickname.GivenName}, "
                    + $"family={contact.Nickname.FamilyName}, name={contact.Nickname.Name}");
            if (contact.Profile is not null)
                _output.WriteLine($"    Profile: about={contact.Profile.About}, "
                    + $"givenName={contact.Profile.GivenName}, hasAvatar={contact.Profile.HasAvatar}, "
                    + $"lastUpdated={contact.Profile.LastUpdatedTimestamp}, "
                    + $"lastname={contact.Profile.Lastname}");
        }
    }

    [Fact]
    public async Task UpdateContact_ReturnsTrue()
    {
        var result = await svc.UpdateContact(_config.PhoneNumber, _config.PhoneNumber, name: "Test Self");
        _output.WriteLine($"UpdateContact={result}");
    }

    [Fact]
    public async Task SyncContacts_ReturnsTrue()
    {
        var result = await svc.SyncContacts(_config.PhoneNumber);
        _output.WriteLine($"SyncContacts={result}");
    }

    #endregion

    #region Devices

    [Fact]
    public async Task ListLinkedDevices_ReturnsDevices()
    {
        var result = await svc.ListLinkedDevices(_config.PhoneNumber);
        Assert.NotNull(result);
        _output.WriteLine($"LinkedDevices={result.Length}");
        foreach (var device in result)
            _output.WriteLine($"  Device id={device.Id}, name={device.Name}, "
                + $"created={device.CreationTimestamp}, lastSeen={device.LastSeenTimestamp}");
    }

    [Fact]
    public async Task GetQrCodeLink_ReturnsBytesOrNull()
    {
        var bytes = await svc.GetQrCodeLink("test-device");
        _output.WriteLine(bytes is not null
            ? $"QR code size={bytes.Length} bytes"
            : "QR code endpoint returned null (expected when already linked)");
    }

    [Fact]
    public async Task GetQrCodeLinkRaw_ReturnsUriOrNull()
    {
        var result = await svc.GetQrCodeLinkRaw("test-device-raw");
        _output.WriteLine(result?.DeviceLinkUri is not null
            ? $"DeviceLinkUri={result.DeviceLinkUri}"
            : "QR code raw endpoint returned null (expected when already linked)");
    }

    [Fact(Skip = "AddDevice requires a real device-link URI from a QR code scan")]
    public async Task AddDevice_RequiresRealUri()
    {
        var result = await svc.AddDevice(_config.PhoneNumber, "sgnl://linkdevice?uuid=test&pub_key=test");
        _output.WriteLine($"AddDevice={result}");
    }

    [Fact(Skip = "RemoveLinkedDevice requires a linked device to remove")]
    public async Task RemoveLinkedDevice_RequiresSpareDevice()
    {
        var result = await svc.RemoveLinkedDevice(_config.PhoneNumber, 99);
        _output.WriteLine($"RemoveLinkedDevice={result}");
    }

    [Fact(Skip = "DeleteLocalAccountData is destructive and cannot be undone")]
    public async Task DeleteLocalAccountData_Destructive()
    {
        var result = await svc.DeleteLocalAccountData(_config.PhoneNumber);
        _output.WriteLine($"DeleteLocalAccountData={result}");
    }

    #endregion

    #region Groups

    [Fact]
    public async Task ListGroups_ReturnsGroups()
    {
        var result = await svc.ListGroups(_config.PhoneNumber);
        Assert.NotNull(result);
        _output.WriteLine($"Groups={result.Length}");
        foreach (var group in result)
            _output.WriteLine($"  Group id={group.Id}, name={group.Name}, "
                + $"members={group.Members.Length}, admins={group.Admins.Length}, "
                + $"blocked={group.Blocked}, internalId={group.InternalId}, "
                + $"inviteLink={group.InviteLink}, "
                + $"pendingInvites={group.PendingInvites.Length}, "
                + $"pendingRequests={group.PendingRequests.Length}, "
                + $"permissions={group.Permissions?.AddMembers}/{group.Permissions?.EditGroup}/{group.Permissions?.SendMessages}");
    }

    [Fact(Skip = "One-shot setup — SmartHaus group already created")]
    public async Task CreateGroup()
    {
        var groupName = _groupName;
        var createRequest = new CreateGroupRequest
        {
            Name = groupName,
            Members = [_config.PhoneNumber],
            Description = $"{groupName} AI Agent test group",
            Permissions = new GroupPermissions
            {
                AddMembers = "every-member",
                EditGroup = "every-member",
                SendMessages = "every-member"
            }
        };

        var created = await svc.CreateGroup(_config.PhoneNumber, createRequest);
        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created.Id));
        _output.WriteLine($"Created group id={created.Id}, name={groupName}");
    }

        [Fact]
    public async Task CreateUpdateAndDeleteGroup_RoundTrip()
    {
        var groupName = $"TestGroup_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var createRequest = new CreateGroupRequest
        {
            Name = groupName,
            Members = [_config.PhoneNumber],
            Description = "Integration test group",
            Permissions = new GroupPermissions
            {
                AddMembers = "every-member",
                EditGroup = "every-member",
                SendMessages = "every-member"
            }
        };

        var created = await svc.CreateGroup(_config.PhoneNumber, createRequest);
        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created.Id));
        _output.WriteLine($"Created group id={created.Id}, name={groupName}");

        // GetGroup
        var fetched = await svc.GetGroup(_config.PhoneNumber, created.Id);
        Assert.NotNull(fetched);
        _output.WriteLine($"GetGroup id={fetched.Id}, name={fetched.Name}");

        // UpdateGroup
        var updateRequest = new UpdateGroupRequest
        {
            Name = $"{groupName}_Updated",
            Description = "Updated description"
        };
        var updated = await svc.UpdateGroup(_config.PhoneNumber, created.Id, updateRequest);
        _output.WriteLine($"UpdateGroup={updated}");

        // Delete
        var deleted = await svc.DeleteGroup(_config.PhoneNumber, created.Id);
        Assert.True(deleted);
        _output.WriteLine($"Deleted group id={created.Id}");
    }

    [Fact(Skip = "AddGroupMembers/RemoveGroupMembers require a second phone number")]
    public async Task AddAndRemoveGroupMembers_RequiresSecondNumber()
    {
        var added = await svc.AddGroupMembers(_config.PhoneNumber, "group-id", ["+10000000001"]);
        _output.WriteLine($"AddGroupMembers={added}");
        var removed = await svc.RemoveGroupMembers(_config.PhoneNumber, "group-id", ["+10000000001"]);
        _output.WriteLine($"RemoveGroupMembers={removed}");
    }

    [Fact(Skip = "AddGroupAdmins/RemoveGroupAdmins require a second phone number")]
    public async Task AddAndRemoveGroupAdmins_RequiresSecondNumber()
    {
        var added = await svc.AddGroupAdmins(_config.PhoneNumber, "group-id", ["+10000000001"]);
        _output.WriteLine($"AddGroupAdmins={added}");
        var removed = await svc.RemoveGroupAdmins(_config.PhoneNumber, "group-id", ["+10000000001"]);
        _output.WriteLine($"RemoveGroupAdmins={removed}");
    }

    [Fact(Skip = "JoinGroup requires a group invite link")]
    public async Task JoinGroup_RequiresInviteLink()
    {
        var result = await svc.JoinGroup(_config.PhoneNumber, "group-id");
        _output.WriteLine($"JoinGroup={result}");
    }

    [Fact(Skip = "QuitGroup is destructive")]
    public async Task QuitGroup_Destructive()
    {
        var result = await svc.QuitGroup(_config.PhoneNumber, "group-id");
        _output.WriteLine($"QuitGroup={result}");
    }

    [Fact(Skip = "BlockGroup is destructive")]
    public async Task BlockGroup_Destructive()
    {
        var result = await svc.BlockGroup(_config.PhoneNumber, "group-id");
        _output.WriteLine($"BlockGroup={result}");
    }

    [Fact]
    public async Task GetGroupAvatar_ReturnsNullForNoAvatar()
    {
        var groups = await svc.ListGroups(_config.PhoneNumber);
        if (groups is null || groups.Length == 0)
        {
            _output.WriteLine("Skipped: No groups available");
            return;
        }

        var avatar = await svc.GetGroupAvatar(_config.PhoneNumber, groups[0].Id);
        _output.WriteLine(avatar is not null
            ? $"GroupAvatar size={avatar.Length} bytes"
            : "GroupAvatar returned null (no avatar set)");
    }

    #endregion

    #region Identities

    [Fact]
    public async Task ListIdentities_ReturnsIdentities()
    {
        var result = await svc.ListIdentities(_config.PhoneNumber);
        Assert.NotNull(result);
        _output.WriteLine($"Identities={result.Length}");
        foreach (var identity in result)
            _output.WriteLine($"  Identity number={identity.Number}, uuid={identity.Uuid}, "
                + $"status={identity.Status}, fingerprint={identity.Fingerprint}, "
                + $"safetyNumber={identity.SafetyNumber}, added={identity.Added}");
    }

    [Fact(Skip = "TrustIdentity requires an untrusted identity to trust")]
    public async Task TrustIdentity_RequiresUntrustedIdentity()
    {
        var result = await svc.TrustIdentity(_config.PhoneNumber, "+10000000001", trustAllKnownKeys: true);
        _output.WriteLine($"TrustIdentity={result}");
    }

    #endregion

    #region Attachments

    [Fact]
    public async Task ListAttachments_ReturnsAttachments()
    {
        var result = await svc.ListAttachments();
        Assert.NotNull(result);
        _output.WriteLine($"Attachments={result.Length}");
        foreach (var attachment in result)
            _output.WriteLine($"  Attachment id={attachment}");
    }

    [Fact]
    public async Task GetAttachment_ReturnsNullForMissingId()
    {
        var result = await svc.GetAttachment("nonexistent-attachment-id");
        Assert.Null(result);
        _output.WriteLine("GetAttachment returned null for nonexistent id (expected)");
    }

    [Fact]
    public async Task DeleteAttachment_ReturnsFalseForMissingId()
    {
        var result = await svc.DeleteAttachment("nonexistent-attachment-id");
        _output.WriteLine($"DeleteAttachment for nonexistent id={result}");
    }

    #endregion

    #region Profile

    [Fact]
    public async Task UpdateProfile_ReturnsTrue()
    {
        var profile = new UpdateProfileRequest
        {
            Name = "Test Profile",
            About = "Integration test"
        };
        var result = await svc.UpdateProfile(_config.PhoneNumber, profile);
        _output.WriteLine($"UpdateProfile={result}");
    }

    #endregion

    #region Search

    [Fact]
    public async Task SearchNumbers_ReturnsResults()
    {
        var result = await svc.SearchNumbers(_config.PhoneNumber, [_config.PhoneNumber]);
        Assert.NotNull(result);
        _output.WriteLine($"SearchResults={result.Length}");
        foreach (var r in result)
            _output.WriteLine($"  Number={r.Number}, Registered={r.Registered}");
    }

    #endregion

    #region Sticker Packs

    [Fact]
    public async Task ListStickerPacks_ReturnsPacks()
    {
        var result = await svc.ListStickerPacks(_config.PhoneNumber);
        Assert.NotNull(result);
        _output.WriteLine($"StickerPacks={result.Length}");
        foreach (var pack in result)
            _output.WriteLine($"  Pack id={pack.PackId}, title={pack.Title}, "
                + $"author={pack.Author}, url={pack.Url}, installed={pack.Installed}");
    }

    [Fact(Skip = "AddStickerPack requires a valid pack ID and key")]
    public async Task AddStickerPack_RequiresPackInfo()
    {
        var result = await svc.AddStickerPack(_config.PhoneNumber, "pack-id", "pack-key");
        _output.WriteLine($"AddStickerPack={result}");
    }

    #endregion
}
