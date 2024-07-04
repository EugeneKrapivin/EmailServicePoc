using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EmailService.Models;

public record class SmtpConfig(
    string ServerUrl,
    string ServerPort,
    string From,
    string FromName,
    string UserName, 
    string Password);

public record class Recipient(string Name, string Address);

public record class TemplateRequest(string MessageId, 
    string ClientId, 
    string Type, 
    string Lang,
    Recipient Recipient,
    JsonObject TemplateContext);

public record class EmailRequest(
    string MessageId,
    string Recipient,
    string RecipientName,
    string Subject,
    string Body,
    string ClientId);

public record Template(string Language, string Type, string SubjectTemplate, string BodyTemplate);

public class LocaleCollection
{
    public Dictionary<string, Template> Templates { get; set; } = [];
}


[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(SmtpConfig))]
[JsonSerializable(typeof(EmailRequest))]
[JsonSerializable(typeof(TemplateRequest))]
[JsonSerializable(typeof(Template))]
[JsonSerializable(typeof(Recipient))]
public partial class SourceGenerationContext : JsonSerializerContext
{
}
