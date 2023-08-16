{{/*
Expand the name of the chart.
*/}}
{{- define "cartitemprocessor-chart.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "cartitemprocessor-chart.fullname" -}}
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
{{- define "cartitemprocessor-chart.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "cartitemprocessor-chart.labels" -}}
helm.sh/chart: {{ include "cartitemprocessor-chart.chart" . }}
{{ include "cartitemprocessor-chart.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "cartitemprocessor-chart.selectorLabels" -}}
app.kubernetes.io/name: {{ include "cartitemprocessor-chart.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
cluster: {{ .Values.CLUSTER__DISCOVERY__SERVICENAME.value }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "cartitemprocessor-chart.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "cartitemprocessor-chart.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}
