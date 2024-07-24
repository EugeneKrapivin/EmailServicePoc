using EmailService.Models;

using Microsoft.AspNetCore.Mvc;

using Processor.Grains.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using SystemTextJsonPatch;

namespace EmailService.SMTPEndpoints;

public static class SmtpEndpoints
{
    public record class ClientStmpConfigResponse(
        string ServerUrl,
        string ServerPort,
        string From,
        string FromName,
        string UserName,
        string Password);

    public static WebApplication RegisterSmtpEndpoints(this WebApplication app)
    {
        var smtpEndpoints = app.MapGroup("/config/{clientId}/smtp");
        smtpEndpoints.WithDescription("endpoints for configuration management of client smtp configs")
            .WithDisplayName("smtp-config")
            .WithTags(["smtp"]);

        smtpEndpoints
            .MapGet("/", async (
                    [FromRoute] string clientId,
                    [FromServices] IClusterClient clusterClient,
                    [FromServices] ILogger<WebApplication> logger) =>
                {
                    var configGrain = clusterClient.GetGrain<ISmtpConfigGrain>(clientId);

                    var config = await configGrain.GetConfig();
                    if (config is null)
                    {
                        logger.LogWarning("smtp configuration for {clientId} not found", clientId);
                        return Results.NotFound();
                    }
                    var result = new ClientStmpConfigResponse
                    (
                        From: config.From,
                        FromName: config.FromName,
                        UserName: config.UserName,
                        Password: "***",
                        ServerPort: config.ServerPort,
                        ServerUrl: config.ServerUrl
                    );

                    return TypedResults.Ok(result);
                })
                .Produces<ClientStmpConfigResponse>(200, "application/json")
                .Produces<NotFound>(404, "application/json")
                .WithName("get-client-smtp-config")
                .WithOpenApi();

        smtpEndpoints
            .MapPost("/", async (
                [FromRoute] string clientId,
                [FromBody] SmtpConfig config,
                [FromServices] ILogger<WebApplication> logger,
                [FromServices] IClusterClient clusterClient) =>
            {
                // TODO: validate input

                var configGrain = clusterClient.GetGrain<ISmtpConfigGrain>(clientId);

                if (await configGrain.GetConfig() is not null)
                {
                    logger.LogWarning("smtp configuration for {clientId} already exists.", clientId);
                    return Results.BadRequest();
                }

                await configGrain.SetConfig(config);

                return TypedResults.CreatedAtRoute(
                   new ClientStmpConfigResponse
                   (
                       From: config.From,
                       FromName: config.FromName,
                       UserName: config.UserName,
                       Password: "***",
                       ServerPort: config.ServerPort,
                       ServerUrl: config.ServerUrl
                   ),
                   "get-client-smtp-config", new { clientId });
            })
            .Produces<ClientStmpConfigResponse>(201, "application/json")
            .WithName("set-client-smtp-config")
            .WithOpenApi();

        smtpEndpoints
            .MapPut("/", async (
                [FromRoute] string clientId,
                [FromBody] SmtpConfig config,
                [FromServices] ILogger<WebApplication> logger,
                [FromServices] IClusterClient clusterClient) =>
            {
                // TODO: validate input

                var configGrain = clusterClient.GetGrain<ISmtpConfigGrain>(clientId);

                if (await configGrain.GetConfig() is null)
                {
                    logger.LogWarning("smtp configuration for {clientId} not found.", clientId);
                    return Results.NotFound();
                }

                await configGrain.SetConfig(config);

                return TypedResults.Ok(
                   new ClientStmpConfigResponse
                   (
                       From: config.From,
                       FromName: config.FromName,
                       UserName: config.UserName,
                       Password: "***",
                       ServerPort: config.ServerPort,
                       ServerUrl: config.ServerUrl
                   ));
            })
            .Produces<ClientStmpConfigResponse>(200, "application/json")
            .Produces<NotFound>(404, "application/json")
            .WithName("update-client-smtp-config")
            .WithOpenApi();

        return app;
    }
}
