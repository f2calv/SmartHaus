namespace CasCap.Tests.Integration;

/// <summary>
/// Well-known xUnit trait names and values for the signal-cli integration tests, allowing
/// subsets to be selected with <c>dotnet test --filter</c>.
/// </summary>
/// <remarks>
/// Two axes are provided: <see cref="Feature"/> classifies a test by the signal-cli endpoint
/// area it exercises (mirroring the <c>#region</c> groupings), and <see cref="Transport"/>
/// flags endpoints whose availability depends on the signal-cli server transport mode.
/// </remarks>
internal static class SignalCliTraits
{
    /// <summary>Trait name for the broad test category (e.g. Integration / Unit).</summary>
    public const string Category = nameof(Category);

    /// <summary>Category value for tests that hit a live signal-cli REST API.</summary>
    public const string Integration = nameof(Integration);

    /// <summary>Trait name for the signal-cli endpoint area exercised by a test.</summary>
    public const string Feature = nameof(Feature);

    /// <summary>Feature value: version / configuration endpoints.</summary>
    public const string General = nameof(General);

    /// <summary>Feature value: send / receive / reaction / receipt endpoints.</summary>
    public const string Messaging = nameof(Messaging);

    /// <summary>Feature value: register / verify / unregister endpoints.</summary>
    public const string Registration = nameof(Registration);

    /// <summary>Feature value: account, PIN, username and settings endpoints.</summary>
    public const string Accounts = nameof(Accounts);

    /// <summary>Feature value: contact list / update / sync endpoints.</summary>
    public const string Contacts = nameof(Contacts);

    /// <summary>Feature value: linked-device and QR-code endpoints.</summary>
    public const string Devices = nameof(Devices);

    /// <summary>Feature value: group CRUD, membership and avatar endpoints.</summary>
    public const string Groups = nameof(Groups);

    /// <summary>Feature value: identity listing and trust endpoints.</summary>
    public const string Identities = nameof(Identities);

    /// <summary>Feature value: attachment listing / retrieval / deletion endpoints.</summary>
    public const string Attachments = nameof(Attachments);

    /// <summary>Feature value: profile update endpoints.</summary>
    public const string Profile = nameof(Profile);

    /// <summary>Feature value: number-search endpoints.</summary>
    public const string Search = nameof(Search);

    /// <summary>Feature value: sticker-pack listing / install endpoints.</summary>
    public const string StickerPacks = nameof(StickerPacks);

    /// <summary>Trait name describing the signal-cli transport mode an endpoint requires.</summary>
    public const string Transport = nameof(Transport);

    /// <summary>
    /// Transport value for endpoints only served over HTTP polling (Normal / Native mode).
    /// </summary>
    /// <remarks>
    /// The REST <c>GET /v1/receive</c> endpoint returns HTTP 400 when signal-cli runs in
    /// json-rpc mode (message reception is WebSocket-only). Exclude these with
    /// <c>--filter "Transport!=Polling"</c> when testing against a json-rpc deployment.
    /// </remarks>
    public const string Polling = nameof(Polling);
}
