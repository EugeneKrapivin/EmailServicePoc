using Azure.Data.Tables;

using EmailService.Models;

using KafkaFlow;
using KafkaFlow.Admin.Dashboard;
using KafkaFlow.Configuration;
using KafkaFlow.Serializer;

using Orleans.Configuration;
using Orleans.Serialization;
using Orleans.Hosting;

using Processor.Grains;
using Processor.Host;

using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.Extensions.Options;
using Confluent.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<AzureConfig>()
    .BindConfiguration("Azure");

builder.Services
    .AddOptions<KafkaConfig>()
    .BindConfiguration("Kafka");

builder.AddServiceDefaults();

builder.Host.UseOrleans((ctx,siloBuilder) =>
{
    var secrets = builder.Configuration.GetSection("Azure").Get<AzureConfig>();
    
    if (string.IsNullOrEmpty(secrets?.AzureStorageConnectionString))
    { 
        throw new Exception("you've got to spill some secrets..."); 
    }

    siloBuilder.Services.AddSerializer(serializerBuilder =>
    {
        serializerBuilder.AddJsonSerializer(type => type.Namespace!.StartsWith("EmailService.Models"), SourceGenerationContext.Default.Options);
        serializerBuilder.AddJsonSerializer(type => type.Namespace!.StartsWith("Processor.Grains"), OrleansGenerationContext.Default.Options);
    });

    siloBuilder.AddActivityPropagation();


    if (Environment.GetEnvironmentVariable("is_k8s") == "true")
    {
        siloBuilder.UseKubernetesHosting();
    }
    else 
    { 
        siloBuilder.Configure<ClusterOptions>(options =>
        {
            options.ClusterId = Constants.ClusterId;
            options.ServiceId = Constants.ServiceId;
        });

        siloBuilder.ConfigureEndpoints(Dns.GetHostName(), // for demo this is "secure enough"
        11111,
        30000,
        listenOnAnyHostAddress: true);
    }

    siloBuilder
        .UseAzureStorageClustering(opts => 
        {
            // suggest a fix for the following line
            opts.TableServiceClient = new TableServiceClient(secrets.AzureStorageConnectionString);
        })
        .AddAzureTableGrainStorage("smtp-config-storage", (AzureTableStorageOptions opts) =>
        {
            opts.TableServiceClient = new TableServiceClient(secrets.AzureStorageConnectionString);
            opts.TableName = "smtpConfigStorage";
            opts.DeleteStateOnClear = true;            
        })
        .AddAzureTableGrainStorage("outbox", opts =>
        {
            opts.TableServiceClient = new TableServiceClient(secrets.AzureStorageConnectionString);
            opts.TableName = "outbox";
            opts.DeleteStateOnClear = true;
        })
        .AddAzureTableGrainStorage("templates", opts =>
        {
            opts.TableServiceClient = new TableServiceClient(secrets.AzureStorageConnectionString);
            opts.TableName = "templates";
            opts.DeleteStateOnClear = true;
        })
        .AddAzureTableGrainStorageAsDefault(opts =>
        {
            opts.TableServiceClient = new TableServiceClient(secrets.AzureStorageConnectionString);
            opts.TableName = "OrleansDefault";
            opts.DeleteStateOnClear = true; 
        })
        .UseAzureTableReminderService(opts =>
        {
            opts.TableServiceClient = new TableServiceClient(secrets.AzureStorageConnectionString);
        });

    //siloBuilder.UseDashboard(conf =>
    //{
    //    conf.HostSelf = false;
    //    conf.BasePath = "orleans-dashboard";
    //});
});

builder.Services.AddKafkaFlowHostedService(kafka =>
{
    var config = builder.Configuration.GetRequiredSection("Kafka").Get<KafkaConfig>();
    ArgumentNullException.ThrowIfNull(config, nameof(config));

    kafka
        .UseMicrosoftLog()
        .AddOpenTelemetryInstrumentation()
        .AddCluster(cluster => cluster
            .WithBrokers(config.Bootstrap)
            .AddConsumer(consumer =>
                consumer.Topic(Constants.KafkaOutboxEmailTopic)
                    .WithName("email outbox processors")
                    .WithGroupId("outbox-processors")
                    .WithBufferSize(200)
                    .WithWorkersCount(32)
                    .AddMiddlewares(middlewares =>
                        middlewares
                            .AddDeserializer<JsonCoreDeserializer>()
                            .AddTypedHandlers(handlersBuilders =>
                                handlersBuilders.AddHandler<EmailRequestMessageHandler>())))
            .AddConsumer(consumer =>
                consumer.Topic(Constants.RenderEmailTopic)
                    .WithName("template renderers")
                    .WithGroupId("renderers")
                    .WithBufferSize(200)
                    .WithWorkersCount(32)
                    .AddMiddlewares(middlewares =>
                        middlewares
                            .AddDeserializer<JsonCoreDeserializer>()
                            .AddTypedHandlers(handlersBuilders =>
                                handlersBuilders.AddHandler<TemplateRenderHandler>())))
            .AddProducer("outbox-senders", producer => 
                producer.DefaultTopic(Constants.KafkaOutboxEmailTopic)
                        .AddMiddlewares(middlewares => middlewares
                            .AddSerializer<JsonCoreSerializer>()))
            .EnableAdminMessages("kafka-flow.admin", "kfk-flow-admin")
            .EnableTelemetry("kafka-flow.admin", "kfk-flow-telemetry"));
});

builder.Services.AddHostedService<KafkaTopicBootstrap>();

builder.Services
    .AddOpenTelemetry()
    .WithTracing(tracing => tracing
        //.AddSource("SmptSenderService")
        .AddSource("Azure.*")
        .AddSource("MailKit.Net.SmtpClient"))
    .WithMetrics(metrics => metrics
        .AddMeter("SMTPSender")
        .AddMeter("mailkit.net.smtp")
        .AddMeter("mailkit.net.socket")
        .AddMeter("Microsoft.Orleans")
        .AddMeter("TemplatesMeter"));

builder.Services.AddControllers();

var host = builder.Build();

// this is a bit of a hack to get the meter factory
var meterFactory = host.Services.GetService<IMeterFactory>();
MailKit.Telemetry.Configure(meterFactory);

host.UseKafkaFlowDashboard();
//host.UseOrleansDashboard(new OrleansDashboard.DashboardOptions
//{
//    BasePath = "orleans-dashboard"
//});
host.MapControllers();
host.MapDefaultEndpoints();

host.Run();

public class KafkaTopicBootstrap : IHostedService
{
    private readonly IOptions<KafkaConfig> _kafkaConfig;

    public KafkaTopicBootstrap(IOptions<KafkaConfig> kafkaConfig)
    {
        _kafkaConfig = kafkaConfig;
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var admin = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = string.Join(",", _kafkaConfig.Value.Bootstrap)
            }).Build();

            await admin.CreateTopicsAsync([
                new()
                {
                    Name = Constants.KafkaOutboxEmailTopic,
                    NumPartitions = 3,
                    ReplicationFactor = 2
                },
                new()
                {
                    Name = Constants.RenderEmailTopic,
                    NumPartitions = 3,
                    ReplicationFactor = 2
                }]);

        }
        catch { }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
