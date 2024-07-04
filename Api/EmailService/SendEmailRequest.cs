using System.Text.Json.Nodes;

namespace EmailService;

public record class SendEmailRequest(
    string Recipient,
    string RecipientName,
    string Type,
    string Lang,
    JsonObject Context);

public record class ConfigureTemplateRequest(
    string TemplateId,
    string Template,
    string Language);