# CasCap.Api.DDns

Dynamic DNS library that monitors the public IP address and automatically updates Azure DNS A records when it changes.

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
| `IsEnabled` | `bool` | `false` | Whether the Dynamic DNS service is enabled |
| `BaseAddress` | `string` | `"https://api.ipify.org/"` | Base address of the IP discovery service |
| `DnsResourceGroupName` | `string` | — | Azure DNS resource group name |
| `DnsZoneName` | `string` | — | Azure DNS zone name |
| `DnsMetaDataKey` | `string` | — | DNS metadata key for tracking updates |
| `RefreshDelayMs` | `int` | `10000` | Delay in milliseconds between DNS refresh cycles |
| `JsonDebugEnabled` | `bool` | `false` | Whether JSON debug output is enabled |
| `JsonDebugPath` | `string?` | `null` | File path for JSON debug output |

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
      "IsEnabled": true,
      "BaseAddress": "https://api.ipify.org/",
      "DnsZoneName": "example.net",
      "DnsResourceGroupName": "some-rg",
      "DnsMetaDataKey": "dyndns",
      "RefreshDelayMs": 10000,
      "JsonDebugEnabled": false
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
| `CasCap.Api.Azure.Auth` | Azure authentication and token credential helpers (`IAzureAuthConfig`, `TokenCredentialExtensions`) |
| `CasCap.Common.Caching` | Distributed locking (RedLock) |
| `CasCap.Common.Configuration` | Configuration binding and validation |
| `CasCap.Common.Extensions` | Shared extension helpers |
| `CasCap.Common.Logging` | Structured logging helpers |
| `CasCap.Common.Net` | HTTP client base class |
| `CasCap.Common.Serialization.Json` | JSON serialisation helpers |
| `CasCap.Common.Services` | `HttpClientBase` |


## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
