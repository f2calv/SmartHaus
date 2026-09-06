# CasCap.Api.SignalCli.AspNetCore

ASP.NET Core integration for [CasCap.Api.SignalCli](https://www.nuget.org/packages/cascap.api.signalcli). Provides `SignalCliController`, a versioned, read-only MVC controller that surfaces signal-cli queries over your own Web API.

This lives in a separate package so that worker services, console apps and daemons can consume the Signal client without taking a dependency on MVC or API versioning.

## Installation

```bash
dotnet add package CasCap.Api.SignalCli.AspNetCore
```

## Controller

`SignalCliController` is a thin pass-through over `ISignalCliClient`. Every action flows the request abort token into the underlying call, and maps a `null` result to `404 Not Found` rather than `200 OK` with a null body.

| Method | Route | Description |
| --- | --- | --- |
| `GetAbout` | `GET /api/v1/signalcli/about` | Returns signal-cli version and build info |
| `GetConfiguration` | `GET /api/v1/signalcli/configuration` | Retrieves the signal-cli configuration |
| `ListAccounts` | `GET /api/v1/signalcli/accounts` | Lists all registered accounts |
| `ListContacts` | `GET /api/v1/signalcli/contacts?number=` | Lists contacts for an account |
| `ListGroups` | `GET /api/v1/signalcli/groups?number=` | Lists groups for an account |
| `ListLinkedDevices` | `GET /api/v1/signalcli/devices?number=` | Lists linked devices for an account |
| `ListIdentities` | `GET /api/v1/signalcli/identities?number=` | Lists known identities for an account |
| `ListAttachments` | `GET /api/v1/signalcli/attachments` | Lists all stored attachment identifiers |
| `ListStickerPacks` | `GET /api/v1/signalcli/sticker-packs?number=` | Lists installed sticker packs for an account |

The controller carries `[Authorize]`. Configure an authentication scheme in the host; the endpoints expose account metadata, contacts and group membership and must not be left anonymous.

## Registration

The controller lives in its own assembly, so register it as an application part alongside the Signal client:

```csharp
builder.Services.AddSignalCli(builder.Configuration);

builder.Services.AddControllers()
    .AddApplicationPart(typeof(SignalCliController).Assembly);

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1.0);
    options.AssumeDefaultVersionWhenUnspecified = true;
});
```

`AddSignalCli` registers `ISignalCliClient`, which is the controller's only dependency.

## Dependencies

### NuGet packages

| Package | Purpose |
| --- | --- |
| [Asp.Versioning.Mvc](https://www.nuget.org/packages/asp.versioning.mvc) | API versioning for the controller route template |

The project also takes a `Microsoft.AspNetCore.App` framework reference for `ControllerBase`, routing and the typed result helpers.

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.SignalCli` | `ISignalCliClient` and the Signal DTOs |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
