using EmailService.Models;

using Microsoft.AspNetCore.Mvc;

using Processor.Grains.Interfaces;
using Microsoft.Extensions.Azure;

namespace EmailService.Endpoints;

public static class TemplateEndpoints
{
    public static WebApplication RegisterTemplateEndpoints(this WebApplication app)
    {
        var templateEndpoints = app.MapGroup("/config/{clientId}/templates");
        
        templateEndpoints
            .MapGet("/{type}/{lang?}", async ([FromRoute] string clientId, [FromRoute] string type, [FromRoute] string? lang, [FromServices] IClusterClient clusterClient) =>
            {
                var configGrain = clusterClient.GetGrain<IClientTemplateConfigGrain>(clientId);
                if (string.IsNullOrWhiteSpace(lang))
                {
                    var templates = await configGrain.GetTemplates(type);
                    return Results.Json(templates, SourceGenerationContext.Default.Template, "application/json", 200);
                }
                var template = await configGrain.GetTemplate(lang, type);
                if (template is null)
                {
                    return Results.NotFound();
                }
                return Results.Json(template, SourceGenerationContext.Default.Template, "application/json", 200);
            })
            .WithName("get-template")
            .WithOpenApi();

        templateEndpoints
            .MapPost("/", async ([FromRoute] string clientId, [FromBody] Template template, [FromServices] IClusterClient clusterClient) =>
            {
                var configGrain = clusterClient.GetGrain<IClientTemplateConfigGrain>(clientId);
                await configGrain.SetTemplate(template.Language, template.Type, template.SubjectTemplate, template.BodyTemplate);
                return Results.Created($"/config/{clientId}/templates/{template.Type}/{template.Language}", null);
            })
            .WithName("create-client-template")
            .WithOpenApi();

        return app;
    }
}