# CasCap.App.Server.Tests

Integration tests for the SmartHaus ASP.NET Core entry host, its HTTP endpoints, authentication,
health checks, and feature-service registration.

## Purpose

The project boots `CasCap.App.Server` through `WebApplicationFactory<AppEntryPoint>` with
deterministic test configuration. The server-owned marker avoids colliding with the test runner's
generated entry point. The factory replaces external configuration and authorization where needed
so the host pipeline can be exercised without starting the normal deployment infrastructure.

Backend services, domain behavior, MCP contracts, and agent evaluation live in
[CasCap.Backend.Tests](../CasCap.Backend.Tests/README.md).

## Test Classes

| Class | Methods | Cases | Category | Description |
| --- | --- | --- | --- | --- |
| `FeatureServiceRegistrationTests` | 4 | 4 | Integration | `IBgFeature` registrations and enabled-feature filtering |
| `FeatureConfigTests` | 3 | 6 | Configuration | Empty values, unknown-name rejection, and case-insensitive parsing |
| `HealthTests` | 4 | 7 | Integration | Health, liveness, readiness, and startup endpoints |
| `HostStartupConfigurationTests` | 1 | 1 | Integration | Options binding, Key Vault disablement, feature selection, and safe core DI services |
| `SystemControllerTests` | 4 | 4 | Integration | `GET /api/system` and Basic authentication behavior |

## Trait Categories

| Category | Meaning |
| --- | --- |
| `Configuration` | Exercises deterministic configuration parsing without starting the host |
| `Integration` | Boots the in-process ASP.NET Core host and exercises its services or endpoints |

`InitializeConfiguration` owns the standard provider chain and unconditionally adds the required
`appsettings.json`, user secrets, and environment variables. It therefore cannot be exercised with
a cleared, in-memory-only configuration root. Configuration failure behavior is covered directly at
the `FeatureConfig` boundary; host binding is covered through `CasCapAppWebApplicationFactory` with
Key Vault disabled before startup.

## Skipped Tests

| Test | Reason | Count |
| --- | --- | --- |
| `FeatureServiceRegistrationTests` | Requires KNX bus and Azure Storage infrastructure | 1 |

## Running The Tests

```bash
dotnet test --project src/CasCap.App.Server.Tests/CasCap.App.Server.Tests.csproj
```

## Test Layout

```text
CasCap.App.Server.Tests/
├── Api/
│   ├── FeatureServiceRegistrationTests.cs
│   ├── HealthTests.cs
│   └── SystemControllerTests.cs
├── Infrastructure/
│   ├── CasCapAppWebApplicationFactory.cs
│   └── WebApiTestBase.cs
├── Tests/
│   ├── Integration/
│   │   └── HostStartupConfigurationTests.cs
│   └── Unit/
│       └── FeatureConfigTests.cs
├── GlobalUsings.cs
└── xunit.runner.json
```

## Dependencies

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.App.Server` | ASP.NET Core host under test |
| `CasCap.Common.Testing` | Shared xUnit logging and test utilities |

### Packages

| Package | Purpose |
| --- | --- |
| `Microsoft.AspNetCore.Mvc.Testing` | In-process `WebApplicationFactory<Program>` host |
| `Microsoft.Testing.Extensions.CodeCoverage` | Microsoft.Testing.Platform coverage integration |
| `xunit.v3` | Test framework and runner |

Test collections run serially through `xunit.runner.json`.

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
