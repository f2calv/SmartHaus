---
title: SmartHaus Grafana dashboards
description: Authoring and provisioning guidance for the SmartHaus dashboards Helm chart
---

## Chart behaviour

Standalone Grafana dashboard JSON files live under `dashboards/`, one per
dashboard. `templates/dashboards.yaml` emits one sidecar-discoverable ConfigMap
per file. The file basename determines the ConfigMap name and data key, while
the dashboard's internal `uid` remains unchanged. The Fronius dashboard retains
its historical ConfigMap name and data key so the replacement matches the
legacy resource exactly.

Set `enabled: false` to render no dashboard resources. The standalone chart
defaults to `enabled: true` for direct rendering and validation.

Datasource UIDs are injected through exact string replacement of these fixed
placeholders:

* `{{ .Values.datasources.prometheus }}`
* `{{ .Values.datasources.loki }}`

The template must not run dashboard JSON through Helm `tpl`. Grafana
`{{label}}` legend tokens share the same double-brace syntax and must remain
unchanged.

## Authoring flow

Edit dashboard JSON in this chart. The `smarthaus` umbrella includes this chart
as a local dependency and enables it through `dashboards.enabled`. Umbrella
values own the Grafana folder and datasource UIDs used by the deployed
dashboards.

## Reversible cutover

The legacy dashboard ConfigMaps in the `KNX_K8S` GitOps repository remain active
until an umbrella release containing this dependency is deployed. Deploy the
replacement umbrella first, confirm all nine dashboards are discovered in the
`SmartHaus` folder, and only then remove the legacy ConfigMaps in the separate
cutover step.

Until that removal is complete, rollback is limited to setting
`dashboards.enabled: false` and redeploying the umbrella. After legacy removal,
restore those manifests before disabling the umbrella dashboards if a rollback
is required. This ordering avoids a period with no dashboard provider.

## Dashboards

| File                           | Title                          | Source                                      |
| ------------------------------ | ------------------------------ | ------------------------------------------- |
| `fronius-solar.json`           | Haus Energy (Fronius)          | Fronius solar, grid, battery, and load data |
| `haus-overview.json`           | Haus Overview                  | Security events and hardware metrics        |
| `haus-hvac.json`               | Haus HVAC                      | KNX HVAC and Buderus boiler metrics         |
| `haus-energy.json`             | Haus Energy                    | Fronius solar and battery metrics           |
| `haus-doors-shutters.json`     | Haus Doors, Windows & Shutters | KNX contacts and shutter state              |
| `haus-smart-plugs.json`        | Haus Smart Plugs               | Shelly power and device metrics             |
| `haus-lighting.json`           | Haus Lighting & Sockets        | KNX lighting, dimmer, and socket state      |
| `haus-signalr-statistics.json` | Haus SignalR Statistics        | SignalR hub event metrics                   |
| `haus-water-pump.json`         | Haus Water Pump                | Sicce temperature and power metrics         |
