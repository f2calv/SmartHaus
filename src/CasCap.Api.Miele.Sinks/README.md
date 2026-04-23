# CasCap.Api.Miele.Sinks

Pluggable event sink implementations for the Miele appliance integration library ([CasCap.Api.Miele](../CasCap.Api.Miele)). Sinks persist `MieleEvent` instances to external storage backends and are loaded based on the feature's `SinkConfig`.

## Purpose

This project provides additional `IEventSink<T>` implementations for the Miele integration. Sinks are discovered at runtime via `SinkTypeAttribute` and loaded based on the feature's `SinkConfig` when `AddMieleWithExtraSinks()` is called.

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.Miele` | Core Miele appliance integration library |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |


## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
