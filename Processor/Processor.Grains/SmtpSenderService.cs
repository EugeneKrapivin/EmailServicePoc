using EmailService.Models;

using MailKit.Net.Smtp;
using MailKit.Security;

using MimeKit;

using Orleans.Concurrency;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;


namespace Processor.Grains;

public interface ISmptSenderService : IGrainWithStringKey
{
    Task<SendResult> SendMessage(SmtpConfig config, EmailRequest emailRequest);
}

public record struct SendResult(bool Success, string Reason);

// note that the SmtpClient is not thread safe (we shouldn't be using reentrant here)
[StatelessWorker]
public class SmtpSenderService : Grain, ISmptSenderService
{
    private SmtpClient _client = default!; // I'm naughty like this
    private readonly ILogger<SmtpSenderService> _logger;

    private readonly IMeterFactory _meterFactory;

    private readonly Meter _meter;
    private readonly Histogram<double> _createClientHistogram;
    private readonly Histogram<double> _connectClientHistogram;
    private readonly Histogram<double> _sendMessageHistogram;
    private readonly Counter<long> _exceptionsCounter;

    public SmtpSenderService(ILogger<SmtpSenderService> logger, IMeterFactory meterFactory)
    {
        _logger = logger;

        _meterFactory = meterFactory;
        _meter = _meterFactory.Create("SMTPSender");

        _createClientHistogram = _meter.CreateHistogram<double>("smtp_create_client", "ms");
        _connectClientHistogram = _meter.CreateHistogram<double>("smtp_connect_client", "ms");
        _sendMessageHistogram = _meter.CreateHistogram<double>("smtp_send", "ms");
        _exceptionsCounter = _meter.CreateCounter<long>("smtp_exception_count", "exception");
    }

    public async Task<SendResult> SendMessage(SmtpConfig config, EmailRequest emailRequest)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            if (_client is null)
            {                
                var startCreate = Stopwatch.GetTimestamp();
               
                _client = new SmtpClient();

                var startConnect = Stopwatch.GetTimestamp();
                
                await _client.ConnectAsync(config.ServerUrl, int.Parse(config.ServerPort), false, cts.Token);

                var elapsedConnect = Stopwatch.GetElapsedTime(startConnect);

                //await _client.AuthenticateAsync(config.UserName, config.Password, cts.Token);

                var elapsedCreate = Stopwatch.GetElapsedTime(startCreate);

                _createClientHistogram.Record(elapsedCreate.TotalMilliseconds);
                _connectClientHistogram.Record(elapsedConnect.TotalMilliseconds, new KeyValuePair<string, object?>("flow", "create"));
            }
            
            var reconnect = false;
            try
            {
                await _client.NoOpAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed NoOp {server}:{port}", config.ServerUrl, config.ServerPort);
                reconnect = true && ex is not SmtpCommandException;
            }

            if (!_client.IsConnected || reconnect)
            {
                _logger.LogWarning("Reconnecting to {server}:{port}", config.ServerUrl, config.ServerPort);
                var startConnect = Stopwatch.GetTimestamp();

                await _client.ConnectAsync(config.ServerUrl, int.Parse(config.ServerPort), false, cts.Token);
                
                await _client.AuthenticateAsync(config.UserName, config.Password, cts.Token);

                var elapsedConnect = Stopwatch.GetElapsedTime(startConnect);
                _connectClientHistogram.Record(elapsedConnect.TotalMilliseconds, new KeyValuePair<string, object?>("flow", "create"));
            }

