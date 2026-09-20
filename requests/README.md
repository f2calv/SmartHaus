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

Some requests change device or application state. Review the method, URL, and payload before sending
them.
