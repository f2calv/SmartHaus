# CasCap.Comms.Tests

Credential-free unit tests for [CasCap.Comms](../CasCap.Comms/README.md) and
[CasCap.Comms.AI](../CasCap.Comms.AI/README.md).

## Purpose

Every orchestration test drives the real `CommunicationsBgService.ExecuteAsync` pipeline through
`CommunicationsBgServiceTestFixture`, over `FakeSignalizrClient`, the speech fakes from
`CasCap.Api.Voice.Testing` and an in-memory Redis. The responder is a real `AgentCommsResponder` over
`StubAIAgent`, which refuses inference, so no test reaches a model, Redis or a gateway.

This project is maintained **byte-identically** in the SmartHaus and CAS repositories until the
shared communications code moves to its own repository. Change both copies in the same change.

## Tests

| Class | Methods | Cases | Category | Description |
| --- | ---: | ---: | --- | --- |
| `CommunicationsBgServiceTests` | 13 | 18 | Messaging | Inbound admission, duplicate suppression, durable attachments, voice-processing modes, transcript echo and reply-queue backpressure |
| `CommunicationsBgServiceStreamTests` | 12 | 15 | Comms | Direct and responder stream delivery, monitor routing, stale and throttled drops, cached media, startup retry, the missing-group fault, resubscription, the no-responder inbound behaviour and the inbound filter |
| `SignalMessageDeduplicatorTests` | 9 | 11 | Comms | Redis-backed duplicate Signal message suppression |
| `MonitorSourcesGroupRouterTests` | 2 | 3 | Comms | Routing of configured operational sources to the monitor group |
| `CommsEventFormatterTests` | 2 | 2 | Comms | The plain and timestamped direct-send formats |
| **Total** | **38** | **49** | | |

## Trait Categories

| Category | Meaning |
| --- | --- |
| `Messaging` | Inbound message orchestration |
| `Comms` | Stream delivery, routing, formatting and duplicate suppression |

## Skipped Tests

None.

## Layout

```text
Tests/
└── Unit/
    ├── CommsEventFormatterTests.cs
    ├── CommunicationsBgServiceStreamTests.cs
    ├── CommunicationsBgServiceTestFixture.cs   # Service over deterministic fakes
    ├── CommunicationsBgServiceTests.cs
    ├── FakeHostEnvironment.cs
    ├── FakeNotificationAttachment.cs
    ├── FakePollTracker.cs
    ├── FakeReceivedNotification.cs
    ├── FakeSessionStore.cs
    ├── FakeSignalMessageDeduplicator.cs
    ├── InMemoryCommsRedis.cs                   # Stream and string commands only
    ├── MonitorSourcesGroupRouterTests.cs
    ├── SignalMessageDeduplicatorTests.cs
    ├── StubAIAgent.cs
    ├── TestMetrics.cs
    └── ThrowingRemoteCache.cs
```

## Running

```powershell
dotnet test --project src/CasCap.Comms.Tests/CasCap.Comms.Tests.csproj
```
