{{- define "email-processor.fullname" -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "validateRequired" -}}
{{- if not .Values.commonRepository -}}
{{- fail "commonRepository is a required value" -}}
{{- end -}}
{{- end -}}