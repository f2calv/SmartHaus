# CasCap.Api.Shelly.Sinks

External storage sinks for the Shelly smart plug integration.

## Purpose

Provides Redis and Azure Table Storage sinks for persisting Shelly smart plug events outside the application process.

## Services

| Service | Description |
| --- | --- |
| `ShellySinkRedisService` | Persists snapshot as a Redis hash and line items as daily sorted sets |
| `ShellySinkAzTablesService` | Persists line items and snapshot to Azure Table Storage |

## Dependencies

### Project References

| Reference | Purpose |
| --- | --- |
| `CasCap.Api.Shelly` | Core Shelly integration library |
| `CasCap.Api.Azure.Auth` | Azure authentication |
| `CasCap.Api.Azure.Storage` | Azure Table Storage client extensions |
| `CasCap.Common.Caching` | Redis remote cache abstraction |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
