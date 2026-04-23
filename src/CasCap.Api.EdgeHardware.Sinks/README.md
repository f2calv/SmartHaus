# CasCap.Api.EdgeHardware.Sinks

Pluggable event sink implementations for the edge hardware integration library ([CasCap.Api.EdgeHardware](../CasCap.Api.EdgeHardware)).

## Purpose

This project provides additional `IEventSink<T>` implementations that persist edge hardware sensor readings to external storage backends (Redis, Azure Table Storage, etc.). Sinks are discovered at runtime via `SinkTypeAttribute` and loaded based on the feature's `SinkConfig`.

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.EdgeHardware` | Core edge hardware integration library |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Table Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |


## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
