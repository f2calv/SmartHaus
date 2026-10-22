# CasCap.Api.DDns

Dynamic DNS library that monitors the public IP address and automatically updates Azure DNS A records when it changes.

## Usage

This source-only .NET 10 library is included by the SmartHaus host; it is not published as a NuGet
package. Enable `DDns` in `CasCap:FeatureConfig:EnabledFeatures` and supply the configuration below.
It needs Azure DNS write permission, Azure authentication and the host's Redis/RedLock services.
It modifies DNS records, so configure an appropriate test zone before enabling it.

Custom hosts register it with `services.AddDDns(configuration)` from
`Microsoft.Extensions.DependencyInjection`, and start its [`IBgFeature`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IBgFeature.cs) worker through the shared
feature runner.

The current leadership API is provisional: build in Debug against the sibling shared-library
checkout. Its new runner is not yet available in the published caching package, so Release builds
must wait for the coordinated package release rather than substituting an invented package version.

## Services

| Service | Description |
| --- | --- |
| `DDnsBgService` | Delegates ownership to the shared `ILeadershipRunner` using the existing logical resource |
| `DDnsUpdateService` | Polls the public IP and updates matching Azure DNS A records within one ownership run |
| `DDnsFindMyIpClientService` | HTTP client for discovering the current external IP address via ipify |
| `DDnsQueryService` | Read-only query service exposing current external IP via the controller |

## Controller

`DDnsController` exposes read-only query endpoints for the Dynamic DNS service via the Haus internal Web API:

| Method | Route | Description |
| --- | --- | --- |
| `GetCurrentIp` | `GET /api/v1/ddns/ip` | Returns the current external IP address |

## Extensions

| Method | Description |
| --- | --- |
| `AddDDns` | Registers all Dynamic DNS services, HTTP client, and background worker |

## Leadership Lifecycle

DDNS uses the caching registration's existing Redis factory and
`CachingConfig.Redlock.Profiles.LeaderElection` timings. There is no separate DDNS lock configuration.
`Redlock.LeadershipCheckIntervalMs` defaults to one second; it observes ownership, not renewal.

Only the acquired owner runs the update service. Observed loss cancels HTTP discovery, Azure
enumeration and writes through the ownership token, and the shared runner awaits completion before
releasing or reacquiring. Every new run clears its subscription/IP shortcut state to revalidate DNS.
The feature host also awaits worker cleanup during shutdown.

This is coordination, not downstream fencing: a lease check cannot make an Azure DNS write atomic
with ownership. Polling and cancellation have a detection/response delay. Align host shutdown and
container termination budgets; the runner does not abandon cleanup after a timeout.

`AddDDns(..., lite: true)` registers read-only query/client services without the update worker or a
leadership dependency. Focused credential-free tests cover delegation and HTTP cancellation; they do
not execute Azure DNS writes or prove Redis renewal under partitions.

## Configuration

### `DDnsConfig` (`CasCap:DDnsConfig`)

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `BaseAddress` | `string` | `"https://api.ipify.org/"` | Base address of the IP discovery service |
| `DnsResourceGroupName` | `string` | — | Azure DNS resource group name |
| `DnsZoneName` | `string` | — | Azure DNS zone name |
| `DnsMetaDataKey` | `string` | — | DNS metadata key for tracking updates |
| `RefreshDelayMs` | `int` | `30000` | Delay in milliseconds between DNS refresh cycles |

## Configuration Examples

### Minimal

```json
{
  "CasCap": {
    "DDnsConfig": {
      "DnsZoneName": "example.net",
      "DnsResourceGroupName": "some-rg",
      "DnsMetaDataKey": "dyndns"
    }
  }
}
```

### Fully configured

```json
{
  "CasCap": {
    "DDnsConfig": {
      "BaseAddress": "https://api.ipify.org/",
      "DnsZoneName": "example.net",
      "DnsResourceGroupName": "some-rg",
      "DnsMetaDataKey": "dyndns",
      "RefreshDelayMs": 30000
    }
  }
}
```

## Dependencies

### NuGet packages

| Package | Purpose |
| --- | --- |
| [Azure.ResourceManager.Dns](https://www.nuget.org/packages/azure.resourcemanager.dns) | Azure DNS management |
| [Microsoft.Extensions.Http](https://www.nuget.org/packages/microsoft.extensions.http) | `IHttpClientFactory` |

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.Azure.Auth` | Azure authentication and token credential helpers ([`IAzureAuthConfig`](https://github.com/f2calv/CasCap.Api.Azure/blob/main/src/CasCap.Api.Azure.Auth/Abstractions/IAzureAuthConfig.cs), `TokenCredentialExtensions`) |
| `CasCap.Common.Caching` | Shared leadership runner, lock factory and Redlock options |
| `CasCap.Common.Configuration` | Configuration binding and validation |
| `CasCap.Common.Extensions` | Shared extension helpers |
| `CasCap.Common.Logging` | Structured logging helpers |
| `CasCap.Common.Net` | HTTP client base class |
| `CasCap.Common.Serialization.Json` | JSON serialisation helpers |
| `CasCap.Common.Services` | Feature-host lifecycle |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
