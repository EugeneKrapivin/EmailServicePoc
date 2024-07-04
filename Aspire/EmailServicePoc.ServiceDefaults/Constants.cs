namespace Microsoft.Extensions.Hosting;

public static class Constants
{
    public static readonly string ClusterId = "1002";
    public static readonly string ServiceId = "email-processor-poc";
    public static readonly string KafkaOutboxEmailTopic = "outbox";
    public static readonly string RenderEmailTopic = "template-render";
}
