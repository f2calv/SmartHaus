# REST Client Requests

These request collections exercise SmartHaus controllers and their upstream device or cloud APIs
through the VS Code REST Client extension.

## Setup

1. Copy `requests/.env.example` to `requests/.env`.
2. Replace the synthetic values with local endpoints, credentials, and device identifiers.
3. Open a `.http` file and select **Send Request** above the request to execute.

`requests/.env` is ignored by Git. Keep credentials, authorization values, device identifiers, and
private endpoints there. Stable public API endpoints and localhost demo URLs may remain in the
request files.

## Collections

| Files | Purpose |
| --- | --- |
| `*-controller.http` | SmartHaus application endpoints |
| `*-client.http` | Direct upstream device or cloud APIs |
| `knx-controller.http` | KNX bus, HVAC, lighting, outlet, and shutter endpoints |
| `ubiquiti-demo.http` | Local Docker Compose demo webhook requests |

## Execution Safety

Review each request's `# @note`, URL, credentials and payload before sending it. A `GET` is not
necessarily read-only: DoorBird device commands can unlock doors or reboot hardware, while
DoorBird and Ubiquiti event callbacks publish events. Depending on enabled sinks, callbacks can
capture media, invoke analysis and send notifications. Demo events also enter the configured
pipeline; synthetic identifiers do not make them a dry run.

KNX names are synthetic examples using the current category and room names. Choose existing
group/address names from the read endpoints before sending a command. Examples with `dryRun=true`
resolve proposed changes without queuing or sending KNX commands; the other command examples can
operate physical devices. Body fields and validation apply to dry runs too.

Controller endpoints are available only when their feature is enabled. In particular, enabling
`DDns` exposes its read-only IP query but also enables the DNS update worker; do not enable it merely
to try the request without first configuring the feature's write permissions and target zone.
