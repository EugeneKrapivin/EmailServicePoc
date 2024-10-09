using KafkaFlow.OpenTelemetry;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Scalar.AspNetCore;

using System.Diagnostics;
using System.Reflection;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        builder.Services.AddOpenApi(opts =>
        {
            opts.AddDocumentTransformer((doc, ctx, ct) =>
            {
                doc.Servers.Clear();
                doc.Servers.Add(new OpenApi.Models.OpenApiServer()
                {
                    Url = "/",
                });

                return Task.CompletedTask;
            });
        });

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(otel =>
            {
                otel.IncludeFormattedMessage = true;
                otel.IncludeScopes = true;
            }).AddConsole();
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter("Microsoft.Orleans");
            })
            .WithTracing(tracing =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    // We want to view all traces in development
                    tracing.SetSampler(new AlwaysOnSampler());
                }

                AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

                tracing
                    .AddSource("Azure.*")
                    .AddSource("Azure.Data.Tables")
                    .AddSource("Microsoft.Orleans.Runtime")
                    .AddSource("Microsoft.Orleans.Application")
                    .AddSource(KafkaFlowInstrumentation.ActivitySourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation(o =>
                    {
                        o.FilterHttpRequestMessage = (_) => Activity.Current?.Parent?.Source?.Name != "Azure.Core.Http";
                    });
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4137";

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(rb => rb
                .AddEnvironmentVariableDetector()
                .AddContainerDetector()
                .AddHostDetector()
                .AddProcessDetector()
                .AddProcessRuntimeDetector()
                .AddDetector(new ResourceDetector()));

        builder.Logging.AddOpenTelemetry(logging =>
            logging
                .AddOtlpExporter((exporterOptions, processorOptions) =>
                {
                    exporterOptions.Endpoint = new Uri(otelEndpoint);
                    exporterOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    exporterOptions.ExportProcessorType = ExportProcessorType.Batch;


                    processorOptions.ExportProcessorType = ExportProcessorType.Batch;
                    processorOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;
                    processorOptions.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = (int)TimeSpan.FromSeconds(30).TotalMilliseconds;
                    processorOptions.BatchExportProcessorOptions.MaxQueueSize = 2048;
                    processorOptions.BatchExportProcessorOptions.MaxExportBatchSize = 512;
                }));

        otel.WithMetrics(metrics =>
            metrics
            .SetExemplarFilter(ExemplarFilterType.TraceBased)
            .AddOtlpExporter("default", (opts, reader) =>
            {
                opts.Endpoint = new Uri(otelEndpoint);
                opts.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;

                reader.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;
                reader.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;
            })
            .AddPrometheusExporter());

        otel.WithTracing(tracing => tracing
            .AddOtlpExporter("default", (opts) =>
            {
                opts.Endpoint = new Uri(otelEndpoint);
                opts.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;

                opts.ExportProcessorType = ExportProcessorType.Batch;
                opts.BatchExportProcessorOptions.ScheduledDelayMilliseconds = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;
                opts.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = (int)TimeSpan.FromSeconds(30).TotalMilliseconds;
                opts.BatchExportProcessorOptions.MaxQueueSize = 2048;
                opts.BatchExportProcessorOptions.MaxExportBatchSize = 512;
            }));

        return builder;
    }

    public class ResourceDetector : IResourceDetector
    {
        public Resource Detect()
        {
            var resource = ResourceBuilder.CreateDefault()
                .AddAttributes([
                    new("version", GetVersion()),
                    new("serviceName", Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown")
                ]);

            return resource.Build();
        }

        static string GetVersion()
           => Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0.0";
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference();
        }

        app.MapOpenApi();
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new()
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}