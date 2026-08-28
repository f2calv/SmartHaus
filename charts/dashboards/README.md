---
title: SmartHaus Grafana dashboards
description: Authoring and provisioning guidance for the SmartHaus dashboards Helm chart
---

## Chart behaviour

Standalone Grafana dashboard JSON files live under `dashboards/`, one per
dashboard. `templates/dashboards.yaml` emits one sidecar-discoverable ConfigMap
per file. The file basename determines the ConfigMap name and data key, while
the dashboard's internal `uid` remains unchanged.

Datasource UIDs are injected through exact string replacement of these fixed
placeholders:

* `{{ .Values.datasources.prometheus }}`
* `{{ .Values.datasources.loki }}`

The template must not run dashboard JSON through Helm `tpl`. Grafana
`{{label}}` legend tokens share the same double-brace syntax and must remain
unchanged.

## Authoring flow

Edit dashboard JSON in this chart. Once the chart is added as a local dependency
of `smarthaus`, the existing deployment workflow will package the dashboards
with the application chart.

## Dashboards

| File                                | Title                           | Source                                      |
|-------------------------------------|---------------------------------|---------------------------------------------|
| `fronius-solar.json`                | Haus Energy (Fronius)           | Fronius solar, grid, battery, and load data |
| `haus-overview.json`                | Haus Overview                   | Security events and hardware metrics        |
| `haus-hvac.json`                    | Haus HVAC                       | KNX HVAC and Buderus boiler metrics         |
| `haus-energy.json`                  | Haus Energy                     | Fronius solar and battery metrics           |
| `haus-doors-shutters.json`          | Haus Doors, Windows & Shutters  | KNX contacts and shutter state              |
| `haus-smart-plugs.json`             | Haus Smart Plugs                | Shelly power and device metrics             |
| `haus-lighting.json`                | Haus Lighting & Sockets         | KNX lighting, dimmer, and socket state      |
| `haus-signalr-statistics.json`      | Haus SignalR Statistics         | SignalR hub event metrics                   |
| `haus-water-pump.json`              | Haus Water Pump                 | Sicce temperature and power metrics         |
