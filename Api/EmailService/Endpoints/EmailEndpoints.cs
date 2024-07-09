using EmailService.Models;

using KafkaFlow.Producers;

using Microsoft.AspNetCore.Mvc;

using Processor.Grains.Interfaces;

using System.Text.Json.Nodes;

namespace EmailService.Endpoints;

public static class EmailEndpoints
{
    public static WebApplication RegisterEmailEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("email");
        group.WithDescription("endpoints for sending emails")
            .WithDisplayName("email-endpoints")
            .WithTags(["email"]);

        group.MapPost("/{clientId}", async (
            [FromRoute] string clientId,
            [FromBody] SendEmailRequest emailRequest,
            [FromServices] IClusterClient clusterClient,
            [FromServices] IProducerAccessor producers,
            [FromServices] ILogger<SendEmailRequest> logger) =>
            {
                    
                if (await clusterClient.GetGrain<ISmtpConfigGrain>(clientId).GetConfig() == default)
                {
                    logger.LogWarning("client {clientId} not found", clientId);
                    return Results.NotFound();
                }
                
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