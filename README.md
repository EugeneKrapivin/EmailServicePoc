# Email distribution service

This is a demo service created for a presentation/lecture on modern dotnet observability.

Used technologies:
* [AspNetCore](https://dotnet.microsoft.com/en-us/apps/aspnet)
* [Orleans](https://learn.microsoft.com/en-us/dotnet/orleans/overview)
* [KafkaFlow](https://farfetch.github.io/kafkaflow/)
* [MailKit](https://github.com/jstedfast/MailKit)
* [OpenTelemetry (Tracing, Metrics, Logging)](https://opentelemetry.io/docs/languages/net/)

## Purpose

This project was built as a proof of concept for scheduling retries with [Orleans Reminders](https://learn.microsoft.com/en-us/dotnet/orleans/grains/timers-and-reminders#reminders) at scale.  
After it surved its initial purpose it became a learning playground for various new technologies like distributed tracing using OTEL.

## Setup

The setup of this project requires:

1. [OTEL Collector](https://opentelemetry.io/docs/collector/) to which you could pass your signals. You could also use a standalone [Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/overview?tabs=bash).
1. Kafka - This project relays on a Kafaka cluster (could be 1 broker). The used topics could be found `EmailServicePoc.ServiceDefaults.Constants`.
1. Azure Storage Tables - At the moment Orleans in configured to use Azure Storage Tables for Grain storage and Reminders storage.

### Configuration

#### OTEL configuration

The `docker-compose.yml` defines 3 OTEL related environment variables

```yaml
- OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:5317
- OTEL_SERVICE_NAME=EmailService_API
- OTEL_RESOURCE_ATTRIBUTES=deployment.environment=development,service.namespace=EmailServicePoc,service.version=1.0.0.0-beta01
```

The OTEL SDK is configured to use these environment variables internally.

#### Kafka bootstrapping configuration

Kafka is configured through a `Kafka:Bootstrap` configuration using the `Options<T>` pattern.
The `Bootstrap` property on the `KafkaConfig` class is a `string[]` requiring bootstrap servers and their ports, i.e. `["broker1:9092"]`.  

It is possible to configure this property in the `appsettings.json` or pass it as an environment variable

```env
Kafka__Bootstrap__0="broker:9092"
```

#### Azure Table Storage

Currently Orleans is configured to use Azure Table Storage as a provider. It is planned in the future to replace it with Postgres.
In this case the documentation would reflect the change.

To pass the connection string it is a best practice to use the `secrets.json` on your local development environment, or a secret valut in production.  
The configuration key is
```
Azure:AzureStorageConnectionString
```

as before, you can define it in a json:
```
{
	"Azure" : { 
		"AzureStorageConnectionString" : "redacted" 
	}
}
```
You could put it in the `secrets.json` or your `appsettings.json`

> Warning: Avoid pushing secrets into your git repo. If by mistake you did push a secret, roll it **as soon as possible**.


## API

Run the API Service to get the Swagger UI to get a glimps of the API.

## Loading

Before loading this service and testing behavior:
1. Setup at least 1 SMTP sink server/testing server
1. Prep the templates and SMTP configs in the service

### SMTP sink

The application sends real SMTP messages, meaning you'll have to spin up a sink SMTP server. I suggest:
1. [Mailhog](https://github.com/mailhog/MailHog) - has the advantage of chaos monkey, but seems to be unmaintained.
1. [Mailpit](https://mailpit.axllent.org/) - fast, great UI, lots of features, not chaos monkey.

### Service configuration

The `/email/{clientId}` endpoint sends an email to a specific address. The `clientId` is used to determine through
which SMTP server the email would be sent and who would be the Sender and what would be the Sender Address.

To configure and SMTP server for a client:

```json
POST /config/{clientId}/smtp

{
  "serverUrl": "string",
  "serverPort": "string",
  "from": "string",
  "fromName": "string",
  "userName": "string",
  "password": "string"
}
```

This will store the SMTP configuration for `clientId`

Sending an email requires a template. This demo service allows to define various templates using the [Fluid library](https://github.com/sebastienros/fluid).

To define a template call:

```json
POST /config/{clientId}/templates

{
  "language": "string",
  "type": "string",
  "subjectTemplate": "string",
  "bodyTemplate": "string"
}
```

### Loading

After there is a client configured with an SMTP config and at least 1 template, sending an email through the system:

```json
POST /email/{clientId}

{
  "recipient": "string",
  "recipientName": "string",
  "type": "string",
  "lang": "string",
  "context": {
   }
}
```

The `context` object would be used to render the selected template (called `type`).

To load this service you can use a simple C# console application as such:

```csharp
var client = new HttpClient()
{
	BaseAddress = new Uri("https://localhost:63076/")
};

var faker = new Faker<Person>();
faker.RuleFor(x => x.Name, (f, p) => f.Person.FullName);
faker.RuleFor(x => x.EmailAddress, (f, p) => f.Person.Email);

var shopFaker = new Faker<Shop>();
shopFaker.RuleFor(x => x.Name, (f, s) => f.Company.CompanyName());

var shops = shopFaker.Generate(1000).ToArray();

for (int i = 0; i < 1; i++)
{
	var rng = Random.Shared.Next(1, 1000);
	var shop = shops[rng];
	var person = faker.Generate();
	var req = new
	{
		recipient = person.EmailAddress,
		recipientName = person.Name,
		type = "customer-account-welcome",
		lang = "en",
		context = new {
			customer = new {
				name = person.Name
			},
			shop = new {
				name = shop.Name
			}
		}
	};
	
	var job = await client.PostAsync($"email/{rng}", new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"));	
	if (!job.IsSuccessStatusCode)
	{
		Console.WriteLine("fuck");
	}
	await Task.Delay(TimeSpan.FromMicroseconds(100));
}

public class Person
{
	public string Name { get; set; }
	public string EmailAddress { get; set; }
}

public class Shop
{
	public string Name { get; set; }
}
```
> Note: this example uses Bogus

or you could use a simple [`Grafana K6`](https://k6.io/) script to do the same.

## Architecture and design

![Design diagram](Assets/email%20service%20structure.drawio.svg)

* This service uses Orleans for the data access and smart caching. 
* The API service has no logic it simply delegates requests into Orleans, or publishes email requests into a kafka queue.
* The sending process is split into 2 steps: Rendering and Sending. This allows to buffer rendered emails in a persistent storage (kafka) allowing the sending step to take a bit more time, without disturbing the processing and rendering of new requests.
* Send attempts that fail trigger an Orleans scheduling grain that will store the rendered email and setup a reminder to attempt to send it a at a later time.
* There is a limited amount of attempts (hard coded) after which the email is dumped.

All frameworks and libraries used are registered to output metrics and distributed traces. All observability signals are sent to
an open telemetry collector using the OpenTelemetry Line Protocol (OTLP).

> Note: known issue: https://github.com/dotnet/orleans/issues/9052