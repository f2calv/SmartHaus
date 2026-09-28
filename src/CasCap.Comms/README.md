# CasCap.Comms

The Signalizr communications pipeline shared by applications that talk to Signal Messenger through
a [signalizr](https://github.com/f2calv/signalizr) gateway.

## Purpose

`CommunicationsBgService` is the single component of an application that talks to Signal. It:

- consumes `CommsEvent` entries that any pod writes to a Redis Stream, applying flood protection
  (a token-bucket rate limit and a stale-event cut-off, both announced by a drop notice);
- routes each event to the chat group or an operator-only monitor group, attaching media cached in
  Redis;
- subscribes to inbound group messages, suppresses redeliveries, acknowledges them with reactions,
  and transcribes voice notes;
- answers through an optional `ICommsResponder` via a bounded reply queue, with typing indicators and
  optional spoken replies.

Without an available responder, chat-bound events are formatted and sent directly and inbound
messages are only logged. [CasCap.Comms.AI](../CasCap.Comms.AI/README.md) provides the AI agent
responder.

This project is maintained **byte-identically** in the SmartHaus and CAS repositories until the
shared communications code moves to its own repository. Change both copies in the same change.

## Public Surface

| Type | Purpose |
| --- | --- |
| `CommunicationsBgService` | The `Comms` background feature |
| `CommsStreamSinkService` | `IEventSink<CommsEvent>` that writes to the comms stream (`[SinkType("CommsStream")]`) |
| `CommsMediaStore` | Caches attachment bytes in Redis and returns the `MediaReference` a producer puts in `JsonPayload` |
| `ICommsResponder` | Seam that produces replies: stream-event turns, poll votes, commands and queued turns |
| `CommsTurn`, `CommsReply`, `CommsCommandOutcome` | The units of work and results exchanged with a responder |
| `PlainCommsEventFormatter` | Default `ICommsEventFormatter`: the event message unchanged |
| `TimestampedCommsEventFormatter` | `ICommsEventFormatter` producing `HH:mm:ss.fff UTC [environment] [source] message` |
| `MonitorSourcesGroupRouter` | Default `ICommsGroupRouter`: `CommsConfig.MonitorSources` go to the monitor group |
| `ISignalMessageDeduplicator`, `RedisSignalMessageDeduplicator` | Duplicate suppression by hashed message identity |
| `CommsServiceCollectionExtensions` | `AddCommsStreamSink()` for producers and `AddComms(configuration)` for the `Comms` feature |

`ICommsEventFormatter`, `ICommsGroupRouter`, `CommsEvent` and `MediaReference` live in
`CasCap.Common.Abstractions`. Register a formatter, router or deduplicator before `AddComms` to replace
the default.

## Configuration

`CommsConfig` binds from `CasCap:CommsConfig`. Group names must match the gateway's configured
groups exactly, including spaces and case.

| Setting | Default | Description |
| --- | --- | --- |
| `GroupName` | `"My Test Group Name"` | User-facing chat group |
| `MonitorGroupName` | `null` | Operator-only diagnostics group; unset disables diagnostics |
| `MonitorSources` | empty | Event sources delivered directly to the monitor group |
| `EchoTranscriptToDebugChat` | `false` | Echo voice transcripts to the monitor group |
| `DelegationMessagesEnabled` | `true` | Let an agent responder announce sub-agent delegation |
| `StreamKey` | `"comms:stream:events"` | Redis Stream key |
| `ConsumerGroup` | `"comms:agents"` | Redis consumer group |
| `ConsumerName` | `{MachineName}-{AppName}` | Consumer within the group |
| `ConsumerGroupStartId` | `"0"` | Start position when creating the consumer group |
| `StreamReadPosition` | `">"` | `XREADGROUP` read position |
| `StreamReadCount` | `10` | Entries per read |
| `PollingIntervalMs` | `5000` | Retry interval for the stream and the subscription |
| `HealthCheckProbeDelayMs` | `2000` | Retry interval while the gateway is unreachable at startup |
| `AllowedSources` | empty | When set, only these event sources are processed |
| `StreamSendThrottlingEnabled` | `true` | Rate-limit stream-originated sends |
| `StreamSendRatePerMinute` | `20` | Sustained send rate |
| `StreamSendBurst` | `10` | Initial burst allowance |
| `StaleEventDroppingEnabled` | `true` | Drop events older than `MaxEventAgeMs` |
| `MaxEventAgeMs` | `60000` | Stale-event cut-off |
| `DropNoticeIntervalMs` | `60000` | Minimum interval between drop notices |
| `ReplyQueueCapacity` | `100` | Reply queue bound; producers wait when it is full |
| `MediaCacheKeyPrefix` | `"comms:cache:media"` | Redis key prefix for cached attachments |
| `MediaCacheTtlMs` | `300000` | Lifetime of an unsent cached attachment |
| `MessageDeduplicationTtlHours` | `168` | Duplicate-suppression window |

## Configuration Examples

Minimal:

```json
{
  "CasCap": {
    "CommsConfig": {
      "GroupName": "My Test Group Name"
    }
  }
}
```

With an operator monitor group and operational sources:

```json
{
  "CasCap": {
    "CommsConfig": {
      "GroupName": "My Test Group Name",
      "MonitorGroupName": "My Test Monitor Group Name",
      "MonitorSources": [ "SchedulerBgService" ],
      "EchoTranscriptToDebugChat": true
    }
  }
}
```

`AddComms` also registers `CasCap:SignalizrClientConfig`, `CasCap:SpeechToTextConfig` and
`CasCap:TextToSpeechConfig`; voice processing does no work until its `Mode` enables it.

## Dependencies

| Dependency | Use |
| --- | --- |
| `CasCap.Signalizr.Client` | Gateway client and its best-effort and startup helpers |
| `CasCap.Api.Voice` | Speech-to-text and text-to-speech |
| `CasCap.Common.Abstractions` | `CommsEvent`, `ICommsEventFormatter`, `ICommsGroupRouter`, `MediaReference`, `IBgFeature` |
| `CasCap.Common.Caching` | `IRemoteCache` for the Redis Stream and cached media |
| `CasCap.Common.Configuration`, `CasCap.Common.Extensions` | Options binding and host helpers |

Debug builds reference adjacent source checkouts; Release builds use the published packages.
