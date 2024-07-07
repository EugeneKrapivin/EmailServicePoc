{{- define "email-processor.fullname" -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "required-repository" -}}
{{- if (eq .Values.commonRepository "REQUIRED") -}}
{{- fail "commonRepository is a required value" -}}
{{- end -}}
{{- end -}}

{{- define "required-ssl-cert" -}}
{{- if (or (eq (index .Values.ingress.tls 0).secretName "REQUIRED") (eq .Values.ingress.annotations.sslCertificate "REQUIRED")) -}}
{{- fail "Both ingress.tls[0].secretName and ingress.annotations.sslCertificate are required values" -}}
{{- end -}}
{{- end -}}