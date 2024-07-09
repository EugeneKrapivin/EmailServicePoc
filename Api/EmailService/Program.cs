using EmailService;
using EmailService.Models;

using KafkaFlow;
using KafkaFlow.Serializer;
using KafkaFlow.Configuration;

using Orleans.Serialization;
using Orleans.Configuration;
using Orleans.Clustering.AzureStorage;

using EmailService.Endpoints;
using EmailService.SMTPEndpoints;

using Azure.Data.Tables;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<AzureConfig>()
    .BindConfiguration("Azure");

builder.Services
    .AddOptions<KafkaConfig>()
    .BindConfiguration("Kafka");

builder.AddServiceDefaults();

builder.Services.AddOrleansClient(clientBuilder =>
{
    var secrets = builder.Configuration.GetSection("Azure").Get<AzureConfig>();

    if (string.IsNullOrEmpty(secrets?.AzureStorageConnectionString))
    {
        throw new Exception("you've got to spill some secrets...");
    }

    clientBuilder.UseAzureStorageClustering((AzureStorageGatewayOptions opts) =>
    {
        opts.TableServiceClient = new TableServiceClient(secrets.AzureStorageConnectionString);
    });
    
    clientBuilder.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = Environment.GetEnvironmentVariable("processorService__ClusterId") ?? Constants.ClusterId;
        options.ServiceId = Environment.GetEnvironmentVariable("processorService__ServiceId") ?? Constants.ServiceId;
    });
    clientBuilder.UseConnectionRetryFilter((ex, c) => Task.FromResult(true));
    clientBuilder.AddActivityPropagation();
    clientBuilder.Services.AddSerializer(serializerBuilder =>
    {
        serializerBuilder.AddJsonSerializer(type => type.Namespace!.StartsWith("EmailService.Models"), SourceGenerationContext.Default.Options);
    });
});

builder.Services.AddKafka(kafka =>
{
    var config = builder.Configuration
        .GetRequiredSection("Kafka")
        .Get<KafkaConfig>();

    kafka
        .UseMicrosoftLog()
        .AddOpenTelemetryInstrumentation()
        .AddCluster(cluster => cluster
            .WithBrokers(config.Bootstrap)
            .AddProducer("template-renderers", producer => producer
                .DefaultTopic(Constants.RenderEmailTopic)
                .AddMiddlewares(middlewares => middlewares
                    .AddSerializer<JsonCoreSerializer>()
                )
            )
        );
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.RegisterSmtpEndpoints()
    .RegisterTemplateEndpoints()
    .RegisterEmailEndpoints();

app.UseHttpsRedirection();

app.Run();