            DelayDeactivation(TimeSpan.FromSeconds(30));
        }
        catch (AuthenticationException ex)
        {
            _exceptionsCounter.Add(1,
                new KeyValuePair<string, object?>("exception", ex.GetType().Name),
                new KeyValuePair<string, object?>("flow", "connect"));

            _logger.LogError(ex, "failed authenticating with smtp server");
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is TimeoutException)
        {
            _logger.LogError(ex, "failed sending message {messageId} due to a timeout", emailRequest.MessageId);
            _exceptionsCounter.Add(1,
                    new KeyValuePair<string, object?>("exception", ex.GetType().Name),
                    new KeyValuePair<string, object?>("flow", "connect"));
            return new(false, "timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed sending message {messageId} due to an exception", emailRequest.MessageId);
            _exceptionsCounter.Add(1,
                    new KeyValuePair<string, object?>("exception", ex.GetType().Name),
                    new KeyValuePair<string, object?>("flow", "connect"));
            return new(false, "exception");
        }

        var message = CreateMimeMessage(config, emailRequest);

        var start = Stopwatch.GetTimestamp();
        try
        {
            var r = await _client.SendAsync(message, cts.Token);

            var total = Stopwatch.GetElapsedTime(start);

            _sendMessageHistogram.Record(total.TotalMilliseconds, new KeyValuePair<string, object?>("status", "success"));
        }
        catch (SmtpCommandException ex) when (ex.ErrorCode == SmtpErrorCode.RecipientNotAccepted)
        {
            _logger.LogError(ex, "failed sending an email with error {errorCode}", ex.ErrorCode);
            var total = Stopwatch.GetElapsedTime(start);

            _sendMessageHistogram.Record(total.TotalMilliseconds, new KeyValuePair<string, object?>("status", "failed"));
            _exceptionsCounter.Add(1,
                    new KeyValuePair<string, object?>("exception", ex.GetType().Name),
                    new KeyValuePair<string, object?>("flow", "send"));

            return new(false, $"failed with {ex.ErrorCode}");
        }
        catch (SmtpCommandException ex) when (ex.ErrorCode == SmtpErrorCode.SenderNotAccepted)
        {
            _logger.LogError(ex, "failed sending an email with error {errorCode}", ex.ErrorCode);
            var total = Stopwatch.GetElapsedTime(start);

            _sendMessageHistogram.Record(total.TotalMilliseconds, new KeyValuePair<string, object?>("status", "failed"));
            _exceptionsCounter.Add(1,
                new KeyValuePair<string, object?>("exception", ex.GetType().Name),
                new KeyValuePair<string, object?>("flow", "send"));

            return new(false, $"failed with {ex.ErrorCode}");
        }
        catch (SmtpCommandException ex) when (ex.ErrorCode == SmtpErrorCode.MessageNotAccepted)
        {
            _logger.LogError(ex, "failed sending an email with error {errorCode}", ex.ErrorCode);
            var total = Stopwatch.GetElapsedTime(start);

            _sendMessageHistogram.Record(total.TotalMilliseconds, new KeyValuePair<string, object?>("status", "failed"));
            _exceptionsCounter.Add(1,
                new KeyValuePair<string, object?>("exception", ex.GetType().Name),
                new KeyValuePair<string, object?>("flow", "send"));

            return new(false, $"failed with {ex.ErrorCode}");
        }
        catch (SmtpCommandException ex) when (ex.ErrorCode == SmtpErrorCode.UnexpectedStatusCode)
        {
            _logger.LogError(ex, "failed sending an email with error {errorCode}", ex.ErrorCode);
            var total = Stopwatch.GetElapsedTime(start);

            _sendMessageHistogram.Record(total.TotalMilliseconds, new KeyValuePair<string, object?>("status", "failed"));
            _exceptionsCounter.Add(1,
                new KeyValuePair<string, object?>("exception", ex.GetType().Name),
                new KeyValuePair<string, object?>("flow", "send"));

            return new(false, $"failed with {ex.ErrorCode}");
        }
        catch (SmtpProtocolException ex)
        {
            _logger.LogError(ex, "failed sending message {messageId} due to smtp protocol exception", emailRequest.MessageId);
            var total = Stopwatch.GetElapsedTime(start);

            _sendMessageHistogram.Record(total.TotalMilliseconds, new KeyValuePair<string, object?>("status", "failed"));
            _exceptionsCounter.Add(1,
                new KeyValuePair<string, object?>("exception", ex.GetType().Name),
                new KeyValuePair<string, object?>("flow", "send"));

            return new(false, ex.Message);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "failed sending message {messageId} due to an IO exception", emailRequest.MessageId);
            var total = Stopwatch.GetElapsedTime(start);

            _sendMessageHistogram.Record(total.TotalMilliseconds, new KeyValuePair<string, object?>("status", "failed"));
            _exceptionsCounter.Add(1,
                new KeyValuePair<string, object?>("exception", ex.GetType().Name),
                new KeyValuePair<string, object?>("flow", "send"));

            return new(false, ex.Message);
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is TimeoutException)
        {
            _logger.LogError(ex, "failed sending message {messageId} due to a timeout", emailRequest.MessageId);
            _exceptionsCounter.Add(1,
                    new KeyValuePair<string, object?>("exception", ex.GetType().Name),
                    new KeyValuePair<string, object?>("flow", "send"));

            return new(false, "timeout");
        }
        finally
        {
            //await _client.DisconnectAsync(true, cts.Token);
        }

        return new(true, "sent");
    }

    private static MimeMessage CreateMimeMessage(SmtpConfig config, EmailRequest emailRequest)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config.FromName, config.From));
        message.To.Add(new MailboxAddress(emailRequest.RecipientName, emailRequest.Recipient));
        message.Subject = emailRequest.Subject;

        var body = new BodyBuilder
        {
            HtmlBody = emailRequest.Body
        };

        message.Body = body.ToMessageBody();

        return message;
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            await _client.DisconnectAsync(true, cancellationToken);
            _client = null!;
        }
        
        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}