using System.Diagnostics;
using System.Text.Json;
using System.Text;
using Bogus;
using ShellProgressBar;
using Humanizer;
using System.Net.NetworkInformation;

var client = new HttpClient()
{
    BaseAddress = new Uri("https://localhost:18081/") // this should be the address to the EmailService API
};

var faker = new Faker<Person>();
faker.RuleFor(x => x.Name, (f, p) => f.Person.FullName);
faker.RuleFor(x => x.EmailAddress, (f, p) => f.Person.Email);

var shopFaker = new Faker<Shop>();
shopFaker.RuleFor(x => x.Name, (f, s) => f.Company.CompanyName());

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var total = 10000;
var shops = shopFaker.Generate(1000).ToArray();
var users = faker.Generate(total).ToArray();
var c = 0;
var pb = new ProgressBar(maxTicks: total, "starting...", new ProgressBarOptions
{
    ForegroundColor = ConsoleColor.Yellow,
    BackgroundColor = ConsoleColor.DarkYellow,
    ProgressCharacter = '─',
    DisplayTimeInRealTime = true,
    ShowEstimatedDuration = true
});
var sem = new SemaphoreSlim(1, 1);
var parellelism = 7;
var avg = 0d;
var opts = new ParallelOptions { MaxDegreeOfParallelism = parellelism, CancellationToken = cts.Token };

// this try is wrapping the Parallel.ForAsync call to catch and log any exception, except TaskCanceledException
try
{
    await Parallel.ForAsync(0, total, opts, async (i, ct) =>
    {
        var ts = Stopwatch.GetTimestamp();
        var rng = Random.Shared.Next(1, 1000);
        var shop = shops[rng];
        var person = users[i];
        var req = new
        {
            recipient = person.EmailAddress,
            recipientName = person.Name,
            type = "customer-account-welcome",
            lang = "en",
            context = new
            {
                customer = new
                {
                    name = person.Name
                },
                shop = new
                {
                    name = shop.Name
                }
            }
        };

        var job = await client.PostAsync($"email/{rng}", new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"), ct);
        if (!job.IsSuccessStatusCode)
        {
            pb.WriteErrorLine($"Failed to send email to {person.EmailAddress} ({job.StatusCode})");
        }
        
        var elapsed = Stopwatch.GetElapsedTime(ts);
        
        await sem.WaitAsync(ct);

        var n = Interlocked.Increment(ref c);
        avg = (avg * (n - 1) + elapsed.TotalMilliseconds) / n;
        pb.Message = $"sending... {n}/{total} ({avg:F2}ms, left {TimeSpan.FromMilliseconds((total - n) * avg / parellelism).Humanize()})";
        pb.EstimatedDuration = TimeSpan.FromMilliseconds((total - n) * avg / parellelism);
        pb.Tick();
        
        sem.Release();
    });
}
catch (Exception ex) when (ex is not TaskCanceledException)
{
    pb.WriteErrorLine(ex.Message);
}
    
pb.Message = "Done";

public class Person
{
    public string Name { get; set; }
    public string EmailAddress { get; set; }
}

public class Shop
{
    public string Name { get; set; }
}