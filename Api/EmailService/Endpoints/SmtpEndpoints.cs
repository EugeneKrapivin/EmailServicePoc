using EmailService.Models;

using Microsoft.AspNetCore.Mvc;

using Processor.Grains.Interfaces;
using Microsoft.Extensions.Azure;
using System.Net;
using System.Text.Json.Serialization;
using static EmailService.SMTPEndpoints.SmtpEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;

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

        smtpEndpoints
            .MapGet("/", async ([FromRoute] string clientId, [FromServices] IClusterClient clusterClient) =>
            {
                var configGrain = clusterClient.GetGrain<ISmtpConfigGrain>(clientId);

                var config = await configGrain.GetConfig();

                return config is null
                    ? Results.NotFound()
                    : TypedResults.Ok(
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
            .WithName("get-client-smtp-config")
            .WithOpenApi();

        smtpEndpoints
            .MapPost("/", async ([FromRoute] string clientId, [FromBody] SmtpConfig config, [FromServices] IClusterClient clusterClient) =>
            {
                // TODO: validate input

                var configGrain = clusterClient.GetGrain<ISmtpConfigGrain>(clientId);

                await configGrain.SetConfig(config);

                // TODO: handle create-at header
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

        return app;
    }
}