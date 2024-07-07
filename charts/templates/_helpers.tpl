{{- define "required-repository" -}}
{{- if (eq .Values.commonRepository "REQUIRED") -}}
{{- fail "commonRepository is a required value" -}}
{{- end -}}
{{- end -}}

{{- define "required-ssl-cert" -}}
{{- if (eq .Values.ingress.annotations.sslCertificate "REQUIRED") -}}
{{- fail "ingress.annotations.sslCertificate is a required value" -}}
{{- end -}}
{{- end -}}

{{- define "email-processor.labels" -}}
app.kubernetes.io/name: {{ .Release.Name | trunc 63 | trimSuffix "-" }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion }}
app.kubernetes.io/component: {{ .Values.component | default "email-processor" }}
app.kubernetes.io/part-of: {{ .Values.partOf | default "email-processor" }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}