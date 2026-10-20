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

## Services

| Service | Description |
| --- | --- |
| `DDnsBgService` | Polls the public IP and updates the configured Azure DNS zone records when it changes (uses RedLock for distributed locking) |
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
| `CasCap.Common.Caching` | Distributed locking (RedLock) |
| `CasCap.Common.Configuration` | Configuration binding and validation |
| `CasCap.Common.Extensions` | Shared extension helpers |
| `CasCap.Common.Logging` | Structured logging helpers |
| `CasCap.Common.Net` | HTTP client base class |
| `CasCap.Common.Serialization.Json` | JSON serialisation helpers |
| `CasCap.Common.Services` | `HttpClientBase` |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
