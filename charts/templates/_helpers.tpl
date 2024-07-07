{{- define "email-processor.fullname" -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

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