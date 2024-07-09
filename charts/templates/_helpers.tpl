{{- define "repository" -}}
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
{{- end -}}