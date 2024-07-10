using EmailService.Models;

using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Logging;

using Orleans.Runtime;

using Processor.Grains.Interfaces;

using System.Diagnostics.Eventing.Reader;
using System.Security.Cryptography;
using System.Text;

namespace Processor.Grains;

public interface IMessageGrain : IGrainWithStringKey
{
    Task<ScheduleResult> ScheduleMessage(EmailRequest request, TimeSpan retryAfter, TimeSpan timeToLive);

    Task<bool> TrySend();
}

public static class EmailStatus
{
    public const string NotInitialized = "not initialized";
    public const string Initialized = "initialized";
    public const string Sent = "sent";
    public const string Scheduled = "scheduled";
    public const string Failed = "failed";
}

public class Outbox
{
    public EmailRequest EmailRequest { get; internal set; }

    public int Attempt { get; internal set; }

    public TimeSpan RetryAfter { get; internal set; }

    public DateTime NextAttmpt { get; internal set; }

    public DateTime DeadLine { get; internal set; }

    public int MaxAttempts { get; set; } = 5;

    public string Status { get; set; } = "none";
}

public record class ScheduleResult(bool Success, string Reason);

public class SmtpMessageGrain : Grain, IMessageGrain, IRemindable
{
    private readonly IPersistentState<Outbox> _state;
    private readonly IClusterClient _client;
    private readonly ILogger<SmtpMessageGrain> _logger;
    private IDisposable _timer;

    public SmtpMessageGrain(
        [PersistentState("outbox-schedule", "outbox")] IPersistentState<Outbox> state,
        IClusterClient client,
        ILogger<SmtpMessageGrain> logger)
    {
        _state = state;
        _client = client;
        _logger = logger;
        _timer = null!;
    }
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        return base.OnActivateAsync(cancellationToken);
    }

    private const string retryReminder = "next_attempt";
    private const string reviveReminder = "revive";

    public async Task<ScheduleResult> ScheduleMessage(EmailRequest request, TimeSpan retryAfter, TimeSpan timeToLive)
    {
        if (_state.RecordExists)
        {
            return new(false, "already scheduled");
        }

        _state.State = new()
        {
            Attempt = 1,
            RetryAfter = retryAfter,
            DeadLine = DateTime.UtcNow.Add(timeToLive),
            EmailRequest = request
        };

        await _state.WriteStateAsync();

        if (retryAfter < TimeSpan.FromMinutes(1))
        {
            DelayDeactivation(retryAfter);
            await SetTimer(retryAfter);
        }
        else 
        { 
            await this.RegisterOrUpdateReminder(retryReminder, retryAfter, retryAfter);
            DeactivateOnIdle();
        }

        return new(true, "scheduled");
    }

    public async Task<bool> TrySend()
    {
        return await SendMessage();
    }


    private async Task SetTimer(TimeSpan retryAfter)
    {
        if (_timer is not null) return;

        await this.RegisterOrUpdateReminder(reviveReminder, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        var target = this.GetPrimaryKeyString();

        _timer = RegisterTimer(async _ =>
        {
            var r = await _client
                .GetGrain<IMessageGrain>(target)
                .TrySend();

            if (r)
            {
                await Cleanup();
                return;
            }

            _state.State.Attempt++;
            await _state.WriteStateAsync();
        }, null, retryAfter, retryAfter);
    }

    private async Task Cleanup()
    {
        if (_state.State.EmailRequest != null)
            _logger.LogInformation("cleaning up timer for {messageId}", _state.State.EmailRequest.MessageId);
        
        var revive = await this.GetReminder(reviveReminder);
        if (revive != null) await this.UnregisterReminder(revive);
        
        var reminder = await this.GetReminder(retryReminder);
        if (reminder is not null) await this.UnregisterReminder(reminder);

        if (!string.IsNullOrEmpty(_state.Etag)) await _state.ClearStateAsync();
        
        _timer?.Dispose();
        
        DeactivateOnIdle();
    }

    private async Task<bool> SendMessage()
    {
        if (!IsMessageValidInTime()) return true; // just to trigger cleanup, it should not be a bool it should be a tuple

        var msg = _state.State.EmailRequest;
        
        var conf = await GrainFactory.GetGrain<ISmtpConfigGrain>(_state.State.EmailRequest.ClientId).GetConfig() 
            ?? throw new Exception("client is missing configuration");
        
        var key = $"{_state.State.EmailRequest.ClientId}:{conf.UserName}:{conf.Password}";

        var senderId = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

        var r = await GrainFactory
            .GetGrain<ISmptSenderService>(senderId)
            .SendMessage(conf, msg);

        return r.Success;
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (!_state.RecordExists || _state.State.EmailRequest == null)
        {
            _logger.LogInformation("recieved a reminder for a dead state");
            await Cleanup();
            return;
        }

        if (reminderName == reviveReminder)
        {
            await SetTimer(_state.State.RetryAfter);
            return;
        }

        if (reminderName == retryReminder)
        {
            var reminder = await this.GetReminder(reminderName);
            if (IsMessageValidInTime() && reminder is not null)
            {
                await this.UnregisterReminder(reminder);
            }

            try
            {
                if (await SendMessage())
                {
                    if (reminder is not null)
                    {
                        await this.UnregisterReminder(reminder);
                    }
                }
            }
            catch
            {
                if (reminder is not null)
                {
                    await this.UnregisterReminder(reminder);
                }
            }
        }
    }

    private bool IsMessageValidInTime() => 
        _state.State.DeadLine <= DateTime.UtcNow 
        || _state.State.Attempt >= _state.State.MaxAttempts;
}