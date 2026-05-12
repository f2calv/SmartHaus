{{/*
Expand the name of the chart.
*/}}
{{- define "workload.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "workload.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "workload.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "workload.labels" -}}
helm.sh/chart: {{ include "workload.chart" . }}
{{ include "workload.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "workload.selectorLabels" -}}
app.kubernetes.io/name: {{ include "workload.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "workload.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "workload.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Allow the release namespace to be overwritten for multi-namespace deployments in combined charts.
*/}}
{{- define "workload.namespace" -}}
{{- default .Release.Namespace .Values.namespaceOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Default startup probe configuration.
*/}}
{{- define "workload.defaultStartupProbe" -}}
httpGet:
  path: /healthz/startup
  port: 8080
periodSeconds: 10
initialDelaySeconds: 20
failureThreshold: 5
{{- end -}}

{{/*
Default readiness probe configuration.
*/}}
{{- define "workload.defaultReadinessProbe" -}}
httpGet:
  path: /healthz/ready
  port: 8080
periodSeconds: 10
initialDelaySeconds: 60
failureThreshold: 10
{{- end -}}

{{/*
Default liveness probe configuration.
*/}}
{{- define "workload.defaultLivenessProbe" -}}
httpGet:
  path: /healthz/live
  port: 8080
periodSeconds: 10
initialDelaySeconds: 60
failureThreshold: 10
{{- end -}}

{{/*
Consolidated probe block. Renders startup, readiness, and liveness probes.
Set a probe to false to disable it. Set it to a map to override the default.
*/}}
{{- define "workload.probes" -}}
{{- if not (kindIs "bool" .Values.startupProbe) }}
startupProbe:
  {{- if .Values.startupProbe }}
  {{- toYaml .Values.startupProbe | nindent 2 }}
  {{- else }}
  {{- include "workload.defaultStartupProbe" . | nindent 2 }}
  {{- end }}
{{- end }}
{{- if not (kindIs "bool" .Values.readinessProbe) }}
readinessProbe:
  {{- if .Values.readinessProbe }}
  {{- toYaml .Values.readinessProbe | nindent 2 }}
  {{- else }}
  {{- include "workload.defaultReadinessProbe" . | nindent 2 }}
  {{- end }}
{{- end }}
{{- if not (kindIs "bool" .Values.livenessProbe) }}
livenessProbe:
  {{- if .Values.livenessProbe }}
  {{- toYaml .Values.livenessProbe | nindent 2 }}
  {{- else }}
  {{- include "workload.defaultLivenessProbe" . | nindent 2 }}
  {{- end }}
{{- end }}
{{- end -}}


