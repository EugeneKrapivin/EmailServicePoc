using EmailService.Models;

using System.Text.Json.Serialization;

namespace Processor.Grains;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Smtp))]
[JsonSerializable(typeof(SmtpConfig))]
[JsonSerializable(typeof(SendResult))]
[JsonSerializable(typeof(Outbox))]
[JsonSerializable(typeof(EmailRequest))]
[JsonSerializable(typeof(ScheduleResult))]
[JsonSerializable(typeof(Template))]
public partial class OrleansGenerationContext : JsonSerializerContext
{
}
