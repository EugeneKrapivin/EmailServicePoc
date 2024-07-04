namespace EmailService;

public class AzureConfig
{
    public required string AzureStorageConnectionString { get; set; }
}

public class KafkaConfig
{
    public required string[] Bootstrap { get; set; } = [];
}