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
| `BuderusQueryServiceTests` | 5 | 9 | Device Commands | Path validation/normalization, preflight/discovery cancellation, and MCP boolean rejection using fake HTTP |
| `FeatureServiceRegistrationTests` | 4 | 4 | Integration | `IBgFeature` registrations and enabled-feature filtering |
| `FeatureConfigTests` | 3 | 6 | Configuration | Empty values, unknown-name rejection, and case-insensitive parsing |
| `DDnsLeadershipTests` | 3 | 4 | Leadership | Existing resource delegation, cancellation and timeout recovery through IP discovery, without Azure/Redis calls |
| `DeviceApiContractTests` | 7 | 46 | Integration | History cancellation, explicit power values, typed write outcomes, pilot range boundaries/validation, and Swagger metadata |
| `HealthTests` | 4 | 7 | Integration | Health, liveness, readiness, and startup endpoints |
| `HostStartupConfigurationTests` | 2 | 3 | Integration | Options binding, Key Vault disablement, feature-gated DDns controller discovery, and safe core DI services |
| `McpAuthenticationTests` | 2 | 2 | Integration | Anonymous rejection and Basic-authenticated MCP endpoint access |
| `MieleApiContractTests` | 11 | 59 | Integration | Typed bodies, HTTP outcomes, validation, cancellation, no command replay, sanitized failures, disposal, and generated Swagger using fake HTTP |
| `MieleClientFailureTests` | 2 | 7 | Device Commands | Real Polly timeout/circuit exception translation and requested cancellation through an offline handler |
| `MieleMcpContractTests` | 3 | 24 | Integration | SDK-generated input schemas, nine facade cancellation paths, and boolean command outcomes using the fake provider |
| `SystemControllerTests` | 4 | 4 | Integration | `GET /api/system` and Basic authentication behavior |
| `UbiquitiApiContractTests` | 4 | 11 | Integration | GET/POST webhooks, optional bodies, validation, request cancellation, and generated Swagger |
| `UbiquitiQueryServiceTests` | 7 | 21 | Webhook Parsing | Detection types, thumbnails/timestamps, camera selection, sink failures, and cancellation using in-memory sinks |
| `WebApiContractTests` | 4 | 8 | Integration | String-enum JSON output/input, validation Problem Details, and generated Swagger schemas |
| Total | 65 | 215 | | Source-declared, including skipped cases |

## Trait Categories

| Category | Meaning |
| --- | --- |
| `Configuration` | Exercises deterministic configuration parsing without starting the host |
| `Device Commands` | Uses synthetic configuration and fake HTTP; never contacts live devices |
| `Integration` | Boots the in-process ASP.NET Core host and exercises its services or endpoints |
| `Leadership` | Credential-free DDNS ownership wiring, cancellation and timeout recovery |
| `Webhook Parsing` | Deterministic payload parsing and in-memory dispatch, without camera or storage calls |

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

Use a .NET 10 SDK with xUnit v3 and Microsoft.Testing.Platform from the repository root.
The inventory is derived from source, not an observed test run. `Integration` here includes
in-process host tests and must not be assumed to mean live-device access.

The Miele fixture replaces the named client's primary handler while retaining its configured
resilience pipeline. No appliance is contacted. MCP coverage here verifies SDK-generated schemas
and facade calls, not a complete MCP initialization/discovery/invocation protocol exchange.
The separate `MieleClientFailureTests` are self-contained unit cases: their handler throws actual
Polly timeout and circuit-breaker exception types, without starting a host or contacting a provider.

```bash
dotnet test --project src/CasCap.App.Server.Tests/CasCap.App.Server.Tests.csproj
```

## Test Layout

```text
CasCap.App.Server.Tests/
├── Api/
│   ├── FeatureServiceRegistrationTests.cs
│   ├── HealthTests.cs
│   ├── McpAuthenticationTests.cs
│   └── SystemControllerTests.cs
├── Infrastructure/
│   ├── ApiContractProbeController.cs
│   ├── ApiContractWebApplicationFactory.cs
│   ├── CasCapAppWebApplicationFactory.cs
│   ├── DeviceApiWebApplicationFactory.cs
│   ├── MieleApiWebApplicationFactory.cs
│   ├── MieleHttpHandler.cs
│   ├── StrictAuthCasCapAppWebApplicationFactory.cs
│   └── WebApiTestBase.cs
├── Tests/
│   ├── Integration/
│   │   ├── DeviceApiContractTests.cs
│   │   ├── HostStartupConfigurationTests.cs
│   │   ├── MieleApiContractTests.cs
│   │   ├── MieleMcpContractTests.cs
│   │   ├── UbiquitiApiContractTests.cs
│   │   └── WebApiContractTests.cs
│   └── Unit/
│       ├── BuderusQueryServiceTests.cs
│       ├── DDnsLeadershipTests.cs
│       ├── FeatureConfigTests.cs
│       ├── MieleClientFailureTests.cs
│       └── UbiquitiQueryServiceTests.cs
├── GlobalUsings.cs
└── xunit.runner.json
```

## Dependencies

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.App.Server` | ASP.NET Core host under test |
| `CasCap.Api.DDns` | DDNS ownership and cancellation contract under test |
| `CasCap.Common.Testing` | Shared xUnit logging and test utilities |

### Packages

| Package | Purpose |
| --- | --- |
| `Microsoft.AspNetCore.Mvc.Testing` | In-process `WebApplicationFactory<Program>` host |
| `Microsoft.Testing.Extensions.CodeCoverage` | Microsoft.Testing.Platform coverage integration |
| `xunit.v3` | Test framework and runner |

Test collections run serially through `xunit.runner.json`.

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
