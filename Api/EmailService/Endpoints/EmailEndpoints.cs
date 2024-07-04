using EmailService.Models;

using KafkaFlow;
using KafkaFlow.Producers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;

using System.Text;
using System.Text.Json.Nodes;

namespace EmailService.Endpoints;

public static class EmailEndpoints
{
    public static WebApplication RegisterEmailEndpoints(this WebApplication app)
    {
        app.MapPost("/email/{clientId}", (
            [FromRoute] string clientId,
            [FromBody] SendEmailRequest emailRequest,
            [FromServices] IProducerAccessor producers,
            [FromServices] ILogger<SendEmailRequest> logger) =>
                {
                    // TODO: check that client SMTP config exists and ENABLED
                    var msgId = Guid.NewGuid().ToString("N");

                    producers["template-renderers"]
                        .Produce(
                            clientId, 
                            new TemplateRequest(
                                msgId, 
                                clientId,
                                emailRequest.Type ?? "customer-account-welcome", 
                                emailRequest.Lang ?? "en",
                                new Recipient(emailRequest.RecipientName, emailRequest.Recipient),
                                emailRequest.Context ?? new JsonObject()
                                {
                                    ["customer"] = new JsonObject() { ["name"] = "unknown" },
                                    ["shop"] = new JsonObject() { ["name"] = "shop-unknown"}
                                }),
                            deliveryHandler: report =>
                            {
                                if (report.Status != Confluent.Kafka.PersistenceStatus.Persisted)
                                {
                                    logger.LogWarning("possibly lost a message");
                                }
                            });

                    return Results.Accepted();
                })
            .WithName("send-email")
            .WithOpenApi();

        return app;
    }
}