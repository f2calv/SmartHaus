# CasCap.Api.Wiz.Sinks

Pluggable event sink implementations for the Wiz smart lighting integration library ([CasCap.Api.Wiz](../CasCap.Api.Wiz)).

## Purpose

This project provides additional `IEventSink<T>` implementations that persist Wiz bulb events to external storage backends (Redis, Azure Table Storage, etc.). Sinks are discovered at runtime via `SinkTypeAttribute` and loaded based on the feature's `SinkConfig`.

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.Wiz` | Core Wiz smart lighting integration library |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
