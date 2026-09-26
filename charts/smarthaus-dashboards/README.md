# SmartHaus Grafana dashboards

Publishes the SmartHaus Grafana dashboards as sidecar-discoverable ConfigMaps. The chart is
independent of the [`smarthaus`](../smarthaus/README.md) application chart, which does not own any
Grafana resources, and is versioned by its own `Chart.yaml`. It is published to
`oci://ghcr.io/f2calv/charts/smarthaus-dashboards`.

## Install

### Helm

Install the dashboards into the namespace your Grafana sidecar watches:

```bash
helm install smarthaus-dashboards oci://ghcr.io/f2calv/charts/smarthaus-dashboards --version 0.1.1 \
  --namespace my-namespace --create-namespace \
  --set-string datasources.prometheus=prometheus
```

Upgrade to the latest stable chart published in GHCR:

```bash
helm upgrade --install smarthaus-dashboards oci://ghcr.io/f2calv/charts/smarthaus-dashboards \
  --namespace my-namespace --create-namespace \
  --set-string datasources.prometheus=prometheus
```

### Argo CD Application

[Argo CD](https://argo-cd.readthedocs.io/) can consume the same OCI package directly:

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: smarthaus-dashboards
  namespace: argocd
spec:
  project: default
  destination:
    namespace: my-namespace
    server: https://kubernetes.default.svc
  source:
    repoURL: ghcr.io/f2calv
    chart: charts/smarthaus-dashboards
    targetRevision: 0.1.1
    helm:
      valuesObject:
        dashboardFolder: SmartHaus
        datasources:
          prometheus: prometheus
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
```

## Configuration

| Value | Default | Notes |
| --- | --- | --- |
| `enabled` | `true` | Set `false` to render no dashboards |
| `dashboardFolder` | `SmartHaus` | Written to the `grafana_folder` annotation |
| `datasources.prometheus` | `prometheus` | Prometheus datasource UID |

Each file under `dashboards/` becomes a ConfigMap named `grafana-dashboard-<file>` with the
`grafana_dashboard: "1"` label, and the datasource UID is substituted into the JSON. Grafana must
run the dashboard sidecar with `folderAnnotation: grafana_folder` for the folder to apply.

The Fronius dashboard keeps its established identity: its ConfigMap is named
`grafana-dashboard-fronius-solar-cm` with the data key `grafana-dashboard-fronius-solar.json`, so
upgrading from an earlier install replaces it in place rather than creating a duplicate.

### Default Values

```yaml
# Renders the dashboard ConfigMaps.
enabled: true

# Grafana folder annotation.
dashboardFolder: SmartHaus

# Datasource UIDs substituted into the dashboard JSON.
datasources:
  prometheus: prometheus
```

## Dashboards

| File | Title | Source |
| --- | --- | --- |
| `fronius-solar.json` | Haus Energy (Fronius) | Fronius solar, grid, battery, and load data |
| `haus-overview.json` | Haus Overview | Security events and hardware metrics |
| `haus-hvac.json` | Haus HVAC | KNX HVAC and Buderus boiler metrics |
| `haus-energy.json` | Haus Energy | Fronius solar and battery metrics |
| `haus-doors-shutters.json` | Haus Doors, Windows & Shutters | KNX contacts and shutter state |
| `haus-smart-plugs.json` | Haus Smart Plugs | Shelly power and device metrics |
| `haus-lighting.json` | Haus Lighting & Sockets | KNX lighting, dimmer, and socket state |
| `haus-signalr-statistics.json` | Haus SignalR Statistics | SignalR hub event metrics |
| `haus-water-pump.json` | Haus Water Pump | Sicce temperature and power metrics |

## Related Projects

- [Grafana dashboard provisioning](https://grafana.com/docs/grafana/latest/administration/provisioning/#dashboards)
- [kube-prometheus-stack](https://github.com/prometheus-community/helm-charts/tree/main/charts/kube-prometheus-stack)
  runs the Grafana sidecar that discovers these ConfigMaps.
- [smarthaus](../smarthaus/README.md) deploys the application these dashboards observe.