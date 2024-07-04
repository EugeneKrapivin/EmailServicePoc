using EmailService.Models;

using KafkaFlow;
using KafkaFlow.Producers;

using Processor.Grains;
using Processor.Grains.Interfaces;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Fluid.Parser;
using Fluid;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public class TemplateRenderHandler : IMessageHandler<TemplateRequest>
{
    private readonly IProducerAccessor _accessor;
    private readonly IClusterClient _orleansClient;
    private readonly ILogger<TemplateRenderHandler> _logger;
    private FluidParser _parser;

    public TemplateRenderHandler(IProducerAccessor accessor, IClusterClient orleansClient, ILogger<TemplateRenderHandler> logger)
    {
        _accessor = accessor;
        _orleansClient = orleansClient;
        _logger = logger;

        _parser = new FluidParser();
        var options = new TemplateOptions();

        // How Fluid will access the properties on the JsonElement.
        options.MemberAccessStrategy.Register<JsonElement, object>((obj, name) =>
        {
            var property = obj.EnumerateObject()
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            return property.Value;
        });

        options.MemberAccessStrategy.Register<JsonObject, object?>((obj, name) =>
        {
            return obj.GetValueKind() switch
            {
                JsonValueKind.Undefined => null,
                JsonValueKind.Object => obj.AsObject().TryGetPropertyValue(name, out var r) ? r : null,
                _ => throw new IndexOutOfRangeException()
            };
        });

        options.MemberAccessStrategy.Register<JsonValue, object?>((val, name) =>
        {
            return val.GetValueKind() switch
            {
                JsonValueKind.Array => val.AsArray(),
                JsonValueKind.String => val.GetValue<string>(),
                JsonValueKind.Number => val.GetValue<decimal>(),
                JsonValueKind.True or JsonValueKind.False => val.GetValue<bool>(),
                JsonValueKind.Null => null,
                _ => throw new IndexOutOfRangeException()
            };
        });
    }

    public async Task Handle(IMessageContext context, TemplateRequest message)
    {
        var templateConfig = await _orleansClient
            .GetGrain<IClientTemplateConfigGrain>(message.ClientId)
            .GetTemplate(message.Lang, message.Type);
        
        if (templateConfig is null)
        {
            _logger.LogWarning("no template found for client {clientId} with type {type} and lang {lang}", message.ClientId, message.Type, message.Lang);
            return;

            // TODO: send message to DLQ
        }
        
        if (_parser.TryParse(templateConfig.BodyTemplate, out var templateBody, out var err) 
            && _parser.TryParse(templateConfig.SubjectTemplate, out var subjectBody, out err))
        {
            var parserContext = new TemplateContext(message.TemplateContext);

            _accessor.GetProducer("outbox-senders")
                .Produce(message.ClientId, new EmailRequest(
                    message.MessageId, 
                    message.Recipient.Address, 
                    message.Recipient.Name,
                    subjectBody.Render(parserContext),
                    templateBody.Render(parserContext),
                    message.ClientId),
                    deliveryHandler: report =>
                    {
                        if (report.Status != Confluent.Kafka.PersistenceStatus.Persisted)
                        {
                            _logger.LogWarning("possibly lost a message");
                        }
                    });
        }
        else 
        { 
            _logger.LogError("{clientId} Encountered template error with fluid {err}", message.ClientId, err);
            return;
        }
    }
}

public class EmailRequestMessageHandler : IMessageHandler<EmailRequest>
{
    private readonly IClusterClient _orleansClient;
    private readonly ILogger<EmailRequestMessageHandler> _logger;

    public EmailRequestMessageHandler(IClusterClient client, ILogger<EmailRequestMessageHandler> logger)
    {
        _orleansClient = client;
        _logger = logger;
    }

    public async Task Handle(IMessageContext context, EmailRequest message)
    {
        var smtpConfig = await _orleansClient
            .GetGrain<ISmtpConfigGrain>(message.ClientId)
            .GetConfig();

        if (smtpConfig is null)
        {
            _logger.LogWarning("There is no smtp configuration for client with {clientId}", message.ClientId);
            return;
        }

        var key = $"{message.ClientId}:{smtpConfig.UserName}:{smtpConfig.Password}";

        var senderId = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

        try 
        { 
            var sendRequest = await _orleansClient
                .GetGrain<ISmptSenderService>(senderId)
                .SendMessage(smtpConfig, message);

            if (!sendRequest.Success)
            {
                var scheduleResult = await _orleansClient
                    .GetGrain<IMessageGrain>(message.MessageId)
                    .ScheduleMessage(message, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5));

                if (!scheduleResult.Success)
                {
                    _logger.LogError("failed to schedule a message for client {clientId}", message.ClientId);
                }
            }
        }
        catch (Exception ex) 
        {
            _logger.LogError(ex, "exception while attempting to send email {clientId}", message.ClientId);
        }
    }
}
